using System.Timers;

namespace PartnerPlusPoints
{

    // TODO: Update fatal errors to show exception.
    // Look, I get it, "only catch specific errors," but I just want this to be done. It'll be handled later, I just want to put this out there and have it usable for now.

    public class Program
    {
        public static Settings UserSettings = Task.Run(SettingsManager.GetSettings).GetAwaiter().GetResult();
        public static string? TempGQLAuthToken = UserSettings.GQLAuth; // This is the variable used when the GQL authorization token is not stored.
        public static int PartnerPlusPoints = 0;
        public static readonly OperatingSystemInfo UserOS = new();
        public static string? BashScript;
        private static string NextCheckTime = "";
        private static readonly System.Timers.Timer GQLTimer = new();

        static void Main(string[] args)
        {
            Console.Clear();

            for (var i = 0; i < args.Length; i++)
            {
                var argument = args[i];
                int dictionaryIndex = -1;

                if (argument.StartsWith("--")) { argument = argument[2..]; }
                else if (argument.StartsWith("-")) { argument = argument[1..]; }
                else
                {
                    Console.WriteLine($"Invalid argument at index {i}.");
                    Environment.Exit(1);
                }

                if (char.TryParse(argument, out char argChar)) { dictionaryIndex = SettingsManager.LaunchArguments.FindIndex(arg => arg.ArgumentCode == argChar); }
                else { dictionaryIndex = SettingsManager.LaunchArguments.FindIndex(arg => arg.FullArgument == argument); }

                switch (dictionaryIndex) {
                    case 0: // Help argument
                        Console.WriteLine("Syntax example: ./PartnerPlusPoints.exe [command] <argument>\n");
                        foreach (var arg in SettingsManager.LaunchArguments) { ConsoleHelper.WriteLine($"-{arg.ArgumentCode} or --{arg.FullArgument}: {arg.Description}\n"); }
                        Environment.Exit(0);
                        break;
                    case 1: // GQLToken argument
                        var gqlToken = args[i + 1];
                        TempGQLAuthToken = gqlToken;
                        i++;
                        break;
                    case 2: // NoStore argument
                        if (UserSettings.StoreSensitiveInfo)
                        {
                            UserSettings.GQLAuth = null;
                            UserSettings.StoreSensitiveInfo = false;
                        }
                        break;
                    case 3: // Reset argument
                        if (args.Length > 1)
                        {
                            Console.WriteLine("The argument -r can't be used with other arguments. Please try again.");
                            Environment.Exit(1);
                        }
                        else
                        {
                            UserSettings = new Settings();
                            if (File.Exists("Settings.json")) { File.Delete("Settings.json"); }
                            Console.WriteLine("Settings reset. Please launch the program again.");
                            Environment.Exit(0);
                        }
                        break;
                    case 4: // TwitchToken argument
                        var apiToken = args[i + 1];
                        UserSettings.APIAuth = apiToken;
                        i++;
                        break;
                    default:
                        Console.WriteLine($"Invalid argument at index {i}.");
                        Environment.Exit(1);
                        break;
                }
            }

            Console.ForegroundColor = ConsoleColor.White;

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            Task.Run(Startup).GetAwaiter().GetResult();

            string? userInput = Console.ReadLine();
            while (true)
            {
                if (userInput != null) { userInput = userInput.ToLower(); }
                ConsoleHelper.HandleCommand(userInput);
                userInput = Console.ReadLine();
            }
        }

