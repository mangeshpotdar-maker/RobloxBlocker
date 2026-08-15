using System;
using System.Drawing;
using System.Windows.Forms;
using MPCustom.Security;

namespace MPCustom.Control.Forms
{
    public class LoginForm : Form
    {
        private readonly IPinSecurityService _pinService;
        private Label _lblTitle;
        private Label _lblSubTitle;
        private TextBox _txtPin;
        private Button _btnUnlock;
        private Button _btnAdminRecovery;
        private Label _lblMessage;
        private System.Windows.Forms.Timer _lockoutTimer;

        public LoginForm(IPinSecurityService pinService)
        {
            _pinService = pinService ?? throw new ArgumentNullException(nameof(pinService));

            InitializeComponent();
            CheckLockoutStatus();

            _lockoutTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _lockoutTimer.Tick += LockoutTimer_Tick;
            _lockoutTimer.Start();
        }

        private void InitializeComponent()
        {
            this.Text = "MPCustom Control";
            this.Size = new Size(420, 320);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            _lblTitle = new Label
            {
                Text = "MPCustom Control",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                Location = new Point(30, 25),
                Size = new Size(350, 30),
                TextAlign = ContentAlignment.TopCenter
            };

            _lblSubTitle = new Label
            {
                Text = "Administrator Authentication",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                Location = new Point(30, 60),
                Size = new Size(350, 25),
                TextAlign = ContentAlignment.TopCenter
            };

            var lblPinPrompt = new Label
            {
                Text = "Enter PIN:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(50, 105),
                Size = new Size(80, 25)
            };

            _txtPin = new TextBox
            {
                Location = new Point(135, 102),
                Size = new Size(180, 25),
                PasswordChar = '•'
            };
            _txtPin.KeyDown += TxtPin_KeyDown;

            _btnUnlock = new Button
            {
                Text = "Unlock",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(135, 140),
                Size = new Size(180, 32)
            };
            _btnUnlock.Click += BtnUnlock_Click;

            _btnAdminRecovery = new Button
            {
                Text = "Windows Admin Recovery",
                Font = new Font("Segoe UI", 8F),
                Location = new Point(135, 180),
                Size = new Size(180, 25)
            };
            _btnAdminRecovery.Click += BtnAdminRecovery_Click;

            _lblMessage = new Label
            {
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.Red,
                Location = new Point(20, 215),
                Size = new Size(370, 45),
                TextAlign = ContentAlignment.TopCenter
            };

            this.Controls.Add(_lblTitle);
            this.Controls.Add(_lblSubTitle);
            this.Controls.Add(lblPinPrompt);
            this.Controls.Add(_txtPin);
            this.Controls.Add(_btnUnlock);
            this.Controls.Add(_btnAdminRecovery);
            this.Controls.Add(_lblMessage);
        }

        private void TxtPin_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                BtnUnlock_Click(sender, e);
            }
        }

        private void CheckLockoutStatus()
        {
            if (_pinService.IsLockedOut(out TimeSpan remaining))
            {
                _txtPin.Enabled = false;
                _btnUnlock.Enabled = false;
                _lblMessage.Text = $"Temporary Lockout Active.\nTry again in {remaining.Minutes:D2}:{remaining.Seconds:D2}";
            }
            else
            {
                _txtPin.Enabled = true;
                _btnUnlock.Enabled = true;
            }
        }

        private void LockoutTimer_Tick(object? sender, EventArgs e)
        {
            CheckLockoutStatus();
        }

        private void BtnUnlock_Click(object? sender, EventArgs e)
        {
            string pin = _txtPin.Text;
            if (string.IsNullOrEmpty(pin))
            {
                _lblMessage.Text = "Please enter administrator PIN.";
                return;
            }

            if (_pinService.VerifyPin(pin))
            {
                _lockoutTimer.Stop();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                _txtPin.Clear();
                if (!_pinService.IsLockedOut(out _))
                {
                    _lblMessage.Text = $"Incorrect PIN. Failed attempts: {_pinService.FailedAttempts}";
                }
                else
                {
                    CheckLockoutStatus();
                }
            }
        }

        private void BtnAdminRecovery_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Windows Administrator Recovery allows resetting the PIN if running as a Windows Administrator.\n\nDo you want to reset the PIN now?",
                "Administrator Recovery",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                string newPin = PromptForNewPin();
                if (!string.IsNullOrEmpty(newPin))
                {
                    if (_pinService.ResetPinAsAdministrator(newPin))
                    {
                        MessageBox.Show("PIN successfully reset! You can now log in.", "Recovery Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        _lblMessage.Text = string.Empty;
                        CheckLockoutStatus();
                    }
                    else
                    {
                        MessageBox.Show("PIN recovery failed. Ensure you are running this application with elevated Windows Administrator privileges.", "Recovery Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private static string PromptForNewPin()
        {
            using var prompt = new Form
            {
                Width = 320,
                Height = 180,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Set New PIN",
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var textLabel = new Label { Left = 20, Top = 20, Width = 260, Text = "Enter new PIN (4+ chars):" };
            var textBox = new TextBox { Left = 20, Top = 45, Width = 260, PasswordChar = '•' };
            var confirmation = new Button { Text = "OK", Left = 180, Width = 100, Top = 85, DialogResult = DialogResult.OK };

            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text.Trim() : string.Empty;
        }
    }
}
