using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Playnite.Common;

namespace GameJoltLibrary
{
    public static class GameJolt
    {
        public static string InstallationPath
        {
            get
            {
                var prog = Programs.GetUnistallProgramsList().FirstOrDefault(a => a.DisplayName?.Contains("game jolt", StringComparison.OrdinalIgnoreCase) ?? false);
                return prog == null ? string.Empty : prog.InstallLocation;
            }
        }

        public static string ClientExecPath
        {
            get
            {
                var installDir = InstallationPath;
                if (string.IsNullOrEmpty(installDir))
                {
                    return string.Empty;
                }

                var exePath = Path.Combine(InstallationPath, "GameJoltClient.exe");
                return File.Exists(exePath) ? exePath : string.Empty;
            }
        }

        public static bool IsInstalled
        {
            get
            {
                return InstalledVersion.Major >= 2;
            }
        }

        public static Version InstalledVersion
        {
            get
            {
                var fileVersioInfo = FileVersionInfo.GetVersionInfo(ClientExecPath);
                return new Version(fileVersioInfo.FileVersion);
            }
        }

        public static string Icon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"Resources\GameJoltIcon.png");

        public static void StartClient()
        {
            ProcessStarter.StartProcess(ClientExecPath, string.Empty);
        }
    }
}
