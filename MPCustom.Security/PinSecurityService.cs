using System;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using MPCustom.Core.Config;
using MPCustom.Core.Logging;

namespace MPCustom.Security
{
    public class PinSecurityService : IPinSecurityService
    {
        private readonly IConfigRepository _configRepo;
        private readonly ILoggerService _logger;

        private const int SaltByteSize = 16;
        private const int HashByteSize = 32;
        private const int Pbkdf2Iterations = 100000;

        public int FailedAttempts
        {
            get
            {
                var config = _configRepo.GetConfig();
                return config.Security.FailedAttempts;
            }
        }

        public PinSecurityService(IConfigRepository configRepo, ILoggerService logger)
        {
            _configRepo = configRepo ?? throw new ArgumentNullException(nameof(configRepo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool HasPinSet()
        {
            var config = _configRepo.GetConfig();
            return !string.IsNullOrWhiteSpace(config.Security.PinHash) && !string.IsNullOrWhiteSpace(config.Security.PinSalt);
        }

        public bool IsLockedOut(out TimeSpan remainingLockout)
        {
            remainingLockout = TimeSpan.Zero;
            var config = _configRepo.GetConfig();

            if (config.Security.LockoutExpiry.HasValue)
            {
                var now = DateTimeOffset.UtcNow;
                if (now < config.Security.LockoutExpiry.Value)
                {
                    remainingLockout = config.Security.LockoutExpiry.Value - now;
                    return true;
                }
            }

            return false;
        }

        public bool VerifyPin(string pin)
        {
            if (string.IsNullOrWhiteSpace(pin)) return false;

            if (IsLockedOut(out var remaining))
            {
                _logger.LogSecurity("Authentication", $"Authentication rejected due to active lockout ({remaining.TotalSeconds:F0}s remaining)");
                return false;
            }

            var config = _configRepo.GetConfig();
            if (!HasPinSet())
            {
                _logger.LogWarning("Authentication", "Verification attempt made before PIN was set");
                return false;
            }

            byte[] salt = Convert.FromBase64String(config.Security.PinSalt);
            byte[] expectedHash = Convert.FromBase64String(config.Security.PinHash);

            byte[] actualHash = HashPin(pin, salt);

            if (CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
            {
                // Successful verification: reset failed attempt counter and lockout
                _configRepo.UpdateConfig(c =>
                {
                    c.Security.FailedAttempts = 0;
                    c.Security.LockoutExpiry = null;
                });

                _logger.LogSecurity("Authentication", "Administrator PIN successfully verified");
                return true;
            }

            // Failed verification: increment counter & throttle
            int newFailedCount = 0;
            DateTimeOffset? newLockout = null;

            _configRepo.UpdateConfig(c =>
            {
                c.Security.FailedAttempts++;
                newFailedCount = c.Security.FailedAttempts;

                if (newFailedCount >= c.Security.MaxFailedAttemptsThreshold)
                {
                    // Progressive lockout duration
                    int lockoutMultiplier = newFailedCount - c.Security.MaxFailedAttemptsThreshold + 1;
                    int lockoutMinutes = c.Security.InitialLockoutMinutes * lockoutMultiplier;
                    newLockout = DateTimeOffset.UtcNow.AddMinutes(lockoutMinutes);
                    c.Security.LockoutExpiry = newLockout;
                }
            });

            _logger.LogSecurity("Authentication", $"Failed PIN authentication attempt #{newFailedCount}");
            if (newLockout.HasValue)
            {
                _logger.LogSecurity("Authentication", $"Lockout triggered until {newLockout.Value:yyyy-MM-dd HH:mm:ss UTC}");
            }

            return false;
        }

        public bool SetPin(string newPin)
        {
            if (string.IsNullOrWhiteSpace(newPin) || newPin.Length < 4)
            {
                _logger.LogWarning("Authentication", "Attempted to set an invalid or short PIN");
                return false;
            }

            byte[] salt = RandomNumberGenerator.GetBytes(SaltByteSize);
            byte[] hash = HashPin(newPin, salt);

            _configRepo.UpdateConfig(c =>
            {
                c.Security.PinSalt = Convert.ToBase64String(salt);
                c.Security.PinHash = Convert.ToBase64String(hash);
                c.Security.FailedAttempts = 0;
                c.Security.LockoutExpiry = null;
            });

            _logger.LogSecurity("Authentication", "Administrator PIN set/updated successfully");
            return true;
        }

        public bool ChangePin(string currentPin, string newPin)
        {
            if (!VerifyPin(currentPin))
            {
                _logger.LogSecurity("Authentication", "PIN change failed: Current PIN verification failed");
                return false;
            }

            return SetPin(newPin);
        }

        public bool ResetPinAsAdministrator(string newPin)
        {
            if (!IsCurrentProcessAdministrator())
            {
                _logger.LogSecurity("Authentication", "PIN recovery rejected: Current user lacks Windows Administrator privileges");
                return false;
            }

            bool success = SetPin(newPin);
            if (success)
            {
                _logger.LogSecurity("Authentication", "PIN successfully reset via Windows Administrator Recovery mechanism");
            }
            return success;
        }

        private static bool IsCurrentProcessAdministrator()
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    using var identity = WindowsIdentity.GetCurrent();
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
                catch
                {
                    return false;
                }
            }

            // Non-Windows environment fallback for dev/testing
            return true;
        }

        private static byte[] HashPin(string pin, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(pin, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(HashByteSize);
        }
    }
}
