using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WootMouseRemap.UI
{
    /// <summary>
    /// Comprehensive help system with interactive guides and tooltips
    /// </summary>
    public static class HelpSystem
    {
        private static readonly Dictionary<string, HelpTopic> _helpTopics = new();

        static HelpSystem()
        {
            InitializeHelpTopics();
        }

        public static void ShowContextualHelp(Control control, string topicKey)
        {
            if (_helpTopics.TryGetValue(topicKey, out var topic))
            {
                var helpForm = new HelpForm(topic);
                helpForm.ShowDialog(control.FindForm());
            }
        }

        public static void ShowQuickStartGuide(Form parentForm)
        {
            var guide = new QuickStartGuide();
            guide.ShowDialog(parentForm);
        }

        public static ToolTip CreateAdvancedTooltip()
        {
            return new ToolTip
            {
                AutoPopDelay = 10000,
                InitialDelay = 500,
                ReshowDelay = 500,
                ShowAlways = true,
                ToolTipIcon = ToolTipIcon.Info,
                IsBalloon = true
            };
        }

        private static void InitializeHelpTopics()
        {
            _helpTopics["strength"] = new HelpTopic
            {
                Title = "Anti-Recoil Strength",
                Content = @"The strength setting controls how much compensation is applied to counteract recoil.

• 0% = No compensation
• 50% = Half of detected recoil is compensated
• 100% = Full compensation (may cause overcorrection)

Recommended values:
• Light recoil weapons: 20-40%
• Medium recoil weapons: 40-60%
• Heavy recoil weapons: 60-80%

Tips:
- Start with lower values and increase gradually
- Very high values (>90%) may cause jerky movement
- Test in practice mode before using in games",
                Examples = new[] { "AK-47: Try 65%", "M4A4: Try 45%", "SMGs: Try 30%" }
            };

            _helpTopics["threshold"] = new HelpTopic
            {
                Title = "Activation Threshold",
                Content = @"The threshold determines the minimum vertical movement required to trigger anti-recoil compensation.

Purpose:
- Prevents activation from minor mouse movements
- Reduces false positives during precise aiming
- Conserves system resources

Recommended values:
• High sensitivity users: 1-3 pixels
• Medium sensitivity users: 3-6 pixels
• Low sensitivity users: 6-12 pixels

Warning signs:
- Too low: Compensation activates during normal aiming
- Too high: Compensation doesn't activate during actual recoil",
                Examples = new[] { "800 DPI: Try 4-6 pixels", "1600 DPI: Try 2-4 pixels", "3200 DPI: Try 1-2 pixels" }
            };

            _helpTopics["patterns"] = new HelpTopic
            {
                Title = "Recoil Patterns",
                Content = @"Recoil patterns are recordings of weapon-specific recoil behavior that enable precise compensation.

How to create patterns:
1. Select 'Record' and enter a pattern name
2. Fire the weapon in a controlled environment
3. Stop recording when finished
4. The pattern will be automatically applied

Pattern management:
• Rename: Give patterns descriptive names
• Export: Share patterns with others
• Transform: Apply smoothing, normalization, etc.
• Notes: Add weapon details and settings used

Best practices:
- Record patterns in practice mode
- Use consistent firing technique
- Record multiple bursts for better accuracy
- Test patterns before using in competitive play",
                Examples = new[] { "AK47_FullSpray", "M4_15Rounds", "AWP_QuickScope" }
            };

            _helpTopics["simulation"] = new HelpTopic
            {
                Title = "Pattern Simulation",
                Content = @"The simulation feature allows you to test and preview pattern compensation before applying it in real scenarios.

Features:
• Visual preview of input vs compensated movement
• Playback control with timeline scrubbing
• Export simulation results for analysis
• Multiple display modes for different perspectives

Display modes:
- Input Only: Shows raw recoil pattern
- Output Only: Shows compensated result
- Input & Output: Compares both simultaneously
- Compensation Only: Shows applied compensation
- All: Comprehensive view of all data

Use simulation to:
- Test pattern effectiveness
- Adjust compensation settings
- Verify pattern accuracy
- Train muscle memory",
                Examples = new[] { "Test new patterns", "Compare compensation strengths", "Verify pattern quality" }
            };

            _helpTopics["telemetry"] = new HelpTopic
            {
                Title = "Live Telemetry",
                Content = @"Real-time telemetry provides live monitoring of anti-recoil system performance and mouse movements.

Monitored data:
• Vertical mouse movements
• Applied compensation
• Accumulated compensation
• System activation status
• Performance metrics

Display options:
- Auto-scaling for optimal view
- Multiple visualization modes
- Statistics overlay
- Performance monitoring

Telemetry benefits:
- Verify system is working correctly
- Identify performance issues
- Fine-tune compensation settings
- Monitor system behavior during gameplay

The telemetry data can be exported for detailed analysis.",
                Examples = new[] { "Monitor during practice", "Verify compensation accuracy", "Debug configuration issues" }
            };

            _helpTopics["advanced"] = new HelpTopic
            {
                Title = "Advanced Settings",
                Content = @"Advanced settings provide fine-grained control over anti-recoil behavior for power users.

Max Tick Compensation:
- Limits compensation applied per mouse movement
- Prevents extreme corrections from outlier movements
- Recommended: 5-15 pixels

Max Total Compensation:
- Caps accumulated compensation over time
- Prevents drift from compensation buildup
- Recommended: 50-150 pixels

Cooldown Period:
- Pause between compensation cycles
- Reduces system overhead
- Useful for semi-automatic weapons

Decay Rate:
- Gradual reduction of accumulated compensation
- Prevents long-term drift
- Measured in pixels per millisecond

These settings require careful tuning and testing.",
                Examples = new[] { "Burst fire: Enable cooldown", "Full auto: Use decay", "Precision: Limit max tick" }
            };
        }
    }

    public class HelpTopic
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string[] Examples { get; set; } = Array.Empty<string>();
    }

    public class HelpForm : Form
    {
        private readonly HelpTopic _topic;

        public HelpForm(HelpTopic topic)
        {
            _topic = topic;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = $"Help - {_topic.Title}";
            Size = new Size(600, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.White;

            // Title
            var titleLabel = new Label
            {
                Text = _topic.Title,
                Font = new Font(Font.FontFamily, 16f, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(560, 30),
                ForeColor = Color.White
            };
            Controls.Add(titleLabel);

            // Content
            var contentTextBox = new RichTextBox
            {
                Text = _topic.Content,
                Location = new Point(20, 60),
                Size = new Size(560, 350),
                ReadOnly = true,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9f)
            };
            Controls.Add(contentTextBox);

            // Examples
            if (_topic.Examples.Length > 0)
            {
                var examplesLabel = new Label
                {
                    Text = "Examples:",
                    Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
                    Location = new Point(20, 420),
                    Size = new Size(100, 20),
                    ForeColor = Color.LightGray
                };
                Controls.Add(examplesLabel);

                var examplesText = string.Join(" • ", _topic.Examples);
                var examplesLabel2 = new Label
                {
                    Text = $"• {examplesText}",
                    Location = new Point(120, 420),
                    Size = new Size(460, 40),
                    ForeColor = Color.LightGray,
                    Font = new Font(Font.FontFamily, 8.5f)
                };
                Controls.Add(examplesLabel2);
            }

            // Close button
            var closeButton = new Button
            {
                Text = "Close",
                Location = new Point(505, 420),
                Size = new Size(75, 25),
                BackColor = Color.FromArgb(0, 120, 200),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            closeButton.Click += (s, e) => Close();
            Controls.Add(closeButton);
        }
    }

    public class QuickStartGuide : Form
    {
        private int _currentStep = 0;
        private readonly string[] _steps = {
            "Welcome to Anti-Recoil Configuration!\n\nThis guide will help you get started with basic setup and configuration.",

            "Step 1: Basic Settings\n\n• Enable anti-recoil compensation\n• Set strength to 30-50% initially\n• Set threshold to 3-5 pixels\n• Leave advanced settings at defaults",

            "Step 2: Record a Pattern\n\n• Go to the Patterns tab\n• Click 'Record' and enter a weapon name\n• Fire the weapon in practice mode\n• Click 'Stop Recording' when finished",

            "Step 3: Test Your Setup\n\n• Use the Simulation tab to preview compensation\n• Monitor the Telemetry tab during gameplay\n• Adjust strength based on results\n• Fine-tune threshold as needed",

            "Step 4: Advanced Features\n\n• Explore pattern transforms for optimization\n• Use validation warnings to improve settings\n• Enable auto-backup in preferences\n• Export/import patterns for sharing",

            "You're all set!\n\nRemember:\n• Start with conservative settings\n• Test thoroughly before competitive play\n• Use telemetry to verify performance\n• Check help topics for detailed guidance"
        };

        public QuickStartGuide()
        {
            InitializeComponent();
            UpdateStep();
        }

        private void InitializeComponent()
        {
            Text = "Quick Start Guide";
            Size = new Size(500, 400);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.White;

            // Content area
            var contentPanel = new Panel
            {
                Location = new Point(20, 20),
                Size = new Size(460, 300),
                BackColor = Color.FromArgb(60, 60, 60),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(contentPanel);

            var contentLabel = new Label
            {
                Location = new Point(20, 20),
                Size = new Size(420, 260),
                Font = new Font(Font.FontFamily, 10f),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };
            contentLabel.Tag = "content";
            contentPanel.Controls.Add(contentLabel);

            // Navigation buttons
            var prevButton = new Button
            {
                Text = "Previous",
                Location = new Point(20, 340),
                Size = new Size(80, 30),
                BackColor = Color.FromArgb(80, 80, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            prevButton.Tag = "prev";
            prevButton.Click += PrevButton_Click;
            Controls.Add(prevButton);

            var nextButton = new Button
            {
                Text = "Next",
                Location = new Point(320, 340),
                Size = new Size(80, 30),
                BackColor = Color.FromArgb(0, 120, 200),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            nextButton.Tag = "next";
            nextButton.Click += NextButton_Click;
            Controls.Add(nextButton);

            var closeButton = new Button
            {
                Text = "Close",
                Location = new Point(410, 340),
                Size = new Size(70, 30),
                BackColor = Color.FromArgb(120, 0, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            closeButton.Click += (s, e) => Close();
            Controls.Add(closeButton);

            // Step indicator
            var stepLabel = new Label
            {
                Location = new Point(200, 345),
                Size = new Size(100, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.LightGray,
                Font = new Font(Font.FontFamily, 8f)
            };
            stepLabel.Tag = "step";
            Controls.Add(stepLabel);
        }

        private void UpdateStep()
        {
            var contentLabel = Controls.Find("content", true)[0] as Label;
            var prevButton = Controls.Find("prev", true)[0] as Button;
            var nextButton = Controls.Find("next", true)[0] as Button;
            var stepLabel = Controls.Find("step", true)[0] as Label;

            if (contentLabel != null)
                contentLabel.Text = _steps[_currentStep];

            if (prevButton != null)
                prevButton.Enabled = _currentStep > 0;

            if (nextButton != null)
            {
                nextButton.Enabled = _currentStep < _steps.Length - 1;
                nextButton.Text = _currentStep == _steps.Length - 1 ? "Finish" : "Next";
            }

            if (stepLabel != null)
                stepLabel.Text = $"Step {_currentStep + 1} of {_steps.Length}";
        }

        private void PrevButton_Click(object? sender, EventArgs e)
        {
            if (_currentStep > 0)
            {
                _currentStep--;
                UpdateStep();
            }
        }

        private void NextButton_Click(object? sender, EventArgs e)
        {
            if (_currentStep < _steps.Length - 1)
            {
                _currentStep++;
                UpdateStep();
            }
            else
            {
                Close();
            }
        }
    }
}