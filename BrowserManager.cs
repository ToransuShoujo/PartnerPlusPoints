using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Diagnostics;
using System.Web;

namespace PartnerPlusPoints
{
    public class BrowserManager
    {
        public enum BrowserOperation
        {
            Check,
            Extract,
            Run
        }

        public static async Task<Version?> GetCurrentBrowserVersion()
        {
            string[] firefoxInformation;

            if (Program.UserOS.OperatingSystem == OS.Windows)
            {
                try { firefoxInformation = await File.ReadAllLinesAsync("./Firefox/win/application.ini"); }
                catch { return null; }

                string currentFirefoxVersion;
                foreach (string line in firefoxInformation)
                {
                    if (line.Contains("Version="))
                    {
                        currentFirefoxVersion = line[8..];
                        return new Version(currentFirefoxVersion);
                    }
                }
                return null;
            }
            else if (Program.UserOS.OperatingSystem == OS.MacOSX || Program.UserOS.OperatingSystem == OS.Linux)
            {
                var bashResponse = await BashHelper.RunScript(BrowserOperation.Check);

                string? firefoxVersion;
                if (string.IsNullOrWhiteSpace(bashResponse.Value.Error)) { firefoxVersion = bashResponse.Value.Standard; }
                else { return null; }
                return new Version(firefoxVersion);

            }
            return null;
        }

        public static async Task<Version?> GetLatestBrowserVersion()
        {
            Console.Write("Getting latest Firefox version... ");
            const string firefoxProductDetails = "https://product-details.mozilla.org/1.0/firefox_versions.json";
            var client = new RestClient();
            var request = new RestRequest(firefoxProductDetails);
            RestResponse response = await client.ExecuteAsync(request);
            if (response.Content != null)
            {
                dynamic responseObject = JObject.Parse(response.Content);
                string? latestVersion = responseObject.LATEST_FIREFOX_VERSION;
                if (latestVersion != null)
                {
                    ConsoleHelper.Success();
                    return new Version(latestVersion);
                }
            }
            ConsoleHelper.Failure();
            return null;
        }

        public static async Task<bool> DownloadBrowser()
        {
            if (Program.UserOS.ShortName == null) { ConsoleHelper.HandleFatalError(1); } // Operating system is not supported.
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Firefox"));

            Console.Write("Downloading Firefox... ");
            string downloadURL = $"https://download.mozilla.org/?product=firefox-latest&os={Program.UserOS.ShortName}&lang=en-US";
            string? fileExtension = Program.UserOS.InstallerExtension;

            using var client = new HttpClient();
            try
            {
                using var stream = await client.GetStreamAsync(downloadURL);
                using var file = new FileStream($"./Firefox/firefox.{fileExtension}", FileMode.OpenOrCreate);
                await stream.CopyToAsync(file);
                ConsoleHelper.Success();
                return true;
            }
            catch
            {
                ConsoleHelper.Failure();
                return false;
            }
        }

        public static async Task<bool> ExtractBrowser()
        {
            Console.Write("Extracting Firefox... ");
            switch (Program.UserOS.ShortName)
            {
                case "win":
                case "win64":
                    string currentDir = Directory.GetCurrentDirectory();
                    string cmdText = $"/C \"{currentDir}\\Firefox\\firefox.exe\" /ExtractDir=\"{currentDir}\\Firefox\\_temp\"";
                    using (Process cmd = new Process())
                    {
                        cmd.StartInfo.FileName = "CMD.exe";
                        cmd.StartInfo.Arguments = cmdText;
                        cmd.Start();
                        await cmd.WaitForExitAsync();
                    }
                    Directory.Move($"{currentDir}\\Firefox\\_temp\\core", $"{currentDir}\\Firefox\\win");
                    Directory.Delete($"{currentDir}\\Firefox\\_temp", true);
                    ConsoleHelper.Success();
                    break;
                default:
                    var bashOutput = await BashHelper.RunScript(BrowserOperation.Extract);
                    if (string.IsNullOrWhiteSpace(bashOutput.Value.Error)) { ConsoleHelper.Success(); }
                    else
                    {
                        ConsoleHelper.Failure();
                        Console.WriteLine(bashOutput.Value.Error);
                        return false;
                    }
                    break;
            }
            return true;
        }

        private static async Task CreateProfile()
        {
            Console.Write("Creating profile... ");

            var cookieDirectory = Path.Combine(Program.UserOS.FirefoxDirectory, "Cookie");
            try { await Task.Run(() => Directory.CreateDirectory(cookieDirectory)); }
            catch { ConsoleHelper.HandleFatalError(7); }
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string json = "{ \"created\": " + currentTime + ", \"firstUse\": null }";

            try
            {
                using StreamWriter file = File.CreateText(Path.Combine(cookieDirectory, "times.json"));
                await file.WriteAsync(json);
                await file.DisposeAsync();
                ConsoleHelper.Success();
            }
            catch { ConsoleHelper.HandleFatalError(6); }

        }

