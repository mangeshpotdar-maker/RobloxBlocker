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
        private TextBox _txtPin;
        private TextBox _txtConfirmPin;
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
            this.Text = "MPCustom Control Setup Wizard";
            this.Size = new Size(580, 420);
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
                Size = new Size(500, 80)
            };

            _txtPin = new TextBox
            {
                Location = new Point(150, 150),
                Size = new Size(200, 25),
                PasswordChar = '•',
                Visible = false
            };

            _txtConfirmPin = new TextBox
            {
                Location = new Point(150, 190),
                Size = new Size(200, 25),
                PasswordChar = '•',
                Visible = false
            };

            _lblPinError = new Label
            {
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(150, 225),
                Size = new Size(350, 25),
                Visible = false
            };

            _rbAlways = new RadioButton
            {
                Text = "Always Protected (24/7 continuous protection)",
                Location = new Point(50, 150),
                Size = new Size(400, 25),
                Checked = true,
                Visible = false
            };

            _rbScheduled = new RadioButton
            {
                Text = "Scheduled Protection (Enforce protection based on custom schedule)",
                Location = new Point(50, 185),
                Size = new Size(400, 25),
                Visible = false
            };

            _btnBack = new Button
            {
                Text = "< Back",
                Location = new Point(330, 330),
                Size = new Size(90, 30),
                Enabled = false
            };
            _btnBack.Click += BtnBack_Click;

            _btnNext = new Button
            {
                Text = "Next >",
                Location = new Point(430, 330),
                Size = new Size(90, 30)
            };
            _btnNext.Click += BtnNext_Click;

            this.Controls.Add(_lblStepTitle);
            this.Controls.Add(_lblDescription);
            this.Controls.Add(_txtPin);
            this.Controls.Add(_txtConfirmPin);
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

            _txtPin.Visible = false;
            _txtConfirmPin.Visible = false;
            _lblPinError.Visible = false;
            _rbAlways.Visible = false;
            _rbScheduled.Visible = false;

            if (_currentStep == 1)
            {
                _lblStepTitle.Text = "Welcome to MPCustom Control";
                _lblDescription.Text = "This setup wizard will configure system protection and set up your secure Administrator PIN.\n\nPlease click Next to begin.";
                _btnNext.Text = "Next >";
            }
            else if (_currentStep == 2)
            {
                _lblStepTitle.Text = "Create Administrator PIN";
                _lblDescription.Text = "Enter a secure Administrator PIN (minimum 4 characters). You will need this PIN to open MPCustom Control and change protection settings.\n\nNew PIN:\n\nConfirm PIN:";
                _txtPin.Visible = true;
                _txtConfirmPin.Visible = true;
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
                    _lblPinError.Text = "PIN must be at least 4 characters long.";
                    _lblPinError.Visible = true;
                    return;
                }

                if (pin1 != pin2)
                {
                    _lblPinError.Text = "PINs do not match. Please re-enter.";
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