        static async Task Startup()
        {
            if (UserSettings.FirstRun)
            {
                ConsoleHelper.WriteLine("Welcome to PartnerPlusPoints.");
                ConsoleHelper.WriteLine("This program works by using Twitch's GraphQL endpoint to retrieve subscription information, and a Twitch API endpoint to get new subs as they happen.");
                ConsoleHelper.Warn("This is an undocumented endpoint and is not meant for public use.");
                ConsoleHelper.WriteLine("A version of this program using the Helix endpoint will release in the future.");
                ConsoleHelper.WriteLine("If you would like to save and quit, press Q. Otherwise, press any other key. ");
                if (Console.ReadKey().Key == ConsoleKey.Q) { Environment.Exit(0); }
                Console.Clear();

                ConsoleHelper.WriteLine("Because your Twitch OAuth token used for GraphQL is sensitive, you may choose to not store it in the settings file.");
                var storeSensitiveInfo = ConsoleHelper.PromptYesNo("Would you like to store your Twitch OAuth token?");
                if (storeSensitiveInfo) { UserSettings.StoreSensitiveInfo = true; }
                Console.Clear();

                ConsoleHelper.WriteLine("Partner Plus Points can be displayed in two different ways. They can either be displayed as a fluctuating number like sub points, or as a progressive number over the span of a month.");
                ConsoleHelper.WriteLine("For example, the number will start at 0 on January 1st and counts up for every (non-gifted) new and renewed sub.");
                if (ConsoleHelper.PromptYesNo("Would you like your Partner Plus Points to show as a progressive number?")) { UserSettings.DisplayActualCalc = true; }
                Console.Clear();
            }

            if (UserSettings.LifetimeTierThrees == -1)
            {
                ConsoleHelper.WriteLine("Any lifetime tier three subs you have given away do not count towards your Partner Plus Points.");
                ConsoleHelper.WriteLine("You will need to enter the number of lifetime tier three subs you have given to users or bots, but do not include your own lifetime tier three sub.");
                string lifetimeTierThrees = ConsoleHelper.PromptQuestion("Enter the number of lifetime tier three subs you have given away");
                int convertedLifetime;
                while (!int.TryParse(lifetimeTierThrees, out convertedLifetime) || convertedLifetime < 0 || convertedLifetime > 5)
                {
                    Console.WriteLine("Invalid value given. Remember that your input must be a number between 0 and 5 inclusive.");
                    lifetimeTierThrees = ConsoleHelper.PromptQuestion("Enter the number of lifetime tier three subs you have given away");
                }
                UserSettings.LifetimeTierThrees = convertedLifetime;
                Console.Clear();
            }

            if (UserSettings.GoalPoints == -1)
            {
                ConsoleHelper.WriteLine("You may change the displayed goal from 350 to whatever number you wish. 350 is the default.");
                ConsoleHelper.WriteLine("Enter the number of Partner Plus Points you would like to be your goal number. If you enter nothing, the default will be used.");
                ConsoleHelper.WriteLine("You may change this at any time by typing \"goal $number\" in the console while the program is running, with $number being your new goal.");
                Console.Write("Enter your goal number of Partner Plus Points: ");
                string? goalPoints = Console.ReadLine();
                int convertedPoints = 350;
                while (!string.IsNullOrWhiteSpace(goalPoints) && !int.TryParse(goalPoints, out convertedPoints))
                {
                    Console.WriteLine("Invalid value given. Please enter any number, or press enter to use the default of 350.");
                    Console.Write("Enter your goal number of Partner Plus Points: ");
                    goalPoints = Console.ReadLine();
                }
                UserSettings.GoalPoints = convertedPoints;
                Console.Clear();
            }

            if (string.IsNullOrWhiteSpace(UserSettings.OutputString))
            {
                ConsoleHelper.WriteLine("This program will output your Partner Plus Points in a text file. You may add extra text to the output for use in a program like OBS.");
                ConsoleHelper.WriteLine("To configure this setting, please enter any string you would like displayed. Include two or three asterisks (** or ***), which will be replaced by your actual Partner Plus Points count.");
                ConsoleHelper.WriteLine("Two asterisks (**) will simply be replaced with the number of Partner Plus Points you have. Three asterisks (***) will be replaced with the fraction of Partner Plus Points you have versus what your goal points are.");
                ConsoleHelper.WriteLine("For example, the string \"Partner Plus Points: **\" will be displayed as \"Partner Plus Points: 172\". If it were three asterisks (***), it would be \"Partner Plus Points: 172/350\".");
                var outputString = ConsoleHelper.PromptQuestion("Please enter the string you would like displayed");
                while (!outputString.Contains("**"))
                {
                    Console.WriteLine("Your string does not include two or three asterisks (** or ***).");
                    ConsoleHelper.PromptQuestion("Please enter the string you would like displayed");
                }
                UserSettings.OutputString = outputString;
                Console.Clear();
            }

            UserSettings.FirstRun = false;

            if (string.IsNullOrWhiteSpace(TempGQLAuthToken) || string.IsNullOrWhiteSpace(UserSettings.APIAuth)) { await BrowserSetup(); }
            else
            {
                if (!await APIManager.ValidateToken()) { await BrowserSetup(); }

                var tokenCheckResponse = await APIManager.GetPartnerPlusPoints();
                if (tokenCheckResponse == -2) { ConsoleHelper.HandleFatalError(5); }
                else if (tokenCheckResponse == -1)
                {
                    ConsoleHelper.Failure();
                    ConsoleHelper.Warn("One or both of your API tokens have expired. You must generate new ones.");
                    await BrowserSetup();
                }
                else
                {
                    ConsoleHelper.Success();
                    if (UserSettings.StoreSensitiveInfo && string.IsNullOrEmpty(UserSettings.GQLAuth)) { UserSettings.GQLAuth = TempGQLAuthToken; }
                    PartnerPlusPoints = tokenCheckResponse;
                    await WritePointsFile();
                }
            }

            await SettingsManager.WriteSettings(UserSettings);

            GQLTimer.Interval = UserSettings.UpdateInterval * 60000;
            GQLTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            GQLTimer.Enabled = true;
            NextCheckTime = DateTime.Now.AddMinutes(5).ToString("hh:mm tt");
            await APIManager.ListenForSubs();
            Console.WriteLine($"You have {PartnerPlusPoints}/{UserSettings.GoalPoints} Partner Plus Points.");
            Console.WriteLine($"Next API check will occur at {NextCheckTime}.");
        }

