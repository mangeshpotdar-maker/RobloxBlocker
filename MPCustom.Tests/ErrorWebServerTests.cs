using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using MPCustom.Core.Logging;
using MPCustom.ErrorPage;
using Xunit;

namespace MPCustom.Tests
{
    public class ErrorWebServerTests
    {
        [Fact]
        public async Task ErrorWebServer_ServesGenericHttp403WithoutInternalKeywords()
        {
            var logger = new RollingLoggerService(Path.Combine(Path.GetTempPath(), "mpcustom_test_logs"));
            var server = new ErrorWebServer(logger);

            int testPort = 40305;
            await server.StartAsync(testPort);

            try
            {
                using var client = new HttpClient();
                var response = await client.GetAsync($"http://127.0.0.1:{testPort}/");

                Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);

                string content = await response.Content.ReadAsStringAsync();
                Assert.Contains("HTTP ERROR 403", content);
                Assert.Contains("This site can’t be reached", content);

                // Ensure sensitive or explicit keywords are strictly NOT exposed in response
                Assert.DoesNotContain("Roblox", content, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Parental Control", content, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("MPCustom", content, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Administrator Blocked", content, StringComparison.OrdinalIgnoreCase);
            };
            finally
            {
                await server.StopAsync();
            }
        }
    }
}
