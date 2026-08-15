using System;
using System.Windows.Forms;
using MPCustom.Control.Forms;
using MPCustom.Core.Config;
using MPCustom.Core.Logging;
using MPCustom.Network;
using MPCustom.Security;

namespace MPCustom.Control
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            var configRepo = new ConfigRepository();
            var logger = new RollingLoggerService();
            var pinService = new PinSecurityService(configRepo, logger);
            var firewallRepo = new FirewallRuleManager(logger);
            var domainRepo = new DomainBlockManager(logger);

            // Step 1: First-Run Check
            if (!pinService.HasPinSet())
            {
                using var wizard = new SetupWizardForm(pinService, configRepo);
                if (wizard.ShowDialog() != DialogResult.OK)
                {
                    return; // Exit if setup wizard cancelled
                }
            }

            // Step 2: PIN Login Authentication
            using var login = new LoginForm(pinService);
            if (login.ShowDialog() != DialogResult.OK)
            {
                return; // Exit if login failed or closed
            }

            // Step 3: Run Main Administrator Dashboard
            Application.Run(new MainForm(configRepo, logger, pinService, firewallRepo, domainRepo));
        }
    }
}
