using System.Collections.Generic;

namespace ClubSiivaWPF.Databases
{
    public class Config
    {
        /// <summary>
        /// The Discord Bot Token to use https://discordapp.com/developers/applications/
        /// </summary>
        public string DiscordToken { get; set; }
        /// <summary>
        /// The list of roles in a string that mods are to use the mod only commands
        /// </summary>
        public List<string> ModRoles { get; set; }
        /// <summary>
        /// The channel where we are going to put now playing messages
        /// </summary>
        public string MessageChannel { get; set; }
        /// <summary>
        /// The channel where we are going to ask for mod approvals in
        /// </summary>
        public string ApprovalChannel { get; set; }
        public string Guild { get; set; }

    }
}
