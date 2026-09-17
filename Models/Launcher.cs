using System;

namespace AuroraClock.Models
{
    public enum LauncherKind
    {
        /// <summary>An executable, shortcut or document - started through the shell.</summary>
        App,

        /// <summary>A shell command line, started in a terminal inside a working folder.</summary>
        Command,

        /// <summary>A folder, opened in Explorer.</summary>
        Folder
    }

    /// <summary>One tile in the dock's quick-launch grid.</summary>
    public sealed class LauncherItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = "";

        public LauncherKind Kind { get; set; } = LauncherKind.App;

        /// <summary>Executable path, shell command line, or folder path.</summary>
        public string Target { get; set; } = "";

        public string Args { get; set; } = "";

        /// <summary>Folder the process starts in (also what <c>cd</c> would have set).</summary>
        public string WorkingDir { get; set; } = "";

        /// <summary>Terminal to host a <see cref="LauncherKind.Command"/>: "", "cmd", "pwsh", "wt".</summary>
        public string Terminal { get; set; } = "";

        /// <summary>Keep the terminal open after the command ends.</summary>
        public bool KeepOpen { get; set; } = true;

        public bool Admin { get; set; }

        /// <summary>Optional icon override; empty = pull the icon out of <see cref="Target"/>.</summary>
        public string IconPath { get; set; } = "";

        /// <summary>Optional per-tile accent (hex); empty = use the theme accent.</summary>
        public string Accent { get; set; } = "";

        public string KindLabel => Kind switch
        {
            LauncherKind.Command => "命令",
            LauncherKind.Folder => "文件夹",
            _ => "程序"
        };

        public string Detail
        {
            get
            {
                if (Kind == LauncherKind.Command)
                {
                    var dir = string.IsNullOrWhiteSpace(WorkingDir) ? "~" : WorkingDir;
                    return $"{TerminalLabel} · {dir}";
                }
                return string.IsNullOrWhiteSpace(Target) ? "(未设置)" : Target;
            }
        }

        public string TerminalLabel => Terminal switch
        {
            "pwsh" => "PowerShell",
            "wt" => "Windows Terminal",
            "cmd" => "cmd",
            _ => "cmd"
        };

        /// <summary>The tooltip shown on the dock tile.</summary>
        public string Tooltip
        {
            get
            {
                var lines = new System.Collections.Generic.List<string> { Name };
                if (Kind == LauncherKind.Command)
                {
                    lines.Add("> " + Target + (string.IsNullOrWhiteSpace(Args) ? "" : " " + Args));
                    if (!string.IsNullOrWhiteSpace(WorkingDir)) lines.Add("目录 " + WorkingDir);
                }
                else if (!string.IsNullOrWhiteSpace(Target))
                {
                    lines.Add(Target);
                }
                return string.Join(Environment.NewLine, lines);
            }
        }
    }
}
