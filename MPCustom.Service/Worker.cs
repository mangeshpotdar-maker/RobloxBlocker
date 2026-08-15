using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using MPCustom.Core.Config;
using MPCustom.Core.Logging;
using MPCustom.ErrorPage;
using MPCustom.Network;
using MPCustom.Service.Engine;
using MPCustom.Service.Ipc;

namespace MPCustom.Service
{
    public class Worker : BackgroundService
    {
        private readonly IConfigRepository _configRepo;
        private readonly ILoggerService _logger;
        private readonly IFirewallRuleManager _firewallRepo;
        private readonly IDomainBlockManager _domainRepo;
        private readonly IErrorWebServer _errorWebServer;
        private readonly IProcessProtectionService _processProtection;
        private readonly IScheduleEvaluator _scheduleEvaluator;
        private readonly IIpcServer _ipcServer;

        public Worker(
            IConfigRepository configRepo,
            ILoggerService logger,
            IFirewallRuleManager firewallRepo,
            IDomainBlockManager domainRepo,
            IErrorWebServer errorWebServer,
            IProcessProtectionService processProtection,
            IScheduleEvaluator scheduleEvaluator,
            IIpcServer ipcServer)
        {
            _configRepo = configRepo ?? throw new ArgumentNullException(nameof(configRepo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _firewallRepo = firewallRepo ?? throw new ArgumentNullException(nameof(firewallRepo));
            _domainRepo = domainRepo ?? throw new ArgumentNullException(nameof(domainRepo));
            _errorWebServer = errorWebServer ?? throw new ArgumentNullException(nameof(errorWebServer));
            _processProtection = processProtection ?? throw new ArgumentNullException(nameof(processProtection));
            _scheduleEvaluator = scheduleEvaluator ?? throw new ArgumentNullException(nameof(scheduleEvaluator));
            _ipcServer = ipcServer ?? throw new ArgumentNullException(nameof(ipcServer));
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInfo("Service", "MPCustom Service starting...");
            await _ipcServer.StartAsync(cancellationToken);

            var config = _configRepo.GetConfig();
            await _errorWebServer.StartAsync(config.ErrorServerPort);

            await base.StartAsync(cancellationToken);
            _logger.LogInfo("Service", "MPCustom Service started successfully");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int healthCheckCounter = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var config = _configRepo.GetConfig();
                    bool isProtectionActive = _scheduleEvaluator.IsProtectionShouldBeActive(config);

                    // Process detection and termination run every tick (2 seconds)
                    _processProtection.CheckAndTerminateRobloxProcesses(isProtectionActive);

                    // Health check and repair daemon runs every 15 ticks (~30 seconds)
                    healthCheckCounter++;
                    if (healthCheckCounter >= 15)
                    {
                        healthCheckCounter = 0;
                        PerformHealthCheckAndAutoRepair(config, isProtectionActive);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("ServiceWorker", "Error in main service worker loop", ex);
                }

                await Task.Delay(2000, stoppingToken);
            }
        }

        private void PerformHealthCheckAndAutoRepair(Core.Models.ProtectionConfig config, bool isProtectionActive)
        {
            try
            {
                var discoveredExes = _processProtection.DiscoverRobloxExecutables();

                if (isProtectionActive)
                {
                    // Ensure Firewall rules & Domain blocks active
                    _firewallRepo.RepairRules(discoveredExes);
                    _domainRepo.RepairDomainBlocks(config.RobloxRules.BlockedDomains);

                    // Ensure Error Server running
                    if (!_errorWebServer.IsRunning)
                    {
                        _errorWebServer.StartAsync(config.ErrorServerPort);
                    }
                }
                else
                {
                    // Clean rules if temporary allow or disabled
                    _firewallRepo.RemoveAllRules();
                    _domainRepo.RemoveDomainBlocks();
                }

                // Update verification timestamp
                _configRepo.UpdateConfig(c => c.LastVerified = DateTimeOffset.UtcNow);

                // Housekeeping: clean old log files
                _logger.CleanOldLogs(config.LogRetentionDays);
            }
            catch (Exception ex)
            {
                _logger.LogError("ServiceWorker", "Error during health check and auto-repair cycle", ex);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInfo("Service", "MPCustom Service stopping...");
            _ipcServer.Stop();
            await _errorWebServer.StopAsync();
            await base.StopAsync(cancellationToken);
            _logger.LogInfo("Service", "MPCustom Service stopped successfully");
        }
    }
}
