# MPCustom Service - Troubleshooting Guide

This document provides troubleshooting procedures for common operational scenarios with **MPCustom Service**.

---

## 1. Windows Service Issues

### Service `MPCustomService` fails to start
- **Symptom**: Service control manager reports an error when starting `MPCustomService`.
- **Root Cause**: Insufficient privileges, corrupted configuration file, or port conflict.
- **Resolution**:
  1. Verify the service executable exists at `%ProgramFiles%\MPCustom\MPCustom.Service.exe`.
  2. Open an elevated Command Prompt (Run as Administrator) and execute:
     ```cmd
     sc query MPCustomService
     ```
  3. If missing, reinstall using `MPCustom.Installer.exe`.
  4. Inspect log files located at `%CommonProgramData%\MPCustom\Logs\`.

### MPCustom Control cannot connect to Service
- **Symptom**: Dashboard shows "Service Unreachable".
- **Resolution**:
  1. Open Windows Services (`services.msc`) and verify `MPCustom Service` is listed and in **Running** state.
  2. If stopped, right-click and select **Start**.
  3. In MPCustom Control, navigate to **Protection Diagnostics** and click **Run Protection Test**.

---

## 2. Authentication & PIN Issues

### Forgotten PIN or Lockout
- **Symptom**: Unable to log in to MPCustom Control or locked out due to failed attempts.
- **Resolution**:
  1. Right-click **MPCustom Control** and select **Run as Administrator**.
  2. On the PIN authentication screen, click **Windows Admin Recovery**.
  3. Confirm the prompt to reset the PIN to a new value.
  4. Log in using your new PIN.

---

## 3. Firewall Rules & Domain Protection Issues

### Roblox opens despite active protection
- **Symptom**: Roblox client launches or connects.
- **Root Cause**: Roblox was updated or moved to a new installation folder, or firewall rules were cleared.
- **Resolution**:
  1. Open **MPCustom Control** -> **Protection Diagnostics**.
  2. Click **Run Protection Test** to identify failing rules.
  3. Click **Repair Protection**. The auto-repair engine will rediscover all Roblox installation folders and recreate firewall outbound blocking rules (`MPCustom_Block_*`).

### Hosts file entries deleted or edited
- **Symptom**: `roblox.com` opens normally in web browser.
- **Resolution**:
  1. MPCustom Service automatically checks `hosts` file integrity every 30 seconds.
  2. To force immediate repair, open **MPCustom Control** -> **Protection Diagnostics** and click **Repair Protection**.

---

## 4. HTTP Error Page Server Port Conflicts

### Port 4030 already in use
- **Symptom**: Log file indicates "Failed to start HTTP Error Web Server".
- **Resolution**:
  1. Open `%CommonProgramData%\MPCustom\Config\protection_config.json`.
  2. Modify `"ErrorServerPort": 4030` to an open port (e.g., `4031` or `40300`).
  3. Restart `MPCustom Service` via `services.msc` or Command Prompt:
     ```cmd
     sc stop MPCustomService
     sc start MPCustomService
     ```

---

## 5. Log Locations & Directory Permissions

- **Configuration File**: `%CommonProgramData%\MPCustom\Config\protection_config.json`
- **Log Directory**: `%CommonProgramData%\MPCustom\Logs\`
- **Access Control**: Both directories are protected with Windows ACLs allowing access only to `SYSTEM` and `Administrators`.
