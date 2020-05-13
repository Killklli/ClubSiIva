namespace ClubSiivaWPF.Data
{
    public class Song
    {
        /// <summary>
        /// The LiteDB ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// The id of the youtube song
        /// </summary>
        public string YoutubeId { get; set; }
        /// <summary>
        /// The DiscordID of the requestor
        /// </summary>
        public ulong RequesterId { get; set; }
        /// <summary>
        /// The Discord Username of the requestor
        /// </summary>
        public string RequesterUsername { get; set; }
        /// <summary>
        /// The Title of the video from youtube
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// The duration of the song
        /// </summary>
        public string Duration { get; set; }
        /// <summary>
        /// The temporary file to dump the video into
        /// </summary>
        public string File { get; set; }
        /// <summary>
        /// The Priority level of the song in the queue
        /// 0 is Mods/Songs set as a priority
        /// 1 is people who have not requested a song before
        /// 2 is people who have already requested a song
        /// </summary>
        public int Priority { get; set; }
        /// <summary>
        /// The description of the video pulled from https://siivagunner.fandom.com/
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// If the song is mod approved or not
        /// </summary>
        public bool Approved { get; set; }
        public ulong ApprovalMessage { get; set; }
    }
}
