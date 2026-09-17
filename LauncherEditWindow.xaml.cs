using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AuroraClock.Models;
using AuroraClock.Services;

namespace AuroraClock
{
    /// <summary>Editor for one launcher tile: a program, a folder, or a cmd command + working folder.</summary>
    public partial class LauncherEditWindow : Window
    {
        private readonly LauncherItem _item;
        private LauncherKind _kind;
        private string _terminal = "cmd";

        public LauncherEditWindow(LauncherItem item)
        {
            _item = item;
            _kind = item.Kind;
            _terminal = string.IsNullOrWhiteSpace(item.Terminal) ? "cmd" : item.Terminal;

            InitializeComponent();

            HeadTitle.Text = string.IsNullOrWhiteSpace(item.Target) && string.IsNullOrWhiteSpace(item.Name)
                ? "添加启动项"
                : "编辑启动项";

            NameBox.Text = item.Name;
            TargetBox.Text = item.Target;
            ArgsBox.Text = item.Args;
            WorkBox.Text = item.WorkingDir;
            KeepOpenToggle.IsChecked = item.KeepOpen;
            AdminToggle.IsChecked = item.Admin;

            KindAppBtn.Click += (_, _) => SetKind(LauncherKind.App);
            KindCmdBtn.Click += (_, _) => SetKind(LauncherKind.Command);
            KindFolderBtn.Click += (_, _) => SetKind(LauncherKind.Folder);

            TermCmdBtn.Click += (_, _) => SetTerminal("cmd");
            TermPwshBtn.Click += (_, _) => SetTerminal("pwsh");
            TermWtBtn.Click += (_, _) => SetTerminal("wt");

            BrowseTargetBtn.Click += (_, _) => BrowseTarget();
            BrowseWorkBtn.Click += (_, _) => BrowseFolder();
            PresetBtn.Click += (_, _) => ShowPresets();

            OkBtn.Click += (_, _) => Accept();
            CancelBtn.Click += (_, _) => DialogResult = false;
            CloseBtn.Click += (_, _) => DialogResult = false;

            NameBox.TextChanged += (_, _) => RefreshPreview();
            TargetBox.TextChanged += (_, _) => RefreshPreview();
            ArgsBox.TextChanged += (_, _) => RefreshPreview();
            WorkBox.TextChanged += (_, _) => RefreshPreview();

            PreviewBox.MouseLeftButtonDown += (_, _) => DragMove();
            MouseLeftButtonDown += (_, e) =>
            {
                if (e.OriginalSource is Border || e.OriginalSource is Grid) DragMove();
            };
            KeyDown += (_, e) => { if (e.Key == Key.Escape) DialogResult = false; };

            SetKind(_kind);
            SetTerminal(_terminal);
        }

        // ------------------------------------------------------------------ state
        private void SetKind(LauncherKind kind)
        {
            _kind = kind;

            Segment(KindAppBtn, kind == LauncherKind.App);
            Segment(KindCmdBtn, kind == LauncherKind.Command);
            Segment(KindFolderBtn, kind == LauncherKind.Folder);

            bool isCommand = kind == LauncherKind.Command;
            bool isFolder = kind == LauncherKind.Folder;

            TargetLabel.Text = isCommand ? "命令" : isFolder ? "文件夹" : "程序";
            TargetRow.Visibility = Visibility.Visible;
            ArgsRow.Visibility = isFolder ? Visibility.Collapsed : Visibility.Visible;
            WorkRow.Visibility = Visibility.Visible;
            TermRow.Visibility = isCommand ? Visibility.Visible : Visibility.Collapsed;
            BrowseTargetBtn.Visibility = isCommand ? Visibility.Collapsed : Visibility.Visible;

            HeadGlyph.Text = isCommand ? "\uE756" : isFolder ? "\uE8B7" : "\uE71D";
            RefreshPreview();
        }

        private void SetTerminal(string terminal)
        {
            _terminal = terminal;
            Segment(TermCmdBtn, terminal == "cmd");
            Segment(TermPwshBtn, terminal == "pwsh");
            Segment(TermWtBtn, terminal == "wt");
            RefreshPreview();
        }

        private void Segment(Button button, bool on)
            => button.Style = (Style)FindResource(on ? "AccentButton" : "GhostButton");

        // ------------------------------------------------------------------ browse
        private void BrowseTarget()
        {
            if (_kind == LauncherKind.Folder)
            {
                BrowseFolder();
                return;
            }

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择程序",
                Filter = "程序与快捷方式|*.exe;*.lnk;*.bat;*.cmd;*.ps1;*.msc|所有文件|*.*",
                CheckFileExists = true
            };

            var start = SafeDirectory(TargetBox.Text);
            if (start != null) dlg.InitialDirectory = start;

            if (dlg.ShowDialog(this) != true) return;
            TargetBox.Text = dlg.FileName;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
                NameBox.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
            RefreshPreview();
        }