        static async Task BrowserSetup()
        {
            if (UserOS.OperatingSystem != OS.Windows) { BashScript = await BashHelper.CreateTempScript(); }

            ConsoleHelper.WriteLine("To get your OAuth tokens, you must sign in to Twitch using a provided Firefox browser.");
            ConsoleHelper.WriteLine("This browser is downloaded directly from Mozilla, and the cookies will be cleared after the token is obtained.");
            ConsoleHelper.WriteLine("If PartnerPlusPoints has already downloaded the latest version, that will be launched.");
            ConsoleHelper.WriteLine("You will need to log in, and then approve access to \"Partner Plus Points Counter.\" After that you may close the browser.");
            ConsoleHelper.WriteLine("If you would like to save and quit, press Q. Otherwise, press any key to download and/or start Firefox.");
            if (Console.ReadKey().Key == ConsoleKey.Q) { Environment.Exit(0); }
            Console.Clear();

            Version? localFirefoxVersion = await BrowserManager.GetCurrentBrowserVersion();
            Version? latestFirefoxVersion = await BrowserManager.GetLatestBrowserVersion();
            bool firefoxDownloadSuccess = false;

            switch (localFirefoxVersion)
            {
                case null: // No local Firefox install found.
                    if (latestFirefoxVersion == null)
                    {
                        ConsoleHelper.Failure();
                        ConsoleHelper.Warn("Could not get the latest Firefox version.");
                        if (ConsoleHelper.PromptYesNo("Try to download Firefox anyway?")) { firefoxDownloadSuccess = await BrowserManager.DownloadBrowser(); }
                    }
                    else
                    {
                        Console.WriteLine($"version {latestFirefoxVersion} is available for download.");
                        firefoxDownloadSuccess = await BrowserManager.DownloadBrowser();
                    }
                    if (!firefoxDownloadSuccess) { ConsoleHelper.HandleFatalError(2); }
                    break;
                default: // Local Firefox install found.
                    if (latestFirefoxVersion == null)
                    {
                        ConsoleHelper.Failure();
                        ConsoleHelper.Warn("Could not get latest Firefox version. The current install will be used.");
                    }
                    else if (latestFirefoxVersion.Major > localFirefoxVersion.Major) // The browser we use must be within 2 major revisions of the latest release (inclusive), otherwise Twitch will not allow us to sign in.
                    {
                        Console.WriteLine($"version {latestFirefoxVersion} is available for download.");
                        firefoxDownloadSuccess = await BrowserManager.DownloadBrowser();
                        if (!firefoxDownloadSuccess) { Console.WriteLine("The current install will be used instead."); }
                    }
                    else { Console.WriteLine("Your Firefox version is up to date."); }
                    break;
            }

            if (firefoxDownloadSuccess)
            {
                var extractionSuccess = await BrowserManager.ExtractBrowser();
                if (!extractionSuccess)
                {
                    if (localFirefoxVersion != null) { ConsoleHelper.Warn("Could not extract Firefox. The current install will be used instead."); }
                    else { ConsoleHelper.HandleFatalError(2); }
                }
            }

            await BrowserManager.RunBrowser();

            var checkToken = await APIManager.GetPartnerPlusPoints();
            if (checkToken < -1) { ConsoleHelper.HandleFatalError(5); }
            else if (checkToken == -1) { ConsoleHelper.HandleFatalError(3); }
            PartnerPlusPoints = checkToken;
            ConsoleHelper.Success();
            await WritePointsFile();
            BashHelper.DeleteTempScript();
        }

