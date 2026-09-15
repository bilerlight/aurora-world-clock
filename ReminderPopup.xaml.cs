using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AuroraClock.Models;

namespace AuroraClock
{
    /// <summary>A comic-book style "POW!" reminder card. Click anywhere to dismiss.</summary>
    public partial class ReminderPopup : Window
    {
        private static int _stack;
        private bool _closing;
        private DateTime _shownAt;

        public ReminderPopup(Reminder reminder, bool sticky = false)
        {
            InitializeComponent();

            HeaderText.Text = reminder.Repeat == RepeatMode.Interval ? "该休息一下!" : "提醒!";
            TimeText.Text = reminder.Repeat == RepeatMode.Interval
                ? $"每 {reminder.IntervalMinutes} 分钟 · {DateTime.Now:HH:mm}"
                : $"{reminder.RepeatLabel} · {DateTime.Now:HH:mm}";
            BodyText.Text = string.IsNullOrWhiteSpace(reminder.Text) ? "时间到了" : reminder.Text;

            PlaceOnScreen();
            Loaded += OnLoaded;

            if (!sticky)
            {
                MouseLeftButtonDown += (_, _) =>
                {
                    // ignore the click that may already be in flight while the card pops up
                    if ((DateTime.Now - _shownAt).TotalMilliseconds < 450) return;
                    Dismiss();
                };
                PreviewKeyDown += (_, e) =>
                {
                    if ((DateTime.Now - _shownAt).TotalMilliseconds < 450) return;
                    if (e.Key == Key.Escape || e.Key == Key.Enter || e.Key == Key.Space) Dismiss();
                };
            }

            Closed += (_, _) => { if (_stack > 0) _stack--; };
        }

        private void PlaceOnScreen()
        {
            var wa = SystemParameters.WorkArea;
            int slot = _stack++;
            double x = wa.Right - Width - 26;
            double y = wa.Bottom - Height - 26 - slot * 74;

            // never appear underneath the pointer - the first click would dismiss it
            var cursor = System.Windows.Forms.Cursor.Position;
            var rect = new Rect(x, y, Width, Height);
            if (rect.Contains(cursor.X, cursor.Y))
            {
                y = cursor.Y - Height - 40;
                x = cursor.X - Width - 40;
            }

            if (y < wa.Top + 20) y = wa.Top + 20;
            Left = Math.Max(wa.Left + 10, x);
            Top = y;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _shownAt = DateTime.Now;
            Opacity = 0;
            PopScale.ScaleX = PopScale.ScaleY = 0.35;
            PopRotate.Angle = -10;

            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.9 };
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));

            var sx = new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(430)) { EasingFunction = ease };
            PopScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
            PopScale.BeginAnimation(ScaleTransform.ScaleYProperty, sx);
            PopRotate.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(-10, -1.5, TimeSpan.FromMilliseconds(430)) { EasingFunction = ease });

            // small idle wobble so it feels alive
            var wobble = new DoubleAnimation(-1.5, 1.5, TimeSpan.FromMilliseconds(1400))
            {
                BeginTime = TimeSpan.FromMilliseconds(500),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            PopRotate.BeginAnimation(RotateTransform.AngleProperty, wobble);

            Activate();
        }

        private void Dismiss()
        {
            if (_closing) return;
            _closing = true;

            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            var sx = new DoubleAnimation(PopScale.ScaleX, 1.18, TimeSpan.FromMilliseconds(170)) { EasingFunction = ease };
            PopScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
            PopScale.BeginAnimation(ScaleTransform.ScaleYProperty, sx);

            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(170)) { EasingFunction = ease };
            fade.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fade);
        }
    }
}

