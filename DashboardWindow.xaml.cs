using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AuroraClock.Models;
using AuroraClock.Services;

namespace AuroraClock
{
    public partial class DashboardWindow : Window
    {
        private readonly AppController _app;
        private string _pane = "todo";

        private sealed record NoteRow(NoteItem Item, string Text, string Life);
        private sealed record UsageVm(string App, string Tokens, string Requests, string Cost);

        public DashboardWindow(AppController app)
        {
            _app = app;
            InitializeComponent();

            RemindRepeat.ItemsSource = new List<string>
            {
                "单次", "每天", "仅工作日", "大小周", "每 30 分钟", "每 1 小时", "每 2 小时"
            };
            RemindRepeat.SelectedIndex = 1;

            NoteLife.ItemsSource = new List<string>
            {
                "5 分钟后消失", "30 分钟后消失", "1 小时后消失", "4 小时后消失", "今天结束消失", "永久"
            };
            NoteLife.SelectedIndex = 1;

            Loaded += (_, _) => { Select(_pane); RefreshAll(); };
            PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        }

        // ------------------------------------------------------------ chrome
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is string tag) Select(tag);
        }

        private void Select(string pane)
        {
            _pane = pane;
            PaneTodo.Visibility = pane == "todo" ? Visibility.Visible : Visibility.Collapsed;
            PaneRemind.Visibility = pane == "remind" ? Visibility.Visible : Visibility.Collapsed;
            PaneNote.Visibility = pane == "note" ? Visibility.Visible : Visibility.Collapsed;
            PaneUsage.Visibility = pane == "usage" ? Visibility.Visible : Visibility.Collapsed;

            foreach (var (btn, key) in new[] { (NavTodo, "todo"), (NavRemind, "remind"), (NavNote, "note"), (NavUsage, "usage") })
                btn.Background = key == pane
                    ? new SolidColorBrush(Color.FromArgb(0x40, 0x7C, 0xC4, 0xFF))
                    : Brushes.Transparent;

            if (pane == "usage") RefreshUsage();
        }

        public void RefreshAll()
        {
            RefreshTodo();
            RefreshRemind();
            RefreshNotes();
            if (_pane == "usage") RefreshUsage();
            UpdateSub();
        }

        private void UpdateSub()
        {
            int open = _app.Config.Todos.Count(t => !t.Done);
            SubText.Text = $"待办 {open} · 提醒 {_app.Config.Reminders.Count} · 便签 {_app.Config.Notes.Count}";
        }

        // -------------------------------------------------------------- todo
        private void RefreshTodo()
        {
            TodoList.ItemsSource = null;
            TodoList.ItemsSource = _app.Config.Todos.ToList();
            UpdateSub();
        }

        private void TodoInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddTodo();
        }

        private void TodoAdd_Click(object sender, RoutedEventArgs e) => AddTodo();

        private void AddTodo()
        {
            var text = TodoInput.Text.Trim();
            if (text.Length == 0) return;
            _app.Config.Todos.Insert(0, new TodoItem { Text = text });
            TodoInput.Text = "";
            _app.Save();
            RefreshTodo();
        }

        private void Todo_Check(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is TodoItem item)
            {
                item.Done = !item.Done;
                _app.Save();
                RefreshTodo();
            }
        }

        private void Todo_Delete(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is TodoItem item)
            {
                _app.Config.Todos.Remove(item);
                _app.Save();
                RefreshTodo();
            }
        }

        // --------------------------------------------------------- reminders
        private void RefreshRemind()
        {
            RemindList.ItemsSource = null;
            RemindList.ItemsSource = _app.Config.Reminders
                .OrderBy(r => r.Time, StringComparer.Ordinal).ToList();
            WeekHint.Text = $"本周：{Workday.WeekLabel(DateTime.Now)}（小周周六上班）";
            UpdateSub();
        }

        private void RemindText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddReminder();
        }

        private void RemindAdd_Click(object sender, RoutedEventArgs e) => AddReminder();

        private void AddReminder()
        {
            var text = RemindText.Text.Trim();
            if (text.Length == 0) return;

            var time = RemindTime.Text.Trim();
            if (!TimeSpan.TryParseExact(time, @"hh\:mm", CultureInfo.InvariantCulture, out _) &&
                !TimeSpan.TryParseExact(time, @"h\:mm", CultureInfo.InvariantCulture, out _))
                time = DateTime.Now.ToString("HH:mm");

            var r = new Reminder { Text = text, Time = time.PadLeft(5, '0') };

            int idx = RemindRepeat.SelectedIndex;
            switch (idx)
            {
                case 0: r.Repeat = RepeatMode.Once; r.Date = DateTime.Now.ToString("yyyy-MM-dd"); break;
                case 1: r.Repeat = RepeatMode.Daily; break;
                case 2: r.Repeat = RepeatMode.Weekdays; break;
                case 3: r.Repeat = RepeatMode.BigSmallWeek; break;
                case 4: r.Repeat = RepeatMode.Interval; r.IntervalMinutes = 30; break;
                case 5: r.Repeat = RepeatMode.Interval; r.IntervalMinutes = 60; break;
                default: r.Repeat = RepeatMode.Interval; r.IntervalMinutes = 120; break;
            }

            _app.Config.Reminders.Add(r);
            RemindText.Text = "";
            _app.Save();
            RefreshRemind();
        }

        private void Remind_Toggle(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is Reminder r)
            {
                r.Enabled = !r.Enabled;
                _app.Save();
                RefreshRemind();
            }
        }

        private void Remind_Delete(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is Reminder r)
            {
                _app.Config.Reminders.Remove(r);
                _app.Save();
                RefreshRemind();
            }
        }

        // ------------------------------------------------------------- notes
        private void RefreshNotes()
        {
            var rows = _app.Config.Notes.Select(n => new NoteRow(n, n.Text, DescribeLife(n))).ToList();
            NoteList.ItemsSource = null;
            NoteList.ItemsSource = rows;
            UpdateSub();
        }

        private static string DescribeLife(NoteItem n)
        {
            var exp = n.Expiry;
            if (exp == null) return "永久";
            var left = exp.Value - DateTime.Now;
            if (left.TotalSeconds <= 0) return "已过期";
            return left.TotalHours >= 1
                ? $"还剩 {(int)left.TotalHours} 小时 {left.Minutes} 分"
                : $"还剩 {left.Minutes} 分 {left.Seconds} 秒";
        }

        private void NoteText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddNote();
        }

        private void NoteAdd_Click(object sender, RoutedEventArgs e) => AddNote();

        private void AddNote()
        {
            var text = NoteText.Text.Trim();
            if (text.Length == 0) return;

            var note = new NoteItem { Text = text };
            switch (NoteLife.SelectedIndex)
            {
                case 0: note.ExpiresAt = DateTime.Now.AddMinutes(5).ToString("o"); break;
                case 1: note.ExpiresAt = DateTime.Now.AddMinutes(30).ToString("o"); break;
                case 2: note.ExpiresAt = DateTime.Now.AddHours(1).ToString("o"); break;
                case 3: note.ExpiresAt = DateTime.Now.AddHours(4).ToString("o"); break;
                case 4: note.ExpiresAt = DateTime.Today.AddDays(1).ToString("o"); break;
                default: note.ExpiresAt = ""; break;
            }

            _app.Config.Notes.Insert(0, note);
            NoteText.Text = "";
            _app.Save();
            RefreshNotes();
            _app.ShowNoteWindow(note);
        }

        private void Note_Show(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is NoteRow row)
                _app.ShowNoteWindow(row.Item);
        }

        private void Note_Hide(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is NoteRow row)
                _app.HideNoteWindow(row.Item);
        }

        private void Note_Delete(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is NoteRow row)
            {
                _app.HideNoteWindow(row.Item);
                _app.Config.Notes.Remove(row.Item);
                _app.Save();
                RefreshNotes();
            }
        }

        // ------------------------------------------------------------- usage
        private void UsageRefresh_Click(object sender, RoutedEventArgs e) => RefreshUsage();

        private void UsageWidget_Toggle(object sender, RoutedEventArgs e)
        {
            _app.Config.ShowUsageWidget = UsageWidgetToggle.IsChecked == true;
            _app.Save();
            _app.SyncUsageWidget();
        }

        public void RefreshUsage()
        {
            bool running = CcSwitchUsage.IsRunning();
            UsageWidgetToggle.IsChecked = _app.Config.ShowUsageWidget;

            if (!running)
            {
                UsageOffline.Visibility = Visibility.Visible;
                UsageLive.Visibility = Visibility.Collapsed;
                UsageOfflineText.Text = CcSwitchUsage.Available
                    ? "CC Switch 未运行 — 启动后这里会实时显示用量。"
                    : "未找到 CC Switch 数据（~/.cc-switch/cc-switch.db）。";
                UsageList.ItemsSource = null;
                return;
            }

            UsageOffline.Visibility = Visibility.Collapsed;
            UsageLive.Visibility = Visibility.Visible;

            var today = CcSwitchUsage.Day();
            decimal cost = today.Sum(r => r.Cost);
            long inTok = today.Sum(r => r.Input);
            long outTok = today.Sum(r => r.Output);
            long cache = today.Sum(r => r.CacheRead);
            int req = today.Sum(r => r.Requests);

            UsageCost.Text = "$" + cost.ToString("0.00");
            UsageTokens.Text = $"输入 {CcSwitchUsage.FormatTokens(inTok)} · 输出 {CcSwitchUsage.FormatTokens(outTok)} · 缓存 {CcSwitchUsage.FormatTokens(cache)} · {req} 次请求";
            UsageRecent.Text = $"最近 5 分钟 {CcSwitchUsage.RecentRequests(5)} 次请求 · 更新于 {DateTime.Now:HH:mm:ss}";
            UsageUpdated.Text = DateTime.Now.ToString("HH:mm:ss");

            var vm = today.Select(r => new UsageVm(
                r.App,
                "输入 " + CcSwitchUsage.FormatTokens(r.Input) + " / 输出 " + CcSwitchUsage.FormatTokens(r.Output),
                r.Requests + " 次",
                "$" + r.Cost.ToString("0.00"))).ToList();

            UsageList.ItemsSource = null;
            UsageList.ItemsSource = vm;
        }
    }
}
