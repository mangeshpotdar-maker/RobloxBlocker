using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MPCustom.Core.Config;
using MPCustom.Core.Logging;
using MPCustom.ErrorPage;
using MPCustom.Network;
using MPCustom.Security;
using MPCustom.Service.Engine;
using MPCustom.Service.Ipc;

namespace MPCustom.Service
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "MPCustomService";
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<IConfigRepository, ConfigRepository>();
                    services.AddSingleton<ILoggerService, RollingLoggerService>();
                    services.AddSingleton<IPinSecurityService, PinSecurityService>();
                    services.AddSingleton<IDpapiEncryptionService, DpapiEncryptionService>();
                    services.AddSingleton<IFirewallRuleManager, FirewallRuleManager>();
                    services.AddSingleton<IDomainBlockManager, DomainBlockManager>();
                    services.AddSingleton<IErrorWebServer, ErrorWebServer>();
                    services.AddSingleton<IProcessProtectionService, ProcessProtectionService>();
                    services.AddSingleton<IScheduleEvaluator, ScheduleEvaluator>();
                    services.AddSingleton<IIpcServer, IpcServer>();

                    services.AddHostedService<Worker>();
                });

            var host = builder.Build();
            host.Run();
        }
    }
}
