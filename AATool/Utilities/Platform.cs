using System;
using System.Diagnostics;
using System.IO;

namespace AATool
{
    public static class Platform
    {
        public static readonly bool IsWindows = Environment.OSVersion.Platform
            is PlatformID.Win32NT or PlatformID.Win32Windows or PlatformID.Win32S or PlatformID.WinCE;

        public static readonly bool IsLinux = !IsWindows && Directory.Exists("/proc");

        //aaupdate installs the official windows release, so it can only be used on windows
        public static bool SupportsAutoUpdate => IsWindows;

        public static string Describe()
        {
            string os = Environment.OSVersion.VersionString;
            if (IsLinux)
            {
                try
                {
                    //use the distro's friendly name if available
                    foreach (string line in File.ReadAllLines("/etc/os-release"))
                    {
                        if (line.StartsWith("PRETTY_NAME="))
                            os = line.Substring("PRETTY_NAME=".Length).Trim('"') + $" ({os})";
                    }
                }
                catch { }
            }
            string architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
            return $"{os} {architecture}";
        }

        public static string GetCommandLine(Process process)
        {
            if (!IsLinux)
                return null;

            //arguments are separated by null characters
            string[] args = File.ReadAllText($"/proc/{process.Id}/cmdline").TrimEnd('\0').Split('\0');
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Contains(" "))
                    args[i] = $"\"{args[i]}\"";
            }
            return string.Join(" ", args);
        }
    }
}
