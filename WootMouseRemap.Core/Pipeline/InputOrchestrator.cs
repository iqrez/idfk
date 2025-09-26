using System.Threading.Channels;
using WootMouseRemap.Core.Mapping;
using Mapster;
using Microsoft.Extensions.Logging;

namespace WootMouseRemap.Core.Pipeline;

/// <summary>
/// High-performance input orchestrator with proper threading and batched ViGEm updates
/// </summary>
public sealed class InputOrchestrator : IDisposable
{
    private const int LEFT_MOUSE_BUTTON = 0;
    private const int RIGHT_MOUSE_BUTTON = 1;
    private const int MIDDLE_MOUSE_BUTTON = 2;
    private readonly ILogger<InputOrchestrator> _logger;
    private readonly Channel<RawInputEvent> _inputChannel;
    private readonly ChannelWriter<RawInputEvent> _inputWriter;
    private readonly ChannelReader<RawInputEvent> _inputReader;
    private readonly Xbox360ControllerWrapper _controller;
    private readonly StickMapper _stickMapper;
    private readonly System.Threading.Timer _tickTimer;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _processingTask;
    
    private InputMappingProfile _currentProfile = new();
    private volatile bool _isRunning;
    private readonly object _stateLock = new();
    
    // Batched state for efficient ViGEm updates
    private readonly ControllerStateBatch _pendingState = new();
    
    public InputOrchestrator(ILogger<InputOrchestrator> logger, Xbox360ControllerWrapper controller)
    {
        _logger = logger;
        _controller = controller;
        _stickMapper = new StickMapper();
        
        // High-capacity channel for input events
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };
        
        _inputChannel = Channel.CreateBounded<RawInputEvent>(options);
        _inputWriter = _inputChannel.Writer;
        _inputReader = _inputChannel.Reader;
        
