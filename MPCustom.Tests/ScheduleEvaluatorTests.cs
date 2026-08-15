using System;
using System.Collections.Generic;
using MPCustom.Core.Models;
using MPCustom.Service.Engine;
using Xunit;

namespace MPCustom.Tests
{
    public class ScheduleEvaluatorTests
    {
        private readonly ScheduleEvaluator _evaluator = new ScheduleEvaluator();

        [Fact]
        public void ModeActive_ReturnsTrue()
        {
            var config = new ProtectionConfig { Mode = ProtectionMode.Active };
            Assert.True(_evaluator.IsProtectionShouldBeActive(config));
        }

        [Fact]
        public void ModeDisabled_ReturnsFalse()
        {
            var config = new ProtectionConfig { Mode = ProtectionMode.Disabled };
            Assert.False(_evaluator.IsProtectionShouldBeActive(config));
        }

        [Fact]
        public void TemporaryAllowActive_ReturnsFalse()
        {
            var config = new ProtectionConfig
            {
                Mode = ProtectionMode.Active,
                TemporaryAllow = new TemporaryAllowConfig
                {
                    IsActive = true,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
                }
            };

            Assert.False(_evaluator.IsProtectionShouldBeActive(config));
        }

        [Fact]
        public void ScheduledMode_OutsideHours_ReturnsFalse()
        {
            var now = DateTime.Now;
            var currentDay = now.DayOfWeek;

            var config = new ProtectionConfig
            {
                Mode = ProtectionMode.Scheduled,
                Schedule = new ScheduleConfig
                {
                    Enabled = true,
                    Rules = new List<ScheduleRule>
                    {
                        new ScheduleRule
                        {
                            Days = new List<DayOfWeek> { currentDay },
                            StartTime = TimeSpan.FromHours(2), // 02:00
                            EndTime = TimeSpan.FromHours(3)    // 03:00
                        }
                    }
                }
            };

            // If current time is not between 02:00 and 03:00, protection should evaluate correctly
            bool expected = now.TimeOfDay >= TimeSpan.FromHours(2) && now.TimeOfDay <= TimeSpan.FromHours(3);
            Assert.Equal(expected, _evaluator.IsProtectionShouldBeActive(config));
        }
    }
}
