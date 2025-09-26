using Mapster;
using System.Text.Json.Serialization;

namespace WootMouseRemap.Core.Mapping;

/// <summary>
/// Mapster-based input mapping profile for keyboard/mouse to controller transformations
/// </summary>
public sealed class InputMappingProfile
{
    public string Name { get; set; } = "Default";
    public string Description { get; set; } = "";
    
    // Keyboard mappings
    public Dictionary<int, ControllerInput> KeyboardMap { get; set; } = new();
    
    // Mouse mappings  
    public MouseMappingConfig MouseConfig { get; set; } = new();
    // Mouse DPI configured for this profile (UI expects this)
    public int MouseDpi { get; set; } = 1600;
    
    // Transform settings
    public CurveSettings CurveSettings { get; set; } = new();
    // Backwards-compatibility alias for older code that expects 'Curves'
    public CurveSettings Curves { get => CurveSettings; set => CurveSettings = value; }
    
    // Controller preferences
    public int PreferredControllerIndex { get; set; } = 0;
    public ControllerType TargetControllerType { get; set; } = ControllerType.Xbox360;
}

public sealed class MouseMappingConfig
{
    public bool EnableMouseToRightStick { get; set; } = true;
    public MouseButton LeftClickMapping { get; set; } = MouseButton.RightTrigger;
    public MouseButton RightClickMapping { get; set; } = MouseButton.LeftTrigger;
    public MouseButton MiddleClickMapping { get; set; } = MouseButton.None;
    public float WheelSensitivity { get; set; } = 1.0f;
}

public sealed class CurveSettings
{
    public float Sensitivity { get; set; } = 0.35f;
    public float Expo { get; set; } = 0.6f;
    public float AntiDeadzone { get; set; } = 0.05f;
    public float MaxSpeed { get; set; } = 1.0f;
    public float EmaAlpha { get; set; } = 0.35f;
    public float ScaleX { get; set; } = 1.0f;
    public float ScaleY { get; set; } = 1.0f;
    public float JitterFloor { get; set; } = 0.0f;
}

public enum ControllerInput
{
    None, A, B, X, Y, LeftBumper, RightBumper, LeftTrigger, RightTrigger,
    LeftStickClick, RightStickClick, DPadUp, DPadDown, DPadLeft, DPadRight,
    Start, Back, LeftStickUp, LeftStickDown, LeftStickLeft, LeftStickRight
}

public enum MouseButton
{
    None, LeftTrigger, RightTrigger, A, B, X, Y, LeftBumper, RightBumper
}

public enum ControllerType
{
    Xbox360, DualShock4
}

/// <summary>
/// Mapster configuration for input mapping transformations
/// </summary>
public static class InputMappingConfig
{
    private const int VK_W = 0x57;
    private const int VK_S = 0x53;
    private const int VK_A = 0x41;
    private const int VK_D = 0x44;
    private const int VK_SPACE = 0x20;
    public static void Configure()
    {
        TypeAdapterConfig<RawInputEvent, ControllerState>
            .NewConfig()
            .Map(dest => dest.Timestamp, src => DateTimeOffset.UtcNow)
            .Map(dest => dest.IsValid, src => true);
            
        TypeAdapterConfig<KeyboardEvent, ControllerInput>
            .NewConfig()
            .MapWith(src => MapKeyboardInput(src));
            
        TypeAdapterConfig<MouseEvent, ControllerInput>
            .NewConfig()
            .MapWith(src => MapMouseInput(src));
    }
    
    private static ControllerInput MapKeyboardInput(KeyboardEvent keyEvent)
    {
        return keyEvent.VirtualKey switch
        {
            VK_W => ControllerInput.LeftStickUp,
            VK_S => ControllerInput.LeftStickDown,
            VK_A => ControllerInput.LeftStickLeft,
            VK_D => ControllerInput.LeftStickRight,
            VK_SPACE => ControllerInput.A,
            _ => ControllerInput.None
        };
    }
    
    private static ControllerInput MapMouseInput(MouseEvent mouseEvent)
    {
        return mouseEvent.Button switch
        {
            0 => ControllerInput.RightTrigger,  // Left click
            1 => ControllerInput.LeftTrigger,   // Right click  
            2 => ControllerInput.B,             // Middle click
            _ => ControllerInput.None
        };
    }
}

// Supporting event types
public record RawInputEvent(int Type, object Data, DateTimeOffset Timestamp);
public record KeyboardEvent(int VirtualKey, bool IsDown) : RawInputEvent(1, new { VirtualKey, IsDown }, DateTimeOffset.UtcNow);
public record MouseEvent(int Button, bool IsDown, int DeltaX = 0, int DeltaY = 0) : RawInputEvent(2, new { Button, IsDown, DeltaX, DeltaY }, DateTimeOffset.UtcNow);
public record ControllerState(DateTimeOffset Timestamp, bool IsValid);