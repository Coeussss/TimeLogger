using System;
using System.Text.Json.Serialization;

namespace WorkTimeTracker.Models
{
    public class TimeLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(30);
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        [JsonIgnore]
        public string FormattedTime => Timestamp.ToString("ddd, MMM d • h:mm tt");

        [JsonIgnore]
        public string TimeOfDayDisplay => Timestamp.ToString("h:mm tt");

        [JsonIgnore]
        public string DurationDisplay => Duration.TotalHours >= 1 
            ? $"{(int)Duration.TotalHours}h {Duration.Minutes}m" 
            : $"{Duration.TotalMinutes:0} min";

        public TimeLog Clone() => new()
        {
            Id = Id,
            Timestamp = Timestamp,
            Duration = Duration,
            Category = Category,
            Description = Description
        };
    }
}
