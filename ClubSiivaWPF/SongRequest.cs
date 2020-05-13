using ClubSiivaWPF.Data;
using Discord.WebSocket;
using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using YoutubeExplode;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Threading;
using Discord;
using ClubSiivaWPF.Databases;

namespace ClubSiivaWPF
{
    public class SongRequest
    {
        /// <summary>
        /// Get the description of the video sent
        /// </summary>
        /// <param name="title">The title of the song</param>
        /// <returns>Returns the description of the video</returns>
        public static async System.Threading.Tasks.Task<string> GetDescriptionAsync(string title)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    // Call out to the fandom page replacing spaces with underscores
                    using (var request = new HttpRequestMessage(new HttpMethod("GET"), "https://siivagunner.fandom.com/api.php?action=parse&page=" + title.Replace(" ", "_") + "&prop=wikitext&section=1&format=json"))
                    {
                        // Read th json and parse it
                        var response = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();
                        dynamic data = JObject.Parse(response);
                        var description = data.parse.wikitext.ToString();
                        IList<JToken> obj = JObject.Parse(description);
                        var result = ((JProperty)obj[0]).First;
                        // Check if we found information
                        bool found = false;
                        // Read the data
                        using (StringReader sr = new StringReader(result.ToString()))
                        {
                            string line;
                            int linenum = 0;
                            // Parse the lines for if we have the joke block for data
                            while ((line = sr.ReadLine()) != null && linenum < 1)
                            {
                                if (line.ToLower().Contains("joke"))
                                {
                                    found = true;
                                }
                                linenum++;
                            }
                        }
                        // If we found it split the data into the relevant description
                        if (found == true)
                        {
                            string[] lines = result.ToString().Split(Environment.NewLine.ToCharArray()).Skip(1).ToArray();
                            string output = string.Join(Environment.NewLine, lines);
                            // Return the data combined into a usable string
                            return output;
                        }
                        else
                        {
                            // Return we didnt find anything
                            return "No Description Avail";
                        }
                    }
                }
            }
            catch
            {
                // if we failed just tell them we couldnt get it
                return "No Description Avail";
            }
        }
        /// <summary>
        /// Request a song and add it to the relevant data
        /// </summary>
        /// <param name="message">The socketmessage to parse</param>
        /// <param name="hidb">The History DB</param>
        /// <param name="db">The Queue DB</param>
        /// <param name="usrdb">The User DB</param>
        /// <param name="priority">The priority to add the song at</param>
        /// <param name="username">The username to impersonate</param>
        /// <param name="roles">The list of roles to check against for mods</param>
        /// <returns>Returns the return message</returns>
        public static async System.Threading.Tasks.Task<string> RequestSongAsync(Config conf, DiscordSocketClient discordclient, SocketMessage message, LiteDatabase hidb, LiteDatabase db, LiteDatabase usrdb, List<string> roles, int priority = 0, string username = null)
        {
            try
            {
                // Try to parse the string
                var id = YoutubeClient.ParseVideoId(message.Content.Split().ToList()[1]);
                var client = new YoutubeClient();
                bool approved = false;
                // Check if the user is a mod, if they are automatically approve the video
                if(DataFunctions.IsMod(message, roles))
                {
                    approved = true;
                }
                // Check if the user has already requested a song in the queue
                var queuedb = db.GetCollection<Song>("Queue");
                var resultsrequested = queuedb.Find(x => x.RequesterId == message.Author.Id);
                if (DataFunctions.IsMod(message, roles) == false && resultsrequested.ToList().Count > 0)
                {
                    return "You have already requested a song";
                }
                // Check if the song is revoked for bad words
                if (DataFunctions.IsBadSong(id, hidb))
                {
                    return "The song you requested was removed for bad language";
                }
                // Check if the song has already been played tonight but isin't in the queue
                var historydb = hidb.GetCollection<History>("History");
                var results = historydb.Find(x => x.YoutubeId.StartsWith(id) && x.Played == true);
                if (results.ToList().Count > 0)
                {
                    return "Song has already been played";
                }
                // Check if the song is already in the queue
                var resultsq = queuedb.Find(x => x.YoutubeId.StartsWith(id));
                if (resultsq.ToList().Count > 0)
                {
                    return "Song has already been requested";
                }
                // Get the video data from youtube
                YoutubeExplode.Models.Video video = null;
                for (int attempts = 0; attempts < 3; attempts++)
                {
                    try
                    {
                        video = await client.GetVideoAsync(id);
                        await GetDescriptionAsync(video.Title);
                        break;
                    }
                    catch { }
                    Thread.Sleep(2000);
                }
                // If the video actually returned correctly
                if (video != null)
                {
                    // Make sure the song is from Siivagunner
                    if (video.Author.ToLower() == "siivagunner")
                    {
                        // Create a temp file to store the song in
                        string tempfile = FileFunctions.CreateTmpFile();
                        // Set the username to either the users or impersonate
                        string tempuser = message.Author.Username;
                        if (username != null)
                        {
                            tempuser = username;
                        }
                        ulong chan = 0;
                        foreach (var channel in discordclient.GetGuild(Convert.ToUInt64(conf.Guild)).Channels)
                        {
                            if (channel.Name.ToLower() == conf.ApprovalChannel.ToLower())
                            {
                                chan = channel.Id;
                                break;
                            }
                        }
                        var toEmebed = new EmbedBuilder();
                        toEmebed.WithTitle("Song Request");
                        toEmebed.WithColor(Color.Red);
                        string description = await GetDescriptionAsync(video.Title);
                        string longembed = "Requested by: " + tempuser + "\nSong Title: " + video.Title + "\nhttps://www.youtube.com/watch?v=" + id + "\n" + description;
                        toEmebed.WithDescription(longembed.Truncate(1950));

                        var messageid = await discordclient.GetGuild(Convert.ToUInt64(conf.Guild)).GetTextChannel(chan).SendMessageAsync("", false, toEmebed.Build());
                        _ = messageid.AddReactionsAsync(new[] { new Emoji("👍"), new Emoji("👎") });


                        // Generate the template data for the song to add
                        var newsong = new Song
                        {
                            RequesterId = message.Author.Id,
                            RequesterUsername = tempuser,
                            Duration = video.Duration.ToString(),
                            Title = video.Title,
                            File = tempfile,
                            YoutubeId = id,
                            Priority = priority,
                            Description = description,
                            Approved = approved,
                            ApprovalMessage = messageid.Id
                        };
                        // Insert the song into the database
                        queuedb.Insert(newsong);
                        // Check if the user had already requested something
                        if (DataFunctions.AlreadyRequested(message.Author, db) == false)
                        {
                            // Find the user and update their data to now have already requested something
                            var userdb = usrdb.GetCollection<Users>("Users");
                            var resultsusr = userdb.Find(x => x.DiscordUserId == message.Author.Id);
                            if (resultsusr.Count() >= 1)
                            {
                                Users usr = new Users();
                                usr = resultsusr.First();
                                usr.AlreadyRequested = true;
                                userdb.Update(usr);
                            }

                        }
                        // If the song was already approved if they are a mod just add it to the queue
                        if (approved == true)
                        {
                            return "Your song has now been added to the queue";
                        }
                        else
                        {
                            // If they are not a mod tell them we need to approve it
                            return "Your song has now been added to the queue, it needs to be approved by a mod";
                        }

                    }
                    else
                    {
                        // Tell them they messed up cause its not owned by siivagunner
                        return "This song is not owned by Siivagunner";
                    }
                }
                // Return that we had an error
                else
                {
                    return "An error has occured please try again.";
                }
            }
            catch
            {
                // Safety response just return we had an error
                return "An error has occured please try again.";
            }
        }

    }

}
