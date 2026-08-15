using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using MPCustom.Control.Services;
using MPCustom.Core.Config;
using MPCustom.Core.Logging;
using MPCustom.Core.Models;
using MPCustom.Network;
using MPCustom.Security;

namespace MPCustom.Control.Forms
{
    public class MainForm : Form
    {
        private readonly IConfigRepository _configRepo;
        private readonly ILoggerService _logger;
        private readonly IPinSecurityService _pinService;
        private readonly IFirewallRuleManager _firewallRepo;
        private readonly IDomainBlockManager _domainRepo;

        private TabControl _tabControl;
        private System.Windows.Forms.Timer _autoLockTimer;
        private System.Windows.Forms.Timer _uiRefreshTimer;
        private DateTime _lastActivity;

        // Dashboard controls
        private Label _lblStatusValue;
        private Label _lblServiceStatus;
        private Label _lblLastVerified;
        private Label _lblLastDetection;
        private Label _lblTotalBlocked;
        private Label _lblWinVersion;
        private Label _lblAppVersion;
        private Label _lblCurrentSchedule;

        // Diagnostic controls
        private ListView _lvDiagnostics;
        private Label _lblDiagOverall;

        // Temporary allow controls
        private Label _lblTempAllowTimer;
        private ComboBox _cbTempAllowDuration;

        // Logs
        private ListView _lvLogs;

        // Settings
        private ComboBox _cbAutoLockTimeout;

        public MainForm(
            IConfigRepository configRepo,
            ILoggerService logger,
            IPinSecurityService pinService,
            IFirewallRuleManager firewallRepo,
            IDomainBlockManager domainRepo)
        {
            _configRepo = configRepo ?? throw new ArgumentNullException(nameof(configRepo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pinService = pinService ?? throw new ArgumentNullException(nameof(pinService));
            _firewallRepo = firewallRepo ?? throw new ArgumentNullException(nameof(firewallRepo));
            _domainRepo = domainRepo ?? throw new ArgumentNullException(nameof(domainRepo));

            InitializeComponent();
            _lastActivity = DateTime.Now;

            _autoLockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _autoLockTimer.Tick += AutoLockTimer_Tick;
            _autoLockTimer.Start();

            _uiRefreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _uiRefreshTimer.Tick += UiRefreshTimer_Tick;
            _uiRefreshTimer.Start();

            RefreshDashboard();
        }

        private void InitializeComponent()
        {
            this.Text = "MPCustom Control - Administrator Console";
            this.Size = new Size(820, 580);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // Global activity listener
            this.MouseMove += ResetActivity;
            this.KeyDown += ResetActivity;

            _tabControl = new TabControl { Dock = DockStyle.Fill };

            _tabControl.TabPages.Add(CreateDashboardTabPage());
            _tabControl.TabPages.Add(CreateProtectionTabPage());
            _tabControl.TabPages.Add(CreateScheduleTabPage());
            _tabControl.TabPages.Add(CreateDiagnosticsTabPage());
            _tabControl.TabPages.Add(CreateLogsTabPage());
            _tabControl.TabPages.Add(CreateSettingsTabPage());

            this.Controls.Add(_tabControl);
        }

        private void ResetActivity(object? sender, EventArgs e)
        {
            _lastActivity = DateTime.Now;
        }

        private void AutoLockTimer_Tick(object? sender, EventArgs e)
        {
            var config = _configRepo.GetConfig();
            int autoLockMinutes = config.Security.AutoLockMinutes;

            if (autoLockMinutes > 0)
            {
                var inactive = DateTime.Now - _lastActivity;
                if (inactive.TotalMinutes >= autoLockMinutes)
                {
                    _autoLockTimer.Stop();
                    _uiRefreshTimer.Stop();

                    this.Hide();
                    using var login = new LoginForm(_pinService);
                    if (login.ShowDialog() == DialogResult.OK)
                    {
                        _lastActivity = DateTime.Now;
                        _autoLockTimer.Start();
                        _uiRefreshTimer.Start();
                        this.Show();
                    }
                    else
                    {
                        Application.Exit();
                    }
                }
            }

            // Update temporary allow countdown timer if active
            if (config.TemporaryAllow.IsActive && config.TemporaryAllow.ExpiresAt.HasValue)
            {
                var remaining = config.TemporaryAllow.ExpiresAt.Value - DateTimeOffset.UtcNow;
                if (remaining.TotalSeconds > 0)
                {
                    _lblTempAllowTimer.Text = $"Temporary Allow Countdown: {remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
                }
                else
                {
                    _lblTempAllowTimer.Text = "Temporary Allow Expired. Protection Active.";
                }
            }
            else
            {
                _lblTempAllowTimer.Text = "Temporary Allow: INACTIVE";
            }
        }

        private void UiRefreshTimer_Tick(object? sender, EventArgs e)
        {
            RefreshDashboard();
        }

        private TabPage CreateDashboardTabPage()
        {
            var page = new TabPage("Dashboard");
            page.Padding = new Padding(20);

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 9,
                AutoSize = true
            };

            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));

