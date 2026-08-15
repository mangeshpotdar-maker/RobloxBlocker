using System;
using MPCustom.Core.Models;

namespace MPCustom.Service.Engine
{
    public class ScheduleEvaluator : IScheduleEvaluator
    {
        public bool IsProtectionShouldBeActive(ProtectionConfig config)
        {
            if (config == null) return true;

            // Check Temporary Allow first
            if (config.TemporaryAllow.IsActive && config.TemporaryAllow.ExpiresAt.HasValue)
            {
                if (DateTimeOffset.UtcNow < config.TemporaryAllow.ExpiresAt.Value)
                {
                    return false; // Temporarily allowed
                }
            }

            switch (config.Mode)
            {
                case ProtectionMode.Disabled:
                    return false;

                case ProtectionMode.Active:
                    return true;

                case ProtectionMode.Scheduled:
                    return EvaluateSchedule(config.Schedule);

                default:
                    return true;
            }
        }

        private static bool EvaluateSchedule(ScheduleConfig scheduleConfig)
        {
            if (scheduleConfig == null || !scheduleConfig.Enabled || scheduleConfig.Rules.Count == 0)
            {
                return true; // Default to active if schedule enabled without rules
            }

            var nowLocal = DateTime.Now;
            var currentDay = nowLocal.DayOfWeek;
            var currentTimeOfDay = nowLocal.TimeOfDay;

            foreach (var rule in scheduleConfig.Rules)
            {
                if (rule.Days.Contains(currentDay))
                {
                    if (rule.AlwaysBlocked)
                    {
                        return true;
                    }

                    // Handles overnight time spans (e.g. 21:00 to 07:00)
                    if (rule.StartTime > rule.EndTime)
                    {
                        if (currentTimeOfDay >= rule.StartTime || currentTimeOfDay <= rule.EndTime)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (currentTimeOfDay >= rule.StartTime && currentTimeOfDay <= rule.EndTime)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
