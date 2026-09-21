using System;

namespace WorkTimeTracker.Models
{
    public class CategorySummary
    {
        public string Category { get; set; } = string.Empty;
        public TimeSpan TotalDuration { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }
        public string ColorHex { get; set; } = "#0078D4";

        public string FormattedDuration
        {
            get
            {
                int hours = (int)TotalDuration.TotalHours;
                int minutes = TotalDuration.Minutes;
                if (hours > 0 && minutes > 0)
                    return $"{hours}h {minutes}m";
                if (hours > 0)
                    return $"{hours}h";
                return $"{minutes}m";
            }
        }

        public string FormattedCount => $"{Count} {(Count == 1 ? "block" : "blocks")} (30m ea)";

        public string PercentageDisplay => $"{Percentage:F1}%";
    }
}
