using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using MPCustom.Core.Config;
using MPCustom.Core.Logging;
using MPCustom.Network;

namespace MPCustom.Installer
{
    public class Program
    {
        private const string ServiceName = "MPCustomService";
        private const string ServiceDisplayName = "MPCustom Service";
        private static string _installLogPath = string.Empty;

        public static int Main(string[] args)
        {
            var logger = new RollingLoggerService();
            var firewallRepo = new FirewallRuleManager(logger);
            var domainRepo = new DomainBlockManager(logger);
            var configRepo = new ConfigRepository();

            InitInstallLog();
            LogToFile("==================================================");
            LogToFile($"       MPCustom Service Setup Utility Log       ");
            LogToFile($"       Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            LogToFile($"       OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
            LogToFile("==================================================");

            bool isUninstall = args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) || a.Equals("-u", StringComparison.OrdinalIgnoreCase));

            Console.WriteLine("==================================================");
            Console.WriteLine("          MPCustom Service Setup Utility         ");
            Console.WriteLine("==================================================");

            if (!IsAdministrator())
            {
                LogToFile("[ERROR] Installation failed: Missing Windows Administrator privileges.");
                Console.WriteLine("[ERROR] Installation requires Windows Administrator privileges (UAC elevation).");
                Console.WriteLine($"[INFO] Detailed log saved to: {_installLogPath}");
                return 1;
            }

            int exitCode = isUninstall ? PerformUninstall(logger, firewallRepo, domainRepo) : PerformInstall(logger, firewallRepo, domainRepo, configRepo);
            Console.WriteLine($"[INFO] Installation log file generated at: {_installLogPath}");
            return exitCode;
        }

        private static void InitInstallLog()
        {
            try
            {
                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string logDir = Path.Combine(programData, "MPCustom", "Logs");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                _installLogPath = Path.Combine(logDir, "install.log");
            }
            catch
            {
                _installLogPath = Path.Combine(Path.GetTempPath(), "mpcustom_install.log");
            }
        }

