using System;
using System.Collections.Generic;

namespace MPCustom.Core.Logging
{
    public interface ILoggerService
    {
        void Log(LogLevel level, string category, string message, Exception? exception = null);
        void LogInfo(string category, string message);
        void LogWarning(string category, string message);
        void LogError(string category, string message, Exception? exception = null);
        void LogSecurity(string category, string message);
        List<LogEvent> GetRecentLogs(int maxCount = 100);
        void CleanOldLogs(int retentionDays);
        string LogDirectory { get; }
    }
}
