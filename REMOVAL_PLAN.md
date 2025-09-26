# Mode System Removal Plan

## Files to Delete Completely:
1. **Modes Directory**: All mode handler files
   - ControllerOutputMode.cs
   - ControllerPassthroughMode.cs  
   - IModeHandler.cs
   - ModeManager.cs

2. **Mode Services**: Core mode management
   - ModeService.cs
   - IModeService.cs
   - ModeController.cs
   - ThreadSafeModeController.cs
   - ModeTransitionManager.cs
   - ModeStateValidator.cs
   - ModeStatusFormatter.cs

3. **Passthrough System**: XInput passthrough
   - XInputPassthrough.cs
   - ControllerDetector.cs
   - PassthroughDiagnostics.cs

4. **Mode Diagnostics**: Mode-specific diagnostics
   - ModeDiagnostics.cs
   - ModeDiagnosticTool.cs
   - ModeDiagnosticForm.cs

5. **Device Management**: Physical controller hiding
   - DeviceHider.cs

## Files to Modify:
1. **OverlayForm.cs**: Remove all mode switching, passthrough, hotkeys
2. **TrayManager.cs**: Remove mode-related menu items
3. **Enums.cs**: Remove InputMode enum
4. **Program.cs**: Simplify startup

## Keep:
- AntiRecoil system
- Mouse/keyboard input capture (RawInputService)
- Virtual controller (Xbox360ControllerWrapper)
- StickMapper for mouse-to-stick conversion
- ProfileManager for settings