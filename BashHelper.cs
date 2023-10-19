using System;
using System.Diagnostics;
using System.Reflection;

namespace PartnerPlusPoints
{
	public class BashHelper
	{
        public struct BashOutput
        {
            public string Standard;
            public string Error;
            public BashOutput(string standard, string error)
            {
                Standard = standard;
                Error = error;
            }
        }

        public static async Task<BashOutput?> RunScript(BrowserManager.BrowserOperation operation)
        {
            var workingDirectory = Directory.GetCurrentDirectory();
            var terminalProcess = new ProcessStartInfo()
            {
                UseShellExecute = false,
                FileName = Program.BashScript,
                Arguments = $"{operation} {workingDirectory} {Program.UserOS.OperatingSystem}",
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            Process? script = Process.Start(terminalProcess);
            if (script == null)
            {
                ConsoleHelper.HandleFatalError(4);
                return null;
            }
            else
            {
                BashOutput output = new() { Standard = await script.StandardOutput.ReadToEndAsync(), Error = await script.StandardError.ReadToEndAsync() };
                return output;
            }
        }

        public static async Task<string?> CreateTempScript()
        {
            string tempFilePath = Path.GetTempFileName();
            Stream? scriptStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PartnerPlusPoints.BrowserScript.sh");
            if (scriptStream == null) { return null; }

            StreamReader streamReader = new(scriptStream);
            await File.WriteAllTextAsync(tempFilePath, await streamReader.ReadToEndAsync());
            File.Move(tempFilePath, tempFilePath = tempFilePath[..^3] + "sh");

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = "/bin/bash",
                    Arguments = $" -c \"chmod 777 {tempFilePath}\""
                }
            };
            process.Start();
            await process.WaitForExitAsync();
            return tempFilePath;
        }

        public static void DeleteTempScript()
        {
            if (Program.BashScript != null) { File.Delete(Program.BashScript); }
        }
    }
}

