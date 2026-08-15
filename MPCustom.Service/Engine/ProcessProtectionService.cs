using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MPCustom.Core.Config;
using MPCustom.Core.Logging;

namespace MPCustom.Service.Engine
{
    public class ProcessProtectionService : IProcessProtectionService
    {
        private readonly IConfigRepository _configRepo;
        private readonly ILoggerService _logger;

        public ProcessProtectionService(IConfigRepository configRepo, ILoggerService logger)
        {
            _configRepo = configRepo ?? throw new ArgumentNullException(nameof(configRepo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public List<string> DiscoverRobloxExecutables()
        {
            var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var config = _configRepo.GetConfig();

            // Include explicitly configured paths
            foreach (var path in config.RobloxRules.ExecutablePaths)
            {
                if (File.Exists(path)) discovered.Add(path);
            }

            // Search standard Windows Roblox installation directories
            var searchFolders = new List<string>();

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                searchFolders.Add(Path.Combine(localAppData, "Roblox"));
                searchFolders.Add(Path.Combine(localAppData, "Programs", "Roblox"));
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
            {
                searchFolders.Add(Path.Combine(programFiles, "Roblox"));
            }

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(programFilesX86))
            {
                searchFolders.Add(Path.Combine(programFilesX86, "Roblox"));
            }

            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrEmpty(programData))
            {
                searchFolders.Add(Path.Combine(programData, "Roblox"));
            }

            foreach (var folder in searchFolders)
            {
                if (Directory.Exists(folder))
                {
                    try
                    {
                        var exeFiles = Directory.GetFiles(folder, "*.exe", SearchOption.AllDirectories);
                        foreach (var exe in exeFiles)
                        {
                            var fileName = Path.GetFileNameWithoutExtension(exe);
                            if (config.RobloxRules.ProcessNames.Any(pn => pn.Equals(fileName, StringComparison.OrdinalIgnoreCase)) ||
                                exe.Contains("Roblox", StringComparison.OrdinalIgnoreCase))
                            {
                                discovered.Add(exe);
                            }
                        }
                    }
                    catch
                    {
                        // Ignore permission errors on specific subfolders
                    }
                }
            }

            // Save newly discovered executable paths to configuration
            if (discovered.Count > 0)
            {
                _configRepo.UpdateConfig(c =>
                {
                    foreach (var path in discovered)
                    {
                        if (!c.RobloxRules.ExecutablePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                        {
                            c.RobloxRules.ExecutablePaths.Add(path);
                        }
                    }
                });
            }

            return discovered.ToList();
        }

        public int CheckAndTerminateRobloxProcesses(bool isProtectionActive)
        {
            if (!isProtectionActive) return 0;

            int terminatedCount = 0;
            var config = _configRepo.GetConfig();
            var targetNames = config.RobloxRules.ProcessNames;

            try
            {
                var processes = Process.GetProcesses();
                foreach (var process in processes)
                {
                    try
                    {
                        string processName = process.ProcessName;
                        bool isRoblox = targetNames.Any(tn => tn.Equals(processName, StringComparison.OrdinalIgnoreCase));

                        if (!isRoblox)
                        {
                            try
                            {
                                string? mainModulePath = process.MainModule?.FileName;
                                if (!string.IsNullOrEmpty(mainModulePath) && mainModulePath.Contains("Roblox", StringComparison.OrdinalIgnoreCase))
                                {
                                    isRoblox = true;
                                }
                            }
                            catch
                            {
                                // MainModule access may throw Win32Exception for elevated processes
                            }
                        }

                        if (isRoblox)
                        {
                            _logger.LogWarning("ProcessProtection", $"Target Roblox process detected (PID: {process.Id}, Name: {processName}). Terminating process...");
                            process.Kill(entireProcessTree: true);
                            terminatedCount++;

                            _configRepo.UpdateConfig(c =>
                            {
                                c.LastRobloxDetected = DateTimeOffset.UtcNow;
                                c.TotalBlockedCount++;
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("ProcessProtection", $"Error checking or terminating process PID {process.Id}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("ProcessProtection", "Error retrieving process list", ex);
            }

            return terminatedCount;
        }
    }
}
