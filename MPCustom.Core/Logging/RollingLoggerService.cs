using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace MPCustom.Core.Logging
{
    public class RollingLoggerService : ILoggerService
    {
        private readonly string _logDirectory;
        private readonly object _lock = new object();
        private readonly List<LogEvent> _memoryLogs = new List<LogEvent>();
        private const int MaxMemoryLogs = 500;

        public string LogDirectory => _logDirectory;

        public RollingLoggerService(string? customDirectory = null)
        {
            if (!string.IsNullOrEmpty(customDirectory))
            {
                _logDirectory = customDirectory;
            }
            else
            {
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                _logDirectory = Path.Combine(programData, "MPCustom", "Logs");
            }

            EnsureDirectoryAndPermissions(_logDirectory);
        }

        public void Log(LogLevel level, string category, string message, Exception? exception = null)
        {
            // Sanitize message to prevent accidental logging of sensitive key phrases or tokens
            var sanitizedMessage = SanitizeMessage(message);

            var logEvent = new LogEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = level,
                Category = category,
                Message = sanitizedMessage,
                Exception = exception
            };

            lock (_lock)
            {
                _memoryLogs.Add(logEvent);
                if (_memoryLogs.Count > MaxMemoryLogs)
                {
                    _memoryLogs.RemoveAt(0);
                }

                try
                {
                    EnsureDirectoryAndPermissions(_logDirectory);
                    var fileName = $"mpcustom_log_{DateTime.UtcNow:yyyy-MM-dd}.log";
                    var filePath = Path.Combine(_logDirectory, fileName);
                    File.AppendAllText(filePath, logEvent.ToString() + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // Fail-safe: Logging failure should not crash the host service
                }
            }
        }

        public void LogInfo(string category, string message) => Log(LogLevel.Info, category, message);
        public void LogWarning(string category, string message) => Log(LogLevel.Warning, category, message);
        public void LogError(string category, string message, Exception? exception = null) => Log(LogLevel.Error, category, message, exception);
        public void LogSecurity(string category, string message) => Log(LogLevel.Security, category, message);

        public List<LogEvent> GetRecentLogs(int maxCount = 100)
        {
            lock (_lock)
            {
                return _memoryLogs.TakeLast(maxCount).ToList();
            }
        }

        public void CleanOldLogs(int retentionDays)
        {
            if (retentionDays <= 0) return;

            try
            {
                lock (_lock)
                {
                    if (!Directory.Exists(_logDirectory)) return;

                    var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
                    var dir = new DirectoryInfo(_logDirectory);

                    foreach (var file in dir.GetFiles("mpcustom_log_*.log"))
                    {
                        if (file.CreationTimeUtc < cutoff && file.LastWriteTimeUtc < cutoff)
                        {
                            try { file.Delete(); } catch { }
                        }
                    }
                }
            }
            catch
            {
                // Non-critical cleanup exception
            }
        }

        private static string SanitizeMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;

            // Remove or mask potential PIN patterns or secrets if inadvertently passed
            var sanitized = message;
            if (sanitized.Contains("PIN", StringComparison.OrdinalIgnoreCase))
            {
                // Ensure raw pin values aren't exposed
                sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"(?i)(pin\s*=\s*)([^\s,]+)", "$1****");
            }
            return sanitized;
        }

        private static void EnsureDirectoryAndPermissions(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var directoryInfo = new DirectoryInfo(path);
                    var security = directoryInfo.GetAccessControl();

                    // Lock down folder to Administrators and SYSTEM
                    security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

                    var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                    var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

                    security.AddAccessRule(new FileSystemAccessRule(
                        adminSid,
                        FileSystemRights.FullControl,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));

                    security.AddAccessRule(new FileSystemAccessRule(
                        systemSid,
                        FileSystemRights.FullControl,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));

                    directoryInfo.SetAccessControl(security);
                }
                catch
                {
                    // Fail gracefully if process is running without administrative ACL privileges during dev/test
                }
            }
        }
    }
}
