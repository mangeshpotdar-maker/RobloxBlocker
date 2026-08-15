using System;
using System.IO;
using MPCustom.Core.Config;
using MPCustom.Core.Logging;
using MPCustom.Security;
using Xunit;

namespace MPCustom.Tests
{
    public class PinSecurityTests
    {
        private readonly string _testPath;
        private readonly ConfigRepository _configRepo;
        private readonly RollingLoggerService _logger;
        private readonly PinSecurityService _pinService;

        public PinSecurityTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"mpcustom_test_{Guid.NewGuid():N}.json");
            _configRepo = new ConfigRepository(_testPath);
            _logger = new RollingLoggerService(Path.Combine(Path.GetTempPath(), "mpcustom_test_logs"));
            _pinService = new PinSecurityService(_configRepo, _logger);
        }

        [Fact]
        public void SetPinAndVerify_ValidPin_ReturnsTrue()
        {
            Assert.False(_pinService.HasPinSet());

            bool setOk = _pinService.SetPin("123456");
            Assert.True(setOk);
            Assert.True(_pinService.HasPinSet());

            bool verifyOk = _pinService.VerifyPin("123456");
            Assert.True(verifyOk);
        }

        [Fact]
        public void VerifyPin_InvalidPin_ReturnsFalseAndIncrementsFailedAttempts()
        {
            _pinService.SetPin("9999");
            Assert.Equal(0, _pinService.FailedAttempts);

            bool verifyFail = _pinService.VerifyPin("1111");
            Assert.False(verifyFail);
            Assert.Equal(1, _pinService.FailedAttempts);
        }

        [Fact]
        public void VerifyPin_MaxFailedAttempts_TriggersLockout()
        {
            _pinService.SetPin("8888");

            for (int i = 0; i < 5; i++)
            {
                _pinService.VerifyPin("0000");
            }

            Assert.True(_pinService.IsLockedOut(out TimeSpan remaining));
            Assert.True(remaining.TotalMinutes > 0);

            // Authentication during lockout should fail even with correct PIN
            bool failDuringLockout = _pinService.VerifyPin("8888");
            Assert.False(failDuringLockout);
        }

        [Fact]
        public void ChangePin_ValidCurrentPin_UpdatesPin()
        {
            _pinService.SetPin("1111");

            bool changeOk = _pinService.ChangePin("1111", "2222");
            Assert.True(changeOk);

            Assert.True(_pinService.VerifyPin("2222"));
            Assert.False(_pinService.VerifyPin("1111"));
        }

        [Fact]
        public void ResetPinAsAdministrator_ResetsPinAndClearsFailedAttempts()
        {
            _pinService.SetPin("1111");
            _pinService.VerifyPin("wrong");

            bool resetOk = _pinService.ResetPinAsAdministrator("3333");
            Assert.True(resetOk);

            Assert.Equal(0, _pinService.FailedAttempts);
            Assert.True(_pinService.VerifyPin("3333"));
        }
    }
}
