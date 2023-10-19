using System.Runtime.InteropServices;

namespace PartnerPlusPoints
{
    public enum OS
    {
        Windows,
        MacOSX,
        Linux,
        Unsupported
    }

    public class OperatingSystemInfo
    {
        public OS OperatingSystem { get; private set; }
        public string ShortName { get; private set; } // This string is designed for use in the Firefox download URL.
        public string Version { get; private set; }
        public string Architecture { get; private set; }
        public string InstallerExtension { get; private set; }
        public string FirefoxDirectory { get; private set; }

#pragma warning disable CS8618
        public OperatingSystemInfo() // This will never be null, as an unsupported OS version will result in a fatal error before execution reaches a point where it matters.
#pragma warning restore CS8618
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { OperatingSystem = OS.Windows; }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) { OperatingSystem = OS.MacOSX; }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) { OperatingSystem = OS.Linux; }
            else { OperatingSystem = OS.Unsupported; }

            Architecture = Environment.OSVersion.ToString();

            Version = Environment.Version.ToString();

            switch (OperatingSystem)
            {
                case OS.Windows:
                    if (Architecture.Contains("64")) { ShortName = "win64"; }
                    else { ShortName = "win"; }
                    InstallerExtension = "exe";
                    FirefoxDirectory = $"{Directory.GetCurrentDirectory()}\\Firefox\\win";
                    break;
                case OS.MacOSX:
                    ShortName = "osx";
                    InstallerExtension = "dmg";
                    FirefoxDirectory = $@"{Directory.GetCurrentDirectory()}/Firefox";
                    break;
                case OS.Linux:
                    if (Architecture.Contains("64")) { ShortName = "linux64"; }
                    else { ShortName = "linux"; }
                    InstallerExtension = "tar.bz2";
                    FirefoxDirectory = $@"{Directory.GetCurrentDirectory()}/Firefox";
                    break;
                default:
                    ConsoleHelper.HandleFatalError(1);
                    break;
            }
        }
    }
}
