using System;

namespace MPCustom.Core.Logging
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Security
    }

    public class LogEvent
    {
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public LogLevel Level { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }

        public override string ToString()
        {
            var excStr = Exception != null ? $" | Exception: {Exception.Message}" : string.Empty;
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level.ToString().ToUpper()}] [{Category}] {Message}{excStr}";
        }
    }
}