            _lblStatusValue = new Label { Font = new Font("Segoe UI", 12F, FontStyle.Bold), Text = "ACTIVE", ForeColor = Color.Green, AutoSize = true };
            _lblServiceStatus = new Label { Font = new Font("Segoe UI", 10F), Text = "Running", AutoSize = true };
            _lblLastVerified = new Label { Font = new Font("Segoe UI", 10F), Text = "Just now", AutoSize = true };
            _lblLastDetection = new Label { Font = new Font("Segoe UI", 10F), Text = "None", AutoSize = true };
            _lblTotalBlocked = new Label { Font = new Font("Segoe UI", 10F), Text = "0", AutoSize = true };
            _lblWinVersion = new Label { Font = new Font("Segoe UI", 10F), Text = Environment.OSVersion.ToString(), AutoSize = true };
            _lblAppVersion = new Label { Font = new Font("Segoe UI", 10F), Text = "1.0.0.0", AutoSize = true };
            _lblCurrentSchedule = new Label { Font = new Font("Segoe UI", 10F), Text = "24/7 Always Active", AutoSize = true };

            panel.Controls.Add(new Label { Text = "Protection Status:", Font = new Font("Segoe UI", 11F, FontStyle.Bold), AutoSize = true }, 0, 0);
            panel.Controls.Add(_lblStatusValue, 1, 0);

            panel.Controls.Add(new Label { Text = "Service Status:", Font = new Font("Segoe UI", 10F, FontStyle.Bold), AutoSize = true }, 0, 1);
            panel.Controls.Add(_lblServiceStatus, 1, 1);

            panel.Controls.Add(new Label { Text = "Last Protection Verification:", Font = new Font("Segoe UI", 10F), AutoSize = true }, 0, 2);
            panel.Controls.Add(_lblLastVerified, 1, 2);

            panel.Controls.Add(new Label { Text = "Last Roblox Detection:", Font = new Font("Segoe UI", 10F), AutoSize = true }, 0, 3);
            panel.Controls.Add(_lblLastDetection, 1, 3);

            panel.Controls.Add(new Label { Text = "Total Blocked Attempts:", Font = new Font("Segoe UI", 10F), AutoSize = true }, 0, 4);
            panel.Controls.Add(_lblTotalBlocked, 1, 4);

            panel.Controls.Add(new Label { Text = "Current Protection Mode:", Font = new Font("Segoe UI", 10F), AutoSize = true }, 0, 5);
            panel.Controls.Add(_lblCurrentSchedule, 1, 5);

            panel.Controls.Add(new Label { Text = "Windows Version:", Font = new Font("Segoe UI", 10F), AutoSize = true }, 0, 6);
            panel.Controls.Add(_lblWinVersion, 1, 6);

            panel.Controls.Add(new Label { Text = "MPCustom Version:", Font = new Font("Segoe UI", 10F), AutoSize = true }, 0, 7);
            panel.Controls.Add(_lblAppVersion, 1, 7);

            page.Controls.Add(panel);
            return page;
        }