        private void BrowseFolder()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择文件夹",
                ShowNewFolderButton = true,
                UseDescriptionForTitle = true
            };

            var current = _kind == LauncherKind.Folder ? TargetBox.Text : WorkBox.Text;
            var start = SafeDirectory(current);
            if (start != null) dlg.SelectedPath = start;

            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            if (_kind == LauncherKind.Folder)
            {
                TargetBox.Text = dlg.SelectedPath;
                if (string.IsNullOrWhiteSpace(NameBox.Text)) NameBox.Text = Path.GetFileName(dlg.SelectedPath);
            }
            else
            {
                WorkBox.Text = dlg.SelectedPath;
            }
            RefreshPreview();
        }

        private static string? SafeDirectory(string? path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return null;
                if (Directory.Exists(path)) return path;
                var dir = Path.GetDirectoryName(path);
                return !string.IsNullOrEmpty(dir) && Directory.Exists(dir) ? dir : null;
            }
            catch
            {
                return null;
            }
        }

        // ----------------------------------------------------------------- presets
        private void ShowPresets()
        {
            var menu = new ContextMenu();

            AddPreset(menu, "Claude Code", "claude", "pwsh");
            AddPreset(menu, "Codex CLI", "codex", "pwsh");
            AddPreset(menu, "OpenCode", "opencode", "pwsh");
            AddPreset(menu, "Gemini CLI", "gemini", "pwsh");
            AddPreset(menu, "Aider", "aider", "pwsh");

            menu.Items.Add(new Separator());
            menu.Items.Add(Preset("npm run dev", "npm", "pwsh"));
            menu.Items.Add(Preset("uv / python 项目", "uv run python main.py", "pwsh"));
            menu.Items.Add(Preset("git pull + status", "git pull && git status", "cmd"));

            menu.PlacementTarget = PresetBtn;
            menu.IsOpen = true;
        }

        private void AddPreset(ContextMenu menu, string name, string command, string terminal)
            => menu.Items.Add(Preset(name, command, terminal));

        private MenuItem Preset(string name, string command, string terminal)
        {
            var item = new MenuItem { Header = name };
            item.Click += (_, _) =>
            {
                SetKind(LauncherKind.Command);
                SetTerminal(terminal == "pwsh" ? "pwsh" : "cmd");
                NameBox.Text = name;
                TargetBox.Text = command;
                if (string.IsNullOrWhiteSpace(WorkBox.Text))
                    WorkBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                KeepOpenToggle.IsChecked = true;
                RefreshPreview();
                NameBox.Focus();
            };
            return item;
        }

        // ---------------------------------------------------------------- preview
        private void RefreshPreview()
        {
            string target = TargetBox.Text.Trim();
            string name = NameBox.Text.Trim();
            if (name.Length == 0) name = _kind == LauncherKind.Command ? "命令启动" : "(未命名)";

            PreviewTitle.Text = name;

            if (_kind == LauncherKind.Command)
            {
                var dir = string.IsNullOrWhiteSpace(WorkBox.Text) ? "~" : WorkBox.Text.Trim();
                var term = _terminal == "pwsh" ? "PowerShell" : _terminal == "wt" ? "Windows Terminal" : "cmd";
                PreviewDetail.Text = term + " · " + dir + "\n> " + target +
                                     (string.IsNullOrWhiteSpace(ArgsBox.Text) ? "" : " " + ArgsBox.Text.Trim());
            }
            else
            {
                PreviewDetail.Text = target.Length == 0 ? "还没有选择目标" : target;
            }

            IconPreview.Content = BuildPreviewIcon();
        }

        private FrameworkElement BuildPreviewIcon()
        {
            if (_kind != LauncherKind.Command)
            {
                var source = IconFactory.FileIcon(TargetBox.Text);
                if (source != null)
                    return new Image { Source = source, Stretch = Stretch.Uniform };
            }

            var text = NameBox.Text.Trim();
            var chip = new Border
            {
                CornerRadius = new CornerRadius(9),
                Background = ThemeService.Brush(ThemeService.Accent),
                Width = 30,
                Height = 30
            };
            chip.Child = new TextBlock
            {
                Text = text.Length > 0
                    ? text.Substring(0, 1).ToUpperInvariant()
                    : _kind == LauncherKind.Command ? ">" : "?",
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            return chip;
        }

        // ----------------------------------------------------------------- accept
        private void Accept()
        {
            var target = TargetBox.Text.Trim();
            if (target.Length == 0)
            {
                PreviewDetail.Text = "请先填写目标或命令";
                return;
            }

            var name = NameBox.Text.Trim();
            if (name.Length == 0)
            {
                if (_kind == LauncherKind.Command) name = target.Split(' ')[0];
                else if (SafeDirectory(target) != null) name = Path.GetFileName(target.TrimEnd('\\', '/'));
            }

            _item.Kind = _kind;
            _item.Name = string.IsNullOrWhiteSpace(name) ? target : name;
            _item.Target = target;
            _item.Args = ArgsBox.Text.Trim();
            _item.WorkingDir = WorkBox.Text.Trim();
            _item.Terminal = _kind == LauncherKind.Command ? _terminal : "";
            _item.KeepOpen = KeepOpenToggle.IsChecked == true;
            _item.Admin = AdminToggle.IsChecked == true;

            DialogResult = true;
        }
    }
}
