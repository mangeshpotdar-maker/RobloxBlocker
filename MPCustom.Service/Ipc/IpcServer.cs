using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MPCustom.Core.Config;
using MPCustom.Core.Logging;
using MPCustom.Core.Models;
using MPCustom.Network;
using MPCustom.Service.Engine;

namespace MPCustom.Service.Ipc
{
    public class IpcMessage
    {
        public string Command { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
    }

    public class DiagnosticResult
    {
        public bool ServiceStatus { get; set; } = true;
        public bool FirewallRulesStatus { get; set; } = true;
        public bool ProcessDetectionStatus { get; set; } = true;
        public bool DomainProtectionStatus { get; set; } = true;
        public bool ScheduleStatus { get; set; } = true;
        public bool ErrorExperienceStatus { get; set; } = true;
        public bool OverallStatus => ServiceStatus && FirewallRulesStatus && ProcessDetectionStatus && DomainProtectionStatus && ScheduleStatus && ErrorExperienceStatus;
        public string DiagnosticMessage { get; set; } = "All protection subsystems operational.";
    }

    public class IpcServer : IIpcServer
    {
        public const string PipeName = "MPCustomPipe";
        private readonly IConfigRepository _configRepo;
        private readonly ILoggerService _logger;
        private readonly IFirewallRuleManager _firewallRepo;
        private readonly IDomainBlockManager _domainRepo;
        private readonly IProcessProtectionService _processProtection;
        private readonly IScheduleEvaluator _scheduleEvaluator;
        private bool _isRunning;

