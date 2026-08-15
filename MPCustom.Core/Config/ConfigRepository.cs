using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using MPCustom.Core.Models;

namespace MPCustom.Core.Config
{
    public class ConfigRepository : IConfigRepository
    {
        private readonly string _configFilePath;
        private readonly object _lock = new object();
        private ProtectionConfig _cachedConfig;

        public string ConfigFilePath => _configFilePath;

        public ConfigRepository(string? customPath = null)
        {
            if (!string.IsNullOrEmpty(customPath))
            {
                _configFilePath = customPath;
            }
            else
            {
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var configDir = Path.Combine(programData, "MPCustom", "Config");
                EnsureDirectoryAndPermissions(configDir);
                _configFilePath = Path.Combine(configDir, "protection_config.json");
            }

            _cachedConfig = LoadOrCreateConfig();
        }

        public ProtectionConfig GetConfig()
        {
            lock (_lock)
            {
                // Return a deep or new copy or current cached config
                return _cachedConfig;
            }
        }

        public void SaveConfig(ProtectionConfig config)
        {
            lock (_lock)
            {
                config.LastConfigChange = DateTimeOffset.UtcNow;
                var directory = Path.GetDirectoryName(_configFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    EnsureDirectoryAndPermissions(directory);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(config, options);

                // Atomic write via temp file
                var tempPath = _configFilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _configFilePath, overwrite: true);

                SetFilePermissions(_configFilePath);
                _cachedConfig = config;
            }
        }

        public void UpdateConfig(Action<ProtectionConfig> updateAction)
        {
            lock (_lock)
            {
                var current = GetConfig();
                updateAction(current);
                SaveConfig(current);
            }
        }

        private ProtectionConfig LoadOrCreateConfig()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_configFilePath))
                    {
                        var json = File.ReadAllText(_configFilePath);
                        var loaded = JsonSerializer.Deserialize<ProtectionConfig>(json);
                        if (loaded != null)
                        {
                            return loaded;
                        }
                    }
                }
                catch
                {
                    // If reading fails or file corrupted, return new default config
                }

                var defaultConfig = new ProtectionConfig();
                SaveConfig(defaultConfig);
                return defaultConfig;
            }
        }

        private static void EnsureDirectoryAndPermissions(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            SetDirectoryPermissions(path);
        }

        private static void SetDirectoryPermissions(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var directoryInfo = new DirectoryInfo(path);
                    var security = directoryInfo.GetAccessControl();

                    security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

                    var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                    var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

                    security.AddAccessRule(new FileSystemAccessRule(
                        adminSid,
                        FileSystemRights.FullControl,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));

                    security.AddAccessRule(new FileSystemAccessRule(
                        systemSid,
                        FileSystemRights.FullControl,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));

                    directoryInfo.SetAccessControl(security);
                }
                catch
                {
                    // Fail gracefully if not running elevated during test execution
                }
            }
        }

        private static void SetFilePermissions(string filePath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(filePath))
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var security = fileInfo.GetAccessControl();

                    security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

                    var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                    var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

                    security.AddAccessRule(new FileSystemAccessRule(
                        adminSid,
                        FileSystemRights.FullControl,
                        AccessControlType.Allow));

                    security.AddAccessRule(new FileSystemAccessRule(
                        systemSid,
                        FileSystemRights.FullControl,
                        AccessControlType.Allow));

                    fileInfo.SetAccessControl(security);
                }
                catch
                {
                    // Fail gracefully
                }
            }
        }
    }
}
