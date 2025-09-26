using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Complete Input Mapper Application</summary>
public sealed class InputMapperApp : IDisposable
{
    private readonly ILogger<InputMapperApp> _logger;
    private readonly InputOrchestrator _orchestrator;
    private readonly RawInputCapture _rawInput;
    
    public InputMapperApp()
    {
        var services = new ServiceCollection()
            .AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information))
            .BuildServiceProvider();
            
        _logger = services.GetRequiredService<ILogger<InputMapperApp>>();
        _orchestrator = new InputOrchestrator(services.GetRequiredService<ILogger<InputOrchestrator>>());
        _rawInput = new RawInputCapture();
        
        // Wire events
        _rawInput.KeyEvent += _orchestrator.QueueKey;
        _rawInput.MouseMove += _orchestrator.QueueMouse;
        _rawInput.MouseButton += _orchestrator.QueueButton;
    }
    
    public async Task RunAsync()
    {
        _logger.LogInformation("Starting Input Mapper...");
        _orchestrator.Start();
        
        Console.WriteLine("=== Input Mapper Active ===");
        Console.WriteLine("WASD → Left Stick");
        Console.WriteLine("Mouse → Right Stick");
        Console.WriteLine("LMB → Right Trigger");
        Console.WriteLine("RMB → Left Trigger");
        Console.WriteLine("Space → A Button");
        Console.WriteLine("Press Ctrl+C to exit");
        
        var tcs = new TaskCompletionSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; tcs.SetResult(); };
        
        await tcs.Task;
        _logger.LogInformation("Shutting down...");
    }
    
    public void Dispose()
    {
        _orchestrator?.Dispose();
        _rawInput?.Dispose();
    }
    
    public static async Task Main()
    {
        using var app = new InputMapperApp();
        await app.RunAsync();
    }
}