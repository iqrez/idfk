using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WootMouseRemap.Diagnostics;

namespace WootMouseRemap.Diagnostics
{
    /// <summary>
    /// Diagnostic tools for troubleshooting controller passthrough issues
    /// </summary>
    public static class PassthroughDiagnostics
    {
        public static PassthroughDiagnosticsReport GenerateReport(
            XInputPassthrough xpass,
            Xbox360ControllerWrapper pad,
            ControllerDetector? detector)
        {
            var report = new PassthroughDiagnosticsReport
            {
                GeneratedAt = DateTime.UtcNow
            };

            try
            {
                // Check XInput passthrough status
                report.XInputPassthroughRunning = xpass?.IsRunning ?? false;
                report.XInputPassthroughConnected = xpass?.IsConnected ?? false;

                // Check virtual controller status
                report.VirtualControllerConnected = pad?.IsConnected ?? false;

                // Check physical controller detection
                report.PhysicalControllerDetected = detector?.Connected ?? false;

                // Check for controller indices
                var connectedIndices = new List<int>();
                for (int i = 0; i < 4; i++)
                {
                    if (XInputHelper.TryGetState(i, out _))
                    {
                        connectedIndices.Add(i);
                    }
                }
                report.ConnectedControllerIndices = connectedIndices.ToArray();

                // Check system requirements
                report.ViGEmDriverInstalled = CheckViGEmDriver();
                report.XInputLibraryAvailable = CheckXInputLibrary();

                // Get current controller state if available
                var firstConnected = XInputHelper.FirstConnectedIndex();
                if (firstConnected >= 0 && XInputHelper.TryGetState(firstConnected, out var state))
                {
                    report.CurrentControllerState = new ControllerStateSnapshot
                    {
                        PlayerIndex = firstConnected,
                        PacketNumber = state.dwPacketNumber,
                        ButtonMask = state.Gamepad.wButtons,
                        LeftTrigger = state.Gamepad.bLeftTrigger,
                        RightTrigger = state.Gamepad.bRightTrigger,
                        LeftStickX = state.Gamepad.sThumbLX,
                        LeftStickY = state.Gamepad.sThumbLY,
                        RightStickX = state.Gamepad.sThumbRX,
                        RightStickY = state.Gamepad.sThumbRY
                    };
                }

                // Get virtual controller snapshot if available
                if (pad?.IsConnected == true)
                {
                    var snapshot = pad.GetSnapshot();
                    report.VirtualControllerState = new VirtualControllerSnapshot
                    {
                        LeftStickX = snapshot.LX,
                        LeftStickY = snapshot.LY,
                        RightStickX = snapshot.RX,
                        RightStickY = snapshot.RY,
                        LeftTrigger = snapshot.LT,
                        RightTrigger = snapshot.RT,
                        ButtonsPressed = GetPressedButtons(snapshot)
                    };
                }

                // Analyze potential issues
                report.PotentialIssues = AnalyzeIssues(report);
                report.Recommendations = GenerateRecommendations(report);

            }
            catch (Exception ex)
            {
                report.DiagnosticError = ex.Message;
                Logger.Error("Error generating passthrough diagnostics", ex);
            }

            return report;
        }

