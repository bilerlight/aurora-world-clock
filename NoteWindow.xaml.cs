using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AuroraClock.Models;

namespace AuroraClock
{
    /// <summary>A draggable sticky note that removes itself once its expiry time passes.</summary>
    public partial class NoteWindow : Window
    {
        private readonly NoteItem _note;
        private readonly Services.AppController _app;
        private readonly DispatcherTimer _tick;
        private bool _closing;

        public NoteWindow(NoteItem note, Services.AppController app)
        {
            _note = note;
            _app = app;
            InitializeComponent();

            Text.Text = note.Text;
            HintText.Text = $"便签 · {DateTime.Now:HH:mm}";

            if (note.Expiry is { } exp)
            {
                var left = exp - DateTime.Now;
                HintText.Text = $"便签 · {left.TotalMinutes:0} 分钟后消失";
            }

            _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _tick.Tick += (_, _) => UpdateCountdown();
            Loaded += OnLoaded;
            Closed += (_, _) => _tick.Stop();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var wa = SystemParameters.WorkArea;
            double x = double.IsNaN(_note.X) ? wa.Right - Width - 40 : _note.X;
            double y = double.IsNaN(_note.Y) ? wa.Top + 120 : _note.Y;
            Left = Math.Max(wa.Left, Math.Min(x, wa.Right - Width));
            Top = Math.Max(wa.Top, Math.Min(y, wa.Bottom - Height));

            Opacity = 0;
            Pop.ScaleX = Pop.ScaleY = 0.7;
            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 };
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            var sc = new DoubleAnimation(0.7, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease };
            Pop.BeginAnimation(ScaleTransform.ScaleXProperty, sc);
            Pop.BeginAnimation(ScaleTransform.ScaleYProperty, sc);

            _tick.Start();
            UpdateCountdown();
        }

        private void UpdateCountdown()
        {
            var exp = _note.Expiry;
            if (exp == null)
            {
                CountdownText.Text = "永久";
                return;
            }

            var left = exp.Value - DateTime.Now;
            if (left.TotalSeconds <= 0)
            {
                FadeAndClose();
                return;
            }

            CountdownText.Text = left.TotalHours >= 1
                ? $"{(int)left.TotalHours}h {left.Minutes:00}m"
                : $"{left.Minutes}:{left.Seconds:00}";
        }

        private void FadeAndClose()
        {
            if (_closing) return;
            _closing = true;
            _tick.Stop();
            var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(320));
            fade.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fade);
        }

        private void Text_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            _note.Text = Text.Text;
            _app.NotesChanged();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed) return;
            DragMove();
            _note.X = Left;
            _note.Y = Top;
            _app.Save();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => FadeAndClose();
    }
}
