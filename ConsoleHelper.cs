using System.Diagnostics;
using System.Reflection;

namespace PartnerPlusPoints
{
    public class ConsoleHelper
    {
        public static void WriteLine(string paragraph, int tabSize = 8) // This WriteLine method supports word wrap, which is important for the very long text on setup. Stolen from https://stackoverflow.com/questions/20534318/make-console-writeline-wrap-words-instead-of-letters
        {
            if (string.IsNullOrWhiteSpace(paragraph)) { return; }

            string[] lines = paragraph
            .Replace("\t", new string(' ', tabSize))
            .Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                string process = lines[i];
                List<string> wrapped = new();

                while (process.Length > Console.WindowWidth)
                {
                    int wrapAt = process.LastIndexOf(' ', Math.Min(Console.WindowWidth - 1, process.Length));
                    if (wrapAt <= 0) break;

                    wrapped.Add(process[..wrapAt]);
                    process = process.Remove(0, wrapAt + 1);
                }

                foreach (string wrap in wrapped)
                {
                    Console.WriteLine(wrap);
                }

                Console.WriteLine(process);
            }
        }

        public static void Success()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("success.");
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static void Failure()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("failed.");
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static void Warn(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static bool PromptYesNo(string message)
        {
            message += " y/n: ";
            Console.Write(message);
            char response = char.ToLower(Console.ReadKey().KeyChar);
            while (!(response == 'y' || response == 'n'))
            {
                Console.WriteLine("\nInvalid response. Please enter y for yes or n for no.");
                Console.Write(message);
                response = char.ToLower(Console.ReadKey().KeyChar);
            }

            Console.WriteLine();
            if (response == 'n') { return false; }
            else { return true; }
        }

        public static string PromptQuestion(string message)
        {
            message += ": ";
            Console.Write(message);
            var response = Console.ReadLine();
            while (string.IsNullOrWhiteSpace(response))
            {
                Console.WriteLine("\nInvalid response. Please type a response.");
                Console.Write(message);
                response = Console.ReadLine();
            }

            Console.WriteLine();
            return response;
        }

        public static void HandleFatalError(int errorCode)
        {
            DisplayFatalError(errorCode);
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
            Environment.Exit(errorCode);
        }

        public static void HandleFatalError(int errorCode, Exception exception)
        {
            DisplayFatalError(errorCode);
            if (PromptYesNo("Would you like to view additional error information?"))
            {
                Console.WriteLine(exception.Message);
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
            Environment.Exit(errorCode);
        }

        private static void DisplayFatalError(int errorCode)
        {
            Failure();
            string errorDescription = FatalErrors.Errors.GetValueOrDefault(errorCode) ?? "An unknown error occurred. Try again later.";
            Console.WriteLine(errorDescription);
        }

        public static async void HandleCommand(string? command)
        {
            if (command != null) { command = command.Split(' ').FirstOrDefault(); }
            switch (command)
            {
                case "settings":
                    SettingsMenu();
                    break;
                case "update":
                    await Program.UpdatePoints();
                    break;
                case "quit":
                    Environment.Exit(0);
                    break;
                default:
                    WriteLine("Unrecognized command. Type \"settings\" to update your settings, \"update\" to force a Partner Plus Points update, or \"quit\" to quit.");
                    break;
            }
        }

        private static async void SettingsMenu()
        {
            var doneUpdatingSettings = false;

            Console.Clear();
            while (!doneUpdatingSettings)
            {
                Console.WriteLine("You may update the following settings:");
                Console.WriteLine("1) Output string");
                Console.WriteLine("2) Goal points");
                Console.WriteLine("3) Point display method");
                Console.WriteLine("4) Lifetime tier 3 subs");
                Console.WriteLine("5) Store sensitive info");
                Console.WriteLine("6) API update interval");
                var inputString = PromptQuestion("Select an option or press q to return");

                switch (inputString)
                {
                    case "1":
                        Console.Clear();
                        WriteLine("You will update your output string. Remember that your string must include two (**) or three (***) asterisks.");
                        WriteLine("Two asterisks will be replaced with your Partner Plus Points count, while three will be replaced with the count over the goal amount.");
                        var newString = PromptQuestion("Type a new output string, or q to go back");
                        while (!newString.Contains("**") && newString != "q")
                        {
                            WriteLine("Invalid string. Your string must contain two or three asterisks.");
                            newString = PromptQuestion("Type a new output string, or q to go back");
                        }
                        if (newString != "q")
                        {
                            Program.UserSettings.OutputString = newString;
                            await Program.UpdatePoints();
                            doneUpdatingSettings = true;
                        }
                        break;
                    case "2":
                        Console.Clear();
                        WriteLine("You will update your goal points.");
                        var newGoal = PromptQuestion("Enter your new Partner Plus Points goal, or q to go back");
                        int convertedGoal;
                        while (!int.TryParse(newGoal, out convertedGoal) && newGoal != "q")
                        {
                            WriteLine("Invalid integer. Your new goal must be a number.");
                            newGoal = PromptQuestion("Enter your new Partner Plus Points goal, or q to go back");
                        }
                        if (newGoal != "q")
                        {
                            Program.UserSettings.GoalPoints = convertedGoal;
                            await Program.UpdatePoints();
                            doneUpdatingSettings = true;
                        }
                        break;
                    case "3":
                        Console.Clear();
                        string currentSetting;
                        if (Program.UserSettings.DisplayActualCalc) { currentSetting = "progressive"; }
                        else { currentSetting = "fluctuating"; }
                        WriteLine("You will update the display method of your Partner Plus Points.");
                        WriteLine("Twitch counts Partner Plus Points by setting your total points to 0 at the start of the month, counting upwards upon every new sub and resub.");
                        WriteLine("You may also display your Partner Plus Points as a constantly fluctuating number, just like sub points.");
                        WriteLine("Your points are currently being displayed as a " + currentSetting + " number.");
                        if (PromptYesNo("Would you like to switch it?"))
                        {
                            Program.UserSettings.DisplayActualCalc ^= true;
                            Program.PartnerPlusPoints = 0;
                            await Program.UpdatePoints();
                            doneUpdatingSettings = true;
                        }
                        break;
                    case "4":
                        Console.Clear();
                        WriteLine("You will update the amount of lifetime tier three subs you have given away.");
                        WriteLine("As a partner, you can give out three lifetime tier three subs to users, and two lifetime tier three subs to bots.");
                        WriteLine($"You said you have given away {Program.UserSettings.LifetimeTierThrees} lifetime tier three subs.");
                        var newLifetimeSubs = PromptQuestion("Enter the new amount you have given away, or q to go back");
                        int convertedLifetimeSubs;
                        while (!int.TryParse(newLifetimeSubs, out convertedLifetimeSubs) && convertedLifetimeSubs <= 5 && convertedLifetimeSubs >= 0 && newLifetimeSubs != "q")
                        {
                            Console.Write("Invalid number. Remember that your lifetime sub count can only be between 0 and 5 inclusive.");
                            PromptQuestion("Enter the new amount you have given away, or q to go back");
                        }
                        if (newLifetimeSubs != "q")
                        {
                            Program.UserSettings.LifetimeTierThrees = convertedLifetimeSubs;
                            if (Program.UserSettings.DisplayActualCalc) { await SettingsManager.WriteSettings(Program.UserSettings); }
                            else { await Program.UpdatePoints(); }
                            doneUpdatingSettings = true;
                        }
                        break;
                    case "5":
                        Console.Clear();
                        string storingSettings;
                        if (Program.UserSettings.StoreSensitiveInfo) { storingSettings = "are"; }
                        else { storingSettings = "are not"; }
                        WriteLine("You will update whether or not your GQL auth token is stored.");
                        WriteLine("Your GQL auth token is very sensitive and is used to access and authorize everything you do on Twitch.");
                        WriteLine("You currently " + storingSettings + " storing your GQL auth token.");
                        if (PromptYesNo("Would you like to switch it?"))
                        {
                            Program.UserSettings.StoreSensitiveInfo ^= true;
                            if (!Program.UserSettings.StoreSensitiveInfo) { Program.UserSettings.GQLAuth = null; }
                            else { Program.UserSettings.GQLAuth = Program.TempGQLAuthToken; }
                            await SettingsManager.WriteSettings(Program.UserSettings);
                            doneUpdatingSettings = true;
                        }
                        break;
                    case "6":
                        Console.Clear();
                        WriteLine("You will update the amount of time it takes between API checks.");
                        WriteLine($"API checks currently happen once every {Program.UserSettings.UpdateInterval} minutes.");
                        Warn("Changing this setting is not recommended. We want to ping the GQL API as little as possible.");
                        var newInterval = PromptQuestion("Please enter the new interval in minutes, or q to go back");
                        int convertedInterval;
                        while (!int.TryParse(newInterval, out convertedInterval) && newInterval != "q")
                        {
                            WriteLine("Invalid interval. The new interval must be an integer.");
                            newInterval = PromptQuestion("Please enter the new interval in minutes, or q to go back");
                        }
                        if (newInterval != "q")
                        {
                            Program.UserSettings.UpdateInterval = convertedInterval;
                            await SettingsManager.WriteSettings(Program.UserSettings);
                            Warn("You must restart the application for this setting to take effect.");
                            doneUpdatingSettings = true;
                        }
                        break;
                    case "q":
                        doneUpdatingSettings = true;
                        break;
                    default:
                        Console.WriteLine("Invalid option selected.");
                        break;
                }
            }
        }
    }
}
