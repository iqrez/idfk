using System.Collections.Concurrent;
// Logger is in WootMouseRemap namespace

namespace WootMouseRemap.Core.Services;

public sealed class RecoilProcessor : IRecoilProcessor, IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new();
    private bool _disposed;
    
    private float _strength = 0.5f;
    private int _activationDelayMs = 50;
    private float _verticalThreshold = 2.0f;
    private float _horizontalCompensation = 0.0f;
    private bool _adaptiveCompensation = false;
    private bool _enabled = false;
    private float _maxTickCompensation = 10.0f;
    private float _maxTotalCompensation = 100.0f;
    private int _cooldownMs = 0;
    private float _decayPerMs = 0.0f;
    
    private bool _isActive;
    private DateTime _activationStart;
    private float _accumulatedCompensation;
    private bool _isShootingDetected;
    private DateTime _lastShotTime;
    private DateTime _lastCompensationTime;
    private float _lastAppliedCompensation;
    private readonly ConcurrentQueue<float> _recentVerticalMovements = new();
    private readonly ConcurrentQueue<float> _recentVerticalBuffer = new();
    
    public event Action<float, float>? CompensationApplied;
    public event Action? SettingsChanged;
    
    public bool Enabled
    {
        get { _lock.EnterReadLock(); try { return _enabled; } finally { _lock.ExitReadLock(); } }
        set { _lock.EnterWriteLock(); try { _enabled = value; if (!value) Reset(); } finally { _lock.ExitWriteLock(); } SettingsChanged?.Invoke(); }
    }
    
    public float Strength
    {
        get { _lock.EnterReadLock(); try { return _strength; } finally { _lock.ExitReadLock(); } }
        set { _lock.EnterWriteLock(); try { _strength = Math.Clamp(value, 0f, 1f); } finally { _lock.ExitWriteLock(); } SettingsChanged?.Invoke(); }
    }
    
    public int ActivationDelayMs
    {
        get { _lock.EnterReadLock(); try { return _activationDelayMs; } finally { _lock.ExitReadLock(); } }
        set { _lock.EnterWriteLock(); try { _activationDelayMs = Math.Max(0, value); } finally { _lock.ExitWriteLock(); } SettingsChanged?.Invoke(); }
    }
    
    public float VerticalThreshold
    {
        get { _lock.EnterReadLock(); try { return _verticalThreshold; } finally { _lock.ExitReadLock(); } }
        set { _lock.EnterWriteLock(); try { _verticalThreshold = Math.Max(0f, value); } finally { _lock.ExitWriteLock(); } SettingsChanged?.Invoke(); }
    }
    
    public float HorizontalCompensation
    {
        get { _lock.EnterReadLock(); try { return _horizontalCompensation; } finally { _lock.ExitReadLock(); } }
        set { _lock.EnterWriteLock(); try { _horizontalCompensation = Math.Clamp(value, 0f, 1f); } finally { _lock.ExitWriteLock(); } SettingsChanged?.Invoke(); }
    }
    
    public bool AdaptiveCompensation
    {
        get { _lock.EnterReadLock(); try { return _adaptiveCompensation; } finally { _lock.ExitReadLock(); } }
        set { _lock.EnterWriteLock(); try { _adaptiveCompensation = value; } finally { _lock.ExitWriteLock(); } SettingsChanged?.Invoke(); }
    }
    
    public float MaxTickCompensation
    {
        get { _lock.EnterReadLock(); try { return _maxTickCompensation; } finally { _lock.ExitReadLock(); } }
        set { _lock.EnterWriteLock(); try { _maxTickCompensation = Math.Max(0f, value); } finally { _lock.ExitWriteLock(); } SettingsChanged?.Invoke(); }
    }
    
    public float MaxTotalCompensation
    {
        get { _lock.EnterReadLock(); try { return _maxTotalCompensation; } finally { _lock.ExitReadLock(); } }
        set { _lock.EnterWriteLock(); try { _maxTotalCompensation = Math.Max(0f, value); } finally { _lock.ExitWriteLock(); } SettingsChanged?.Invoke(); }
    }
    
    public int CooldownMs
    {
        get { _lock.EnterReadLock(); try { return _cooldownMs; } finally { _lock.ExitReadLock(); } }
        set { _lock.EnterWriteLock(); try { _cooldownMs = Math.Max(0, value); } finally { _lock.ExitWriteLock(); } SettingsChanged?.Invoke(); }
    }
    
    public float DecayPerMs
    {
        get { _lock.EnterReadLock(); try { return _decayPerMs; } finally { _lock.ExitReadLock(); } }
        set { _lock.EnterWriteLock(); try { _decayPerMs = Math.Max(0f, value); } finally { _lock.ExitWriteLock(); } SettingsChanged?.Invoke(); }
    }
    
    public bool IsActive
    {
        get { _lock.EnterReadLock(); try { return _isActive; } finally { _lock.ExitReadLock(); } }
    }
    
    public bool IsShootingDetected
    {
        get { _lock.EnterReadLock(); try { return _isShootingDetected; } finally { _lock.ExitReadLock(); } }
    }
    
    public float LastAppliedCompensation
    {
        get { _lock.EnterReadLock(); try { return _lastAppliedCompensation; } finally { _lock.ExitReadLock(); } }
    }
    
    public float AccumulatedCompensation
    {
        get { _lock.EnterReadLock(); try { return _accumulatedCompensation; } finally { _lock.ExitReadLock(); } }
    }
    
    public void OnShootingStarted()
    {
        if (_disposed || !_enabled) return;
        
        _lock.EnterWriteLock();
        try
        {
            _isShootingDetected = true;
            _lastShotTime = DateTime.UtcNow;
            _activationStart = DateTime.UtcNow;
            _accumulatedCompensation = 0f;
            while (_recentVerticalMovements.TryDequeue(out _)) { }
        }
        finally { _lock.ExitWriteLock(); }
    }
    
    public void OnShootingStopped()
    {
        if (_disposed) return;
        
        _lock.EnterWriteLock();
        try
        {
            _isShootingDetected = false;
            _isActive = false;
        }
        finally { _lock.ExitWriteLock(); }
    }
    
    public (float dx, float dy) ProcessMouseMovement(float dx, float dy)
    {
        if (_disposed || !_enabled) return (dx, dy);
        
        _lock.EnterWriteLock();
        try
        {
            var now = DateTime.UtcNow;
            
            _recentVerticalBuffer.Enqueue(dy);
            while (_recentVerticalBuffer.Count > 120) _recentVerticalBuffer.TryDequeue(out _);
            
            if (!_isShootingDetected) return (dx, dy);
            
            if ((now - _lastShotTime).TotalMilliseconds > 1000)
            {
                _isShootingDetected = false;
                _isActive = false;
                return (dx, dy);
            }
            
            var elapsed = (now - _activationStart).TotalMilliseconds;
            if (elapsed < _activationDelayMs) return (dx, dy);
            
            if (_cooldownMs > 0 && (now - _lastCompensationTime).TotalMilliseconds < _cooldownMs)
                return (dx, dy);
            
            if (_decayPerMs > 0 && _accumulatedCompensation > 0)
            {
                var timeSinceLastComp = (now - _lastCompensationTime).TotalMilliseconds;
                if (timeSinceLastComp > 0 && (!_isShootingDetected || dy <= _verticalThreshold))
                {
                    var decay = (float)(timeSinceLastComp * _decayPerMs);
                    _accumulatedCompensation = Math.Max(0f, _accumulatedCompensation - decay);
                }
            }
            
            _recentVerticalMovements.Enqueue(dy);
            while (_recentVerticalMovements.Count > 10) _recentVerticalMovements.TryDequeue(out _);
            
            if (dy <= _verticalThreshold) return (dx, dy);
            
            _isActive = true;
            float vComp = dy * _strength;
            
            if (_adaptiveCompensation && _recentVerticalMovements.Count >= 3)
            {
                var movements = _recentVerticalMovements.ToArray();
                var avg = movements.Average();
                if (avg > _verticalThreshold)
                    vComp *= Math.Min(2f, avg / _verticalThreshold);
            }
            
            if (_maxTickCompensation > 0 && vComp > _maxTickCompensation)
                vComp = _maxTickCompensation;
            
            if (_maxTotalCompensation > 0 && (_accumulatedCompensation + vComp) > _maxTotalCompensation)
                vComp = Math.Max(0f, _maxTotalCompensation - _accumulatedCompensation);
            
            float hComp = 0f;
            if (_horizontalCompensation > 0f && Math.Abs(dx) > 1f)
                hComp = dx * _horizontalCompensation;
            
            _accumulatedCompensation += vComp;
            _lastAppliedCompensation = vComp;
            _lastCompensationTime = now;
            
            var outDx = dx - hComp;
            var outDy = dy - vComp;
            
            return (outDx, outDy);
        }
        finally 
        { 
            _lock.ExitWriteLock();
            try { CompensationApplied?.Invoke(-dx + (dx - 0), -dy + (dy - 0)); } catch { }
        }
    }
    
    public void Reset()
    {
        if (_disposed) return;
        
        _lock.EnterWriteLock();
        try
        {
            _isActive = false;
            _isShootingDetected = false;
            _accumulatedCompensation = 0f;
        }
        finally { _lock.ExitWriteLock(); }
    }
    
    public string GetStatusInfo()
    {
        if (_disposed) return "Disposed";
        
        _lock.EnterReadLock();
        try
        {
            if (!_enabled) return "Disabled";
            if (!_isShootingDetected) return "Standby (waiting for fire button)";
            var ms = (DateTime.UtcNow - _activationStart).TotalMilliseconds;
            if (ms < _activationDelayMs) return $"Armed (delay: {_activationDelayMs - (int)ms}ms)";
            if (!_isActive) return "Ready (waiting for recoil)";
            return $"Active (comp: {_accumulatedCompensation:F1})";
        }
        finally { _lock.ExitReadLock(); }
    }
    
    public string GetDiagnosticInfo()
    {
        if (_disposed) return "RecoilProcessor: Disposed";
        
        _lock.EnterReadLock();
        try
        {
            return string.Join('\n',
                $"Enabled: {_enabled}",
                $"Shooting: {_isShootingDetected}",
                $"Active: {_isActive}",
                $"Strength: {_strength:P0}",
                $"Delay: {_activationDelayMs}ms",
                $"Threshold: {_verticalThreshold:F1}",
                $"HComp: {_horizontalCompensation:P0}",
                $"Adaptive: {_adaptiveCompensation}",
                $"AccumComp: {_accumulatedCompensation:F2}",
                $"LastApplied: {_lastAppliedCompensation:F2}",
                $"MaxTick: {_maxTickCompensation:F1}",
                $"MaxTotal: {_maxTotalCompensation:F1}",
                $"Cooldown: {_cooldownMs}ms",
                $"Decay: {_decayPerMs:F3}/ms");
        }
        finally { _lock.ExitReadLock(); }
    }
    
    public IReadOnlyList<float> GetRecentVerticalMovements()
    {
        if (_disposed) return new List<float>();
        return _recentVerticalBuffer.ToList();
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lock.Dispose();
    }
}