using System;

namespace WorkTimeTracker.Models
{
    public class DaySummary
    {
        public DateTime Date { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public int Count { get; set; }
        public bool IsCurrentMonth { get; set; } = true;
        public bool IsSelected { get; set; }
        
        public const double TargetHours = 7.5;

        public double TotalHours => Math.Round(TotalDuration.TotalHours, 2);

        public bool IsTargetMet => TotalHours >= TargetHours;

        public bool IsToday => Date.Date == DateTime.Today;

        public int DayNumber => Date.Day;

        public string DayOfWeekShort => Date.ToString("ddd");

        public string FormattedHours
        {
            get
            {
                int h = (int)TotalDuration.TotalHours;
                int m = TotalDuration.Minutes;
                if (h > 0 && m > 0) return $"{h}h {m}m";
                if (h > 0) return $"{h}h";
                if (m > 0) return $"{m}m";
                return "0h";
            }
        }

        public double TargetProgressPercent => Math.Min((TotalHours / TargetHours) * 100.0, 100.0);

        public string StatusBadgeText
        {
            get
            {
                if (IsTargetMet)
                    return $"✓ {FormattedHours}";
                if (TotalHours > 0)
                    return $"{FormattedHours} / 7.5h";
                return "0h";
            }
        }

        public string BadgeBackgroundHex
        {
            get
            {
                if (IsTargetMet) return "#107C41"; // Strong green for target met
                if (TotalHours > 0) return "#8A5700"; // Amber / partial
                return "#222222"; // Neutral gray
            }
        }

        public string BadgeForegroundHex
        {
            get
            {
                if (IsTargetMet) return "#E6FFED";
                if (TotalHours > 0) return "#FFE2B3";
                return "#777777";
            }
        }
    }
}