        public static async Task UpdatePoints()
        {
            var newPoints = await APIManager.GetPartnerPlusPoints();

            if (newPoints == -1)
            {
                ConsoleHelper.Failure();
                ConsoleHelper.Warn("Your GraphQL OAuth token has expired. Press any key to generate a new one.");
                Console.ReadLine();
                await BrowserSetup();
            }
            else if (newPoints < -1)
            {
                ConsoleHelper.Failure();
                ConsoleHelper.Warn("The Twitch API returned an error. Skipping this check.");
            }
            else
            {
                ConsoleHelper.Success();
                if (!UserSettings.DisplayActualCalc || newPoints > PartnerPlusPoints)
                {
                    PartnerPlusPoints = newPoints;
                    await WritePointsFile();
                }
                Console.WriteLine($"You have {PartnerPlusPoints}/{UserSettings.GoalPoints} Partner Plus Points.");
            }
        }

        public static async Task UpdatePoints(int subPoints)
        {
            GQLTimer.Stop();

            PartnerPlusPoints += subPoints;
            await WritePointsFile();
            Console.WriteLine($"You have {PartnerPlusPoints}/{UserSettings.GoalPoints} Partner Plus Points.");
            Console.WriteLine($"Next API check will occur at {NextCheckTime}.");

            GQLTimer.Start();
        }

        public static async Task WritePointsFile()
        {
            Console.Write("Writing Partner Plus Points to file... ");

            string? userString = UserSettings.OutputString;
            var goal = UserSettings.GoalPoints;

            if (string.IsNullOrWhiteSpace(userString))
            {
                ConsoleHelper.Warn("Your output string has not been set. Using the default instead.");
                userString = "Partner Plus Points: ***";
            }

            if (userString.Contains("***")) { userString = userString.Replace("***", $"{PartnerPlusPoints}/{goal}"); }
            else if (userString.Contains("**")) { userString = userString.Replace("**", $"{PartnerPlusPoints}"); }
            else
            {
                ConsoleHelper.Warn("Your output string is invalid. Using the default instead.");
                userString = $"Partner Plus Points: {PartnerPlusPoints}/{goal}";
            }

            try
            {
                await File.WriteAllTextAsync(Path.Combine(Directory.GetCurrentDirectory(), "output.txt"), userString);
                ConsoleHelper.Success();
            }
            catch (Exception e)
            {
                ConsoleHelper.Failure();
                Console.WriteLine(e.Message);
                ConsoleHelper.Warn("Could not write Partner Plus Points to file. This operation will be attempted at the next check.");
            }
        }

        static async void OnTimedEvent(object? sender, ElapsedEventArgs e)
        {
            Console.Clear();
            await UpdatePoints();
            NextCheckTime = DateTime.Now.AddMinutes(5).ToString("hh:mm tt");
            Console.WriteLine($"Next API check will occur at {NextCheckTime}.");
        }

        static async void OnProcessExit(object? sender, EventArgs e)
        {
            await SettingsManager.WriteSettings(UserSettings);
        }
    }
}