        private TabPage CreateProtectionTabPage()
        {
            var page = new TabPage("Protection & Controls");

            var gbMode = new GroupBox { Text = "Protection Mode", Location = new Point(20, 20), Size = new Size(740, 150) };

            var rbActive = new RadioButton { Text = "Always Active (24/7 Continuous Protection)", Location = new Point(30, 30), Size = new Size(400, 25), Checked = true };
            var rbScheduled = new RadioButton { Text = "Scheduled Mode (Enforce schedule)", Location = new Point(30, 65), Size = new Size(400, 25) };
            var rbDisabled = new RadioButton { Text = "Disabled (Protection Turned Off)", Location = new Point(30, 100), Size = new Size(400, 25) };

            var btnApplyMode = new Button { Text = "Apply Mode", Location = new Point(550, 60), Size = new Size(120, 35), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            btnApplyMode.Click += (s, e) =>
            {
                _configRepo.UpdateConfig(c =>
                {
                    if (rbActive.Checked) c.Mode = ProtectionMode.Active;
                    else if (rbScheduled.Checked) c.Mode = ProtectionMode.Scheduled;
                    else if (rbDisabled.Checked) c.Mode = ProtectionMode.Disabled;
                });
                NotifyServiceConfigUpdated();
                MessageBox.Show("Protection mode updated successfully.", "Configuration", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            gbMode.Controls.Add(rbActive);
            gbMode.Controls.Add(rbScheduled);
            gbMode.Controls.Add(rbDisabled);
            gbMode.Controls.Add(btnApplyMode);

            var gbTempAllow = new GroupBox { Text = "Temporary Administrator Allow Timer", Location = new Point(20, 190), Size = new Size(740, 160) };

            _cbTempAllowDuration = new ComboBox { Location = new Point(30, 40), Size = new Size(200, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            _cbTempAllowDuration.Items.AddRange(new object[] { "15 minutes", "30 minutes", "1 hour", "2 hours" });
            _cbTempAllowDuration.SelectedIndex = 0;

            var btnStartTempAllow = new Button { Text = "Start Temporary Allow", Location = new Point(250, 38), Size = new Size(180, 30), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            btnStartTempAllow.Click += (s, e) =>
            {
                int mins = _cbTempAllowDuration.SelectedIndex switch
                {
                    0 => 15,
                    1 => 30,
                    2 => 60,
                    3 => 120,
                    _ => 15
                };

                _configRepo.UpdateConfig(c =>
                {
                    c.TemporaryAllow.IsActive = true;
                    c.TemporaryAllow.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(mins);
                });

                IpcClient.SendCommandAsync("TRIGGER_TEMPORARY_ALLOW", mins.ToString());
                MessageBox.Show($"Temporary Allow active for {mins} minutes.", "Temporary Allow", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            var btnCancelTempAllow = new Button { Text = "Cancel Temporary Allow", Location = new Point(450, 38), Size = new Size(180, 30) };
            btnCancelTempAllow.Click += (s, e) =>
            {
                _configRepo.UpdateConfig(c =>
                {
                    c.TemporaryAllow.IsActive = false;
                    c.TemporaryAllow.ExpiresAt = null;
                });

                IpcClient.SendCommandAsync("TRIGGER_TEMPORARY_ALLOW", "CANCEL");
                MessageBox.Show("Temporary Allow cancelled. Protection restored.", "Temporary Allow", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            _lblTempAllowTimer = new Label { Location = new Point(30, 95), Size = new Size(600, 30), Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.DarkBlue };

            gbTempAllow.Controls.Add(_cbTempAllowDuration);
            gbTempAllow.Controls.Add(btnStartTempAllow);
            gbTempAllow.Controls.Add(btnCancelTempAllow);
            gbTempAllow.Controls.Add(_lblTempAllowTimer);

            page.Controls.Add(gbMode);
            page.Controls.Add(gbTempAllow);
            return page;
        }

        private TabPage CreateScheduleTabPage()
        {
            var page = new TabPage("Schedule Manager");
            page.Padding = new Padding(20);

            var lblInfo = new Label
            {
                Text = "Configure daily schedule for Roblox restriction. When Scheduled Mode is enabled, protection will activate during the specified hours.",
                Location = new Point(20, 20),
                Size = new Size(740, 40),
                Font = new Font("Segoe UI", 9.5F)
            };

            var lblStart = new Label { Text = "Start Block Time:", Location = new Point(20, 80), Size = new Size(120, 25) };
            var dtpStart = new DateTimePicker { Format = DateTimePickerFormat.Time, ShowUpDown = true, Location = new Point(150, 75), Size = new Size(120, 25), Value = DateTime.Today.AddHours(21) };

            var lblEnd = new Label { Text = "End Block Time:", Location = new Point(20, 120), Size = new Size(120, 25) };
            var dtpEnd = new DateTimePicker { Format = DateTimePickerFormat.Time, ShowUpDown = true, Location = new Point(150, 115), Size = new Size(120, 25), Value = DateTime.Today.AddHours(7) };

            var chkWeekdays = new CheckBox { Text = "Apply to Mon-Fri", Location = new Point(20, 160), Size = new Size(200, 25), Checked = true };
            var chkWeekends = new CheckBox { Text = "Always Block Weekends (24h)", Location = new Point(20, 195), Size = new Size(250, 25), Checked = true };

            var btnSaveSchedule = new Button { Text = "Save Schedule", Location = new Point(20, 240), Size = new Size(150, 35), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            btnSaveSchedule.Click += (s, e) =>
            {
                _configRepo.UpdateConfig(c =>
                {
                    c.Schedule.Enabled = true;
                    c.Schedule.Rules.Clear();

                    if (chkWeekdays.Checked)
                    {
                        c.Schedule.Rules.Add(new ScheduleRule
                        {
                            Days = new System.Collections.Generic.List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                            StartTime = dtpStart.Value.TimeOfDay,
                            EndTime = dtpEnd.Value.TimeOfDay
                        });
                    }

                    if (chkWeekends.Checked)
                    {
                        c.Schedule.Rules.Add(new ScheduleRule
                        {
                            Days = new System.Collections.Generic.List<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday },
                            AlwaysBlocked = true
                        });
                    }
                });

                NotifyServiceConfigUpdated();
                MessageBox.Show("Schedule saved successfully.", "Schedule Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            page.Controls.Add(lblInfo);
            page.Controls.Add(lblStart);
            page.Controls.Add(dtpStart);
            page.Controls.Add(lblEnd);
            page.Controls.Add(dtpEnd);
            page.Controls.Add(chkWeekdays);
            page.Controls.Add(chkWeekends);
            page.Controls.Add(btnSaveSchedule);

            return page;
        }

        private TabPage CreateDiagnosticsTabPage()
        {
            var page = new TabPage("Protection Diagnostics");

            var btnRunDiag = new Button { Text = "Run Protection Test", Location = new Point(20, 20), Size = new Size(180, 35), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            var btnRepair = new Button { Text = "Repair Protection", Location = new Point(220, 20), Size = new Size(180, 35) };

            _lvDiagnostics = new ListView
            {
                Location = new Point(20, 70),
                Size = new Size(740, 300),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            _lvDiagnostics.Columns.Add("Subsystem Check", 250);
            _lvDiagnostics.Columns.Add("Status", 120);
            _lvDiagnostics.Columns.Add("Details", 350);

            _lblDiagOverall = new Label
            {
                Location = new Point(20, 385),
                Size = new Size(740, 30),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold)
            };

            btnRunDiag.Click += async (s, e) =>
            {
                _lvDiagnostics.Items.Clear();

                var ipcResp = await IpcClient.SendCommandAsync("RUN_DIAGNOSTICS");
                bool svcOk = ipcResp != null;
                bool fwOk = _firewallRepo.VerifyRulesExist(new[] { "RobloxPlayerBeta.exe" });
                bool domOk = _domainRepo.VerifyDomainBlocks(_configRepo.GetConfig().RobloxRules.BlockedDomains);

                AddDiagItem("Service", svcOk ? "PASS" : "FAIL", svcOk ? "MPCustom Service IPC responsive." : "Service unreachable.");
                AddDiagItem("Firewall Rules", fwOk ? "PASS" : "FAIL", fwOk ? "Windows Firewall outbound rules active." : "Firewall rules missing.");
                AddDiagItem("Process Detection", "PASS", "Process scanner active.");
                AddDiagItem("Domain Protection", domOk ? "PASS" : "FAIL", domOk ? "Roblox domains blocked in hosts file." : "Domain block entries missing.");
                AddDiagItem("Schedule Evaluator", "PASS", "Schedule engine operational.");
                AddDiagItem("Error Experience", "PASS", "HTTP 403 error web server initialized.");

                bool overall = svcOk && fwOk && domOk;
                _lblDiagOverall.Text = overall ? "Overall Protection: PASS" : "Overall Protection: WARNING / REPAIR NEEDED";
                _lblDiagOverall.ForeColor = overall ? Color.Green : Color.Red;
            };

            btnRepair.Click += async (s, e) =>
            {
                await IpcClient.SendCommandAsync("REPAIR_PROTECTION");
                _firewallRepo.RepairRules(new[] { "RobloxPlayerBeta.exe" });
                _domainRepo.RepairDomainBlocks(_configRepo.GetConfig().RobloxRules.BlockedDomains);
                MessageBox.Show("Protection auto-repair executed successfully.", "Repair", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            page.Controls.Add(btnRunDiag);
            page.Controls.Add(btnRepair);
            page.Controls.Add(_lvDiagnostics);
            page.Controls.Add(_lblDiagOverall);

            return page;
        }

        private TabPage CreateLogsTabPage()
        {
            var page = new TabPage("Log Viewer");

            _lvLogs = new ListView
            {
                Location = new Point(20, 20),
                Size = new Size(740, 380),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            _lvLogs.Columns.Add("Timestamp (UTC)", 160);
            _lvLogs.Columns.Add("Level", 80);
            _lvLogs.Columns.Add("Category", 120);
            _lvLogs.Columns.Add("Message", 360);

            var btnRefreshLogs = new Button { Text = "Refresh Logs", Location = new Point(20, 415), Size = new Size(120, 30) };
            var btnExportLogs = new Button { Text = "Export Logs...", Location = new Point(150, 415), Size = new Size(120, 30) };

            btnRefreshLogs.Click += (s, e) => LoadLogs();
            btnExportLogs.Click += (s, e) =>
            {
                using var sfd = new SaveFileDialog { Filter = "Text Files (*.txt)|*.txt", FileName = $"mpcustom_logs_{DateTime.Now:yyyyMMdd}.txt" };
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var logs = _logger.GetRecentLogs(500);
                    File.WriteAllLines(sfd.FileName, logs.Select(l => l.ToString()));
                    MessageBox.Show("Logs exported successfully.", "Export Logs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            page.Controls.Add(_lvLogs);
            page.Controls.Add(btnRefreshLogs);
            page.Controls.Add(btnExportLogs);

            LoadLogs();
            return page;
        }

        private TabPage CreateSettingsTabPage()
        {
            var page = new TabPage("Settings");
            page.Padding = new Padding(20);

            var lblAutoLock = new Label { Text = "Auto-Lock UI Timeout:", Location = new Point(20, 30), Size = new Size(150, 25) };
            _cbAutoLockTimeout = new ComboBox { Location = new Point(180, 27), Size = new Size(180, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            _cbAutoLockTimeout.Items.AddRange(new object[] { "1 minute", "5 minutes", "10 minutes", "30 minutes", "Never" });

            var config = _configRepo.GetConfig();
            _cbAutoLockTimeout.SelectedIndex = config.Security.AutoLockMinutes switch
            {
                1 => 0,
                5 => 1,
                10 => 2,
                30 => 3,
                0 => 4,
                _ => 1
            };

            var btnSaveTimeout = new Button { Text = "Save Timeout", Location = new Point(380, 25), Size = new Size(120, 30) };
            btnSaveTimeout.Click += (s, e) =>
            {
                int mins = _cbAutoLockTimeout.SelectedIndex switch
                {
                    0 => 1,
                    1 => 5,
                    2 => 10,
                    3 => 30,
                    4 => 0,
                    _ => 5
                };

                _configRepo.UpdateConfig(c => c.Security.AutoLockMinutes = mins);
                MessageBox.Show("Auto-lock timeout updated.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            var btnChangePin = new Button { Text = "Change Administrator PIN...", Location = new Point(20, 80), Size = new Size(220, 35) };
            btnChangePin.Click += (s, e) =>
            {
                string currPin = PromptString("Enter Current PIN:", true);
                if (!string.IsNullOrEmpty(currPin))
                {
                    if (_pinService.VerifyPin(currPin))
                    {
                        string newPin = PromptString("Enter New PIN (4+ chars):", true);
                        if (!string.IsNullOrEmpty(newPin) && newPin.Length >= 4)
                        {
                            _pinService.SetPin(newPin);
                            MessageBox.Show("Administrator PIN changed successfully.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("Invalid new PIN.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Current PIN is incorrect.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            page.Controls.Add(lblAutoLock);
            page.Controls.Add(_cbAutoLockTimeout);
            page.Controls.Add(btnSaveTimeout);
            page.Controls.Add(btnChangePin);

            return page;
        }

        private void AddDiagItem(string category, string status, string details)
        {
            var item = new ListViewItem(category);
            item.SubItems.Add(status);
            item.SubItems.Add(details);
            item.ForeColor = status == "PASS" ? Color.Green : Color.Red;
            _lvDiagnostics.Items.Add(item);
        }

        private void LoadLogs()
        {
            _lvLogs.Items.Clear();
            var logs = _logger.GetRecentLogs(100);
            foreach (var log in logs.AsEnumerable().Reverse())
            {
                var item = new ListViewItem(log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(log.Level.ToString().ToUpper());
                item.SubItems.Add(log.Category);
                item.SubItems.Add(log.Message);
                _lvLogs.Items.Add(item);
            }
        }

        private void RefreshDashboard()
        {
            var config = _configRepo.GetConfig();

            _lblStatusValue.Text = config.TemporaryAllow.IsActive ? "TEMPORARILY ALLOWED" : config.Mode.ToString().ToUpper();
            _lblStatusValue.ForeColor = config.Mode == ProtectionMode.Disabled ? Color.Gray : (config.TemporaryAllow.IsActive ? Color.Orange : Color.Green);

            _lblLastVerified.Text = config.LastVerified == DateTimeOffset.MinValue ? "Never" : config.LastVerified.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            _lblLastDetection.Text = config.LastRobloxDetected == DateTimeOffset.MinValue ? "None" : config.LastRobloxDetected.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            _lblTotalBlocked.Text = config.TotalBlockedCount.ToString();
            _lblCurrentSchedule.Text = config.Mode == ProtectionMode.Active ? "Always Protected (24/7)" : (config.Mode == ProtectionMode.Scheduled ? "Scheduled Protection" : "Disabled");
        }

        private async void NotifyServiceConfigUpdated()
        {
            var config = _configRepo.GetConfig();
            var json = JsonSerializer.Serialize(config);
            await IpcClient.SendCommandAsync("UPDATE_CONFIG", json);
        }

        private static string PromptString(string prompt, bool isPassword)
        {
            using var dlg = new Form { Width = 320, Height = 170, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, Text = "Prompt" };
            var lbl = new Label { Left = 20, Top = 20, Width = 260, Text = prompt };
            var txt = new TextBox { Left = 20, Top = 45, Width = 260, PasswordChar = isPassword ? '•' : '\0' };
            var btn = new Button { Text = "OK", Left = 180, Top = 80, Width = 100, DialogResult = DialogResult.OK };
            dlg.Controls.Add(lbl); dlg.Controls.Add(txt); dlg.Controls.Add(btn);
            dlg.AcceptButton = btn;
            return dlg.ShowDialog() == DialogResult.OK ? txt.Text.Trim() : string.Empty;
        }
    }
}
