using System;

namespace MPCustom.Security
{
    public interface IPinSecurityService
    {
        bool HasPinSet();
        bool VerifyPin(string pin);
        bool SetPin(string newPin);
        bool ChangePin(string currentPin, string newPin);
        bool IsLockedOut(out TimeSpan remainingLockout);
        bool ResetPinAsAdministrator(string newPin);
        int FailedAttempts { get; }
    }
}
