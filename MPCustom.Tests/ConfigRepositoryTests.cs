using System;
using System.IO;
using MPCustom.Core.Config;
using MPCustom.Core.Models;
using Xunit;

namespace MPCustom.Tests
{
    public class ConfigRepositoryTests
    {
        [Fact]
        public void ConfigRepository_SaveAndGetConfig_PersistsState()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"mpcustom_config_test_{Guid.NewGuid():N}.json");
            try
            {
                var repo = new ConfigRepository(tempFile);
                var initialConfig = repo.GetConfig();
                Assert.Equal(ProtectionMode.Active, initialConfig.Mode);

                repo.UpdateConfig(c =>
                {
                    c.Mode = ProtectionMode.Scheduled;
                    c.ErrorServerPort = 5000;
                });

                var updatedRepo = new ConfigRepository(tempFile);
                var loadedConfig = updatedRepo.GetConfig();

                Assert.Equal(ProtectionMode.Scheduled, loadedConfig.Mode);
                Assert.Equal(5000, loadedConfig.ErrorServerPort);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
