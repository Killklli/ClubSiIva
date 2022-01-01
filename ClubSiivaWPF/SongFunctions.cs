using ClubSiivaWPF.Data;
using ClubSiivaWPF.Databases;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode;

namespace ClubSiivaWPF
{
    public class SongFunctions
    {
        /// <summary>
        /// Function to add a song to the queue from Manual requests
        /// </summary>
        /// <param name="url">The URL to add</param>
        /// <param name="form">The main form running</param>
        /// <param name="hidb">The history DB</param>
        /// <param name="db">The queue DB</param>
        public static async void ManualRequest(string url, MainWindow form, LiteDatabase hidb, LiteDatabase db, DiscordSocketClient discordclient, Config conf)
        {
            // Parse the ID
            var client = new YoutubeClient();
            YoutubeExplode.Videos.Video video = null;
            // Attempt to get the video information
            for (int attempts = 0; attempts < 3; attempts++)
            {
                try
                {
                    video = await client.Videos.GetAsync(url);
                    break;
                }
                catch { }
                Thread.Sleep(2000);
            }
            // If we found it add it to the queue
            if (video != null)
            {
                string tempfile = FileFunctions.CreateTmpFile();
                Song temp = new Song
                {
                    Priority = 0,
                    RequesterId = 0,
                    RequesterUsername = "Manual Request",
                    YoutubeId = url,
                    Title = video.Title,
                    Description = await SongRequest.GetDescriptionAsync(video.Title),
                    File = tempfile,
                    Duration = video.Duration.ToString(),
                    Approved = true,
                    Id = 999999999
                };
                _ = TriggerqueueAsync(temp, form, hidb, db, discordclient, conf);
            }
        }
        /// <summary>
        /// Trigger playing of the next song in the queue
        /// </summary>
        /// <param name="songdata">The data of the song to play</param>
        /// <param name="form">The main form thread</param>
        /// <param name="hidb">The history DB</param>
        /// <param name="db">The queue DB</param>
        public static async Task TriggerqueueAsync(Song songdata, MainWindow form, LiteDatabase hidb, LiteDatabase db, DiscordSocketClient client, Config conf)
        {
            // Check if we have already downloaded the file to play
            var song = songdata;
            if (!File.Exists(song.File))
            {
                // Download the video if we havent already
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    FileName = "youtube-dl.exe",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    Arguments = "-f mp4 " + song.YoutubeId + " -o " + "\"" + song.File + "\""
                };

                try
                {
                    Console.WriteLine(startInfo.Arguments);
                    // Start the process with the info we specified.
                    // Call WaitForExit and then the using statement will close.
                    using (Process exeProcess = Process.Start(startInfo))
                    {
                        exeProcess.WaitForExit();
                    }
                }
                catch
                {
                    // Log error.
                }
            }
            // Set the media player source and hit play
            try
            {
                await form.MediaPlayer.Dispatcher.BeginInvoke((Action)(() => form.MediaPlayer.Source = new System.Uri(song.File.Replace(@"\\", @"\"))));
                await form.MediaPlayer.Dispatcher.BeginInvoke((Action)(() => form.MediaPlayer.Play()));
            }
            catch
            {
            }
            // Set the text file information into their files
            await form.SongRequestor.Dispatcher.BeginInvoke((Action)(() => form.SongRequestor.Content = song.RequesterUsername));
            System.IO.File.WriteAllText(Directory.GetCurrentDirectory() + "/textfiles/Requestor.txt", song.RequesterUsername);
            await form.Description.Dispatcher.BeginInvoke((Action)(() => form.Description.Text = song.Description));
            System.IO.File.WriteAllText(Directory.GetCurrentDirectory() + "/textfiles/Description.txt", song.Description);
            await form.SongDuration.Dispatcher.BeginInvoke((Action)(() => form.SongDuration.Content = song.Duration));
            System.IO.File.WriteAllText(Directory.GetCurrentDirectory() + "/textfiles/Duration.txt", song.Duration);
            await form.SongTitle.Dispatcher.BeginInvoke((Action)(() => form.SongTitle.Content = song.Title));
            System.IO.File.WriteAllText(Directory.GetCurrentDirectory() + "/textfiles/Title.txt", song.Title);
            await form.ProgressSlider.Dispatcher.BeginInvoke((Action)(() => form.ProgressSlider.Maximum = TimeSpan.Parse(song.Duration).TotalSeconds));
            await form.Favorite.Dispatcher.BeginInvoke((Action)(() => form.Favorite.Content = "☆"));
            await form.CurrentURL.Dispatcher.BeginInvoke((Action)(() => form.CurrentURL.Text = song.YoutubeId));
            // Find the discord channel to tell were now playing
            ulong chan = 0;
            foreach (var channel in client.GetGuild(Convert.ToUInt64(conf.Guild)).Channels)
            {
                if (channel.Name.ToLower() == conf.MessageChannel.ToLower())
                {
                    chan = channel.Id;
                    break;
                }
            }
            // Build the embedded message
            var toEmebed = new EmbedBuilder();
            toEmebed.WithTitle("Now Playing");
            toEmebed.WithColor(Color.Green);
            var username = "";
            try
            {
                username = client.GetGuild(Convert.ToUInt64(Convert.ToUInt64(conf.Guild))).GetUser(song.RequesterId).Mention;
            }
            catch
            {
                username = "Manual Request/Invalid Name";
            }
            // Send the data with description
            toEmebed.WithDescription("Requested by: " + username + "\nSong Title: " + song.Title + "\nhttps://www.youtube.com/watch?v=" + song.YoutubeId);
            var messageid = await client.GetGuild(Convert.ToUInt64(conf.Guild)).GetTextChannel(chan).SendMessageAsync("", false, toEmebed.Build());
            _ = messageid.AddReactionsAsync(new[] { new Emoji("👍"), new Emoji("👎") });

            // Make sure this is not a manual request
            if (song.Id != 999999999)
            {
                // Add it to the history
                var historydb = hidb.GetCollection<History>("History");
                History newhistory = new History
                {
                    YoutubeId = song.YoutubeId,
                    BadSong = false,
                    Played = true
                };
                historydb.Upsert(newhistory);
            }
            // Remove it from the queue
            var queuedb = db.GetCollection<Song>("Queue");
            queuedb.Delete(song.Id);

        }
        /// <summary>
        /// Triggered when we need to find the oncoming queue
        /// </summary>
        /// <param name="form">The main form</param>
        /// <param name="hidb">The history Database</param>
        /// <param name="db">The queue Database</param>
        public static void Queueevent(MainWindow form, LiteDatabase hidb, LiteDatabase db, DiscordSocketClient discordclient, Config conf)
        {
            // Find the list of queue songs
            Song temp = new Song();
            var queuedb = db.GetCollection<Song>("Queue");
            var results0 = queuedb.Find(x => x.Priority == 0 && x.Approved == true);
            var results1 = queuedb.Find(x => x.Priority == 1 && x.Approved == true);
            var results2 = queuedb.Find(x => x.Priority == 2 && x.Approved == true);
            // Sort them in order
            if (results0.Count() >= 1)
            {
                temp = results0.First();
            }
            else if (results1.Count() >= 1)
            {
                temp = results1.First();
            }
            else if (results2.Count() >= 1)
            {
                temp = results2.First();
            }
            // Play the song
            if (temp.Title != null)
            {
                Task.Run(() => TriggerqueueAsync(temp, form, hidb, db, discordclient, conf));
            }
        }
        /// <summary>
        /// Function to pre download the videos using youtubedl
        /// </summary>
        /// <param name="db">The queue database</param>
        public static void PreloadVideos(LiteDatabase db)
        {
            try
            {
                // Attempt to get the current queue
                var queuedb = db.GetCollection<Song>("Queue");
                List<Song> songs = new List<Song>();
                var results0 = queuedb.Find(x => x.Priority == 0 && x.Approved == true);
                var results1 = queuedb.Find(x => x.Priority == 1 && x.Approved == true);
                var results2 = queuedb.Find(x => x.Priority == 2 && x.Approved == true);
                foreach (var request in results0)
                {
                    songs.Add(request);
                }
                foreach (var request in results1)
                {
                    songs.Add(request);
                }
                foreach (var request in results2)
                {
                    songs.Add(request);
                }
                // If we find more than one song get the first 5 songs
                if (songs.Count >= 1)
                {
                    int counter = 0;
                    if (songs.Count >= 5)
                    {
                        counter = 5;
                    }
                    else
                    {
                        counter = songs.Count;
                    }
                    // For the first five songs download them
                    for (int i = 0; i < counter; i++)
                    {
                        if (songs[i] != null)
                        {
                            // Make sure the file dosent already exist
                            if (!File.Exists(songs[i].File))
                            {
                                ProcessStartInfo startInfo = new ProcessStartInfo();
                                startInfo.CreateNoWindow = true;
                                startInfo.UseShellExecute = false;
                                startInfo.FileName = "youtube-dl.exe";
                                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                startInfo.Arguments = "-f mp4 " + songs[i].YoutubeId + " -o " + "\"" + songs[i].File + "\"";

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
                            }
                        }
                    }
                }
            }
            catch { }
        }
        /// <summary>
        /// Triggers a search into the history DB to play the last song
        /// </summary>
        /// <param name="form">The main form</param>
        /// <param name="hidb">The history database</param>
        /// <param name="db">The queue database</param>
        public static async void Playprevious(MainWindow form, LiteDatabase hidb, LiteDatabase db, DiscordSocketClient discordclient, Config conf)
        {
            // Search for all songs in the hisotry
            var historydb = hidb.GetCollection<History>("History");
            var results = historydb.FindAll();
            var client = new YoutubeClient();
            YoutubeExplode.Videos.Video video = null;
            // Attempt to get the video info of the last one
            for (int attempts = 0; attempts < 3; attempts++)
            {
                try
                {
                    video = await client.Videos.GetAsync(results.Last().YoutubeId);
                    break;
                }
                catch { }
                Thread.Sleep(2000);
            }
            // Create a temp file to download it to and send it to the video player
            if (video != null)
            {
                string tempfile = FileFunctions.CreateTmpFile();
                Song temp = new Song
                {
                    Priority = 0,
                    RequesterId = 0,
                    RequesterUsername = "Previous Track",
                    YoutubeId = results.Last().YoutubeId,
                    Title = video.Title,
                    Description = await SongRequest.GetDescriptionAsync(video.Title),
                    File = tempfile,
                    Duration = video.Duration.ToString(),
                    Approved = true,
                    Id = 999999999
                };
                _ = TriggerqueueAsync(temp, form, hidb, db, discordclient, conf);
            }
        }
        /// <summary>
        /// Function to update the UI for the queue DB
        /// </summary>
        /// <param name="db">The Queue database</param>
        /// <param name="AllQueue">The var to store the queue so we can compare against for updates</param>
        /// <param name="form">The main form</param>
        public static async void UpdateQueueList(LiteDatabase db, List<Song> AllQueue, MainWindow form)
        {
            try
            {
                // Get the queue of songs in order
                var queuedb = db.GetCollection<Song>("Queue");
                List<Song> songs = new List<Song>();
                var results0 = queuedb.Find(x => x.Priority == 0 && x.Approved == true);
                var results1 = queuedb.Find(x => x.Priority == 1 && x.Approved == true);
                var results2 = queuedb.Find(x => x.Priority == 2 && x.Approved == true);
                foreach (var request in results0)
                {
                    songs.Add(request);
                }
                foreach (var request in results1)
                {
                    songs.Add(request);
                }
                foreach (var request in results2)
                {
                    songs.Add(request);
                }
                bool matching = true;
                int counter = 0;
                // Check if the two queues dont match
                if (AllQueue.Count > 0 && songs.Count >= 1)
                {
                    // Check if they match based off ids
                    foreach (var song in songs)
                    {
                        if (song.Id != AllQueue[counter].Id)
                        {
                            matching = false;
                            break;
                        }
                        counter++;
                    }
                    // If they dont match update it
                    if (!matching)
                    {
                        AllQueue = songs;
                        await form.SongList.Dispatcher.BeginInvoke((Action)(() => form.SongList.ItemsSource = songs));
                    }
                }
                else
                {
                    // Edge case where nothing exists yet to match it up
                    AllQueue = songs;
                    await form.SongList.Dispatcher.BeginInvoke((Action)(() => form.SongList.ItemsSource = songs));
                }
            }
            catch { }
        }
    }
}
