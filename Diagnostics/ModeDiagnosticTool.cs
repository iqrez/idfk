using System;
using System.Text;
using WootMouseRemap.Core;
using WootMouseRemap.Features;
using WootMouseRemap;

namespace WootMouseRemap.Diagnostics
{
    /// <summary>
    /// Diagnostic tool to help identify mode-related issues
    /// </summary>
    public static class ModeDiagnosticTool
    {
        /// <summary>
        /// Generate a comprehensive diagnostic report for mode issues
        /// </summary>
        public static string GenerateModeDiagnosticReport(
            ModeService modeService,
            Xbox360ControllerWrapper pad,
            XInputPassthrough xpass,
            ControllerDetector detector,
            AntiRecoil antiRecoil)
        {
            var report = new StringBuilder();
            report.AppendLine("=== MODE DIAGNOSTIC REPORT ===");
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            // Current Mode Status
            report.AppendLine("=== CURRENT MODE STATUS ===");
            try
            {
                report.AppendLine($"Current Mode: {modeService.CurrentMode}");
                report.AppendLine($"Mode Service Status: Active");
            }
            catch (Exception ex)
            {
                report.AppendLine($"Mode Service Error: {ex.Message}");
            }
            report.AppendLine();

            // Controller Status
            report.AppendLine("=== CONTROLLER STATUS ===");
            try
            {
                report.AppendLine($"Virtual Controller Connected: {pad?.IsConnected ?? false}");
                if (pad != null)
                {
                    var snapshot = pad.GetSnapshot();
                    report.AppendLine($"Virtual Controller Last Update: Working");
                    report.AppendLine($"Virtual Controller State: LX={snapshot.LX}, LY={snapshot.LY}, RX={snapshot.RX}, RY={snapshot.RY}");
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"Virtual Controller Error: {ex.Message}");
            }

            try
            {
                report.AppendLine($"Physical Controller Detection: {detector?.Connected ?? false}");
                if (detector != null)
                {
                    report.AppendLine($"Detector Auto Index: {detector.AutoIndex}");
                    report.AppendLine($"Detector Status: Active");
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"Physical Controller Detector Error: {ex.Message}");
            }
            report.AppendLine();

            // XInput Passthrough Status
            report.AppendLine("=== XINPUT PASSTHROUGH STATUS ===");
            try
            {
                report.AppendLine($"Passthrough Running: {xpass?.IsRunning ?? false}");
                report.AppendLine($"Passthrough Connected: {xpass?.IsConnected ?? false}");
            }
            catch (Exception ex)
            {
                report.AppendLine($"XInput Passthrough Error: {ex.Message}");
            }

            // Test XInput Controller Detection
            report.AppendLine();
            report.AppendLine("=== XINPUT CONTROLLER SCAN ===");
            for (int i = 0; i < 4; i++)
            {
                try
                {
                    if (XInputHelper.TryGetState(i, out var state))
                    {
                        report.AppendLine($"Controller {i}: CONNECTED");
                        report.AppendLine($"  Packet: {state.dwPacketNumber}");
                        report.AppendLine($"  Buttons: 0x{state.Gamepad.wButtons:X4}");
                        report.AppendLine($"  Left Stick: ({state.Gamepad.sThumbLX}, {state.Gamepad.sThumbLY})");
                        report.AppendLine($"  Right Stick: ({state.Gamepad.sThumbRX}, {state.Gamepad.sThumbRY})");
                        report.AppendLine($"  Triggers: L={state.Gamepad.bLeftTrigger}, R={state.Gamepad.bRightTrigger}");
                    }
                    else
                    {
                        report.AppendLine($"Controller {i}: NOT CONNECTED");
                    }
                }
                catch (Exception ex)
                {
                    report.AppendLine($"Controller {i}: ERROR - {ex.Message}");
                }
            }
            report.AppendLine();

            // Anti-Recoil Status
            report.AppendLine("=== ANTI-RECOIL STATUS ===");
            try
            {
                report.AppendLine($"Anti-Recoil Enabled: {antiRecoil?.Enabled ?? false}");
                if (antiRecoil != null)
                {
                    report.AppendLine($"Strength: {antiRecoil.Strength:P0}");
                    report.AppendLine($"Threshold: {antiRecoil.VerticalThreshold}");
                    report.AppendLine($"Delay: {antiRecoil.ActivationDelayMs}ms");
                    report.AppendLine($"Status Info: {antiRecoil.GetStatusInfo()}");
                    report.AppendLine($"Is Active: {antiRecoil.IsActive}");
                    report.AppendLine($"Pattern Count: {antiRecoil.Patterns.Count}");
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"Anti-Recoil Error: {ex.Message}");
            }
            report.AppendLine();

            // System Information
            report.AppendLine("=== SYSTEM INFORMATION ===");
            try
            {
                report.AppendLine($"OS: {Environment.OSVersion}");
                report.AppendLine($"64-bit Process: {Environment.Is64BitProcess}");
                report.AppendLine($"Working Directory: {Environment.CurrentDirectory}");
                report.AppendLine($"Process Name: {System.Diagnostics.Process.GetCurrentProcess().ProcessName}");
            }
            catch (Exception ex)
            {
                report.AppendLine($"System Info Error: {ex.Message}");
            }
            report.AppendLine();

            // ViGEm Driver Check
            report.AppendLine("=== VIGEM DRIVER CHECK ===");
            try
            {
                using var client = new Nefarius.ViGEm.Client.ViGEmClient();
                report.AppendLine("ViGEm Driver: AVAILABLE");
            }
            catch (Exception ex)
            {
                report.AppendLine($"ViGEm Driver: ERROR - {ex.Message}");
                report.AppendLine("NOTE: ViGEm Bus Driver may not be installed or may be corrupted.");
                report.AppendLine("Download from: https://github.com/ViGEm/ViGEmBus/releases");
            }
            report.AppendLine();

            // Recommendations
            report.AppendLine("=== RECOMMENDATIONS ===");

            if (!(pad?.IsConnected ?? false))
            {
                report.AppendLine("• Virtual controller is not connected - check ViGEm driver installation");
            }

            if (!(detector?.Connected ?? false))
            {
                report.AppendLine("• No physical controller detected - ensure controller is connected and recognized by Windows");
            }

            if (!(xpass?.IsRunning ?? false) && modeService.CurrentMode == InputMode.ControllerPass)
            {
                report.AppendLine("• Passthrough mode is selected but XInput passthrough is not running");
            }

            if (!(antiRecoil?.Enabled ?? false) && modeService.CurrentMode == InputMode.Native)
            {
                report.AppendLine("• Controller output mode is selected but anti-recoil is disabled");
            }

            report.AppendLine();
            report.AppendLine("=== END OF REPORT ===");

            return report.ToString();
        }

        /// <summary>
        /// Quick mode validation check
        /// </summary>
        public static string QuickModeCheck(ModeService modeService, Xbox360ControllerWrapper pad, ControllerDetector detector)
        {
            var issues = new StringBuilder();

            try
            {
                var currentMode = modeService.CurrentMode;

                switch (currentMode)
                {
                    case InputMode.Native:
                        if (!(pad?.IsConnected ?? false))
                            issues.AppendLine("❌ Virtual controller not connected for Native mode");
                        else
                            issues.AppendLine("✅ Virtual controller connected for Native mode");
                        break;

                    case InputMode.ControllerPass:
                        if (!(detector?.Connected ?? false))
                            issues.AppendLine("❌ No physical controller detected for ControllerPass mode");
                        else
                            issues.AppendLine("✅ Physical controller detected for ControllerPass mode");

                        if (!(pad?.IsConnected ?? false))
                            issues.AppendLine("❌ Virtual controller not connected for ControllerPass mode");
                        else
                            issues.AppendLine("✅ Virtual controller connected for ControllerPass mode");
                        break;
                }
            }
            catch (Exception ex)
            {
                issues.AppendLine($"❌ Error during mode check: {ex.Message}");
            }

            return issues.ToString();
        }
    }
}