        private static bool CheckViGEmDriver()
        {
            try
            {
                // Try to create a ViGEm client - this will fail if driver is not installed
                using var client = new Nefarius.ViGEm.Client.ViGEmClient();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckXInputLibrary()
        {
            try
            {
                // Try to call XInput function - this will fail if XInput is not available
                XInputHelper.TryGetState(0, out _);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string[] GetPressedButtons(Xbox360ControllerWrapper.PadSnapshot snapshot)
        {
            var pressed = new List<string>();

            if (snapshot.A) pressed.Add("A");
            if (snapshot.B) pressed.Add("B");
            if (snapshot.X) pressed.Add("X");
            if (snapshot.Y) pressed.Add("Y");
            if (snapshot.LB) pressed.Add("LB");
            if (snapshot.RB) pressed.Add("RB");
            if (snapshot.Back) pressed.Add("Back");
            if (snapshot.Start) pressed.Add("Start");
            if (snapshot.L3) pressed.Add("L3");
            if (snapshot.R3) pressed.Add("R3");
            if (snapshot.DUp) pressed.Add("DPad Up");
            if (snapshot.DDown) pressed.Add("DPad Down");
            if (snapshot.DLeft) pressed.Add("DPad Left");
            if (snapshot.DRight) pressed.Add("DPad Right");

            return pressed.ToArray();
        }

        private static string[] AnalyzeIssues(PassthroughDiagnosticsReport report)
        {
            var issues = new List<string>();

            if (!report.ViGEmDriverInstalled)
            {
                issues.Add("ViGEm Bus Driver is not installed or not functioning");
            }

            if (!report.XInputLibraryAvailable)
            {
                issues.Add("XInput library is not available or not functioning");
            }

            if (!report.PhysicalControllerDetected)
            {
                issues.Add("No physical controller detected by the system");
            }

            if (report.ConnectedControllerIndices.Length == 0)
            {
                issues.Add("No XInput controllers found at any player index");
            }

            if (!report.VirtualControllerConnected && report.ViGEmDriverInstalled)
            {
                issues.Add("Virtual controller failed to connect despite ViGEm being available");
            }

            if (report.PhysicalControllerDetected && !report.XInputPassthroughRunning)
            {
                issues.Add("Physical controller detected but passthrough is not running");
            }

            if (report.XInputPassthroughRunning && !report.XInputPassthroughConnected)
            {
                issues.Add("Passthrough is running but not connected to a controller");
            }

            if (report.CurrentControllerState != null && report.VirtualControllerState == null)
            {
                issues.Add("Physical controller input detected but virtual controller is not outputting");
            }

            return issues.ToArray();
        }

        private static string[] GenerateRecommendations(PassthroughDiagnosticsReport report)
        {
            var recommendations = new List<string>();

            if (!report.ViGEmDriverInstalled)
            {
                recommendations.Add("Install ViGEm Bus Driver from: https://github.com/ViGEm/ViGEmBus/releases");
            }

            if (!report.PhysicalControllerDetected)
            {
                recommendations.Add("Connect a physical Xbox controller and ensure it's recognized by Windows");
                recommendations.Add("Check Windows Game Controllers (joy.cpl) to verify controller detection");
            }

            if (report.ConnectedControllerIndices.Length == 0)
            {
                recommendations.Add("Try reconnecting your controller or using a different USB port");
                recommendations.Add("Update Xbox controller drivers through Windows Update");
            }

            if (report.PhysicalControllerDetected && !report.XInputPassthroughRunning)
            {
                recommendations.Add("Ensure you're in Controller Passthrough mode");
                recommendations.Add("Try restarting the passthrough mode");
            }

            if (report.XInputPassthroughRunning && !report.VirtualControllerConnected)
            {
                recommendations.Add("Restart the application to reinitialize the virtual controller");
                recommendations.Add("Check if other applications are using the ViGEm driver");
            }

            if (report.CurrentControllerState != null && report.VirtualControllerState == null)
            {
                recommendations.Add("Check if the virtual controller is being blocked by antivirus software");
                recommendations.Add("Run the application as administrator");
                recommendations.Add("Verify that no other applications are intercepting controller input");
            }

            return recommendations.ToArray();
        }

        public static string FormatReport(PassthroughDiagnosticsReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Controller Passthrough Diagnostics Report ===");
            sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(report.DiagnosticError))
            {
                sb.AppendLine($"ERROR: {report.DiagnosticError}");
                sb.AppendLine();
            }

            sb.AppendLine("System Status:");
            sb.AppendLine($"  ViGEm Driver Installed: {report.ViGEmDriverInstalled}");
            sb.AppendLine($"  XInput Library Available: {report.XInputLibraryAvailable}");
            sb.AppendLine();

            sb.AppendLine("Controller Status:");
            sb.AppendLine($"  Physical Controller Detected: {report.PhysicalControllerDetected}");
            sb.AppendLine($"  Connected Controller Indices: [{string.Join(", ", report.ConnectedControllerIndices)}]");
            sb.AppendLine($"  Virtual Controller Connected: {report.VirtualControllerConnected}");
            sb.AppendLine();

            sb.AppendLine("Passthrough Status:");
            sb.AppendLine($"  XInput Passthrough Running: {report.XInputPassthroughRunning}");
            sb.AppendLine($"  XInput Passthrough Connected: {report.XInputPassthroughConnected}");
            sb.AppendLine();

            if (report.CurrentControllerState != null)
            {
                var state = report.CurrentControllerState;
                sb.AppendLine("Current Physical Controller State:");
                sb.AppendLine($"  Player Index: {state.PlayerIndex}");
                sb.AppendLine($"  Packet Number: {state.PacketNumber}");
                sb.AppendLine($"  Button Mask: 0x{state.ButtonMask:X4}");
                sb.AppendLine($"  Left Stick: ({state.LeftStickX}, {state.LeftStickY})");
                sb.AppendLine($"  Right Stick: ({state.RightStickX}, {state.RightStickY})");
                sb.AppendLine($"  Triggers: L={state.LeftTrigger}, R={state.RightTrigger}");
                sb.AppendLine();
            }

            if (report.VirtualControllerState != null)
            {
                var state = report.VirtualControllerState;
                sb.AppendLine("Current Virtual Controller State:");
                sb.AppendLine($"  Left Stick: ({state.LeftStickX}, {state.LeftStickY})");
                sb.AppendLine($"  Right Stick: ({state.RightStickX}, {state.RightStickY})");
                sb.AppendLine($"  Triggers: L={state.LeftTrigger}, R={state.RightTrigger}");
                sb.AppendLine($"  Buttons Pressed: {string.Join(", ", state.ButtonsPressed)}");
                sb.AppendLine();
            }

            if (report.PotentialIssues.Length > 0)
            {
                sb.AppendLine("Potential Issues:");
                foreach (var issue in report.PotentialIssues)
                {
                    sb.AppendLine($"  • {issue}");
                }
                sb.AppendLine();
            }

            if (report.Recommendations.Length > 0)
            {
                sb.AppendLine("Recommendations:");
                foreach (var recommendation in report.Recommendations)
                {
                    sb.AppendLine($"  • {recommendation}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    public class PassthroughDiagnosticsReport
    {
        public DateTime GeneratedAt { get; set; }
        public string DiagnosticError { get; set; } = "";

        // System status
        public bool ViGEmDriverInstalled { get; set; }
        public bool XInputLibraryAvailable { get; set; }

        // Controller status
        public bool PhysicalControllerDetected { get; set; }
        public int[] ConnectedControllerIndices { get; set; } = Array.Empty<int>();
        public bool VirtualControllerConnected { get; set; }

        // Passthrough status
        public bool XInputPassthroughRunning { get; set; }
        public bool XInputPassthroughConnected { get; set; }

        // Current states
        public ControllerStateSnapshot? CurrentControllerState { get; set; }
        public VirtualControllerSnapshot? VirtualControllerState { get; set; }

        // Analysis
        public string[] PotentialIssues { get; set; } = Array.Empty<string>();
        public string[] Recommendations { get; set; } = Array.Empty<string>();
    }

    public class ControllerStateSnapshot
    {
        public int PlayerIndex { get; set; }
        public uint PacketNumber { get; set; }
        public ushort ButtonMask { get; set; }
        public byte LeftTrigger { get; set; }
        public byte RightTrigger { get; set; }
        public short LeftStickX { get; set; }
        public short LeftStickY { get; set; }
        public short RightStickX { get; set; }
        public short RightStickY { get; set; }
    }

    public class VirtualControllerSnapshot
    {
        public short LeftStickX { get; set; }
        public short LeftStickY { get; set; }
        public short RightStickX { get; set; }
        public short RightStickY { get; set; }
        public byte LeftTrigger { get; set; }
        public byte RightTrigger { get; set; }
        public string[] ButtonsPressed { get; set; } = Array.Empty<string>();
    }
}