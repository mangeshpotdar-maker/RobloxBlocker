namespace MPCustom.Core.Models
{
    public enum ProtectionMode
    {
        Active,
        Scheduled,
        Disabled
    }

    public class ScheduleRule
    {
        public List<DayOfWeek> Days { get; set; } = new List<DayOfWeek>();
        public TimeSpan StartTime { get; set; } = new TimeSpan(21, 0, 0); // e.g. 9:00 PM
        public TimeSpan EndTime { get; set; } = new TimeSpan(7, 0, 0);   // e.g. 7:00 AM
        public bool AlwaysBlocked { get; set; } = false;
    }

    public class ScheduleConfig
    {
        public bool Enabled { get; set; } = false;
        public List<ScheduleRule> Rules { get; set; } = new List<ScheduleRule>();
    }

    public class RobloxRuleConfig
    {
        public List<string> ProcessNames { get; set; } = new List<string>
        {
            "RobloxPlayerBeta",
            "RobloxPlayerLauncher",
            "RobloxPlayer",
            "RobloxCrashHandler",
            "Windows10Universal"
        };

        public List<string> ExecutablePaths { get; set; } = new List<string>();

        public List<string> BlockedDomains { get; set; } = new List<string>
        {
            "roblox.com",
            "www.roblox.com",
            "api.roblox.com",
            "auth.roblox.com",
            "setup.roblox.com",
            "assetdelivery.roblox.com",
            "web.roblox.com",
            "clientsettings.roblox.com",
            "gamepersistence.roblox.com"
        };
    }

    public class SecurityConfig
    {
        public string PinHash { get; set; } = string.Empty;
        public string PinSalt { get; set; } = string.Empty;
        public int FailedAttempts { get; set; } = 0;
        public DateTimeOffset? LockoutExpiry { get; set; } = null;
        public int MaxFailedAttemptsThreshold { get; set; } = 5;
        public int InitialLockoutMinutes { get; set; } = 15;
        public int AutoLockMinutes { get; set; } = 5;
    }

    public class TemporaryAllowConfig
    {
        public bool IsActive { get; set; } = false;
        public DateTimeOffset? ExpiresAt { get; set; } = null;
    }

    public class ProtectionConfig
    {
        public ProtectionMode Mode { get; set; } = ProtectionMode.Active;
        public SecurityConfig Security { get; set; } = new SecurityConfig();
        public ScheduleConfig Schedule { get; set; } = new ScheduleConfig();
        public RobloxRuleConfig RobloxRules { get; set; } = new RobloxRuleConfig();
        public TemporaryAllowConfig TemporaryAllow { get; set; } = new TemporaryAllowConfig();
        public int ErrorServerPort { get; set; } = 4030;
        public int LogRetentionDays { get; set; } = 30;
        public DateTimeOffset LastVerified { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset LastRobloxDetected { get; set; } = DateTimeOffset.MinValue;
        public long TotalBlockedCount { get; set; } = 0;
        public DateTimeOffset LastConfigChange { get; set; } = DateTimeOffset.UtcNow;
    }
}
