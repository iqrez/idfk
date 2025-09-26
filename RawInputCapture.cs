using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

/// <summary>Raw Input capture for keyboard/mouse</summary>
public sealed class RawInputCapture : NativeWindow, IDisposable
{
    public event Action<int, bool>? KeyEvent;
    public event Action<int, int>? MouseMove;
    public event Action<int, bool>? MouseButton;
    
    // Performance optimizations
    private readonly byte[] _buffer = new byte[1024]; // Reusable buffer to avoid allocations
    private int _lastMouseX, _lastMouseY; // For optional mouse move aggregation
    private bool _hasPendingMouseMove;
    private readonly System.Windows.Forms.Timer _mouseMoveTimer;
    
    public RawInputCapture()
    {
        CreateParams cp = new() { Caption = "InputCapture", Width = 0, Height = 0 };
        CreateHandle(cp);
        RegisterDevices();
        
        // Optional: Aggregate mouse move events to reduce frequency
        _mouseMoveTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60 FPS
        _mouseMoveTimer.Tick += OnMouseMoveTimerTick;
        _mouseMoveTimer.Start();
    }
    
    private void RegisterDevices()
    {
        var devices = new RAWINPUTDEVICE[2];
        devices[0] = new() { usUsagePage = 1, usUsage = 6, dwFlags = 0x100, hwndTarget = Handle }; // Keyboard
        devices[1] = new() { usUsagePage = 1, usUsage = 2, dwFlags = 0x100, hwndTarget = Handle }; // Mouse
        RegisterRawInputDevices(devices, 2, Marshal.SizeOf<RAWINPUTDEVICE>());
    }
    
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0xFF) ProcessRawInput(m.LParam);
        base.WndProc(ref m);
    }
    
    private unsafe void ProcessRawInput(IntPtr hRawInput)
    {
        uint size = 0;
        GetRawInputData(hRawInput, 0x10000003, IntPtr.Zero, ref size, 24);
        if (size == 0 || size > _buffer.Length) return;
        
        fixed (byte* p = _buffer)
        {
            if (GetRawInputData(hRawInput, 0x10000003, (IntPtr)p, ref size, 24) != size) return;
            
            var header = Marshal.PtrToStructure<RAWINPUTHEADER>((IntPtr)p);
            var dataPtr = (IntPtr)(p + 24);
            
            if (header.dwType == 1) // Keyboard
            {
                var kb = Marshal.PtrToStructure<RAWKEYBOARD>(dataPtr);
                bool down = kb.Message == 0x100 || kb.Message == 0x104;
                bool up = kb.Message == 0x101 || kb.Message == 0x105;
                if (down || up) KeyEvent?.Invoke(kb.VKey, down);
            }
            else if (header.dwType == 0) // Mouse
            {
                var mouse = Marshal.PtrToStructure<RAWMOUSE>(dataPtr);
                
                // Aggregate mouse move events for better performance
                if (mouse.lLastX != 0 || mouse.lLastY != 0)
                {
                    _lastMouseX += mouse.lLastX;
                    _lastMouseY += mouse.lLastY;
                    _hasPendingMouseMove = true;
                }
                
                var flags = mouse.usButtonFlags;
                if ((flags & 1) != 0) MouseButton?.Invoke(0, true);   // LMB down
                if ((flags & 2) != 0) MouseButton?.Invoke(0, false);  // LMB up
                if ((flags & 4) != 0) MouseButton?.Invoke(1, true);   // RMB down
                if ((flags & 8) != 0) MouseButton?.Invoke(1, false);  // RMB up
            }
        }
    }
    
    private void OnMouseMoveTimerTick(object? sender, EventArgs e)
    {
        if (_hasPendingMouseMove && MouseMove != null)
        {
            MouseMove.Invoke(_lastMouseX, _lastMouseY);
            _lastMouseX = _lastMouseY = 0;
            _hasPendingMouseMove = false;
        }
    }
    
    public void Dispose()
    {
        if (Handle != IntPtr.Zero) DestroyHandle();
        _mouseMoveTimer?.Stop();
        _mouseMoveTimer?.Dispose();
    }
    
    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTDEVICE { public ushort usUsagePage, usUsage; public int dwFlags; public IntPtr hwndTarget; }
    
    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTHEADER { public uint dwType, dwSize; public IntPtr hDevice, wParam; }
    
    [StructLayout(LayoutKind.Sequential)]
    struct RAWKEYBOARD { public ushort MakeCode, Flags, Reserved, VKey; public uint Message, ExtraInformation; }
    
    [StructLayout(LayoutKind.Sequential)]
    struct RAWMOUSE { public ushort usFlags; public uint ulButtons, usButtonFlags, usButtonData, ulRawButtons; public int lLastX, lLastY; public uint ulExtraInformation; }
    
    [DllImport("user32.dll")]
    static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, int cbSize);
    
    [DllImport("user32.dll")]
    static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);
}