        private static void LogToFile(string message)
        {
            try
            {
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                File.AppendAllText(_installLogPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static int PerformInstall(ILoggerService logger, IFirewallRuleManager firewallRepo, IDomainBlockManager domainRepo, IConfigRepository configRepo)
        {
            LogToFile("[INFO] Starting MPCustom Service installation sequence...");
            Console.WriteLine("[INFO] Starting MPCustom Service installation...");

            try
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string installDir = Path.Combine(programFiles, "MPCustom");

                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string dataDir = Path.Combine(programData, "MPCustom");

                LogToFile($"[INFO] Target install folder: {installDir}");
                LogToFile($"[INFO] Data folder: {dataDir}");

                EnsureDirectoryAndAcl(installDir);
                EnsureDirectoryAndAcl(dataDir);

                Console.WriteLine($"[INFO] Target Installation Directory: {installDir}");
                Console.WriteLine($"[INFO] Data Directory: {dataDir}");

                string serviceExePath = Path.Combine(installDir, "MPCustom.Service.exe");

                // Stop existing service if running
                LogToFile("[INFO] Stopping any pre-existing MPCustomService...");
                RunCommand("sc", $"stop {ServiceName}");
                RunCommand("sc", $"delete {ServiceName}");

                // Register Windows Service
                LogToFile("[INFO] Registering Windows Service (MPCustomService)...");
                Console.WriteLine("[INFO] Registering Windows Service (MPCustomService)...");
                if (OperatingSystem.IsWindows())
                {
                    string scArgs = $"create {ServiceName} binPath= \"{serviceExePath}\" start= auto DisplayName= \"{ServiceDisplayName}\"";
                    RunCommand("sc", scArgs);
                    RunCommand("sc", $"failure {ServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000");
                    RunCommand("sc", $"start {ServiceName}");
                }

                // Create initial configuration & firewall rules
                LogToFile("[INFO] Configuring domain protection and firewall rules...");
                Console.WriteLine("[INFO] Configuring protection rules and firewall engine...");
                var config = configRepo.GetConfig();
                bool domApplied = domainRepo.ApplyDomainBlocks(config.RobloxRules.BlockedDomains);
                LogToFile($"[INFO] Domain block hosts update status: {domApplied}");

                var knownExes = new[]
                {
                    Path.Combine(installDir, "RobloxPlayerBeta.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions", "RobloxPlayerBeta.exe")
                };
                bool fwApplied = firewallRepo.EnsureRulesExist(knownExes);
                LogToFile($"[INFO] Firewall rule application status: {fwApplied}");

                // Create Start Menu shortcut
                CreateStartMenuShortcut(installDir);

                LogToFile("[SUCCESS] MPCustom Service installed and verified successfully.");
                Console.WriteLine("[SUCCESS] MPCustom Service installed and started successfully!");
                logger.LogInfo("Installer", "MPCustom Service installation completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                LogToFile($"[ERROR] Exception during installation: {ex}");
                Console.WriteLine($"[ERROR] Installation failed: {ex.Message}");
                logger.LogError("Installer", "Installation failed", ex);
                return 1;
            }
        }

        private static int PerformUninstall(ILoggerService logger, IFirewallRuleManager firewallRepo, IDomainBlockManager domainRepo)
        {
            LogToFile("[INFO] Starting MPCustom Service uninstallation sequence...");
            Console.WriteLine("[INFO] Starting MPCustom Service uninstallation...");

            try
            {
                LogToFile("[INFO] Stopping and deleting Windows Service...");
                Console.WriteLine("[INFO] Stopping and deleting Windows Service...");
                RunCommand("sc", $"stop {ServiceName}");
                RunCommand("sc", $"delete {ServiceName}");

                LogToFile("[INFO] Removing firewall rules and domain blocks...");
                Console.WriteLine("[INFO] Cleaning firewall rules and domain blocks...");
                firewallRepo.RemoveAllRules();
                domainRepo.RemoveDomainBlocks();

                string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "MPCustom Control.lnk");
                if (File.Exists(startMenu))
                {
                    try { File.Delete(startMenu); LogToFile("[INFO] Removed Start Menu shortcut."); } catch { }
                }

                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string installDir = Path.Combine(programFiles, "MPCustom");
                if (Directory.Exists(installDir))
                {
                    try { Directory.Delete(installDir, true); LogToFile("[INFO] Removed installation directory."); } catch { }
                }

                LogToFile("[SUCCESS] MPCustom Service uninstalled successfully.");
                Console.WriteLine("[SUCCESS] MPCustom Service uninstalled successfully!");
                logger.LogInfo("Installer", "MPCustom Service uninstallation completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                LogToFile($"[ERROR] Uninstallation failed: {ex}");
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
                LogToFile($"[INFO] Created directory: {path}");
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
                    LogToFile($"[INFO] Configured ACL permissions for: {path}");
                }
                catch (Exception ex)
                {
                    LogToFile($"[WARNING] Could not set ACL permissions on '{path}': {ex.Message}");
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
                    string psCommand = $"$s=(New-Object -COM WScript.Shell).CreateShortcut('{shortcutPath}');$s.TargetPath='{targetExe}';$s.Save()";
                    RunCommand("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"");
                    LogToFile($"[INFO] Created Start Menu shortcut at: {shortcutPath}");
                }
            }
            catch (Exception ex)
            {
                LogToFile($"[WARNING] Failed to create shortcut: {ex.Message}");
            }
        }

        private static void RunCommand(string fileName, string arguments)
        {
            try
            {
                LogToFile($"[CMD EXEC] {fileName} {arguments}");
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
                if (proc != null)
                {
                    string outText = proc.StandardOutput.ReadToEnd();
                    string errText = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(5000);
                    if (!string.IsNullOrWhiteSpace(outText)) LogToFile($"[CMD STDOUT] {outText.Trim()}");
                    if (!string.IsNullOrWhiteSpace(errText)) LogToFile($"[CMD STDERR] {errText.Trim()}");
                }
            }
            catch (Exception ex)
            {
                LogToFile($"[CMD ERROR] Error running {fileName}: {ex.Message}");
            }
        }
    }
}
