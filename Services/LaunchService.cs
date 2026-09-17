using System;
using System.Diagnostics;
using System.IO;
using AuroraClock.Models;

namespace AuroraClock.Services
{
    /// <summary>Starts app shortcuts, folders and shell commands hosted in a terminal.</summary>
    public static class LaunchService
    {
        /// <summary>Runs a tile. Returns an empty string on success, otherwise a message to show.</summary>
        public static string Run(LauncherItem item)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item.Target) && item.Kind != LauncherKind.App)
                    return "没有设置目标";

                switch (item.Kind)
                {
                    case LauncherKind.Folder:
                        OpenFolder(item.Target);
                        break;

                    case LauncherKind.Command:
                        if (string.IsNullOrWhiteSpace(item.Target)) return "没有设置命令";
                        RunCommand(item);
                        break;

                    default:
                        if (string.IsNullOrWhiteSpace(item.Target)) return "没有设置程序";
                        RunApp(item);
                        break;
                }
                return "";
            }
            catch (Exception ex)
            {
                App.Log(ex);
                return ex.Message;
            }
        }

        private static void OpenFolder(string path)
        {
            var dir = Environment.ExpandEnvironmentVariables(path.Trim());
            Process.Start(new ProcessStartInfo
            {
                FileName = Directory.Exists(dir) ? dir : path,
                UseShellExecute = true
            });
        }

        private static void RunApp(LauncherItem item)
        {
            var target = Environment.ExpandEnvironmentVariables(item.Target.Trim());
            var psi = new ProcessStartInfo
            {
                FileName = target,
                Arguments = item.Args ?? "",
                UseShellExecute = true
            };

            var wd = ResolveDir(item.WorkingDir);
            if (wd != null) psi.WorkingDirectory = wd;
            if (item.Admin) psi.Verb = "runas";

            Process.Start(psi);
        }

        /// <summary>
        /// Opens a terminal already sitting in the working folder and runs the command line there.
        /// The folder is applied through the process working directory, so no fragile <c>cd</c>
        /// quoting is involved.
        /// </summary>
        private static void RunCommand(LauncherItem item)
        {
            var line = item.Target.Trim();
            if (!string.IsNullOrWhiteSpace(item.Args)) line += " " + item.Args.Trim();

            var wd = ResolveDir(item.WorkingDir);
            var terminal = (item.Terminal ?? "").Trim().ToLowerInvariant();
            var psi = new ProcessStartInfo { UseShellExecute = true };
            if (wd != null) psi.WorkingDirectory = wd;

            switch (terminal)
            {
                case "pwsh":
                case "powershell":
                    psi.FileName = FindShell("pwsh.exe") ?? "powershell.exe";
                    psi.Arguments = (item.KeepOpen ? "-NoExit " : "") + "-Command " + line;
                    break;

                case "wt":
                    psi.FileName = "wt.exe";
                    psi.Arguments = "-d \"" + (wd ?? "%USERPROFILE%") + "\" cmd " +
                                    (item.KeepOpen ? "/k" : "/c") + " \"" + line.Replace("\"", "\\\"") + "\"";
                    break;

                default:
                    psi.FileName = "cmd.exe";
                    psi.Arguments = (item.KeepOpen ? "/k " : "/c ") + line;
                    break;
            }

            if (item.Admin) psi.Verb = "runas";
            Process.Start(psi);
        }

        /// <summary>PowerShell 7 lives outside PATH on most machines; look in the usual spots.</summary>
        private static string? FindShell(string exe)
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var candidate = Path.Combine(pf, "PowerShell", "7", exe);
            if (File.Exists(candidate)) return candidate;

            var local = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WindowsApps", exe);
            return File.Exists(local) ? local : null;
        }

        private static string? ResolveDir(string? dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return null;
            var value = Environment.ExpandEnvironmentVariables(dir!.Trim().Trim('"'));
            return Directory.Exists(value) ? value : null;
        }
    }
}
