namespace PartnerPlusPoints
{
    public sealed class Settings
    {
        public string? ChannelID { get; set; }
        public string? GQLAuth { get; set; }
        public string? APIAuth { get; set; }
        public string? OutputString { get; set; }
        public int UpdateInterval { get; set; }
        public int LifetimeTierThrees { get; set; }
        public int PartnerPlusPoints { get; set; }
        public int GoalPoints { get; set; }
        public bool DisplayActualCalc { get; set; } // Twitch calculates Partner Plus Points with a number that starts from 0 at the beginning of every month and increments every new or renewed (non-gifted) sub. This bool is to display that as opposed to a fluctuating, day-to-day number. 
        public bool StoreSensitiveInfo { get; set; }
        public bool FirstRun { get; set; }
    }
}
