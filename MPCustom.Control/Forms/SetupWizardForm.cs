using System;
using System.Drawing;
using System.Windows.Forms;
using MPCustom.Core.Config;
using MPCustom.Core.Models;
using MPCustom.Security;

namespace MPCustom.Control.Forms
{
    public class SetupWizardForm : Form
    {
        private readonly IPinSecurityService _pinService;
        private readonly IConfigRepository _configRepo;

        private Label _lblStepTitle;
        private Label _lblDescription;
        private Label _lblPinPrompt;
        private Label _lblConfirmPrompt;
        private TextBox _txtPin;
        private TextBox _txtConfirmPin;
        private CheckBox _chkShowPassword;
        private Label _lblPinError;
        private RadioButton _rbAlways;
        private RadioButton _rbScheduled;
        private Button _btnBack;
        private Button _btnNext;
        private int _currentStep = 1;

        public SetupWizardForm(IPinSecurityService pinService, IConfigRepository configRepo)
        {
            _pinService = pinService ?? throw new ArgumentNullException(nameof(pinService));
            _configRepo = configRepo ?? throw new ArgumentNullException(nameof(configRepo));

            InitializeComponent();
            ShowStep(_currentStep);
        }

        private void InitializeComponent()
        {
            this.Text = "MPCustom Control - First-Run Setup Wizard";
            this.Size = new Size(580, 440);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            _lblStepTitle = new Label
            {
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                Location = new Point(30, 20),
                Size = new Size(500, 30)
            };

            _lblDescription = new Label
            {
                Font = new Font("Segoe UI", 10F),
                Location = new Point(30, 60),
                Size = new Size(500, 70)
            };

            _lblPinPrompt = new Label
            {
                Text = "Enter Administrator PIN / Password:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(30, 140),
                Size = new Size(250, 25),
                Visible = false
            };

            _txtPin = new TextBox
            {
                Location = new Point(30, 165),
                Size = new Size(300, 25),
                PasswordChar = '•',
                Visible = false
            };

            _lblConfirmPrompt = new Label
            {
                Text = "Confirm Administrator PIN / Password:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(30, 200),
                Size = new Size(250, 25),
                Visible = false
            };

            _txtConfirmPin = new TextBox
            {
                Location = new Point(30, 225),
                Size = new Size(300, 25),
                PasswordChar = '•',
                Visible = false
            };

            _chkShowPassword = new CheckBox
            {
                Text = "Show Password",
                Location = new Point(340, 165),
                Size = new Size(150, 25),
                Visible = false
            };
            _chkShowPassword.CheckedChanged += (s, e) =>
            {
                char mask = _chkShowPassword.Checked ? '\0' : '•';
                _txtPin.PasswordChar = mask;
                _txtConfirmPin.PasswordChar = mask;
            };

            _lblPinError = new Label
            {
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(30, 260),
                Size = new Size(480, 40),
                Visible = false
            };

            _rbAlways = new RadioButton
            {
                Text = "Always Protected (24/7 continuous protection)",
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(50, 140),
                Size = new Size(450, 25),
                Checked = true,
                Visible = false
            };

            _rbScheduled = new RadioButton
            {
                Text = "Scheduled Protection (Enforce protection based on schedule)",
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(50, 180),
                Size = new Size(450, 25),
                Visible = false
            };

            _btnBack = new Button
            {
                Text = "< Back",
                Location = new Point(330, 350),
                Size = new Size(90, 32),
                Enabled = false
            };
            _btnBack.Click += BtnBack_Click;

            _btnNext = new Button
            {
                Text = "Next >",
                Location = new Point(430, 350),
                Size = new Size(90, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _btnNext.Click += BtnNext_Click;

            this.Controls.Add(_lblStepTitle);
            this.Controls.Add(_lblDescription);
            this.Controls.Add(_lblPinPrompt);
            this.Controls.Add(_txtPin);
            this.Controls.Add(_lblConfirmPrompt);
            this.Controls.Add(_txtConfirmPin);
            this.Controls.Add(_chkShowPassword);
            this.Controls.Add(_lblPinError);
            this.Controls.Add(_rbAlways);
            this.Controls.Add(_rbScheduled);
            this.Controls.Add(_btnBack);
            this.Controls.Add(_btnNext);
        }

        private void ShowStep(int step)
        {
            _currentStep = step;
            _btnBack.Enabled = _currentStep > 1;

            _lblPinPrompt.Visible = false;
            _txtPin.Visible = false;
            _lblConfirmPrompt.Visible = false;
            _txtConfirmPin.Visible = false;
            _chkShowPassword.Visible = false;
            _lblPinError.Visible = false;
            _rbAlways.Visible = false;
            _rbScheduled.Visible = false;

            if (_currentStep == 1)
            {
                _lblStepTitle.Text = "Welcome to MPCustom Control";
                _lblDescription.Text = "This first-run setup wizard will configure system protection and establish your secure Administrator PIN / Password.\n\nClick Next to begin setup.";
                _btnNext.Text = "Next >";
            }
            else if (_currentStep == 2)
            {
                _lblStepTitle.Text = "Create Administrator Password / PIN";
                _lblDescription.Text = "Set a secure PIN or password (minimum 4 characters). You will need this credential to open MPCustom Control and change protection settings.";
                _lblPinPrompt.Visible = true;
                _txtPin.Visible = true;
                _lblConfirmPrompt.Visible = true;
                _txtConfirmPin.Visible = true;
                _chkShowPassword.Visible = true;
                _btnNext.Text = "Next >";
            }
            else if (_currentStep == 3)
            {
                _lblStepTitle.Text = "Select Protection Mode";
                _lblDescription.Text = "Choose how protection should be enforced on this system:";
                _rbAlways.Visible = true;
                _rbScheduled.Visible = true;
                _btnNext.Text = "Finish";
            }
        }

        private void BtnNext_Click(object? sender, EventArgs e)
        {
            if (_currentStep == 1)
            {
                ShowStep(2);
            }
            else if (_currentStep == 2)
            {
                string pin1 = _txtPin.Text.Trim();
                string pin2 = _txtConfirmPin.Text.Trim();

                if (string.IsNullOrEmpty(pin1) || pin1.Length < 4)
                {
                    _lblPinError.Text = "PIN / Password must be at least 4 characters long.";
                    _lblPinError.Visible = true;
                    return;
                }

                if (pin1 != pin2)
                {
                    _lblPinError.Text = "Credentials do not match. Please re-enter.";
                    _lblPinError.Visible = true;
                    return;
                }

                _pinService.SetPin(pin1);
                ShowStep(3);
            }
            else if (_currentStep == 3)
            {
                _configRepo.UpdateConfig(c =>
                {
                    c.Mode = _rbAlways.Checked ? ProtectionMode.Active : ProtectionMode.Scheduled;
                });

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void BtnBack_Click(object? sender, EventArgs e)
        {
            if (_currentStep > 1)
            {
                ShowStep(_currentStep - 1);
            }
        }
    }
}
