using System;

namespace Compound_Sentry.Models
{
    public class DeviceEvent
    {
        public int Id { get; set; }
        public string MacAddress { get; set; } = string.Empty;
        public string? PhoneType { get; set; }
        public string EventType { get; set; } = string.Empty;
        public int? SignalStrength { get; set; }
        public DateTime EventTimestamp { get; set; } = DateTime.Now;
        public int? DurationSeconds { get; set; }
    }
}