using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WootMouseRemap.Features;
using WootMouseRemap.Core;

namespace WootMouseRemap.UI
{
    /// <summary>
    /// Validation system for anti-recoil configuration with warnings and error handling
    /// </summary>
    public class ValidationSystem
    {
        private readonly List<ValidationRule> _rules = new();
        private readonly Dictionary<Control, ValidationState> _controlStates = new();
        private readonly ErrorProvider _errorProvider;
        private readonly Panel _warningPanel;

        public event EventHandler<ValidationEventArgs>? ValidationChanged;

        public bool HasErrors => _controlStates.Values.Any(s => s.HasError);
        public bool HasWarnings => _controlStates.Values.Any(s => s.HasWarning);
        public int ErrorCount => _controlStates.Values.Count(s => s.HasError);
        public int WarningCount => _controlStates.Values.Count(s => s.HasWarning);

        public ValidationSystem(Form parentForm)
        {
            _errorProvider = new ErrorProvider
            {
                BlinkStyle = ErrorBlinkStyle.NeverBlink,
                Icon = SystemIcons.Error
            };

            // Create warning panel
            _warningPanel = new Panel
            {
                BackColor = Color.FromArgb(60, 40, 0),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false,
                Height = 30,
                Dock = DockStyle.Top
            };

            var warningLabel = new Label
            {
                ForeColor = Color.Orange,
                Font = new Font(parentForm.Font.FontFamily, 9f, FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            warningLabel.Tag = "WarningLabel";

            _warningPanel.Controls.Add(warningLabel);
            parentForm.Controls.Add(_warningPanel);
            _warningPanel.BringToFront();
        }

        public void AddRule(ValidationRule rule)
        {
            _rules.Add(rule);
            if (!_controlStates.ContainsKey(rule.Control))
            {
                _controlStates[rule.Control] = new ValidationState();
            }
        }

        public void ValidateAll()
        {
            foreach (var rule in _rules)
            {
                ValidateRule(rule);
            }
            UpdateWarningPanel();
            ValidationChanged?.Invoke(this, new ValidationEventArgs(HasErrors, HasWarnings, ErrorCount, WarningCount));
        }

        public void ValidateControl(Control control)
        {
            var rules = _rules.Where(r => r.Control == control);
            foreach (var rule in rules)
            {
                ValidateRule(rule);
            }
            UpdateWarningPanel();
            ValidationChanged?.Invoke(this, new ValidationEventArgs(HasErrors, HasWarnings, ErrorCount, WarningCount));
        }

        private void ValidateRule(ValidationRule rule)
        {
            var state = _controlStates[rule.Control];
            var result = rule.Validate();

            switch (result.Type)
            {
                case ValidationType.Error:
                    state.HasError = true;
                    state.ErrorMessage = result.Message;
                    _errorProvider.SetError(rule.Control, result.Message);
                    rule.Control.BackColor = Color.FromArgb(80, 40, 40);
                    break;

                case ValidationType.Warning:
                    state.HasWarning = true;
                    state.WarningMessage = result.Message;
                    if (!state.HasError) // Don't override error styling
                    {
                        rule.Control.BackColor = Color.FromArgb(60, 50, 20);
                    }
                    break;

                case ValidationType.Success:
                    state.HasError = false;
                    state.HasWarning = false;
                    state.ErrorMessage = null;
                    state.WarningMessage = null;
                    _errorProvider.SetError(rule.Control, "");
                    rule.Control.BackColor = rule.Control is NumericUpDown ? Color.FromArgb(60, 60, 60) : Color.FromArgb(45, 45, 48);
                    break;
            }
        }

        private void UpdateWarningPanel()
        {
            var warnings = _controlStates.Values
                .Where(s => s.HasWarning && !string.IsNullOrEmpty(s.WarningMessage))
                .Select(s => s.WarningMessage)
                .Distinct()
                .ToList();

            var warningLabel = _warningPanel.Controls.OfType<Label>().FirstOrDefault(l => l.Tag?.ToString() == "WarningLabel");
            if (warningLabel != null)
            {
                if (warnings.Count > 0)
                {
                    warningLabel.Text = $"⚠ {string.Join(" | ", warnings)}";
                    _warningPanel.Visible = true;
                }
                else
                {
                    _warningPanel.Visible = false;
                }
            }
        }

        public void Dispose()
        {
            _errorProvider?.Dispose();
            _warningPanel?.Dispose();
        }
    }

    public class ValidationRule
    {
        public Control Control { get; }
        public Func<ValidationResult> Validate { get; }
        public string Name { get; }

        public ValidationRule(Control control, Func<ValidationResult> validate, string name = "")
        {
            Control = control;
            Validate = validate;
            Name = name;
        }
    }

    public class ValidationResult
    {
        public ValidationType Type { get; set; }
        public string Message { get; set; } = "";

        public static ValidationResult Success() => new() { Type = ValidationType.Success };
        public static ValidationResult Error(string message) => new() { Type = ValidationType.Error, Message = message };
        public static ValidationResult Warning(string message) => new() { Type = ValidationType.Warning, Message = message };
    }

    public enum ValidationType
    {
        Success,
        Warning,
        Error
    }

    public class ValidationState
    {
        public bool HasError { get; set; }
        public bool HasWarning { get; set; }
        public string? ErrorMessage { get; set; }
        public string? WarningMessage { get; set; }
    }

    public class ValidationEventArgs : EventArgs
    {
        public bool HasErrors { get; }
        public bool HasWarnings { get; }
        public int ErrorCount { get; }
        public int WarningCount { get; }

        public ValidationEventArgs(bool hasErrors, bool hasWarnings, int errorCount, int warningCount)
        {
            HasErrors = hasErrors;
            HasWarnings = hasWarnings;
            ErrorCount = errorCount;
            WarningCount = warningCount;
        }
    }

    /// <summary>
    /// Anti-recoil specific validation rules
    /// </summary>
    public static class AntiRecoilValidationRules
    {
        public static ValidationRule CreateStrengthValidation(AntiRecoilViewModel viewModel, NumericUpDown control)
        {
            return new ValidationRule(control, () =>
            {
                var value = viewModel.Strength;
                if (viewModel.Enabled && value == 0)
                    return ValidationResult.Warning("Anti-recoil is enabled but strength is 0% - no compensation will be applied");
                if (value > 0.9f)
                    return ValidationResult.Warning("Very high strength (>90%) may cause overcorrection");
                return ValidationResult.Success();
            }, "Strength");
        }

        public static ValidationRule CreateThresholdValidation(AntiRecoilViewModel viewModel, NumericUpDown control)
        {
            return new ValidationRule(control, () =>
            {
                var value = viewModel.VerticalThreshold;
                if (value > 20f)
                    return ValidationResult.Warning("High threshold (>20) may prevent activation during normal recoil");
                if (value < 0.5f)
                    return ValidationResult.Warning("Very low threshold (<0.5) may cause activation from minor movements");
                return ValidationResult.Success();
            }, "Threshold");
        }

        public static ValidationRule CreateActivationDelayValidation(AntiRecoilViewModel viewModel, NumericUpDown control)
        {
            return new ValidationRule(control, () =>
            {
                var value = viewModel.ActivationDelayMs;
                if (value > 500)
                    return ValidationResult.Warning("High delay (>500ms) may miss initial recoil compensation");
                return ValidationResult.Success();
            }, "ActivationDelay");
        }

        public static ValidationRule CreateMaxTickCompensationValidation(AntiRecoilViewModel viewModel, NumericUpDown control)
        {
            return new ValidationRule(control, () =>
            {
                var maxTick = viewModel.MaxTickCompensation;
                var threshold = viewModel.VerticalThreshold;
                if (maxTick > 0 && maxTick < threshold)
                    return ValidationResult.Warning("Max tick compensation is lower than threshold - may limit effectiveness");
                if (maxTick > 50f)
                    return ValidationResult.Warning("Very high max tick compensation (>50) may cause jerky movement");
                return ValidationResult.Success();
            }, "MaxTickCompensation");
        }

        public static ValidationRule CreateMaxTotalCompensationValidation(AntiRecoilViewModel viewModel, NumericUpDown control)
        {
            return new ValidationRule(control, () =>
            {
                var maxTotal = viewModel.MaxTotalCompensation;
                var maxTick = viewModel.MaxTickCompensation;
                if (maxTotal > 0 && maxTick > 0 && maxTotal < maxTick * 3)
                    return ValidationResult.Warning("Max total compensation is very low - may cap compensation during long bursts");
                return ValidationResult.Success();
            }, "MaxTotalCompensation");
        }

        public static ValidationRule CreateCooldownValidation(AntiRecoilViewModel viewModel, NumericUpDown control)
        {
            return new ValidationRule(control, () =>
            {
                var cooldown = viewModel.CooldownMs;
                if (cooldown > 1000)
                    return ValidationResult.Warning("High cooldown (>1000ms) may prevent rapid reactivation");
                return ValidationResult.Success();
            }, "Cooldown");
        }

        public static ValidationRule CreateDecayValidation(AntiRecoilViewModel viewModel, NumericUpDown control)
        {
            return new ValidationRule(control, () =>
            {
                var decay = viewModel.DecayPerMs;
                var maxTotal = viewModel.MaxTotalCompensation;
                if (decay > 0 && maxTotal > 0 && decay * 1000 > maxTotal)
                    return ValidationResult.Warning("High decay rate may reset accumulated compensation too quickly");
                return ValidationResult.Success();
            }, "Decay");
        }

        public static ValidationRule CreateConsistencyValidation(AntiRecoilViewModel viewModel, Control control)
        {
            return new ValidationRule(control, () =>
            {
                if (viewModel.Enabled && viewModel.Strength > 0)
                {
                    if (viewModel.MaxTickCompensation > 0 && viewModel.MaxTotalCompensation > 0 &&
                        viewModel.MaxTotalCompensation < viewModel.MaxTickCompensation)
                        return ValidationResult.Error("Max total compensation cannot be less than max tick compensation");

                    if (viewModel.CooldownMs > 0 && viewModel.ActivationDelayMs > 0 &&
                        viewModel.CooldownMs < viewModel.ActivationDelayMs)
                        return ValidationResult.Warning("Cooldown is shorter than activation delay - may cause rapid cycling");
                }
                return ValidationResult.Success();
            }, "Consistency");
        }
    }
}