        public IpcServer(
            IConfigRepository configRepo,
            ILoggerService logger,
            IFirewallRuleManager firewallRepo,
            IDomainBlockManager domainRepo,
            IProcessProtectionService processProtection,
            IScheduleEvaluator scheduleEvaluator)
        {
            _configRepo = configRepo ?? throw new ArgumentNullException(nameof(configRepo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _firewallRepo = firewallRepo ?? throw new ArgumentNullException(nameof(firewallRepo));
            _domainRepo = domainRepo ?? throw new ArgumentNullException(nameof(domainRepo));
            _processProtection = processProtection ?? throw new ArgumentNullException(nameof(processProtection));
            _scheduleEvaluator = scheduleEvaluator ?? throw new ArgumentNullException(nameof(scheduleEvaluator));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _isRunning = true;
            _logger.LogInfo("IPC", "Starting Named Pipe IPC server...");

            _ = Task.Run(() => ServerLoopAsync(cancellationToken), cancellationToken);
            return Task.CompletedTask;
        }

        public void Stop()
        {
            _isRunning = false;
        }

        private async Task ServerLoopAsync(CancellationToken token)
        {
            while (_isRunning && !token.IsCancellationRequested)
            {
                try
                {
                    NamedPipeServerStream pipeStream;

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        var pipeSecurity = new PipeSecurity();
                        var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

                        pipeSecurity.AddAccessRule(new PipeAccessRule(adminSid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
                        pipeSecurity.AddAccessRule(new PipeAccessRule(systemSid, PipeAccessRights.ReadWrite, AccessControlType.Allow));

                        pipeStream = NamedPipeServerStreamAcl.Create(
                            PipeName,
                            PipeDirection.InOut,
                            NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous,
                            0, 0,
                            pipeSecurity);
                    }
                    else
                    {
                        pipeStream = new NamedPipeServerStream(
                            PipeName,
                            PipeDirection.InOut,
                            NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous);
                    }

                    using (pipeStream)
                    {
                        await pipeStream.WaitForConnectionAsync(token);
                        await ProcessClientConnectionAsync(pipeStream, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("IPC", "Error handling IPC connection", ex);
                    await Task.Delay(500, token);
                }
            }
        }

        private async Task ProcessClientConnectionAsync(NamedPipeServerStream pipeStream, CancellationToken token)
        {
            using var reader = new StreamReader(pipeStream, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(pipeStream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            string? jsonLine = await reader.ReadLineAsync(token);
            if (string.IsNullOrEmpty(jsonLine)) return;

            try
            {
                var msg = JsonSerializer.Deserialize<IpcMessage>(jsonLine);
                if (msg == null) return;

                string response = HandleMessage(msg);
                await writer.WriteLineAsync(response.AsMemory(), token);
            }
            catch (Exception ex)
            {
                _logger.LogError("IPC", "Error processing IPC message", ex);
                await writer.WriteLineAsync("ERROR: Invalid message format".AsMemory(), token);
            }
        }

        private string HandleMessage(IpcMessage msg)
        {
            switch (msg.Command.ToUpperInvariant())
            {
                case "GET_STATUS":
                    {
                        var config = _configRepo.GetConfig();
                        bool active = _scheduleEvaluator.IsProtectionShouldBeActive(config);
                        var status = new
                        {
                            ServiceStatus = "RUNNING",
                            IsProtectionActive = active,
                            Mode = config.Mode.ToString(),
                            LastVerified = config.LastVerified,
                            LastRobloxDetected = config.LastRobloxDetected,
                            TotalBlockedCount = config.TotalBlockedCount,
                            LastConfigChange = config.LastConfigChange,
                            TemporaryAllowActive = config.TemporaryAllow.IsActive,
                            TemporaryAllowExpiresAt = config.TemporaryAllow.ExpiresAt
                        };
                        return JsonSerializer.Serialize(status);
                    }

                case "UPDATE_CONFIG":
                    {
                        var updatedConfig = JsonSerializer.Deserialize<ProtectionConfig>(msg.Payload);
                        if (updatedConfig != null)
                        {
                            _configRepo.SaveConfig(updatedConfig);
                            _logger.LogInfo("IPC", "Configuration updated via IPC");

                            // Immediately discover executables and re-apply rules
                            var exes = _processProtection.DiscoverRobloxExecutables();
                            bool active = _scheduleEvaluator.IsProtectionShouldBeActive(updatedConfig);

                            if (active)
                            {
                                _firewallRepo.EnsureRulesExist(exes);
                                _domainRepo.ApplyDomainBlocks(updatedConfig.RobloxRules.BlockedDomains);
                            }
                            else
                            {
                                _firewallRepo.RemoveAllRules();
                                _domainRepo.RemoveDomainBlocks();
                            }

                            return "OK";
                        }
                        return "ERROR: Invalid configuration payload";
                    }

                case "RUN_DIAGNOSTICS":
                    {
                        var config = _configRepo.GetConfig();
                        var exes = _processProtection.DiscoverRobloxExecutables();

                        bool fwOk = _firewallRepo.VerifyRulesExist(exes);
                        bool domOk = _domainRepo.VerifyDomainBlocks(config.RobloxRules.BlockedDomains);

                        var diag = new DiagnosticResult
                        {
                            ServiceStatus = true,
                            FirewallRulesStatus = fwOk,
                            ProcessDetectionStatus = true,
                            DomainProtectionStatus = domOk,
                            ScheduleStatus = true,
                            ErrorExperienceStatus = true,
                            DiagnosticMessage = (fwOk && domOk) ? "All protection subsystems pass." : "One or more protection checks failed. Repair recommended."
                        };

                        return JsonSerializer.Serialize(diag);
                    }

                case "TRIGGER_TEMPORARY_ALLOW":
                    {
                        if (int.TryParse(msg.Payload, out int durationMinutes) && durationMinutes > 0)
                        {
                            _configRepo.UpdateConfig(c =>
                            {
                                c.TemporaryAllow.IsActive = true;
                                c.TemporaryAllow.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(durationMinutes);
                            });

                            _firewallRepo.RemoveAllRules();
                            _domainRepo.RemoveDomainBlocks();

                            _logger.LogSecurity("IPC", $"Temporary allow triggered for {durationMinutes} minutes");
                            return "OK";
                        }
                        else if (msg.Payload.Equals("CANCEL", StringComparison.OrdinalIgnoreCase))
                        {
                            _configRepo.UpdateConfig(c =>
                            {
                                c.TemporaryAllow.IsActive = false;
                                c.TemporaryAllow.ExpiresAt = null;
                            });

                            _logger.LogSecurity("IPC", "Temporary allow cancelled");
                            return "OK";
                        }
                        return "ERROR: Invalid duration";
                    }

                case "REPAIR_PROTECTION":
                    {
                        var config = _configRepo.GetConfig();
                        var exes = _processProtection.DiscoverRobloxExecutables();

                        bool fwRepaired = _firewallRepo.RepairRules(exes);
                        bool domRepaired = _domainRepo.RepairDomainBlocks(config.RobloxRules.BlockedDomains);

                        return (fwRepaired && domRepaired) ? "OK" : "ERROR: Repair incomplete";
                    }

                default:
                    return "ERROR: Unknown command";
            }
        }
    }
}
