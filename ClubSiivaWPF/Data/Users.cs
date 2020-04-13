namespace ClubSiivaWPF.Data
{
    class Users
    {
        /// <summary>
        /// The LiteDB Id of the entry
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// The people who requested songs Discord ID
        /// </summary>
        public ulong DiscordUserId { get; set; }
        /// <summary>
        /// The bool if a user has already requested a song or not
        /// </summary>
        public bool AlreadyRequested { get; set; }
    }
}
