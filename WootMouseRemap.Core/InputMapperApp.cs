using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WootMouseRemap.Core.Pipeline;
using WootMouseRemap.Core.Services;
using WootMouseRemap.Core.Mapping;
using Mapster;

namespace WootMouseRemap.Core;

/// <summary>
/// Minimal console application demonstrating the complete Input Orchestrator pipeline
/// </summary>
public sealed class InputMapperApp : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly ILogger<InputMapperApp> _logger;
    private readonly InputOrchestrator _orchestrator;
    private readonly ProfileService _profileService;
    private readonly RawInput _rawInput;
    private readonly RawInputMsgWindow _msgWindow;
    private readonly CancellationTokenSource _cancellation = new();
    
    public InputMapperApp()
    {
        // Configure services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddSingleton<Xbox360ControllerWrapper>();
        services.AddSingleton<ProfileService>();
        services.AddSingleton<InputOrchestrator>();
        
        _services = services.BuildServiceProvider();
        _logger = _services.GetRequiredService<ILogger<InputMapperApp>>();
        
        // Configure Mapster
        InputMappingConfig.Configure();
        
        // Initialize components
        _profileService = _services.GetRequiredService<ProfileService>();
        var controller = _services.GetRequiredService<Xbox360ControllerWrapper>();
        _orchestrator = _services.GetRequiredService<InputOrchestrator>();
        
        // Setup Raw Input
        _msgWindow = new RawInputMsgWindow();
        _rawInput = new RawInput(_msgWindow);
        
        // Wire up events
        _rawInput.KeyboardEvent += OnKeyboardEvent;
        _rawInput.MouseMove += OnMouseMove;
        _rawInput.MouseButton += OnMouseButton;
    }
    
    public async Task RunAsync()
    {
        try
        {
            _logger.LogInformation("Starting Input Mapper Application...");
            
            // Register Raw Input
            _rawInput.Register();
            _logger.LogInformation("Raw Input registered successfully");
            
            // Load default profile
            var defaultProfile = _profileService.GetDefaultProfile();
            _orchestrator.SetProfile(defaultProfile);
            _logger.LogInformation("Loaded profile: {ProfileName}", defaultProfile.Name);
            
            // Start orchestrator
            _orchestrator.Start();
            _logger.LogInformation("Input Orchestrator started");
            
            // Display usage instructions
            DisplayUsageInstructions();
            
            // Run message loop
            await RunMessageLoop();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Input Mapper Application");
            Dispose();
            throw;
        }
    }
    
    private void DisplayUsageInstructions()
    {
        var instructions = @"
=== Input Mapper - Keyboard/Mouse → Xbox 360 Controller ===

Controls:
  WASD        → Left Stick
  Mouse Move  → Right Stick
  Left Click  → Right Trigger
  Right Click → Left Trigger
  Space       → A Button
  Enter       → Start Button
  Escape      → Back Button

Commands:
  1-3         → Switch profiles (Default/CS2/Valorant)
  Ctrl+C      → Exit
";
        Console.WriteLine(instructions);
    }
    
    private async Task RunMessageLoop()
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _cancellation.Cancel();
        };
        
        var inputTask = Task.Run(() =>
        {
            while (!_cancellation.Token.IsCancellationRequested)
            {
                try
                {
                    var key = Console.ReadKey(true);
                    switch (key.KeyChar)
                    {
                        case '1':
                            SwitchProfile("Default");
                            break;
                        case '2':
                            SwitchProfile("CS2");
                            break;
                        case '3':
                            SwitchProfile("Valorant");
                            break;
                    }
                }
                catch (InvalidOperationException)
                {
                    // Console input not available (e.g., running as service)
                    break;
                }
            }
        });
        
        // Keep application running
        try
        {
            await Task.Delay(-1, _cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Shutdown requested");
        }
    }
    
    private void SwitchProfile(string profileName)
    {
        try
        {
            var profile = _profileService.GetProfile(profileName);
            if (profile == null)
            {
                profile = _profileService.CreateGameProfile(profileName);
            }
            
            _orchestrator.SetProfile(profile);
            _logger.LogInformation("Switched to profile: {ProfileName}", profile.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch to profile: {ProfileName}", profileName);
        }
    }
    
    private void OnKeyboardEvent(int virtualKey, bool isDown)
    {
        _orchestrator.QueueKeyboardEvent(virtualKey, isDown);
    }
    
    private void OnMouseMove(int deltaX, int deltaY)
    {
        const int NO_BUTTON = -1;
        _orchestrator.QueueMouseEvent(NO_BUTTON, false, deltaX, deltaY);
    }
    
    private void OnMouseButton(MouseInput button, bool isDown)
    {
        var buttonIndex = button switch
        {
            MouseInput.Left => 0,
            MouseInput.Right => 1,
            MouseInput.Middle => 2,
            MouseInput.XButton1 => 3,
            MouseInput.XButton2 => 4,
            _ => -1
        };
        
        if (buttonIndex >= 0)
        {
            _orchestrator.QueueMouseEvent(buttonIndex, isDown);
        }
    }
    
    public void Dispose()
    {
        _cancellation.Cancel();
        _orchestrator?.Dispose();
        _rawInput?.Dispose();
        _msgWindow?.Dispose();
        (_services as IDisposable)?.Dispose();
        _cancellation?.Dispose();
    }
    
    /// <summary>
    /// Entry point for console application
    /// </summary>
    public static async Task Main(string[] args)
    {
        try
        {
            using var app = new InputMapperApp();
            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}