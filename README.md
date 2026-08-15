# MPCustom Service

**MPCustom Service** is a production-grade, enterprise-ready Windows background service and administrator control system designed to prevent Roblox from functioning on an authorized Windows PC.

The application delivers seamless background enforcement, automatic protection repair, multi-layered firewall/domain blocking, a generic HTTP 403 error experience, administrator authentication, and comprehensive logging and diagnostics.

---

## Key Features

- **Silent Windows Service (`MPCustomService`)**: Runs automatically in the background using native Windows Service APIs, with no notification area icon and minimal CPU/memory footprint.
- **Generic Non-Deceptive Error Experience**: When Roblox access is attempted, users receive a standard HTTP 403 error ("HTTP ERROR 403 - This site can't be reached") rather than explicit block messages.
- **Multi-Layered Enforcement**:
  - Outbound Windows Firewall blocking rules (`MPCustom_Block_*`).
  - Automated local domain redirection/blocking (`roblox.com`, `api.roblox.com`, `auth.roblox.com`, etc.).
  - Real-time Roblox process detection and termination (`RobloxPlayerBeta`, `RobloxPlayerLauncher`, `Windows10Universal`, etc.).
- **Administrator Control Console (`MPCustom Control`)**: Desktop GUI application featuring First-Run Setup Wizard, PIN authentication, Admin Dashboard, Protection Toggles, Schedule Manager, Temporary Allow countdown timer, Diagnostic Suite, and Log Viewer.
- **Secure PIN Authentication**: PBKDF2/SHA-256 password hashing with unique random salts, progressive lockout throttling against brute-force attempts, DPAPI encryption wrapper, and legitimate Windows Administrator recovery options.
- **Auto-Repair & Persistence**: Automatically restores protection configuration and firewall/domain rules if modified or tampered with. Protection survives reboots, Roblox updates, and re-installations.
- **Clean Uninstallation**: Professional setup utility (`MPCustom.Installer`) providing clean setup and complete uninstallation (removing service, firewall rules, hosts entries, and shortcuts).

---

## Solution Structure

The Visual Studio solution (`MPCustom.sln`) is organized into distinct layer projects:

| Project | Description |
| :--- | :--- |
| **`MPCustom.Core`** | Domain models (`ProtectionConfig`, `ScheduleConfig`), thread-safe JSON repository with Windows ACL permissions, and rolling logger. |
| **`MPCustom.Security`** | PBKDF2/SHA-256 PIN hashing, progressive failed attempt lockout manager, DPAPI encryption wrapper, and Windows Admin PIN recovery. |
| **`MPCustom.Network`** | Outbound Windows Firewall rule engine with auto-repair and hosts file domain blocking engine. |
| **`MPCustom.ErrorPage`** | Local HTTP web server serving generic HTTP 403 error pages on port 4030. |
| **`MPCustom.Service`** | Windows Service worker daemon (`MPCustomService`), process monitor, auto-repair daemon, and Named Pipe IPC server. |
| **`MPCustom.Control`** | WinForms Administrator Control desktop app (`MPCustom Control`) with Setup Wizard, Dashboard, Diagnostics, and Settings. |
| **`MPCustom.Installer`** | Windows administrative setup utility providing UAC elevation, service registration, directory ACL setup, and clean uninstallation. |
| **`MPCustom.Tests`** | Automated unit and integration test suite. |

---

## Prerequisites & Building

- **Target OS**: Windows 10 / Windows 11 (64-bit)
- **SDK**: .NET 8.0 SDK or .NET 10.0 SDK

To build the complete solution from source:

```bash
dotnet build MPCustom.sln
```

To run all automated unit and integration tests:

```bash
dotnet test MPCustom.Tests/MPCustom.Tests.csproj
```

---

## Installation & Setup

1. Run `MPCustom.Installer.exe` as an Administrator (requires UAC elevation).
2. The installer will register and start `MPCustomService`, configure initial firewall and domain rules, set directory ACL permissions, and create a Start Menu shortcut for `MPCustom Control`.
3. Launch `MPCustom Control` from the Start Menu to complete the First-Run Setup Wizard:
   - Create your secure Administrator PIN (minimum 4 characters).
   - Select default protection mode (**Always Active** or **Scheduled**).
   - Run initial protection verification test.

---

## Uninstallation

To cleanly uninstall MPCustom Service and restore all network settings:

```cmd
MPCustom.Installer.exe --uninstall
```

This stops and removes the service, deletes all MPCustom firewall rules, restores the `hosts` file, and cleans up installation files.
