using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

/// <summary>Complete Input Orchestrator - Keyboard/Mouse → Xbox360 Controller</summary>
public sealed class InputOrchestrator : IDisposable
{
    private readonly ILogger<InputOrchestrator> _logger;
    private readonly Channel<InputEvent> _channel;
    private readonly ChannelWriter<InputEvent> _writer;
    private readonly ChannelReader<InputEvent> _reader;
    private readonly Xbox360Controller _controller;
    private readonly System.Threading.Timer _ticker;
    private readonly CancellationTokenSource _cancel = new();
    private Task? _processor;
    private volatile bool _running;
    
    private readonly ControllerState _state = new();
    private readonly Dictionary<int, bool> _keys = new();
    private float _mouseX, _mouseY, _emaX, _emaY;
    
    public InputOrchestrator(ILogger<InputOrchestrator> logger)
    {
        _logger = logger;
        var options = new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest };
        _channel = Channel.CreateBounded<InputEvent>(options);
        _writer = _channel.Writer;
        _reader = _channel.Reader;
        _controller = new Xbox360Controller();
        _ticker = new System.Threading.Timer(Tick, null, -1, -1);
    }
    
    public void Start()
    {
        if (_running) return;
        _running = true;
        _controller.Connect();
        _processor = Task.Run(ProcessEvents);
        _ticker.Change(0, 2); // 500Hz
        _logger.LogInformation("Started 500Hz Input Orchestrator");
    }
    
    public void Stop()
    {
        _running = false;
        _ticker.Change(-1, -1);
        _cancel.Cancel();
        _writer.Complete();
    }
    
    public void QueueKey(int vk, bool down) => _writer.TryWrite(new KeyEvent(vk, down));
    public void QueueMouse(int dx, int dy) => _writer.TryWrite(new MouseEvent(dx, dy));
    public void QueueButton(int btn, bool down) => _writer.TryWrite(new ButtonEvent(btn, down));
    
    private async Task ProcessEvents()
    {
        await foreach (var evt in _reader.ReadAllAsync(_cancel.Token))
        {
            switch (evt)
            {
                case KeyEvent k: _keys[k.VK] = k.Down; break;
                case MouseEvent m: _mouseX += m.DX * 0.01f; _mouseY += m.DY * 0.01f; break;
                case ButtonEvent b: SetButton(b.Button, b.Down); break;
            }
        }
    }
    
    private void Tick(object? _)
    {
        if (!_running) return;
        
        // WASD → Left Stick
        int x = 0, y = 0;
        if (_keys.GetValueOrDefault(0x41)) x -= 1; // A
        if (_keys.GetValueOrDefault(0x44)) x += 1; // D  
        if (_keys.GetValueOrDefault(0x57)) y += 1; // W
        if (_keys.GetValueOrDefault(0x53)) y -= 1; // S
        
        var len = MathF.Sqrt(x * x + y * y);
        if (len > 0) { x = (int)(x / len * 32767); y = (int)(y / len * 32767); }
        
        // Mouse → Right Stick (EMA smoothing)
        const float alpha = 0.3f;
        _emaX += alpha * (_mouseX - _emaX);
        _emaY += alpha * (_mouseY - _emaY);
        
        var rx = (short)Math.Clamp(_emaX * 32767, -32768, 32767);
        var ry = (short)Math.Clamp(-_emaY * 32767, -32768, 32767);
        
        // Batch update
        _controller.SetSticks((short)x, (short)y, rx, ry);
        _controller.SetButtons(_state.A, _state.B, _state.X, _state.Y);
        _controller.SetTriggers(_state.LT, _state.RT);
        _controller.Submit();
        
        _mouseX *= 0.95f; _mouseY *= 0.95f; // Decay
    }
    
    private void SetButton(int btn, bool down)
    {
        switch (btn)
        {
            case 0: _state.RT = (byte)(down ? 255 : 0); break; // LMB → RT
            case 1: _state.LT = (byte)(down ? 255 : 0); break; // RMB → LT
            case 32: _state.A = down; break; // Space → A
        }
    }
    
    public void Dispose()
    {
        Stop();
        _processor?.Wait(1000);
        _ticker?.Dispose();
        _cancel?.Dispose();
        _controller?.Dispose();
    }
}

public record InputEvent;
public record KeyEvent(int VK, bool Down) : InputEvent;
public record MouseEvent(int DX, int DY) : InputEvent;
public record ButtonEvent(int Button, bool Down) : InputEvent;

public class ControllerState
{
    public bool A, B, X, Y;
    public byte LT, RT;
}