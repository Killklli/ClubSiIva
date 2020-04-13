using Discord;
using Discord.WebSocket;
using System;
using System.IO;

namespace ClubSiivaWPF
{
    public class FileFunctions
    {
        /// <summary>
        /// Function to make the directories for storing temp data and text files
        /// </summary>
        public static void MakeTempDir()
        {
            try
            {
                // Try to make the temp file directory for videos
                if (!Directory.Exists(Directory.GetCurrentDirectory() + " /temp/"))
                {
                    Directory.CreateDirectory(Directory.GetCurrentDirectory() + "/temp/");
                }
                // Create the dir for text files
                if (!Directory.Exists(Directory.GetCurrentDirectory() + " /textfiles/"))
                {
                    Directory.CreateDirectory(Directory.GetCurrentDirectory() + "/textfiles/");
                }
            }
            catch { }
        }
        /// <summary>
        /// Delete the file passed to it if it exists
        /// </summary>
        /// <param name="tmpFile">The file to delete</param>
        public static void DeleteTmpFile(string tmpFile)
        {
            try
            {
                // Delete the temp file (if it exists)
                if (File.Exists(tmpFile))
                {
                    File.Delete(tmpFile);
                }
            }
            catch { }
        }
        /// <summary>
        /// Create a file in the temp directory for storing the video
        /// </summary>
        /// <returns>Returns the filename and path of the file</returns>
        public static string CreateTmpFile()
        {
            string fileName = string.Empty;

            try
            {
                fileName = @Directory.GetCurrentDirectory().ToString() + @"\temp\" + Guid.NewGuid().ToString() + ".mp4";
            }
            catch { }

            return fileName;
        }
        /// <summary>
        /// Check if the file exists for allowing requests
        /// </summary>
        /// <returns>Returns true or false</returns>
        public static bool RequestsEnabled()
        {
            // Check if file exists
            if (File.Exists("./allowrequests.json"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
    /// <summary>
    /// Extension class for truncating to char length
    /// </summary>
    public static class StringExt
    {
        /// <summary>
        /// Truncates the string to a character limit
        /// </summary>
        /// <param name="value">The string we are checking against</param>
        /// <param name="maxLength">The length to truncate to</param>
        /// <returns></returns>
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) { return value; }

            return value.Substring(0, Math.Min(value.Length, maxLength));
        }
    }
}
