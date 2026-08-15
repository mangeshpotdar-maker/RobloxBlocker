using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using MPCustom.Core.Config;
using MPCustom.Core.Logging;
using MPCustom.Network;

namespace MPCustom.Installer
{
    public class Program
    {
        private const string ServiceName = "MPCustomService";
        private const string ServiceDisplayName = "MPCustom Service";

        public static int Main(string[] args)
        {
            var logger = new RollingLoggerService();
            var firewallRepo = new FirewallRuleManager(logger);
            var domainRepo = new DomainBlockManager(logger);
            var configRepo = new ConfigRepository();

            bool isUninstall = args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) || a.Equals("-u", StringComparison.OrdinalIgnoreCase));

            Console.WriteLine("==================================================");
            Console.WriteLine("          MPCustom Service Setup Utility         ");
            Console.WriteLine("==================================================");

            if (!IsAdministrator())
            {
                Console.WriteLine("[ERROR] Installation requires Windows Administrator privileges (UAC elevation).");
                return 1;
            }

            if (isUninstall)
            {
                return PerformUninstall(logger, firewallRepo, domainRepo);
            }
            else
            {
                return PerformInstall(logger, firewallRepo, domainRepo, configRepo);
            }
        }

        private static int PerformInstall(ILoggerService logger, IFirewallRuleManager firewallRepo, IDomainBlockManager domainRepo, IConfigRepository configRepo)
        {
            Console.WriteLine("[INFO] Starting MPCustom Service installation...");

            try
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string installDir = Path.Combine(programFiles, "MPCustom");

                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string dataDir = Path.Combine(programData, "MPCustom");

                EnsureDirectoryAndAcl(installDir);
                EnsureDirectoryAndAcl(dataDir);

                Console.WriteLine($"[INFO] Target Installation Directory: {installDir}");
                Console.WriteLine($"[INFO] Data Directory: {dataDir}");

                string serviceExePath = Path.Combine(installDir, "MPCustom.Service.exe");

                // Stop existing service if running
                RunCommand("sc", $"stop {ServiceName}");
                RunCommand("sc", $"delete {ServiceName}");

                // Register Windows Service
                Console.WriteLine("[INFO] Registering Windows Service (MPCustomService)...");
                if (OperatingSystem.IsWindows())
                {
                    string scArgs = $"create {ServiceName} binPath= \"{serviceExePath}\" start= auto DisplayName= \"{ServiceDisplayName}\"";
                    RunCommand("sc", scArgs);
                    RunCommand("sc", $"failure {ServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000");
                    RunCommand("sc", $"start {ServiceName}");
                }

                // Create initial configuration & firewall rules
                Console.WriteLine("[INFO] Configuring protection rules and firewall engine...");
                var config = configRepo.GetConfig();
                domainRepo.ApplyDomainBlocks(config.RobloxRules.BlockedDomains);

                var knownExes = new[]
                {
                    Path.Combine(installDir, "RobloxPlayerBeta.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions", "RobloxPlayerBeta.exe")
                };
                firewallRepo.EnsureRulesExist(knownExes);

                // Create Start Menu shortcut
                CreateStartMenuShortcut(installDir);

                Console.WriteLine("[SUCCESS] MPCustom Service installed and started successfully!");
                logger.LogInfo("Installer", "MPCustom Service installation completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Installation failed: {ex.Message}");
                logger.LogError("Installer", "Installation failed", ex);
                return 1;
            }
        }

        private static int PerformUninstall(ILoggerService logger, IFirewallRuleManager firewallRepo, IDomainBlockManager domainRepo)
        {
            Console.WriteLine("[INFO] Starting MPCustom Service uninstallation...");

            try
            {
                // Stop and delete Windows Service
                Console.WriteLine("[INFO] Stopping and deleting Windows Service...");
                RunCommand("sc", $"stop {ServiceName}");
                RunCommand("sc", $"delete {ServiceName}");

                // Remove firewall rules and domain blocks
                Console.WriteLine("[INFO] Cleaning firewall rules and domain blocks...");
                firewallRepo.RemoveAllRules();
                domainRepo.RemoveDomainBlocks();

                // Clean shortcuts
                string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "MPCustom Control.lnk");
                if (File.Exists(startMenu))
                {
                    try { File.Delete(startMenu); } catch { }
                }

                // Clean files
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string installDir = Path.Combine(programFiles, "MPCustom");
                if (Directory.Exists(installDir))
                {
                    try { Directory.Delete(installDir, true); } catch { }
                }

                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string dataDir = Path.Combine(programData, "MPCustom");
                if (Directory.Exists(dataDir))
                {
                    try { Directory.Delete(dataDir, true); } catch { }
                }

                Console.WriteLine("[SUCCESS] MPCustom Service uninstalled successfully!");
                logger.LogInfo("Installer", "MPCustom Service uninstallation completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Uninstallation failed: {ex.Message}");
                logger.LogError("Installer", "Uninstallation failed", ex);
                return 1;
            }
        }

        private static bool IsAdministrator()
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
            return true;
        }

        private static void EnsureDirectoryAndAcl(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var dirInfo = new DirectoryInfo(path);
                    var security = dirInfo.GetAccessControl();
                    security.SetAccessRuleProtection(true, false);

                    var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                    var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

                    security.AddAccessRule(new FileSystemAccessRule(adminSid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                    security.AddAccessRule(new FileSystemAccessRule(systemSid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));

                    dirInfo.SetAccessControl(security);
                }
                catch
                {
                }
            }
        }

        private static void CreateStartMenuShortcut(string installDir)
        {
            try
            {
                string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");
                string shortcutPath = Path.Combine(startMenuDir, "MPCustom Control.lnk");
                string targetExe = Path.Combine(installDir, "MPCustom.Control.exe");

                if (OperatingSystem.IsWindows() && File.Exists(targetExe))
                {
                    // Create shortcut via PowerShell script
                    string psCommand = $"$s=(New-Object -COM WScript.Shell).CreateShortcut('{shortcutPath}');$s.TargetPath='{targetExe}';$s.Save()";
                    RunCommand("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"");
                }
            }
            catch
            {
            }
        }

        private static void RunCommand(string fileName, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit(5000);
            }
            catch
            {
            }
        }
    }
}
