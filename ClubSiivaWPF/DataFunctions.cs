using ClubSiivaWPF.Data;
using Discord.WebSocket;
using LiteDB;
using System.Collections.Generic;
using System.Linq;
using YoutubeExplode;

namespace ClubSiivaWPF
{
    class DataFunctions
    {
        /// <summary>
        /// Function to check if the user is a mod
        /// </summary>
        /// <param name="user">The SocketUser to check against</param>
        /// <returns>Returns true or false</returns>
        public static bool IsMod(SocketMessage user, List<string> roles)
        {
            try
            {
                // Get the guild we are in
                var chnl = user.Channel as SocketGuildChannel;
                // If we are a DM dont do anything
                if (chnl.Guild != null)
                {
                    // Check if the owner of the guild is the one messaging
                    var GuildOwner = chnl.Guild.OwnerId;
                    if (user.Author.Id == GuildOwner)
                    {
                        return true;
                    }
                    // Else check the requestors role if they are a mod or not
                    foreach (SocketRole rolesfound in ((SocketGuildUser)user.Author).Roles)
                    {
                        // Check against the config roles
                        foreach (var modrole in roles)
                        {
                            if (rolesfound.Name.ToLower().Contains(modrole.ToLower()))
                            {
                                return true;
                            }
                        }
                    }
                    return false;
                }
                else
                {
                    return false;
                }
            }
            catch { return false; }
        }
        /// <summary>
        /// Check if a user has a song in the queue
        /// </summary>
        /// <param name="user">The SockerUser to check against</param>
        /// <param name="db">The database we want to check in</param>
        /// <returns>Returns true or false</returns>
        public static bool UsersSongInQueue(SocketUser user, LiteDatabase db)
        {
            // Get the Queue data
            var queuedb = db.GetCollection<Song>("Queue");
            var results = queuedb.Find(x => x.RequesterId == user.Id);
            // If we found any thing with their name on it, return true
            if (results.Count() >= 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// Checks if the song ID provided is a bad song
        /// </summary>
        /// <param name="id">The ID to check against</param>
        /// <param name="hidb">The history DB</param>
        /// <returns>True or False</returns>
        public static bool IsBadSong(string id, LiteDatabase hidb)
        {
            // Find if the song is in the DB
            var historydb = hidb.GetCollection<History>("History");
            var results = historydb.Find(x => x.BadSong == true && x.YoutubeId == id);
            // Return true if we find any results
            if (results.ToList().Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// Function to remove a song by its youtube ID
        /// </summary>
        /// <param name="song">The Song ID to remove</param>
        /// <param name="db">The queue DB</param>
        /// <param name="user">The SocketUser to check</param>
        /// <returns>The song title as a string</returns>
        public static string RemoveSongBySongId(string song, LiteDatabase db, SocketUser user = null)
        {
            // Try to get the id of the song
            var queuedb = db.GetCollection<Song>("Queue");
            var id = song;
            // Parse it in case we dont already have it as an ID
            try
            {
                id = song;
            }
            catch
            {
            }
            // Find the results
            IEnumerable<Song> results;
            if (user != null)
            {
                results = queuedb.Find(x => x.YoutubeId == id && x.RequesterId == user.Id);
            }
            else
            {
                results = queuedb.Find(x => x.YoutubeId == id);
            }
            // If the results are found return the ID and delete the song from the queue
            if (results.Count() >= 1)
            {
                string title = results.First().Title;
                queuedb.Delete(results.First().Id);
                return title;
            }
            return "";

        }
        /// <summary>
        /// Remove the song by a user rather than the song ID
        /// </summary>
        /// <param name="user">The sockeruser to search for</param>
        /// <param name="db">The Queue DB</param>
        /// <returns>Returns the song removed</returns>
        public static string RemoveSongBySongUser(SocketUser user, LiteDatabase db)
        {
            // Find songs by the user
            var queuedb = db.GetCollection<Song>("Queue");
            var results = queuedb.Find(x => x.RequesterId == user.Id);
            // Remove the song if we find it and delete the file if its in the queue
            if (results.Count() >= 1)
            {
                FileFunctions.DeleteTmpFile(results.First().File);
                string title = results.First().Title;
                queuedb.Delete(results.First().Id);
                return title;
            }
            return "";

        }
        /// <summary>
        /// Check if the user has already requested something
        /// </summary>
        /// <param name="user">The SocketUser to search for</param>
        /// <param name="db">The User DB to check</param>
        /// <returns>Returns true or false</returns>
        public static bool AlreadyRequested(SocketUser user, LiteDatabase db)
        {
            // Find the user in the database
            var queuedb = db.GetCollection<Users>("Users");
            var results = queuedb.Find(x => x.DiscordUserId == user.Id);
            // If we found the user
            if (results.Count() >= 1)
            {
                // Check If the users status is already requested
                if (results.First().AlreadyRequested == true)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            // else just return false
            else
            {
                return false;
            }
        }
    }
}
