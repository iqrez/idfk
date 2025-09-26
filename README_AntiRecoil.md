# Anti-Recoil Feature

## Overview
The anti-recoil system automatically compensates for vertical mouse movement (recoil) when shooting in games. It detects upward mouse movement during shooting and applies downward compensation to counteract recoil patterns.

## Features
- **Automatic Activation**: Triggers when left mouse button is pressed (shooting)
- **Adjustable Strength**: 0% to 100% compensation strength
- **Configurable Delay**: Set activation delay to avoid interfering with initial aim
- **Threshold Control**: Minimum movement required to trigger compensation
- **Settings Persistence**: Settings are automatically saved and restored

## Controls

### Hotkeys
- **F9**: Toggle anti-recoil on/off
- **F10**: Decrease strength by 10%
- **F11**: Increase strength by 10%
- **F12**: Reset all settings to defaults
- **F1**: Run controller diagnostics (for troubleshooting)

### UI Controls
- **Enable Checkbox**: Turn anti-recoil on/off
- **Strength Slider**: Adjust compensation strength (0-100%)
- **Delay Setting**: Set activation delay in milliseconds (0-500ms)

## How It Works

1. **Detection**: Monitors left mouse button state for shooting detection
2. **Activation**: Waits for the configured delay after shooting starts
3. **Processing**: Analyzes vertical mouse movement during shooting
4. **Compensation**: Reduces upward movement by the configured strength percentage
5. **Timeout**: Automatically stops compensating after 1 second of no shooting

## Configuration Tips

### Strength Setting
- **0.3-0.5 (30-50%)**: Good starting point for most games
- **0.6-0.8 (60-80%)**: For games with heavy recoil
- **0.1-0.3 (10-30%)**: For fine-tuned weapons or low recoil

### Delay Setting
- **0-50ms**: For immediate compensation
- **50-100ms**: To avoid interfering with initial aim adjustment
- **100-200ms**: For weapons with delayed recoil patterns

### Threshold
- **1.0-2.0**: Standard sensitivity for most mice
- **2.0-5.0**: For high-DPI mice or to reduce false triggers

## Status Indicators

### Status Text
- **Disabled**: Anti-recoil is turned off
- **Standby**: Enabled but not currently shooting
- **Waiting**: Shooting detected, waiting for activation delay
- **Active**: Currently applying compensation

### Visual Indicators
- **Green Circle**: Anti-recoil is enabled
- **Gray Circle**: Anti-recoil is disabled
- **Green Bar**: Shows current strength level when enabled

## Troubleshooting

### Anti-Recoil Not Working
1. Ensure anti-recoil is enabled (F9 or checkbox)
2. Check that you're in Controller Output mode
3. Verify strength is set above 0%
4. Try reducing the activation delay
5. Check the threshold setting isn't too high

### Too Much/Little Compensation
1. Adjust strength with F10/F11 or the slider
2. Fine-tune the activation delay
3. Adjust the vertical threshold setting

### Controller Detection Issues
1. Press F1 to run diagnostics
2. Check logs for "Total controllers" vs "Physical controllers"
3. Ensure virtual controller is working (test in joy.cpl)
4. Try restarting the application

## File Locations
- **Settings**: `antirecoil_settings.json` (saved automatically)
- **Logs**: Check application logs for detailed operation info

## Compatibility
- Works with any game that uses XInput controllers
- Compatible with mouse-to-controller mapping mode
- Does not interfere with Controller Passthrough mode

## Notes
- Anti-recoil only works in "Controller Output" mode
- Settings are saved automatically when changed
- Compensation is applied before mouse-to-stick conversion
- System respects game's native controller detection

---

## Extended Debugging Steps (If Things Still Fail)

1. Verify ViGEm driver installed: Install the latest ViGEmBus from its official GitHub, then restart the app.
2. Run as Administrator (occasionally low-level hooks or ViGEm need elevated rights on some locked-down systems).
3. Check `Logs/` folder for errors around:
   - "ViGEm connect failed"
   - "LowLevel…" routing errors
   - Mode transition validation failures ("Mode transition validation failed")
4. Confirm no security software is blocking global hooks.
5. If passthrough never shows RUN but you have a controller:
   - Toggle auto-index off/on by adjusting `ControllerDetector` (temporarily log `FirstPhysical()` results).
   - Ensure the controller is truly XInput (some devices need Steam/Xbox drivers).
6. If anti-recoil isn’t acting:
   - Press F9 to ensure it shows Enabled (lime green line).
   - Hold left mouse: verify the Anti-Recoil status line updates (logic hooks left button down/up).

## Potential Future Improvements (Optional)

- Add a small log viewer panel within the overlay.
- Expose a quick "Reconnect ViGEm" button if the virtual pad drops.
- Persist additional anti-recoil tuning profiles or presets.
- Add on-screen graph of recent vertical movement vs compensation.
- Provide per-weapon profile switching via hotkeys.
- Add telemetry toggle to reduce log noise in production.