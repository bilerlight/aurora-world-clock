using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace AuroraClock.Models
{
    public enum WidgetShape
    {
        Circle,
        Square
    }

    public sealed class ClockItem : INotifyPropertyChanged
    {
        private string _city = "Local";
        private string _timeZoneId = TimeZoneInfo.Local.Id;
        private WidgetShape _shape = WidgetShape.Circle;
        private double _size = 210;
        private double _x = double.NaN;
        private double _y = double.NaN;
        private bool _showSeconds = true;
        private bool _locked;
        private string _accent = "#7CC4FF";
        private string _faceImage = "";

        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string City
        {
            get => _city;
            set { if (_city != value) { _city = value; OnChanged(); } }
        }

        public string TimeZoneId
        {
            get => _timeZoneId;
            set { if (_timeZoneId != value) { _timeZoneId = value; OnChanged(); OnChanged(nameof(OffsetText)); } }
        }

        public WidgetShape Shape
        {
            get => _shape;
            set { if (_shape != value) { _shape = value; OnChanged(); OnChanged(nameof(ShapeLabel)); } }
        }

        public double Size
        {
            get => _size;
            set { if (Math.Abs(_size - value) > 0.01) { _size = value; OnChanged(); } }
        }

        public double X
        {
            get => _x;
            set { if (!AreClose(_x, value)) { _x = value; OnChanged(); } }
        }

        public double Y
        {
            get => _y;
            set { if (!AreClose(_y, value)) { _y = value; OnChanged(); } }
        }

        public bool ShowSeconds
        {
            get => _showSeconds;
            set { if (_showSeconds != value) { _showSeconds = value; OnChanged(); } }
        }

        public bool Locked
        {
            get => _locked;
            set { if (_locked != value) { _locked = value; OnChanged(); } }
        }

        public string Accent
        {
            get => _accent;
            set { if (_accent != value) { _accent = value; OnChanged(); } }
        }

        /// <summary>Optional image used as the dial face (empty = the built-in glass face).</summary>
        public string FaceImage
        {
            get => _faceImage;
            set { if (_faceImage != value) { _faceImage = value; OnChanged(); } }
        }

        [JsonIgnore]
        public string ShapeLabel => Shape == WidgetShape.Circle ? "圆形 Circle" : "方形 Square";

        [JsonIgnore]
        public string ShapeShort => Shape == WidgetShape.Circle ? "圆形" : "方形";

        [JsonIgnore]
        public string OffsetText
        {
            get
            {
                try
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
                    var off = tz.GetUtcOffset(DateTime.UtcNow);
                    var sign = off < TimeSpan.Zero ? "-" : "+";
                    var a = off.Duration();
                    return $"UTC{sign}{a.Hours:00}:{a.Minutes:00}";
                }
                catch
                {
                    return "UTC";
                }
            }
        }

        private static bool AreClose(double a, double b)
        {
            if (double.IsNaN(a) && double.IsNaN(b)) return true;
            return Math.Abs(a - b) < 0.01;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ClockItem Clone() => new()
        {
            Id = Id,
            City = City,
            TimeZoneId = TimeZoneId,
            Shape = Shape,
            Size = Size,
            X = X,
            Y = Y,
            ShowSeconds = ShowSeconds,
            Locked = Locked,
            Accent = Accent,
            FaceImage = FaceImage
        };
    }
}
