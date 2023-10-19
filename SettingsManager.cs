using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace PartnerPlusPoints
{
    public class SettingsManager
    {
        public struct Argument
        {
            public char ArgumentCode;
            public string FullArgument;
            public string Description;
            public Argument(char argumentCode, string fullArgument, string description)
            {
                ArgumentCode = argumentCode;
                FullArgument = fullArgument;
                Description = description;
            }
        }

        public static readonly List<Argument> LaunchArguments = new()
        {
            { new Argument('h', "help", "Displays the help message.") },
            { new Argument('g', "gqltoken", "Uses the GQL auth token provided by the user. Best used alongside -t to skip the browser entirely.") },
            { new Argument('n', "nostore", "Sets the \"store sensitive info\" variable to false, meaning your GQL auth token will not be stored. The variable is true otherwise.") },
            { new Argument('r', "reset", "Resets the program to its default state. Can't be used alongside any other arguments.") },
            { new Argument('t', "twitchtoken", "Uses the Twitch user access token provided by the user. Best used alongside -g to skip the browser entirely.") }
        };

        public async static Task<Settings> GetSettings()
        {
            Settings settings = new() { LifetimeTierThrees = -1, GoalPoints = -1, UpdateInterval = 5, StoreSensitiveInfo = false, FirstRun = true };

            if (!File.Exists("Settings.json")) { await WriteSettings(settings); }

            try
            {
                var config = new ConfigurationBuilder()
                    .AddJsonFile(Path.Combine(Environment.CurrentDirectory, "Settings.json"), optional: true, reloadOnChange: true)
                    .Build();

                settings = await Task.Run(config.Get<Settings>) ?? settings;
            }
            catch (Exception e) { Console.WriteLine(e.Message); }

            return settings;
        }

        public async static Task WriteSettings(Settings? settings)
        {
            if (!File.Exists("Settings.json"))
            {
                Console.Write("Creating settings file... ");
            }
            else
            { Console.Write("Saving settings... "); }
            try
            {
                var jsonString = JsonConvert.SerializeObject(settings, Formatting.Indented);
                await File.WriteAllTextAsync("Settings.json", jsonString);
                ConsoleHelper.Success();
            }
            catch (Exception e)
            {
                ConsoleHelper.Failure();
                Console.WriteLine(e.Message);
                if (settings == null)
                {
                    ConsoleHelper.Warn("PartnerPlusPoints will still run. However, none of your settings will be saved.");
                    Console.WriteLine("Press q to quit, or any other key to continue. ");
                    if (Console.ReadKey().Key == ConsoleKey.Q) { Environment.Exit(1); }
                }
                else { ConsoleHelper.Warn("PartnerPlusPoints will still run. However, your settings have not been saved."); }
            }
        }
    }
}