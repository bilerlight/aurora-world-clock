using System;
using System.Globalization;

namespace AuroraClock.Models
{
    public enum RepeatMode
    {
        Once,
        Daily,
        Weekdays,
        BigSmallWeek,
        Interval
    }

    public sealed class Reminder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Text { get; set; } = "";
        public string Time { get; set; } = "09:00";
        public string Date { get; set; } = "";
        public RepeatMode Repeat { get; set; } = RepeatMode.Daily;
        public int IntervalMinutes { get; set; } = 60;
        public bool Enabled { get; set; } = true;
        public string LastFiredKey { get; set; } = "";

        public string RepeatLabel => Repeat switch
        {
            RepeatMode.Once => "单次",
            RepeatMode.Daily => "每天",
            RepeatMode.Weekdays => "仅工作日",
            RepeatMode.BigSmallWeek => "大小周",
            _ => $"每 {IntervalMinutes} 分钟"
        };

        public string Summary => Repeat == RepeatMode.Interval
            ? $"{RepeatLabel} · {Text}"
            : $"{RepeatLabel} {Time} · {Text}";
    }

    public sealed class NoteItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Text { get; set; } = "";
        public string CreatedAt { get; set; } = DateTime.Now.ToString("o");
        /// <summary>ISO timestamp after which the note disappears. Empty = permanent.</summary>
        public string ExpiresAt { get; set; } = "";
        public double X { get; set; } = double.NaN;
        public double Y { get; set; } = double.NaN;

        public DateTime? Expiry => DateTime.TryParse(ExpiresAt, out var d) ? d : null;
    }

    public sealed class TodoItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Text { get; set; } = "";
        public bool Done { get; set; }
        public string Due { get; set; } = "";
        public bool Important { get; set; }

        public string DueLabel => string.IsNullOrWhiteSpace(Due) ? "" : Due;
    }

    /// <summary>Day rules used by reminders.</summary>
    public static class Workday
    {
        public static bool IsWorkday(DateTime d, RepeatMode mode)
        {
            var dow = d.DayOfWeek;
            switch (mode)
            {
                case RepeatMode.Weekdays:
                    return dow >= DayOfWeek.Monday && dow <= DayOfWeek.Friday;

                case RepeatMode.BigSmallWeek:
                    if (dow == DayOfWeek.Sunday) return false;
                    if (dow <= DayOfWeek.Friday) return true;
                    // Saturday: only on 小周 (odd ISO week)
                    return ISOWeek.GetWeekOfYear(d) % 2 == 1;

                default:
                    return true;
            }
        }

        public static string WeekLabel(DateTime d)
        {
            int w = ISOWeek.GetWeekOfYear(d);
            return w % 2 == 1 ? "小周" : "大周";
        }
    }
}
