using ClubSiivaWPF.Data;
using Discord;
using Discord.WebSocket;
using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using YoutubeExplode;

namespace ClubSiivaWPF
{
    class Commands
    {
        /// <summary>
        /// Main Function for passing commands through 
        /// </summary>
        /// <param name="message">The Discord SocketMessage</param>
        /// <param name="hidb">The liteDB for history</param>
        /// <param name="db">The liteDB for queue</param>
        /// <param name="usrdb">The liteDB for the users</param>
        /// <param name="roles">The list of roles from the config</param>
        /// <returns></returns>
        public static async System.Threading.Tasks.Task CommandsAsync(SocketMessage message, LiteDatabase hidb, LiteDatabase db, LiteDatabase usrdb, List<string> roles)
        {
            // Get the first word in the message content
            string command = message.Content.ToLower().Split().ToList()[0];
            // Check if the song is a songrequest
            if (command == "!songrequest")
            {
                // Validate we have all the message parts
                if (message.Content.Split().ToList().Count == 2 || message.Content.Split().ToList().Count == 3)
                {
                    // Generate an empty string to return against
                    string returnmessage = "";
                    // Validate that the user is a mod
                    if (DataFunctions.IsMod(message, roles) == true)
                    {
                        // If the user is a mod and the length is three pass the third value as a username for the request
                        if (message.Content.Split().ToList().Count == 3)
                        {
                            // Request the song with the users name
                            returnmessage = await SongRequest.RequestSongAsync(message, hidb, db, usrdb, roles, 0, message.Content.Split().ToList()[2]);
                        }
                        // Else just request it for the mod themselves
                        else
                        {
                            returnmessage = await SongRequest.RequestSongAsync(message, hidb, db, usrdb, roles, 0);
                        }
                        // If the return message is not empty give them the response it kicked back
                        if (returnmessage != string.Empty)
                        {
                            DiscordFunctions.EmbedThis(message, null, message.Author.Mention + " " + returnmessage, "green");
                        }
                    }
                    // In the case the user is not a mod
                    else
                    {
                        // Check if the file exists for if song requests are enabled
                        if (FileFunctions.RequestsEnabled() == true)
                        {
                            // Check if the user has already requested a song
                            if (DataFunctions.AlreadyRequested(message.Author, db) == true)
                            {
                                // Priority 2 means they've already requested so throw it to the back
                                returnmessage = await SongRequest.RequestSongAsync(message, hidb, db, usrdb, roles, 2);
                            }
                            else
                            {
                                // Prirotiy 1 means they haven't requested anything yet
                                returnmessage = await SongRequest.RequestSongAsync(message, hidb, db, usrdb, roles, 1);
                            }
                            // Kick them back the message we got
                            DiscordFunctions.EmbedThis(message, null, message.Author.Mention + " " + returnmessage, "green");
                        }
                        // Warn them song requests are currently disabled
                        else
                        {
                            DiscordFunctions.EmbedThis(message, null, message.Author.Mention + " Song requests are currently disabled", "yellow");
                        }
                    }
                }
                // Tell the user they never fully specified all of the data
                else
                {
                    DiscordFunctions.EmbedThis(message, null, message.Author.Mention + " You did not specify a song to request", "red");
                }
            }
            // If we want to remove a song
            else if (command == "!remove")
            {
                // If they only sent the command !remove
                if (DataFunctions.UsersSongInQueue(message.Author, db) && message.Content.ToLower().Split().ToList().Count == 1)
                {
                    // Just remove the first song by their username
                    var song = DataFunctions.RemoveSongBySongUser(message.Author, db);
                    if (song != string.Empty)
                    {
                        DiscordFunctions.EmbedThis(message, null, "Song Removed: " + song, "red");
                        return;
                    }
                }
                // If they sent an ID with it but are not a mod
                else if (DataFunctions.UsersSongInQueue(message.Author, db) && message.Content.ToLower().Split().ToList().Count == 2)
                {
                    // Remove the song by the id and their user
                    var song = DataFunctions.RemoveSongBySongId(message.Content.Split().ToList()[1], db, message.Author);
                    if (song != string.Empty)
                    {
                        DiscordFunctions.EmbedThis(message, null, "Song Removed: " + song, "red");
                        return;
                    }
                }
                // If they are a mod, assume they are sending an ID with it
                if (DataFunctions.IsMod(message, roles) == true && message.Content.ToLower().Split().ToList().Count >= 3)
                {
                    // Remove the song by the ID
                    var song = DataFunctions.RemoveSongBySongId(message.Content.Split().ToList()[1], db);
                    if (song != string.Empty)
                    {
                        DiscordFunctions.EmbedThis(message, null, "Song Removed: " + song, "red");
                        return;
                    }
                    else
                    {
                        DiscordFunctions.EmbedThis(message, null, "No song to remove", "yellow");
                        return;
                    }
                }
                else
                {
                    // Return a failure message
                    DiscordFunctions.EmbedThis(message, null, message.Author.Mention + " You are not a mod or you have no songs requested", "yellow");
                    return;
                }
            }
            // Check if we want to ban a video
            else if (command == "!bansong")
            {
                // Check if the user is a mod and we have all the parts of the command
                if (DataFunctions.IsMod(message, roles) == true && message.Content.ToLower().Split().ToList().Count == 2)
                {
                    // Get the ID of the song we want to ban
                    var id = YoutubeClient.ParseVideoId(message.Content.Split().ToList()[1]);
                    // Search for the song in the history 
                    var historydb = hidb.GetCollection<History>("History");
                    var results = historydb.Find(x => x.YoutubeId == id && x.BadSong == true);
                    // Set a hold bool for if we found the song or not already banned
                    bool foundsong = false;
                    // If its already in the history just ban it
                    if (results.ToList().Count > 0)
                    {
                        foundsong = true;
                    }
                    // If we havent found the song
                    if (foundsong == false)
                    {
                        // Remove the song by the ID
                        var resultsinner = historydb.Find(x => x.YoutubeId == id);
                        // Check if we found the song in the history
                        if (resultsinner.ToList().Count > 0)
                        {
                            // If we do have it in the history but it isint banned yet, set the ban value to true and update it
                            // We check all of them in case the song somehow got double played
                            foreach (var badsong in resultsinner)
                            {
                                badsong.BadSong = true;
                                historydb.Update(badsong);
                            }
                        }
                        // If we didnt find anything we have to make a new entry
                        else
                        {
                            // Create a temporary entry and add it to the DB
                            History temp = new History
                            {
                                BadSong = true,
                                Played = false,
                                YoutubeId = id
                            };
                            historydb.Upsert(temp);
                        }
                        // Remove the song if we can
                        var song = DataFunctions.RemoveSongBySongId(id, db);
                        // Notify the user it was removed from the queue and banned
                        if (song != string.Empty)
                        {
                            DiscordFunctions.EmbedThis(message, null, "Song Removed and banned: " + id, "red");
                            return;
                        }
                        // Else just ban the song
                        else
                        {
                            DiscordFunctions.EmbedThis(message, null, "No song to remove from queue, however the song has been banned: " + id, "red");
                            return;
                        }
                    }
                    // If the song was already banned lets assume we want to unban it
                    else
                    {
                        // Find the song in the history DB
                        var resultsinner = historydb.Find(x => x.YoutubeId == id);
                        // If we found the song
                        if (resultsinner.ToList().Count > 0)
                        {
                            // Unban the song and notify the user
                            foreach (var badsong in resultsinner)
                            {
                                badsong.BadSong = false;
                                historydb.Update(badsong);
                            }
                            DiscordFunctions.EmbedThis(message, null, "Song has been unbanned: " + id, "green");
                        }
                        return;
                    }

                }
                // Warn the user they didnt supply a full command
                else if (DataFunctions.IsMod(message, roles) == true && message.Content.ToLower().Split().ToList().Count != 2)
                {
                    DiscordFunctions.EmbedThis(message, null, "You did not supply a full command.", "yellow");
                    return;
                }
                // The user is not a mod, tell them they are bad
                else
                {
                    DiscordFunctions.EmbedThis(message, null, "You are not a mod", "red");
                    return;
                }
            }
            // Check if the command was to check the queue
            else if (command == "!queue")
            {
                // Check if the user is a mod
                if (DataFunctions.IsMod(message, roles) == true)
                {
                    // Get the queues and check all of them
                    List<Song> songs = new List<Song>();
                    var queuedb = db.GetCollection<Song>("Queue");
                    var results0 = queuedb.Find(x => x.Priority == 0);
                    var results1 = queuedb.Find(x => x.Priority == 1);
                    var results2 = queuedb.Find(x => x.Priority == 2);
                    // Add them to a list in the order we find them
                    foreach (var song in results0)
                    {
                        songs.Add(song);
                    }
                    foreach (var song in results1)
                    {
                        songs.Add(song);
                    }
                    foreach (var song in results2)
                    {
                        songs.Add(song);
                    }
                    // If we have more than one song 
                    if (songs.Count >= 1)
                    {
                        // Build the message to send to the mods
                        string messageinfo = "";
                        foreach (var song in songs)
                        {
                            messageinfo = messageinfo + song.RequesterUsername + ": https://www.youtube.com/watch?v=" + song.YoutubeId + " : Pri: " + song.Priority + ": Approved: " + song.Approved.ToString() + Environment.NewLine;
                        }
                        // Send the message
                        if (messageinfo != string.Empty)
                        {
                            DiscordFunctions.EmbedThis(message, null, messageinfo.Truncate(1900), "green");
                        }
                        return;
                    }
                    // Tell them we didnt find any songs
                    else
                    {
                        DiscordFunctions.EmbedThis(message, null, "No songs in the live queue", "green");
                        return;
                    }
                }
                // Tell them they are not a mod
                else
                {
                    DiscordFunctions.EmbedThis(message, null, "You are not a mod", "red");
                    return;
                }
            }
            // Check if the command was to make a song a priority
            else if (command == "!priority")
            {
                // Validate if a user is a mod or not and we have all the parts
                if (DataFunctions.IsMod(message, roles) == true && message.Content.ToLower().Split().ToList().Count == 2)
                {
                    // Get the song ID and check if its in the queue
                    var id = YoutubeClient.ParseVideoId(message.Content.Split().ToList()[1]);
                    var queuedb = db.GetCollection<Song>("Queue");
                    var results = queuedb.Find(x => x.YoutubeId == id);
                    // If we found the song set the priority to the highest we can
                    if (results.ToList().Count > 0)
                    {
                        Song firstsong = results.First();
                        firstsong.Priority = 0;
                        queuedb.Update(firstsong);
                        DiscordFunctions.EmbedThis(message, null, "Song has been made high ranking", "Green");
                        return;
                    }
                    // Else just tell them we didnt find it 
                    else
                    {
                        DiscordFunctions.EmbedThis(message, null, "Song not found", "yellow");
                        return;
                    }

                }
                // Warn the mod they didnt supply a full command
                else if (DataFunctions.IsMod(message, roles) == true && message.Content.ToLower().Split().ToList().Count != 2)
                {
                    DiscordFunctions.EmbedThis(message, null, "You did not supply a full command.", "yellow");
                    return;
                }
                // Warn them they are not a mod
                else
                {
                    DiscordFunctions.EmbedThis(message, null, "You are not a mod", "red");
                    return;
                }
            }
            // Check if the command is to approve the video
            else if (command == "!approve")
            {
                // Check if they are a mod and if we have all the parts
                if (DataFunctions.IsMod(message, roles) == true && message.Content.ToLower().Split().ToList().Count == 2)
                {
                    // Check if the song is in the queue
                    var id = YoutubeClient.ParseVideoId(message.Content.Split().ToList()[1]);
                    var queuedb = db.GetCollection<Song>("Queue");
                    var results = queuedb.Find(x => x.YoutubeId == id);
                    // Approve the song in the DB
                    if (results.ToList().Count > 0)
                    {
                        Song firstsong = results.First();
                        firstsong.Approved = true;
                        queuedb.Update(firstsong);
                        DiscordFunctions.EmbedThis(message, null, "Song has been approved", "Green");
                        return;
                    }
                    // Tell them we didnt find the song
                    else
                    {
                        DiscordFunctions.EmbedThis(message, null, "Song not found", "yellow");
                        return;
                    }

                }
                // If they are a mod and they dont have all the parts warn them we dont have everything
                else if (DataFunctions.IsMod(message, roles) == true && message.Content.ToLower().Split().ToList().Count != 2)
                {
                    DiscordFunctions.EmbedThis(message, null, "You did not supply a full command.", "yellow");
                    return;
                }
                // Tell them they are not a mod
                else
                {
                    DiscordFunctions.EmbedThis(message, null, "You are not a mod", "red");
                    return;
                }
            }
            // If the command is the description command
            else if (command == "!description")
            {
                // Check if they are a mod and if we have all the parts
                if (DataFunctions.IsMod(message, roles) == true && message.Content.ToLower().Split().ToList().Count == 2)
                {
                    // Parse the video information
                    var id = YoutubeClient.ParseVideoId(message.Content.Split().ToList()[1]);
                    YoutubeExplode.Models.Video video = null;
                    var client = new YoutubeClient();
                    // Attempt to get the video with a retry limit of 3 times
                    for (int attempts = 0; attempts < 3; attempts++)
                    {
                        try
                        {
                            video = await client.GetVideoAsync(id);
                            break;
                        }
                        catch { }
                        Thread.Sleep(2000);
                    }
                    // Get the description using the title of the video we grabbed
                    var dis = await SongRequest.GetDescriptionAsync(video.Title);
                    // Send a message with the description limited to the discord character limit
                    if (dis != string.Empty)
                    {
                        DiscordFunctions.EmbedThis(message, "https://siivagunner.fandom.com/wiki/" + video.Title.Replace(" ", "_"), dis.Truncate(1900), "Green");
                    }

                }
                // If the user is a mod and they supplied all the parts
                else if (DataFunctions.IsMod(message, roles) == true && message.Content.ToLower().Split().ToList().Count != 2)
                {
                    DiscordFunctions.EmbedThis(message, null, "You did not supply a full command.", "yellow");
                    return;
                }
                // Warn them they are not a mod
                else
                {
                    DiscordFunctions.EmbedThis(message, null, "You are not a mod", "red");
                    return;
                }
            }
            // Check if the command is to toggle requests on or off
            else if (command == "!togglerequests")
            {
                // Check if they are a mod
                if (DataFunctions.IsMod(message, roles) == true)
                {
                    // If requests are already enabled
                    if (FileFunctions.RequestsEnabled() == true)
                    {
                        // Delete the requests file and notify the status
                        try
                        {
                            File.Delete("./allowrequests.json");
                        }
                        catch { }
                        DiscordFunctions.EmbedThis(message, null, "Song requests are now disabled", "orange");
                    }
                    else
                    {
                        // Create the file to enable the songs
                        try
                        {
                            File.Create("./allowrequests.json");
                        }
                        catch { }
                        DiscordFunctions.EmbedThis(message, null, "Song requests are now enabled", "orange");
                    }
                }
                // Warn the user they are not a mod
                else
                {
                    DiscordFunctions.EmbedThis(message, null, "You are not a mod", "red");
                    return;
                }
            }
            // Check if the command is to see if songs are unapproved
            else if (command == "!unapproved")
            {
                // Check if the user is a mod
                if (DataFunctions.IsMod(message, roles) == true)
                {
                    // Get the queue in order and the approved status
                    var queuedb = db.GetCollection<Song>("Queue");
                    List<Song> songs = new List<Song>();
                    var results0 = queuedb.Find(x => x.Priority == 0 && x.Approved == false);
                    var results1 = queuedb.Find(x => x.Priority == 1 && x.Approved == false);
                    var results2 = queuedb.Find(x => x.Priority == 2 && x.Approved == false);
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
                    // Check if the count of songs is greater than one
                    if (songs.Count >= 1)
                    {
                        // Combine the songs into a single string
                        string messageinfo = "";
                        foreach (var song in songs)
                        {
                            messageinfo = messageinfo + song.RequesterUsername + ": https://www.youtube.com/watch?v=" + song.YoutubeId + " : Pri: " + song.Priority + ": Approved: " + song.Approved.ToString() + Environment.NewLine;
                        }
                        // Send them the list of unapproved songs
                        if (messageinfo != string.Empty)
                        {
                            DiscordFunctions.EmbedThis(message, "Unapproved songs", messageinfo.Truncate(1900), "orange");
                        }
                        return;
                    }
                    // Tell them there are no songs
                    else
                    {
                        DiscordFunctions.EmbedThis(message, "Unapproved songs", "No songs are unapproved", "green");
                    }
                }
                // Tell them they are not a mod
                else
                {
                    DiscordFunctions.EmbedThis(message, null, "You are not a mod", "red");
                    return;
                }
            }
            // Check if the command is history
            else if (command == "!history")
            {
                // Check if the user is a mod
                if (DataFunctions.IsMod(message, roles) == true)
                {
                    // Get all of the history from the DB
                    var hisdb = hidb.GetCollection<History>("History");
                    var results = hisdb.FindAll();
                    string mess = "";
                    // Check if the song has been played and if its not a bad song
                    foreach (var song in results)
                    {
                        if (song.Played == true && song.BadSong == false)
                        {
                            // Add it to the string list
                            mess = mess + " https://www.youtube.com/watch?v=" + song.YoutubeId + "\n";
                        }
                    }
                    // Write the data to a file and upload it to discord
                    if (mess != string.Empty)
                    {
                        System.IO.File.WriteAllText("./History.csv", mess);
                        await message.Channel.SendFileAsync("./History.csv", "Song history");
                        System.IO.File.Delete("./History.csv");
                    }
                    // Tell them nothing has been played
                    else
                    {
                        DiscordFunctions.EmbedThis(message, null, "No songs have been played", "green");
                    }
                }
                // Tell the user they are not a mod
                else
                {
                    DiscordFunctions.EmbedThis(message, null, "You are not a mod", "red");
                    return;
                }
            }
            // Wednesday
            else if (command == "!wednesday")
            {
                // Wednesday
                var toEmebed = new EmbedBuilder();
                toEmebed.WithDescription("***It is Wednesday,***\n" + "***my dudes***\n");
                toEmebed.WithImageUrl("https://i.imgur.com/2SRddtz.jpg");
                toEmebed.WithColor(Color.Blue);

                // Send the message Wednesday
                await message.Channel.SendMessageAsync("", false, toEmebed.Build());
                return;
            }
            // Build the help command
            else if (command == "!help")
            {
                // Build all of the list of commands into a single template
                var generalHelp = new EmbedBuilder();
                generalHelp.WithTitle("Siivabot Commands");
                generalHelp.AddField("Help", "Shows this command");
                generalHelp.AddField("Songrequest <video> <user>", "Requests a youtube song to the queue (If you are a mod you can supply user to submit it as a user)");
                generalHelp.AddField("Remove <video>", "Removes the song requested, if no video is specified removes first song from user");
                generalHelp.AddField("BanSong <video>", "Mod: Allows mods to ban a song from being played");
                generalHelp.AddField("Queue", "Mod: Prints the order of songs in text");
                generalHelp.AddField("Unapproved", "Mod: Prints the list of songs unapproved");
                generalHelp.AddField("History", "Mod: Uploads a csv file of the history of songs played");
                generalHelp.AddField("Priority <video>", "Mod: Allows mods to set a song to the highest priority");
                generalHelp.AddField("Approve <video>", "Mod: Allows mods to approve a song if it is SFW or not");
                generalHelp.AddField("ToggleRequests", "Mod: Allows mods to disable or enable song requests for users (default disabled)");
                generalHelp.AddField("Description <video>", "Mod: Prints out the description of the video posted");
                generalHelp.AddField("Wednesday", "It's Wednesday my dudes");
                generalHelp.WithThumbnailUrl("https://vignette.wikia.nocookie.net/youtube/images/8/8b/SiIvaGunner.png");
                generalHelp.WithColor(Color.Blue);
                // Build the template and send the command
                await message.Channel.SendMessageAsync("", false, generalHelp.Build());
                return;
            }
        }
    }
}
