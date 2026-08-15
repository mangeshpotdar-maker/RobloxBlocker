using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MPCustom.Core.Logging;

namespace MPCustom.Network
{
    public class DomainBlockManager : IDomainBlockManager
    {
        private const string BeginMarker = "# BEGIN MPCUSTOM DOMAIN BLOCK";
        private const string EndMarker = "# END MPCUSTOM DOMAIN BLOCK";
        private readonly ILoggerService _logger;
        private readonly string _hostsFilePath;

        public DomainBlockManager(ILoggerService logger, string? customHostsPath = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (!string.IsNullOrEmpty(customHostsPath))
            {
                _hostsFilePath = customHostsPath;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                _hostsFilePath = Path.Combine(system32, "drivers", "etc", "hosts");
            }
            else
            {
                _hostsFilePath = "/etc/hosts";
            }
        }

        public bool ApplyDomainBlocks(IEnumerable<string> domains)
        {
            if (domains == null) return false;

            try
            {
                var domainList = domains.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.Trim().ToLowerInvariant()).Distinct().ToList();
                if (!domainList.Any()) return true;

                var existingLines = File.Exists(_hostsFilePath) ? File.ReadAllLines(_hostsFilePath).ToList() : new List<string>();
                var cleanedLines = RemoveExistingBlockLines(existingLines);

                var newBlockLines = new List<string> { BeginMarker };
                foreach (var domain in domainList)
                {
                    newBlockLines.Add($"127.0.0.1\t{domain}");
                    newBlockLines.Add($"127.0.0.1\twww.{domain.Replace("www.", "")}");
                    newBlockLines.Add($"::1\t{domain}");
                }
                newBlockLines.Add(EndMarker);

                cleanedLines.AddRange(newBlockLines);

                File.WriteAllLines(_hostsFilePath, cleanedLines);
                _logger.LogInfo("DomainBlock", $"Successfully applied domain blocks for {domainList.Count} domains");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("DomainBlock", "Failed to apply domain blocks to hosts file", ex);
                return false;
            }
        }

        public bool RemoveDomainBlocks()
        {
            try
            {
                if (!File.Exists(_hostsFilePath)) return true;

                var existingLines = File.ReadAllLines(_hostsFilePath).ToList();
                var cleanedLines = RemoveExistingBlockLines(existingLines);

                File.WriteAllLines(_hostsFilePath, cleanedLines);
                _logger.LogInfo("DomainBlock", "Successfully removed domain blocks from hosts file");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("DomainBlock", "Failed to remove domain blocks from hosts file", ex);
                return false;
            }
        }

        public bool VerifyDomainBlocks(IEnumerable<string> domains)
        {
            if (domains == null) return true;

            try
            {
                if (!File.Exists(_hostsFilePath)) return false;

                var lines = File.ReadAllLines(_hostsFilePath);
                var domainList = domains.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.Trim().ToLowerInvariant()).Distinct().ToList();

                foreach (var domain in domainList)
                {
                    bool found = lines.Any(l => !l.TrimStart().StartsWith("#") && l.Contains(domain, StringComparison.OrdinalIgnoreCase));
                    if (!found) return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool RepairDomainBlocks(IEnumerable<string> domains)
        {
            _logger.LogInfo("DomainBlock", "Executing domain block auto-repair check...");
            bool verified = VerifyDomainBlocks(domains);
            if (!verified)
            {
                _logger.LogWarning("DomainBlock", "Domain blocks missing or modified. Repairing hosts file entries...");
                bool repaired = ApplyDomainBlocks(domains);
                if (repaired)
                {
                    _logger.LogInfo("DomainBlock", "Domain blocks auto-repair succeeded");
                }
                return repaired;
            }

            return true;
        }

        private static List<string> RemoveExistingBlockLines(List<string> lines)
        {
            var result = new List<string>();
            bool insideBlock = false;

            foreach (var line in lines)
            {
                if (line.Trim() == BeginMarker)
                {
                    insideBlock = true;
                    continue;
                }

                if (line.Trim() == EndMarker)
                {
                    insideBlock = false;
                    continue;
                }

                if (!insideBlock)
                {
                    result.Add(line);
                }
            }

            return result;
        }
    }
}
