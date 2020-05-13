using ClubSiivaWPF.Data;
using ClubSiivaWPF.Databases;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LiteDB;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;



namespace ClubSiivaWPF
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// Block of holders for databases and data types
        private static readonly LiteDatabase db = new LiteDatabase(@"Queue.db");
        private static readonly LiteDatabase hidb = new LiteDatabase(@"History.db");
        private static readonly LiteDatabase usrdb = new LiteDatabase(@"Users.db");
        private static readonly LiteDatabase favdb = new LiteDatabase(@"Favorites.db");
        private List<Song> AllQueue = new List<Song>();
        public string videofile = "";
        private static DiscordSocketClient discordclient;
        private static IServiceProvider _services;
        private static Config conf;
        // Discord Client for actual connection

        public static List<String> ModRoles = new List<string>();

        /// <summary>
        /// Main entry for the application
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            // Create the temp directories
            FileFunctions.MakeTempDir();
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = "youtube-dl.exe",
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = "--rm-cache-dir"
            };

            try
            {
                // Start the process with the info we specified.
                // Call WaitForExit and then the using statement will close.
                using (Process exeProcess = Process.Start(startInfo))
                {
                    exeProcess.WaitForExit();
                }
            }
            catch
            {
            }
            // Write the tex tfiles
            System.IO.File.WriteAllText(Directory.GetCurrentDirectory() + "/textfiles/Requestor.txt", "");
            System.IO.File.WriteAllText(Directory.GetCurrentDirectory() + "/textfiles/Description.txt", "");
            System.IO.File.WriteAllText(Directory.GetCurrentDirectory() + "/textfiles/Duration.txt", "");
            System.IO.File.WriteAllText(Directory.GetCurrentDirectory() + "/textfiles/Title.txt", "");
            // Start up discord
            Task.Run(() => StartDiscord(db, hidb, usrdb, favdb));
            // Set up a timer for watching data
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
            // Load hard coded banned songs
            List<string> bannedvideos = new List<string>
            {
                "s7xqYhTfYk0",
                "UDQ9kw91ebQ",
                "EF5l2TQCfs8",
                "Stc-DmYW_cU",
                "v9IKa0zL0NU",
                "NvDK6sTHM9c",
                "QPf_3Rm00ig",
                "u9P30IvtATM",
                "6LCWp5TkCZA",
                "CuUgxl2KV1k",
                "6LCWp5TkCZA",
                "u9g5XsoE4-s",
                "SCK15CPpL2k",
                "SJ158CpsrXo"
            };
            // Add them to the history DB as banned
            var historydb = hidb.GetCollection<History>("History");
            foreach (var video in bannedvideos)
            {
                var result = historydb.Find(x => x.YoutubeId == video);
                if (result.ToList().Count == 0)
                {
                    History temp = new History
                    {
                        YoutubeId = video,
                        Played = true,
                        BadSong = true
                    };
                    historydb.Upsert(temp);
                }
            }

        }
        public static async Task StartDiscord(LiteDatabase queue, LiteDatabase history, LiteDatabase users, LiteDatabase fav)
        {
            var config = new DiscordSocketConfig { MessageCacheSize = 100 };
            _services = DiscordFunctions.ConfigureServices();
            discordclient = _services.GetRequiredService<DiscordSocketClient>();
            discordclient = new DiscordSocketClient(config);
            discordclient.Log += DiscordFunctions.LogAsync;
            discordclient.Ready += Ready;
            discordclient.MessageReceived += MessageReceivedAsync;
            discordclient.ReactionAdded += ReactionAddedAsync;
            discordclient.ReactionRemoved += ReactionRemovedAsync;
            // read file into a string and deserialize JSON to a type
            conf = JsonConvert.DeserializeObject<Config>(File.ReadAllText(@"discord.cfg"));
            // Tokens should be considered secret data, and never hard-coded.
            if (conf.DiscordToken == "TokenString")
            {
                MessageBox.Show("Please set your config file discord.cfg to have your bot token" + Environment.NewLine + "---------------------------Instructions---------------------------" + Environment.NewLine + "The required files and links will open after this box closes" + Environment.NewLine + "Add a new application, and under that application add a new bot and get it's token, put that token in the config file" + Environment.NewLine + "And then add the bot to your discord using the client ID from the application" + Environment.NewLine + "https://discordapp.com/oauth2/authorize?&client_id=CLIENTID&scope=bot&permissions=8", "Please configure discord");
                Process process = new Process();
                process.StartInfo.FileName = Directory.GetCurrentDirectory() + "/discord.cfg";
                process.Start();
                System.Diagnostics.Process.Start("https://discordapp.com/developers/applications/");
                System.Environment.Exit(0);
            }
            ModRoles = conf.ModRoles;
            await discordclient.LoginAsync(TokenType.Bot, conf.DiscordToken);
            await discordclient.StartAsync();
            await discordclient.SetGameAsync("ClubSiiva", null, ActivityType.Listening);
            // Block the program until it is closed.
            await Task.Delay(-1);
        }
        public static Task Ready()
        {
            Debug.WriteLine(discordclient.CurrentUser + " is connected!");
            return Task.CompletedTask;
        }
        public static async Task ReactionAddedAsync(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (reaction.UserId == discordclient.CurrentUser.Id)
                return;
            try
            {
                if (channel.Name.ToLower() == conf.ApprovalChannel.ToLower())
                {
                    var qdb = db.GetCollection<Song>("Queue");
                    var resultsq = qdb.Find(x => x.ApprovalMessage == reaction.MessageId);
                    if (resultsq.Count() > 0)
                    {
                        if (reaction.Emote.Name == "👍")
                        {
                            foreach (var item in resultsq)
                            {
                                item.Approved = true;
                                qdb.Update(item);
                            }
                        }
                        else if (reaction.Emote.Name == "👎")
                        {
                            foreach (var item in resultsq)
                            {
                                item.Approved = false;
                                qdb.Update(item);
                                // Remove the song by the ID
                                var song = DataFunctions.RemoveSongBySongId(item.YoutubeId, db);
                                if (song != string.Empty)
                                {
                                    var toEmebed = new EmbedBuilder();
                                    toEmebed.WithTitle("Song Removed: ");
                                    toEmebed.WithDescription(item.Title);
                                    toEmebed.WithColor(Color.Red);
                                    await channel.SendMessageAsync("", false, toEmebed.Build());
                                    return;
                                }
                                else
                                {
                                    var toEmebed = new EmbedBuilder();
                                    toEmebed.WithTitle("No song to remove");
                                    toEmebed.WithDescription(item.Title);
                                    toEmebed.WithColor(Color.Orange);
                                    await channel.SendMessageAsync("", false, toEmebed.Build());
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }
        public static async Task ReactionRemovedAsync(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (reaction.UserId == discordclient.CurrentUser.Id)
                return;
            try
            {
                if (channel.Name.ToLower() == conf.ApprovalChannel.ToLower())
                {
                    var qdb = db.GetCollection<Song>("Queue");
                    var resultsq = qdb.Find(x => x.ApprovalMessage == reaction.MessageId);
                    if (resultsq.Count() > 0)
                    {
                        if (reaction.Emote.Name == "👍")
                        {
                            foreach (var item in resultsq)
                            {
                                item.Approved = false;
                                qdb.Update(item);
                            }
                        }
                    }
                }
            }
            catch { }
        }
        public static async Task MessageReceivedAsync(SocketMessage message)
        {
            // The bot should never respond to itself.
            if (message.Author.Id == discordclient.CurrentUser.Id)
                return;
            try
            {
                if (message.Content != string.Empty)
                {
                    if (message.Content[0] == '!')
                    {
                        _ = Task.Run(() => Commands.CommandsAsync(discordclient, message, hidb, db, usrdb, favdb, ModRoles, conf));
                    }
                }
            }
            catch
            {
                try
                {
                    _ = message.Channel.SendMessageAsync("Something went wrong please try again");
                }
                catch { }
            }
        }
        /// <summary>
        /// Event for when the app has been closed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Delete the temp directory if it exists
            if (Directory.Exists(Directory.GetCurrentDirectory() + "/temp/"))
            {
                Directory.Delete(Directory.GetCurrentDirectory() + "/temp/", true);
            }
            Environment.Exit(0);
        }

        /// <summary>
        /// Timer to update preloading and the queue and the slider info
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Update the progress textbox
                if (Util.GetMediaState(this.MediaPlayer) == MediaState.Play)
                {
                    await this.Progress.Dispatcher.BeginInvoke((Action)(() => Progress.Content = this.MediaPlayer.Position.Hours.ToString("D2") + ":" + this.MediaPlayer.Position.Minutes.ToString("D2") + ":" + this.MediaPlayer.Position.Seconds.ToString("D2")));
                }
            }
            catch { }
            try
            {
                // Update the queue list
                _ = Task.Run(() => SongFunctions.UpdateQueueList(db, AllQueue, this));
            }
            catch { }
            try
            {
                // Pre download the videos
                _ = Task.Run(() => SongFunctions.PreloadVideos(db));
            }
            catch { }
            try
            {
                // Keep the slider in sync
                _ = Task.Run(() => Sliderticktock());
            }
            catch { }
            try
            {
                _ = Task.Run(() => TotalDurationUpdate());
            }
            catch { }
            try
            {
                _ = Task.Run(() => UpdateFavIconAsync());
            }
            catch { }
        }

        private async Task UpdateFavIconAsync()
        {
            try
            {
                var text = this.CurrentURL.Dispatcher.Invoke(new Func<string>(() => CurrentURL.Text));
                if (text != "")
                {
                    var favoritedb = favdb.GetCollection<Favorites>("Favorites");
                    var resultsq = favoritedb.Find(x => x.YoutubeId.Contains(text));
                    if (resultsq.Count() == 0)
                    {
                        await this.Favorite.Dispatcher.BeginInvoke((Action)(() => Favorite.Content = "☆"));
                    }
                    else
                    {
                        await this.Favorite.Dispatcher.BeginInvoke((Action)(() => Favorite.Content = "★"));
                    }
                }
            }
            catch { }
        }
        private void UpdateFavorite(bool fav)
        {
            try
            {
                if (this.CurrentURL.Text != "")
                {
                    var favoritedb = favdb.GetCollection<Favorites>("Favorites");
                    var resultsq = favoritedb.Find(x => x.YoutubeId.Contains(this.CurrentURL.Text));
                    if (fav == false)
                    {
                        foreach (var song in resultsq)
                        {
                            favoritedb.Delete(song.Id);
                        }
                    }
                    else
                    {
                        foreach (var song in resultsq)
                        {
                            return;
                        }
                        Favorites item = new Favorites
                        {
                            YoutubeId = this.CurrentURL.Text
                        };
                        favoritedb.Upsert(item);
                    }
                }
            }
            catch { }
        }
   
        /// <summary>
        /// Function to update the total time textbox
        /// </summary>
        private async void TotalDurationUpdate()
        {
            try
            {
                var queuedb = db.GetCollection<Song>("Queue");
                var results = queuedb.Find(x => x.Approved == true);
                TimeSpan addedduration = new TimeSpan();
                foreach (var song in results)
                {
                    var temp = TimeSpan.Parse(song.Duration);
                    addedduration += temp;
                }
                await this.TotalDuration.Dispatcher.BeginInvoke((Action)(() => TotalDuration.Content = addedduration.ToString()));
            }
            catch { }
        }
        /// <summary>
        /// Event for watching when videos finish
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            // Get the current video path
            string file = this.MediaPlayer.Source.LocalPath;
            // Try to stop it and set the source to null
            try
            {
                this.MediaPlayer.Stop();
            }
            catch { }
            this.MediaPlayer.Source = null;
            // Try to dump the file and set the string storing previous song to where we stored this
            try
            {
                if (videofile != null)
                {
                    if (videofile != string.Empty)
                    {
                        if (System.IO.Path.GetFileName(videofile) != System.IO.Path.GetFileName(this.MediaPlayer.Source.AbsoluteUri))
                        {
                            Task.Run(() => FileFunctions.DeleteTmpFile(file));
                        }
                    }
                }
                videofile = this.MediaPlayer.Source.OriginalString;
            }
            catch { }
            // Queue the next song and hit play
            SongFunctions.Queueevent(this, hidb, db, discordclient, conf);
            this.MediaPlayer.Play();
        }
        /// <summary>
        /// Event to watch for new videos
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MediaPlayer_SourceOpened(object sender, RoutedEventArgs e)
        {
            try
            {
                // Compared if we havent checked against anything
                if (videofile != null)
                {
                    if (videofile != string.Empty)
                    {
                        // Delete the file if it already exists
                        if (System.IO.Path.GetFileName(videofile) != System.IO.Path.GetFileName(this.MediaPlayer.Source.AbsoluteUri))
                        {
                            Task.Run(() => FileFunctions.DeleteTmpFile(videofile));
                        }
                    }

                }
                // Set the last video to the source
                videofile = this.MediaPlayer.Source.OriginalString;

            }
            catch { }
        }
        /// <summary>
        /// Event for watching if we want to play or pause
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            // Get the player state and toggle
            if (Util.GetMediaState(this.MediaPlayer) == MediaState.Play)
            {
                this.MediaPlayer.Pause();
            }
            else
            {
                this.MediaPlayer.Play();
            }
        }
        /// <summary>
        /// Button to trigger to play the last song again
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlayPrevious_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => SongFunctions.Playprevious(this, hidb, db, discordclient,conf));
        }
        /// <summary>
        /// Button to replay the song
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            // Just set the timer to 0 to restart it
            this.MediaPlayer.Position = TimeSpan.Zero;
        }
        /// <summary>
        /// Function to trigger the next song
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlayNext_Click(object sender, RoutedEventArgs e)
        {
            SongFunctions.Queueevent(this, hidb, db, discordclient, conf);
        }
        /// <summary>
        /// Function to manually add a song to play
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlayManual_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the text from the textbox and request it
                var url = this.video.Text;
                Task.Run(() => SongFunctions.ManualRequest(url, this, hidb, db, discordclient, conf));
            }
            catch { }
        }

        /// <summary>
        /// Config button to clear the history of played songs
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            // Warn them if they want to clear it
            MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show("Clear play history", "Are you sure?", System.Windows.MessageBoxButton.YesNo);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                // Get all the songs from history 
                var historydb = hidb.GetCollection<History>("History");
                var results = historydb.FindAll();
                historydb.EnsureIndex(x => x.Id, true);
                // Set them to not played
                foreach (var song in results)
                {
                    song.Played = false;
                    historydb.Update(song);
                }
                // Make sure we make it so all users can request songs at priority 1 again
                var userdb = usrdb.GetCollection<Users>("Users");
                var resultsusr = userdb.FindAll();
                userdb.EnsureIndex(x => x.Id, true);
                // Update their requested status
                foreach (var person in resultsusr)
                {
                    person.AlreadyRequested = false;
                    userdb.Update(person);
                }
            }
        }
        /// <summary>
        /// Button to load the file for the cfg file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Config_Click(object sender, RoutedEventArgs e)
        {
            // Warn them if they want to open it.
            MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show("Make sure you are not streaming before hitting yes", "Are you sure?", System.Windows.MessageBoxButton.YesNo);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                // Start a process with the file so it opens with their default app
                Process process = new Process();
                process.StartInfo.FileName = Directory.GetCurrentDirectory() + "/discord.cfg";
                process.Start();
            }
        }
        /// <summary>
        /// If a username is double clicked on in the UI it will play that song
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SongDoubleClick(object sender, RoutedEventArgs e)
        {
            // Get the selected item
            if (this.SongList.SelectedItem != null)
            {
                // Trigger the queue to play the next song
                Song item = this.SongList.SelectedItem as Song;
                Task.Run(() => SongFunctions.TriggerqueueAsync(item, this, hidb, db, discordclient, conf));
                try
                {
                    // Trigger the queue to be updated in the UI
                    Task.Run(() => SongFunctions.UpdateQueueList(db, AllQueue, this));
                }
                catch { }
            }
        }
        /// <summary>
        /// Event to sync up the progress slider
        /// </summary>
        private async void Sliderticktock()
        {
            try
            {
                await this.ProgressSlider.Dispatcher.BeginInvoke((Action)(() => ProgressSlider.Value = this.MediaPlayer.Position.TotalSeconds));
            }
            catch { }
        }
        /// <summary>
        /// Event to watch for slider events
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Check if the slider was updated by someone
            if (ProgressSlider.IsMouseCaptureWithin)
            {
                // Set the position to what it was set to
                int pos = Convert.ToInt32(ProgressSlider.Value);
                MediaPlayer.Position = new TimeSpan(0, 0, 0, pos, 0);
            }
        }
        /// <summary>
        /// Event for checking if the volume was changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Set the volume of the player to the value
            this.MediaPlayer.Volume = Volume.Value;
        }
        /// <summary>
        /// Check for the event up on the progress bar so we know what to set the time to
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SliderSeek_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Set the position in the video
                int pos = Convert.ToInt32(ProgressSlider.Value);
                MediaPlayer.Position = new TimeSpan(0, 0, 0, pos, 0);
            }
            catch { }
        }

        private void Popout_Duration_Click(object sender, RoutedEventArgs e)
        {
            TransparentForm win2 = new TransparentForm();
            win2.originallabel = this.SongDuration;
            win2.Show();

        }

        private void Popout_Total_Duration_Click(object sender, RoutedEventArgs e)
        {
            TransparentForm win2 = new TransparentForm();
            win2.originallabel = this.TotalDuration;
            win2.Show();
        }

        private void Progress_Click(object sender, RoutedEventArgs e)
        {
            TransparentForm win2 = new TransparentForm();
            win2.originallabel = this.Progress;
            win2.Show();
        }

        private void Requestor_Click(object sender, RoutedEventArgs e)
        {
            TransparentForm win2 = new TransparentForm();
            win2.originallabel = this.SongRequestor;
            win2.Show();
        }

        private void Title_Click(object sender, RoutedEventArgs e)
        {
            TransparentForm win2 = new TransparentForm();
            win2.originallabel = this.SongTitle;
            win2.Show();
        }
        private void Favorite_Click(object sender, RoutedEventArgs e)
        {
            if(Favorite.Content.ToString() == "☆")
            {
                Favorite.Content = "★";
                UpdateFavorite(true);
            }
            else
            {
                Favorite.Content = "☆";
                UpdateFavorite(false);
            }
        }
    }
    public static class Util
    {
        /// <summary>
        /// Helper to check the status of a media player
        /// </summary>
        /// <param name="myMedia">The media element to check against</param>
        /// <returns>Returns the MediaState state</returns>
        public static MediaState GetMediaState(this MediaElement myMedia)
        {
            FieldInfo hlp = typeof(MediaElement).GetField("_helper", BindingFlags.NonPublic | BindingFlags.Instance);
            object helperObject = hlp.GetValue(myMedia);
            FieldInfo stateField = helperObject.GetType().GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance);
            MediaState state = (MediaState)stateField.GetValue(helperObject);
            return state;
        }
    }

}