        // 500Hz tick rate for smooth controller updates
        _tickTimer = new System.Threading.Timer(ProcessTick, null, Timeout.Infinite, Timeout.Infinite);
    }
    
    public void Start()
    {
        if (_isRunning) return;
        
        _isRunning = true;
        _controller.Connect();
        
        try
        {
            _processingTask = Task.Run(ProcessInputEvents, _cancellation.Token)
                .ContinueWith(t => 
                {
                    if (t.IsFaulted)
                        _logger.LogError(t.Exception, "Input processing task failed");
                }, TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start input processing task");
            _isRunning = false;
            throw;
        }
        
        // Start 500Hz tick timer (2ms intervals)
        _tickTimer.Change(0, 2);
        
        _logger.LogInformation("InputOrchestrator started with 500Hz tick rate");
    }
    
    public void Stop()
    {
        if (!_isRunning) return;
        
        _isRunning = false;
        _tickTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _cancellation.Cancel();
        _inputWriter.Complete();
        
        _logger.LogInformation("InputOrchestrator stopped");
    }
    
    public void SetProfile(InputMappingProfile profile)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        
        lock (_stateLock)
        {
            _currentProfile = profile;
            
            var curve = _stickMapper.Curve;
            curve.Sensitivity = profile.CurveSettings.Sensitivity;
            curve.Expo = profile.CurveSettings.Expo;
            curve.AntiDeadzone = profile.CurveSettings.AntiDeadzone;
            curve.MaxSpeed = profile.CurveSettings.MaxSpeed;
            curve.EmaAlpha = profile.CurveSettings.EmaAlpha;
            curve.ScaleX = profile.CurveSettings.ScaleX;
            curve.ScaleY = profile.CurveSettings.ScaleY;
            curve.JitterFloor = profile.CurveSettings.JitterFloor;
        }
        
        _logger.LogInformation("Switched to profile: {ProfileName}", profile.Name);
    }
    
    public void QueueKeyboardEvent(int virtualKey, bool isDown)
    {
        if (!_isRunning) return;
        
        var evt = new KeyboardEvent(virtualKey, isDown);
        if (!_inputWriter.TryWrite(evt))
            _logger.LogWarning("Dropped keyboard event: channel full");
    }
    
    public void QueueMouseEvent(int button, bool isDown, int deltaX = 0, int deltaY = 0)
    {
        if (!_isRunning) return;
        
        var evt = new MouseEvent(button, isDown, deltaX, deltaY);
        if (!_inputWriter.TryWrite(evt))
            _logger.LogWarning("Dropped mouse event: channel full");
    }
    
    private async Task ProcessInputEvents()
    {
        try
        {
            await foreach (var inputEvent in _inputReader.ReadAllAsync(_cancellation.Token))
            {
                ProcessSingleEvent(inputEvent);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing input events");
        }
    }
    
    private void ProcessSingleEvent(RawInputEvent inputEvent)
    {
        lock (_stateLock)
        {
            switch (inputEvent)
            {
                case KeyboardEvent keyEvent:
                    ProcessKeyboardEvent(keyEvent);
                    break;
                    
                case MouseEvent mouseEvent:
                    ProcessMouseEvent(mouseEvent);
                    break;
                    
                default:
                    _logger.LogWarning("Unhandled input event type: {EventType}", inputEvent.GetType().Name);
                    break;
            }
        }
    }
    
    private void ProcessKeyboardEvent(KeyboardEvent keyEvent)
    {
        // Update WASD state for left stick
        _stickMapper.UpdateKey(keyEvent.VirtualKey, keyEvent.IsDown);
        
        // Check for mapped button actions
        if (_currentProfile.KeyboardMap.TryGetValue(keyEvent.VirtualKey, out var mapping))
        {
            MapControllerInput(mapping, keyEvent.IsDown);
        }
    }
    
    private void ProcessMouseEvent(MouseEvent mouseEvent)
    {
        if (mouseEvent.DeltaX != 0 || mouseEvent.DeltaY != 0)
        {
            // Mouse movement -> right stick
            var (x, y) = _stickMapper.MouseToRightStick(mouseEvent.DeltaX, mouseEvent.DeltaY);
            _pendingState.RightStickX = x;
            _pendingState.RightStickY = y;
        }
        
        if (mouseEvent.Button >= 0)
        {
            // Mouse button -> controller button
            var mapping = mouseEvent.Button switch
            {
                LEFT_MOUSE_BUTTON => _currentProfile.MouseConfig.LeftClickMapping,
                RIGHT_MOUSE_BUTTON => _currentProfile.MouseConfig.RightClickMapping,
                MIDDLE_MOUSE_BUTTON => _currentProfile.MouseConfig.MiddleClickMapping,
                _ => MouseButton.None
            };
            
            if (mapping != MouseButton.None)
            {
                MapMouseButtonToController(mapping, mouseEvent.IsDown);
            }
        }
    }
    
    private void MapControllerInput(ControllerInput input, bool isPressed)
    {
        switch (input)
        {
            case ControllerInput.A: SetButtonState(ref _pendingState.A, isPressed); break;
            case ControllerInput.B: SetButtonState(ref _pendingState.B, isPressed); break;
            case ControllerInput.X: SetButtonState(ref _pendingState.X, isPressed); break;
            case ControllerInput.Y: SetButtonState(ref _pendingState.Y, isPressed); break;
            case ControllerInput.LeftBumper: SetButtonState(ref _pendingState.LeftBumper, isPressed); break;
            case ControllerInput.RightBumper: SetButtonState(ref _pendingState.RightBumper, isPressed); break;
            case ControllerInput.LeftTrigger: SetTriggerState(ref _pendingState.LeftTrigger, isPressed); break;
            case ControllerInput.RightTrigger: SetTriggerState(ref _pendingState.RightTrigger, isPressed); break;
        }
    }
    
    private void MapMouseButtonToController(MouseButton mouseButton, bool isPressed)
    {
        switch (mouseButton)
        {
            case MouseButton.LeftTrigger: SetTriggerState(ref _pendingState.LeftTrigger, isPressed); break;
            case MouseButton.RightTrigger: SetTriggerState(ref _pendingState.RightTrigger, isPressed); break;
            case MouseButton.A: SetButtonState(ref _pendingState.A, isPressed); break;
            case MouseButton.B: SetButtonState(ref _pendingState.B, isPressed); break;
            case MouseButton.X: SetButtonState(ref _pendingState.X, isPressed); break;
            case MouseButton.Y: SetButtonState(ref _pendingState.Y, isPressed); break;
            case MouseButton.LeftBumper: SetButtonState(ref _pendingState.LeftBumper, isPressed); break;
            case MouseButton.RightBumper: SetButtonState(ref _pendingState.RightBumper, isPressed); break;
        }
    }
    
    private static void SetButtonState(ref bool button, bool isPressed) => button = isPressed;
    private static void SetTriggerState(ref byte trigger, bool isPressed) => trigger = (byte)(isPressed ? 255 : 0);
    
    private void ProcessTick(object? state)
    {
        if (!_isRunning) return;
        
        try
        {
            lock (_stateLock)
            {
                // Update left stick from WASD
                var (lx, ly) = _stickMapper.WasdToLeftStick();
                _pendingState.LeftStickX = lx;
                _pendingState.LeftStickY = ly;
                
                // Batch update controller state
                ApplyControllerState();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in tick processing");
        }
    }
    
    private void ApplyControllerState()
    {
        // Single batched update to ViGEm
        _controller.SetLeftStick(_pendingState.LeftStickX, _pendingState.LeftStickY);
        _controller.SetRightStick(_pendingState.RightStickX, _pendingState.RightStickY);
        _controller.SetTrigger(false, _pendingState.LeftTrigger);
        _controller.SetTrigger(true, _pendingState.RightTrigger);
        
        // Buttons
        _controller.SetButton(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.A, _pendingState.A);
        _controller.SetButton(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.B, _pendingState.B);
        _controller.SetButton(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.X, _pendingState.X);
        _controller.SetButton(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Y, _pendingState.Y);
        _controller.SetButton(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.LeftShoulder, _pendingState.LeftBumper);
        _controller.SetButton(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.RightShoulder, _pendingState.RightBumper);
        
        // Single submit for all changes
        _controller.Submit();
    }
    
    public void Dispose()
    {
        Stop();
        try
        {
            _processingTask?.Wait(1000);
        }
        catch (AggregateException) { }
        
        _tickTimer?.Dispose();
        _cancellation?.Dispose();
        _controller?.Dispose();
    }
}

/// <summary>
/// Batched controller state for efficient updates
/// </summary>
public sealed class ControllerStateBatch
{
    public short LeftStickX, LeftStickY, RightStickX, RightStickY;
    public byte LeftTrigger, RightTrigger;
    public bool A, B, X, Y, LeftBumper, RightBumper;
}