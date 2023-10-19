namespace PartnerPlusPoints
{
    public class FatalErrors
    {
        public static readonly Dictionary<int, string> Errors = new()
        {
            { 1, "Your operating system is not supported." },
            { 2, "This program will not function without a local Firefox install. Try again later." },
            { 3, "This program will not function without a GQL OAuth token or a User ID. Try again later." },
            { 4, "Failed to run the Firefox bash script. Try again later." },
            { 5, "The Twitch API has returned a fatal error. Try again later." },
            { 6, "Failed to create a required file. Try again later." },
            { 7, "Failed to create a required directory. Try again later." }
        };
    }
}

