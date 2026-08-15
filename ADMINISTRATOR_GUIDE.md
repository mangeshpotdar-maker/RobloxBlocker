# MPCustom Service - Administrator Guide

This guide describes how to configure, administer, and manage the **MPCustom Service** application on a target Windows PC using **MPCustom Control**.

---

## 1. Accessing MPCustom Control

- **Launch Application**: Open **MPCustom Control** from the Start Menu or installation directory (`%ProgramFiles%\MPCustom\MPCustom.Control.exe`).
- **Authentication**: Opening the application presents the Administrator Authentication screen.
- **PIN Requirements**:
  - Requires entering the configured Administrator PIN to unlock the control console.
  - Plaintext PINs are never stored or logged. Credentials are protected using PBKDF2/SHA-256 with a random salt.
- **Auto-Lock Timeout**:
  - For security, MPCustom Control automatically locks the interface after 5 minutes of inactivity (configurable: 1 min, 5 min, 10 min, 30 min, or Never).
  - When locked, sensitive UI data is hidden until the PIN is re-entered.

---

## 2. PIN Security & Administrator Recovery

### Failed Attempt Throttling & Lockout
- Enter your PIN carefully. Incorrect PIN attempts increment a failed-attempt counter.
- After 5 consecutive failed attempts, a temporary 15-minute lockout is enforced.
- Subsequent failed attempts progressively increase the lockout duration.

### Forgotten PIN Recovery
If you forget the Administrator PIN:
1. Ensure you are logged into Windows with a **Windows Administrator** user account.
2. Launch **MPCustom Control** and click **Windows Admin Recovery**.
3. Confirm PIN reset. Because the recovery process verifies native Windows Administrator credentials, you can set a new Administrator PIN without losing configuration data.

---

## 3. Protection Modes

In MPCustom Control, navigate to the **Protection & Controls** tab to select the operating mode:

1. **Always Active**:
   - Protection is enforced 24/7 without interruption.
   - Roblox processes are blocked immediately upon launch, and network requests are redirected to the generic HTTP 403 error page.
2. **Scheduled Mode**:
   - Protection is enforced automatically during specified time ranges (e.g. Monday-Friday 9:00 PM to 7:00 AM, Weekends 24h).
   - Configurable via the **Schedule Manager** tab.
3. **Disabled**:
   - Temporarily turns off all protection rules.
   - Modifying this state requires Administrator PIN authentication.

---

## 4. Temporary Administrator Allow Timer

When an authorized administrator needs temporary access to Roblox:

1. Open **MPCustom Control** -> **Protection & Controls** tab.
2. Select desired duration (**15 minutes**, **30 minutes**, **1 hour**, or **2 hours**).
3. Click **Start Temporary Allow**.
4. The system temporarily lifts firewall and domain blocks while displaying a real-time countdown timer in MPCustom Control.
5. When the timer expires, MPCustom Service automatically restores full protection.
6. Click **Cancel Temporary Allow** at any time to immediately re-enable protection.

---

## 5. Protection Diagnostic Suite

To verify that all protection layers are functioning correctly:

1. Open **MPCustom Control** -> **Protection Diagnostics** tab.
2. Click **Run Protection Test**.
3. The system checks the status of all subsystems:
   - **Service**: PASS (MPCustomService running and responsive over IPC)
   - **Firewall Rules**: PASS (Outbound blocking rules active)
   - **Process Detection**: PASS (Process monitor active)
   - **Domain Protection**: PASS (Hosts entries active)
   - **Schedule Evaluator**: PASS (Schedule engine operational)
   - **Error Experience**: PASS (HTTP 403 server operational)
   - **Overall Protection**: PASS
4. If any rule or entry is missing, click **Repair Protection** to force an immediate auto-repair cycle.

---

## 6. Logs & Maintenance

- **View Logs**: Navigate to the **Log Viewer** tab to inspect system events, protection verifications, blocked process terminations, and authentication events.
- **Export Logs**: Click **Export Logs...** to save recent event logs to a text file for auditing.
- **Log Retention**: Old log files beyond 30 days are automatically pruned during background health check cycles.
- **Privacy & Security**: MPCustom logs strictly exclude passwords, PINs, keystrokes, and browsing history.
