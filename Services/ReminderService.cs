using System;
using System.Linq;
using System.Windows.Threading;
using AuroraClock.Models;

namespace AuroraClock.Services
{
    /// <summary>Watches the configured reminders and raises <see cref="Due"/> when one is triggered.</summary>
    public sealed class ReminderService : IDisposable
    {
        private readonly AppController _app;
        private readonly DispatcherTimer _timer;

        public event Action<Reminder>? Due;

        public ReminderService(AppController app)
        {
            _app = app;
            _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(10) };
            _timer.Tick += (_, _) => Tick();
        }

        public void Start()
        {
            _timer.Start();
            Tick();
        }

        public void Dispose() => _timer.Stop();

        private void Tick()
        {
            var now = DateTime.Now;
            var minute = now.ToString("HH:mm");
            bool dirty = false;

            foreach (var r in _app.Config.Reminders.ToList())
            {
                if (!r.Enabled) continue;

                if (r.Repeat == RepeatMode.Interval)
                {
                    if (r.IntervalMinutes < 1) r.IntervalMinutes = 60;
                    var last = DateTime.TryParse(r.LastFiredKey, out var l) ? l : DateTime.MinValue;
                    if ((now - last).TotalMinutes < r.IntervalMinutes) continue;
                    if (!Workday.IsWorkday(now, RepeatMode.Weekdays)) continue;
                    r.LastFiredKey = now.ToString("o");
                    dirty = true;
                    Due?.Invoke(r);
                    continue;
                }

                if (!string.Equals(r.Time, minute, StringComparison.Ordinal)) continue;

                var stamp = now.ToString("yyyy-MM-dd ") + minute;
                if (r.LastFiredKey == stamp) continue;

                if (r.Repeat == RepeatMode.Once)
                {
                    if (!string.IsNullOrWhiteSpace(r.Date) &&
                        !string.Equals(r.Date, now.ToString("yyyy-MM-dd"), StringComparison.Ordinal))
                        continue;
                }
                else if (!Workday.IsWorkday(now, r.Repeat))
                {
                    continue;
                }

                r.LastFiredKey = stamp;
                dirty = true;
                Due?.Invoke(r);

                if (r.Repeat == RepeatMode.Once) r.Enabled = false;
            }

            if (dirty) _app.Save();
        }
    }
}
