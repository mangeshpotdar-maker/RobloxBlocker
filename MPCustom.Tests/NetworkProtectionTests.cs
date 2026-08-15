using System;
using System.IO;
using System.Linq;
using MPCustom.Core.Logging;
using MPCustom.Network;
using Xunit;

namespace MPCustom.Tests
{
    public class NetworkProtectionTests
    {
        [Fact]
        public void DomainBlockManager_ApplyVerifyAndRemove_ModifiesHostsFileCleanly()
        {
            string tempHostsPath = Path.Combine(Path.GetTempPath(), $"hosts_test_{Guid.NewGuid():N}");
            File.WriteAllLines(tempHostsPath, new[] { "127.0.0.1 localhost", "::1 localhost" });

            try
            {
                var logger = new RollingLoggerService(Path.Combine(Path.GetTempPath(), "mpcustom_test_logs"));
                var manager = new DomainBlockManager(logger, tempHostsPath);

                var testDomains = new[] { "roblox.com", "www.roblox.com", "api.roblox.com" };

                bool applyOk = manager.ApplyDomainBlocks(testDomains);
                Assert.True(applyOk);

                bool verifyOk = manager.VerifyDomainBlocks(testDomains);
                Assert.True(verifyOk);

                string content = File.ReadAllText(tempHostsPath);
                Assert.Contains("roblox.com", content);
                Assert.Contains("# BEGIN MPCUSTOM DOMAIN BLOCK", content);

                bool removeOk = manager.RemoveDomainBlocks();
                Assert.True(removeOk);

                string cleanContent = File.ReadAllText(tempHostsPath);
                Assert.DoesNotContain("roblox.com", cleanContent);
                Assert.DoesNotContain("# BEGIN MPCUSTOM DOMAIN BLOCK", cleanContent);
                Assert.Contains("127.0.0.1 localhost", cleanContent);
            }
            finally
            {
                if (File.Exists(tempHostsPath)) File.Delete(tempHostsPath);
            }
        }
    }
}
