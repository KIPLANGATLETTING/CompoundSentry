using System;

namespace Compound_Sentry.Models
{
    public class CurrentPresence
    {
        public string MacAddress { get; set; } = string.Empty;
        public string? PhoneType { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public int? SignalStrength { get; set; }
    }
}