        public static async Task RunBrowser()
        {
            await CreateProfile();
            Console.Write("Starting browser... ");

            Process firefoxWin = new();
            Task<BashHelper.BashOutput?> firefoxBash;

            switch (Program.UserOS.OperatingSystem)
            {
                case OS.Windows:
                    firefoxWin.StartInfo.FileName = $@"{Program.UserOS.FirefoxDirectory}\firefox.exe";
                    firefoxWin.StartInfo.Arguments = $"-profile \"{Program.UserOS.FirefoxDirectory}\\Cookie\" -url https://shoujo.tv/authorization.html";
                    firefoxWin.StartInfo.RedirectStandardError = true;
                    firefoxWin.StartInfo.RedirectStandardOutput = true;
                    firefoxWin.Start();
                    ConsoleHelper.Success();

                    Console.WriteLine("Please fully sign in to Twitch, and then close the browser.");
                    Console.Write("Waiting for browser to exit... ");
                    await firefoxWin.StandardOutput.ReadToEndAsync();
                    firefoxWin.Kill();
                    firefoxWin.Dispose();
                    ConsoleHelper.Success();
                    break;
                default:
                    firefoxBash = BashHelper.RunScript(BrowserOperation.Run);
                    ConsoleHelper.Success();

                    Console.WriteLine("Please fully sign in to Twitch, and then close the browser.");
                    Console.Write("Waiting for browser to exit... ");
                    await firefoxBash;
                    ConsoleHelper.Success();
                    break;
            }

            Console.Write("Extracting cookies... ");
            (bool gqlExtractionSuccess, bool apiExtractionSuccess) = await ExtractCookieInformation();

            if (gqlExtractionSuccess && apiExtractionSuccess)
            {
                ConsoleHelper.Success();
                if (Program.UserSettings.StoreSensitiveInfo) { Program.UserSettings.GQLAuth = Program.TempGQLAuthToken; } // To honor the user's request to store their GQL token.
                await DeleteCookies();
            }
            else
            {
                ConsoleHelper.Failure();
                await DeleteCookies();
                ConsoleHelper.HandleFatalError(3);
            }

            await SettingsManager.WriteSettings(Program.UserSettings);
        }

        private static async Task<(bool, bool)> ExtractCookieInformation()
        {
            bool gqlExtractionSuccess = true;
            bool apiExtractionSuccess = true;

            var cookieDB = Path.Combine(Program.UserOS.FirefoxDirectory, "Cookie", "cookies.sqlite");
            await using (var connection = new SqliteConnection($"Data Source={cookieDB}"))
            {
                connection.Open();
                await using (var command = connection.CreateCommand())
                {
                    if (string.IsNullOrWhiteSpace(Program.UserSettings.ChannelID))
                    {
                        command.CommandText = "SELECT value FROM moz_cookies WHERE name = \"twilight-user\" AND host = \".twitch.tv\"";
                        await using (var idReader = await command.ExecuteReaderAsync())
                        {
                            while (idReader.Read())
                            {
                                var twilightString = idReader.GetString(0);
                                if (!string.IsNullOrWhiteSpace(twilightString))
                                {
                                    twilightString = HttpUtility.UrlDecode(twilightString);
                                    var twilightObject = JObject.Parse(twilightString);
                                    if (twilightObject.ContainsKey("id")) { Program.UserSettings.ChannelID = (string?)twilightObject["id"]; }
                                    else { gqlExtractionSuccess = false; }
                                }
                                else { gqlExtractionSuccess = false; }
                            }
                        };
                    }

                    if (gqlExtractionSuccess)
                    {
                        command.CommandText = "SELECT value FROM moz_cookies WHERE name = \"auth-token\" AND host = \".twitch.tv\"";
                        await using (var gqlReader = await command.ExecuteReaderAsync())
                        {
                            while (gqlReader.Read())
                            {
                                string oauthToken = gqlReader.GetString(0);
                                if (!string.IsNullOrWhiteSpace(oauthToken)) { Program.TempGQLAuthToken = oauthToken; }
                                else { gqlExtractionSuccess = false; }
                            }
                        };
                    }

                    if (string.IsNullOrWhiteSpace(Program.UserSettings.APIAuth))
                    {
                        command.CommandText = "SELECT value FROM moz_cookies WHERE name = \"access\" AND host = \"shoujo.tv\"";
                        await using (var apiReader = await command.ExecuteReaderAsync())
                        {
                            while (apiReader.Read())
                            {
                                string apiToken = apiReader.GetString(0);
                                if (!string.IsNullOrWhiteSpace(apiToken)) { Program.UserSettings.APIAuth = apiToken; }
                                else { apiExtractionSuccess = false; }
                            }
                        }
                    }
                };
            };

            await Task.Run(SqliteConnection.ClearAllPools);
            return (gqlExtractionSuccess, apiExtractionSuccess);
        }

        private static Task DeleteCookies()
        {
            Console.Write("Deleting profile... ");

            try
            {
                Directory.Delete(Path.Combine(Program.UserOS.FirefoxDirectory, "Cookie"), true);
                ConsoleHelper.Success();
            }
            catch (Exception e)
            {
                ConsoleHelper.Failure();
                Console.WriteLine(e.Message);
                Console.WriteLine("Could not delete cookies.");
            }

            return Task.CompletedTask;
        }
    }
}
