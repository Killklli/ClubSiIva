namespace ClubSiivaWPF.Data
{
    class History
    {
        /// <summary>
        /// ID for holding within the LiteDB database
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// The YoutubeID of the song
        /// </summary>
        public string YoutubeId { get; set; }
        /// <summary>
        /// The status if the song was played or not
        /// </summary>
        public bool Played { get; set; }
        /// <summary>
        /// The marking of if the song is bad or not
        /// </summary>
        public bool BadSong { get; set; }
    }
}
