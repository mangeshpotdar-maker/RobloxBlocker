using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MPCustom.Core.Logging;

namespace MPCustom.Network
{
    public class FirewallRuleManager : IFirewallRuleManager
    {
        private const string RulePrefix = "MPCustom_Block_";
        private readonly ILoggerService _logger;

        public FirewallRuleManager(ILoggerService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool EnsureRulesExist(IEnumerable<string> executablePaths)
        {
            if (executablePaths == null) return false;

            bool allSuccessful = true;
            foreach (var exePath in executablePaths)
            {
                if (string.IsNullOrWhiteSpace(exePath)) continue;

                var ruleName = GetRuleNameForExe(exePath);
                if (!RuleExists(ruleName))
                {
                    bool added = AddOutboundBlockRule(ruleName, exePath);
                    if (added)
                    {
                        _logger.LogInfo("Firewall", $"Created firewall outbound block rule for '{exePath}'");
                    }
                    else
                    {
                        _logger.LogError("Firewall", $"Failed to create firewall outbound block rule for '{exePath}'");
                        allSuccessful = false;
                    }
                }
            }

            return allSuccessful;
        }

        public bool VerifyRulesExist(IEnumerable<string> executablePaths)
        {
            if (executablePaths == null) return true;

            foreach (var exePath in executablePaths)
            {
                if (string.IsNullOrWhiteSpace(exePath)) continue;
                var ruleName = GetRuleNameForExe(exePath);
                if (!RuleExists(ruleName))
                {
                    return false;
                }
            }

            return true;
        }

        public bool RepairRules(IEnumerable<string> executablePaths)
        {
            _logger.LogInfo("Firewall", "Executing firewall rules auto-repair check...");
            bool verified = VerifyRulesExist(executablePaths);
            if (!verified)
            {
                _logger.LogWarning("Firewall", "Firewall rules missing or modified. Repairing rules...");
                bool repaired = EnsureRulesExist(executablePaths);
                if (repaired)
                {
                    _logger.LogInfo("Firewall", "Firewall rules auto-repair succeeded");
                }
                return repaired;
            }

            return true;
        }

        public bool RemoveAllRules()
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    // Strictly remove ONLY MPCustom rules, never system-wide rules!
                    string args = $"advfirewall firewall delete rule name=\"{RulePrefix}*\"";
                    var output = RunNetsh(args);
                    _logger.LogInfo("Firewall", "Removed all MPCustom firewall rules");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Firewall", "Error removing MPCustom firewall rules", ex);
                    return false;
                }
            }

            return true;
        }

        private static string GetRuleNameForExe(string exePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(exePath);
            if (string.IsNullOrEmpty(fileName)) fileName = "App";
            int hash = Math.Abs(exePath.ToLowerInvariant().GetHashCode());
            return $"{RulePrefix}{fileName}_{hash}";
        }

        private static bool RuleExists(string ruleName)
        {
            if (!OperatingSystem.IsWindows()) return true;

            try
            {
                string args = $"advfirewall firewall show rule name=\"{ruleName}\"";
                string output = RunNetsh(args);
                return !string.IsNullOrWhiteSpace(output) && output.Contains(ruleName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool AddOutboundBlockRule(string ruleName, string exePath)
        {
            if (!OperatingSystem.IsWindows()) return true;

            try
            {
                string args = $"advfirewall firewall add rule name=\"{ruleName}\" dir=out action=block program=\"{exePath}\" enable=yes";
                string output = RunNetsh(args);
                return output.Contains("Ok.", StringComparison.OrdinalIgnoreCase) || output.Contains("Updated", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string RunNetsh(string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return string.Empty;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return output;
        }
    }
}
