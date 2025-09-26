using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WootMouseRemap.Diagnostics;
using WootMouseRemap.Security;

namespace WootMouseRemap.Features
{
    /// <summary>
    /// Configuration settings for the anti-recoil system
    /// </summary>
    public class AntiRecoilSettings
    {
        public int Version { get; set; } = 2;
        public bool Enabled { get; set; } = false;
        public float Strength { get; set; } = 0.5f;
        public int ActivationDelayMs { get; set; } = 50;
        public float VerticalThreshold { get; set; } = 2.0f;
        public float HorizontalCompensation { get; set; } = 0.0f;
        public bool AdaptiveCompensation { get; set; } = false;

        // Advanced settings (Version 2+)
        public float MaxTickCompensation { get; set; } = 10.0f;
        public float MaxTotalCompensation { get; set; } = 100.0f;
        public int CooldownMs { get; set; } = 0;
        public float DecayPerMs { get; set; } = 0.0f;
    }

    /// <summary>
    /// Anti-recoil system that counteracts vertical mouse movement (recoil) by applying 
    /// compensating downward movement when upward movement is detected during shooting.
    /// Features adjustable strength, delay, activation conditions and pattern recording.
    /// </summary>
    public sealed class AntiRecoil : IDisposable
    {
        private readonly object _lock = new();
        private bool _disposed;
        private const string SettingsFileName = "antirecoil_settings.json";
        private const string PatternStoreFile = "Profiles/anti_recoil_patterns.json";

        // Configuration
        private float _strength = 0.5f;              // 0.0 = off, 1.0 = 100% compensation
        private int _activationDelayMs = 50;         // Delay before anti-recoil kicks in
        private float _verticalThreshold = 2.0f;     // Minimum vertical movement to trigger
        private float _horizontalCompensation = 0.0f; // Horizontal recoil compensation
        private bool _adaptiveCompensation = false;   // Enable adaptive compensation
        private bool _enabled = false;

        // Advanced configuration
        private float _maxTickCompensation = 10.0f;   // Maximum compensation per tick
        private float _maxTotalCompensation = 100.0f; // Maximum total accumulated compensation
        private int _cooldownMs = 0;                  // Cooldown period after compensation
        private float _decayPerMs = 0.0f;            // Compensation decay rate per millisecond
        
        // State
        private bool _isActive;
        private DateTime _activationStart;
        private float _accumulatedCompensation;
        private bool _isShootingDetected;
        private DateTime _lastShotTime;
        private DateTime _lastCompensationTime;
        private float _lastAppliedCompensation;
        private readonly Queue<float> _recentVerticalMovements = new();
        private readonly Queue<float> _recentVerticalBuffer = new(120); // For telemetry

        // Pattern recording
        private bool _recording;
        private string _recordingName = string.Empty;
        private readonly List<(float dx,float dy)> _recordBuffer = new();
        private readonly List<AntiRecoilPattern> _patterns = new();

        // Events
        public event Action<float, float>? CompensationApplied; // (dx, dy) compensation values
        public event Action<AntiRecoilPattern>? PatternRecorded;
        public event Action? PatternListChanged;
        public event Action? RecordingStarted;
        public event Action? RecordingStopped;
        public event Action? SettingsChanged;

        public IReadOnlyList<AntiRecoilPattern> Patterns { get { lock(_lock) return _patterns.ToList(); } }

        public AntiRecoil()
        {
            LoadSettings();
            LoadPatterns();
            Logger.Info("AntiRecoil initialized");
        }
        
        #region Public Config Properties
        public bool Enabled
        {
            get { if (_disposed) return false; lock (_lock) return _enabled; }
            set
            {
                if (_disposed) return;
                lock (_lock)
                {
                    _enabled = value;
                    if (!value) Reset();
                }
                SaveSettings();
                SettingsChanged?.Invoke();
            }
        }
        public float Strength
        {
            get { if (_disposed) return 0f; lock (_lock) return _strength; }
            set { if (_disposed) return; lock (_lock) { _strength = Math.Clamp(value, 0f, 1f); } SaveSettings(); SettingsChanged?.Invoke(); }
        }
        public int ActivationDelayMs
        {
            get { if (_disposed) return 0; lock (_lock) return _activationDelayMs; }
            set { if (_disposed) return; lock (_lock) { _activationDelayMs = Math.Max(0, value); } SaveSettings(); SettingsChanged?.Invoke(); }
        }
        public float VerticalThreshold
        {
            get { if (_disposed) return 0f; lock (_lock) return _verticalThreshold; }
            set { if (_disposed) return; lock (_lock) { _verticalThreshold = Math.Max(0f, value); } SaveSettings(); SettingsChanged?.Invoke(); }
        }
        public float HorizontalCompensation
        {
            get { if (_disposed) return 0f; lock (_lock) return _horizontalCompensation; }
            set { if (_disposed) return; lock (_lock) { _horizontalCompensation = Math.Clamp(value, 0f, 1f); } SaveSettings(); SettingsChanged?.Invoke(); }
        }
        public bool AdaptiveCompensation
        {
            get { if (_disposed) return false; lock (_lock) return _adaptiveCompensation; }
            set { if (_disposed) return; lock (_lock) { _adaptiveCompensation = value; } SaveSettings(); SettingsChanged?.Invoke(); }
        }

        public float MaxTickCompensation
        {
            get { if (_disposed) return 0f; lock (_lock) return _maxTickCompensation; }
            set { if (_disposed) return; lock (_lock) { _maxTickCompensation = Math.Max(0f, value); } SaveSettings(); SettingsChanged?.Invoke(); }
        }

        public float MaxTotalCompensation
        {
            get { if (_disposed) return 0f; lock (_lock) return _maxTotalCompensation; }
            set { if (_disposed) return; lock (_lock) { _maxTotalCompensation = Math.Max(0f, value); } SaveSettings(); SettingsChanged?.Invoke(); }
        }

        public int CooldownMs
        {
            get { if (_disposed) return 0; lock (_lock) return _cooldownMs; }
            set { if (_disposed) return; lock (_lock) { _cooldownMs = Math.Max(0, value); } SaveSettings(); SettingsChanged?.Invoke(); }
        }

        public float DecayPerMs
        {
            get { if (_disposed) return 0f; lock (_lock) return _decayPerMs; }
            set { if (_disposed) return; lock (_lock) { _decayPerMs = Math.Max(0f, value); } SaveSettings(); SettingsChanged?.Invoke(); }
        }

        public float LastAppliedCompensation
        {
            get { if (_disposed) return 0f; lock (_lock) return _lastAppliedCompensation; }
        }

        public float AccumulatedCompensation
        {
            get { if (_disposed) return 0f; lock (_lock) return _accumulatedCompensation; }
        }
        #endregion

        #region Shooting Lifecycle
        public void OnShootingStarted()
        {
            if (_disposed || !_enabled)
            {
                return;
            }
            lock (_lock)
            {
                _isShootingDetected = true;
                _lastShotTime = DateTime.UtcNow;
                _activationStart = DateTime.UtcNow;
                _accumulatedCompensation = 0f;
                _recentVerticalMovements.Clear();
            }
        }
        public void OnShootingStopped()
        {
            if (_disposed) return;
            lock (_lock)
            {
                _isShootingDetected = false;
                _isActive = false;
            }
        }
        #endregion

        #region Pattern Recording API
        public bool IsRecordingPattern { get { lock(_lock) return _recording; } }
        public int RecordingSampleCount { get { lock(_lock) return _recordBuffer.Count; } }

        public bool StartPatternRecording(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 1000)
                throw new ArgumentException("Invalid input");
            if (_disposed) return false;
            lock(_lock)
            {
                if (_recording) return false;
                _recording = true;
                _recordingName = name.Trim();
                _recordBuffer.Clear();
                RecordingStarted?.Invoke();
                return true;
            }
        }

        public AntiRecoilPattern? StopPatternRecording(bool save)
        {
            AntiRecoilPattern? pattern = null;
            lock(_lock)
            {
                if (!_recording) return null;
                _recording = false;
                if (_recordBuffer.Count == 0)
                {
                    _recordingName = string.Empty;
                    RecordingStopped?.Invoke();
                    return null;
                }
                pattern = new AntiRecoilPattern
                {
                    Name = _recordingName,
                    CreatedUtc = DateTime.UtcNow,
                    Samples = _recordBuffer.Select(p => new AntiRecoilSample { Dx = p.dx, Dy = p.dy }).ToList()
                };
                _recordingName = string.Empty;
                _recordBuffer.Clear();
                RecordingStopped?.Invoke();
                if (save)
                {
                    var existing = _patterns.FindIndex(p => p.Name.Equals(pattern.Name, StringComparison.OrdinalIgnoreCase));
                    if (existing >= 0) _patterns[existing] = pattern; else _patterns.Add(pattern);
                    SavePatterns();
                    PatternListChanged?.Invoke();
                }
            }
            if (pattern != null) { try { PatternRecorded?.Invoke(pattern); } catch { } }
            return pattern;
        }

        public AntiRecoilPattern? GetPattern(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 1000)
                throw new ArgumentException("Invalid input");
            lock(_lock) return _patterns.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        public bool DeletePattern(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 1000)
                throw new ArgumentException("Invalid input");
            lock(_lock)
            {
                var idx = _patterns.FindIndex(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) return false;
                _patterns.RemoveAt(idx);
                SavePatterns();
                PatternListChanged?.Invoke();
                return true;
            }
        }
        public void ExportPattern(AntiRecoilPattern pattern, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || filePath.Length > 1000)
                throw new ArgumentException("Invalid input");
            
            // Path validation to prevent directory traversal attacks
            var validatedPath = SecurePathValidator.ValidateFilePath(filePath);
            if (validatedPath == null)
                throw new UnauthorizedAccessException("Invalid or unsafe path");

            var json = JsonSerializer.Serialize(pattern, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(validatedPath, json);

            // Open the exported file with the default application
            if (!SecureProcessLauncher.TryLaunchFile(validatedPath))
            {
                Logger.Warn("Failed to open exported pattern file: {FilePath}", validatedPath);
            }
        }

        public AntiRecoilPattern? ImportPattern(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || filePath.Length > 1000)
                throw new ArgumentException("Invalid input");
            
            // Path validation to prevent directory traversal attacks
            var validatedPath = SecurePathValidator.ValidateFilePath(filePath);
            if (validatedPath == null)
                throw new UnauthorizedAccessException("Invalid or unsafe path");

            try
            {
                if (!File.Exists(validatedPath)) return null;
                var json = File.ReadAllText(validatedPath);
                AntiRecoilPattern pattern;
                var options = new JsonSerializerOptions 
                { 
                    MaxDepth = 5,
                    PropertyNameCaseInsensitive = false,
                    AllowTrailingCommas = false
                };
                pattern = SecureJsonDeserializer.DeserializeSecure<AntiRecoilPattern>(json) ?? new AntiRecoilPattern();
                if (pattern?.Samples == null || pattern.Samples.Count == 0) return null;

                // Validate pattern data
                if (string.IsNullOrWhiteSpace(pattern.Name))
                    pattern.Name = Path.GetFileNameWithoutExtension(validatedPath);

                if (pattern.CreatedUtc == DateTime.MinValue)
                    pattern.CreatedUtc = DateTime.UtcNow;

                // Ensure version and collections are initialized
                pattern.Version = Math.Max(1, pattern.Version);
                pattern.Tags ??= new List<string>();

                lock (_lock)
                {
                    var existing = _patterns.FindIndex(p => p.Name.Equals(pattern.Name, StringComparison.OrdinalIgnoreCase));
                    if (existing >= 0)
                        _patterns[existing] = pattern;
                    else
                        _patterns.Add(pattern);

                    SavePatterns();
                    PatternListChanged?.Invoke();
                }

                return pattern;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to import pattern from {FilePath}", validatedPath, ex);
                return null;
            }
        }

        public AntiRecoilSimulationResult SimulatePattern(AntiRecoilPattern pattern)
        {
            var result = new AntiRecoilSimulationResult { PatternName = pattern.Name };
            float total = 0f;
            foreach (var s in pattern.Samples)
            {
                if (Math.Abs(s.Dy) <= _verticalThreshold)
                {
                    result.Points.Add(new AntiRecoilSimPoint { InputDx = s.Dx, InputDy = s.Dy, OutputDx = s.Dx, OutputDy = s.Dy, CompensationY = 0 });
                    continue;
                }
                float vComp = s.Dy * _strength;
                float hComp = _horizontalCompensation > 0 ? s.Dx * _horizontalCompensation : 0f;
                total += vComp;
                result.Points.Add(new AntiRecoilSimPoint
                {
                    InputDx = s.Dx,
                    InputDy = s.Dy,
                    OutputDx = s.Dx - hComp,
                    OutputDy = s.Dy - vComp,
                    CompensationY = vComp
                });
            }
            result.TotalCompY = total;
            result.AvgCompY = result.Points.Count > 0 ? result.Points.Average(p => p.CompensationY) : 0f;
            return result;
        }
        #endregion

        #region Core Processing
        public (float dx, float dy) ProcessMouseMovement(float dx, float dy)
        {
            if (_disposed || !_enabled) return (dx, dy);
            lock (_lock)
            {
                var now = DateTime.UtcNow;

                // Record raw when recording (cap to avoid runaway memory)
                if (_recording && _recordBuffer.Count < 5000)
                    _recordBuffer.Add((dx, dy));

                // Add to telemetry buffer
                _recentVerticalBuffer.Enqueue(dy);
                while (_recentVerticalBuffer.Count > 120) _recentVerticalBuffer.Dequeue();

                if (!_isShootingDetected) return (dx, dy);

                // Check if shooting session has timed out
                if ((now - _lastShotTime).TotalMilliseconds > 1000)
                {
                    _isShootingDetected = false;
                    _isActive = false;
                    return (dx, dy);
                }

                // Check activation delay
                var elapsed = (now - _activationStart).TotalMilliseconds;
                if (!SecureArithmetic.TryConvertToInt(elapsed, out int elapsedMs))
                {
                    Logger.Warn("Elapsed time conversion overflow, resetting activation");
                    _activationStart = now;
                    return (dx, dy);
                }
                if (elapsedMs < _activationDelayMs) return (dx, dy);

                // Check cooldown period
                if (_cooldownMs > 0)
                {
                    var cooldownElapsed = (now - _lastCompensationTime).TotalMilliseconds;
                    if (!SecureArithmetic.TryConvertToInt(cooldownElapsed, out int cooldownElapsedMs))
                    {
                        Logger.Warn("Cooldown elapsed time conversion overflow, resetting compensation time");
                        _lastCompensationTime = now;
                        return (dx, dy);
                    }
                    if (cooldownElapsedMs < _cooldownMs)
                        return (dx, dy);
                }

                // Apply decay to accumulated compensation
                if (_decayPerMs > 0 && _accumulatedCompensation > 0)
                {
                    var timeSinceLastComp = (now - _lastCompensationTime).TotalMilliseconds;
                    if (timeSinceLastComp > 0 && (!_isShootingDetected || dy <= _verticalThreshold))
                    {
                        if (SecureArithmetic.TryMultiply((float)timeSinceLastComp, _decayPerMs, out float decay))
                        {
                            _accumulatedCompensation = Math.Max(0f, _accumulatedCompensation - decay);
                        }
                        else
                        {
                            Logger.Warn("Decay calculation overflow, resetting accumulated compensation");
                            _accumulatedCompensation = 0f;
                        }
                    }
                }

                _recentVerticalMovements.Enqueue(dy);
                while (_recentVerticalMovements.Count > 10) _recentVerticalMovements.Dequeue();

                // Check if vertical movement meets threshold
                if (Math.Abs(dy) <= _verticalThreshold) return (dx, dy);

                _isActive = true;
                if (!SecureArithmetic.TryMultiply(dy, _strength, out float vComp))
                {
                    Logger.Warn("Vertical compensation calculation overflow, using clamped value");
                    vComp = Math.Sign(dy) * Math.Min(Math.Abs(dy), 100f) * Math.Min(_strength, 1f);
                }

                // Apply adaptive compensation
                if (_adaptiveCompensation && _recentVerticalMovements.Count >= 3)
                {
                    var avg = _recentVerticalMovements.Average();
                    if (avg > _verticalThreshold && SecureArithmetic.IsWithinBounds(avg) && _verticalThreshold > 0)
                    {
                        var multiplier = Math.Min(2f, avg / _verticalThreshold);
                        if (SecureArithmetic.TryMultiply(vComp, multiplier, out float adaptiveVComp))
                        {
                            vComp = adaptiveVComp;
                        }
                        else
                        {
                            Logger.Warn("Adaptive compensation calculation overflow, using original value");
                        }
                    }
                }

                // Apply per-tick compensation cap
                if (_maxTickCompensation > 0 && vComp > _maxTickCompensation)
                    vComp = _maxTickCompensation;

                // Apply total compensation cap
                if (_maxTotalCompensation > 0)
                {
                    if (SecureArithmetic.TryAdd(_accumulatedCompensation, vComp, out float totalComp))
                    {
                        if (totalComp > _maxTotalCompensation)
                            vComp = Math.Max(0f, _maxTotalCompensation - _accumulatedCompensation);
                    }
                    else
                    {
                        Logger.Warn("Total compensation calculation overflow, capping compensation");
                        vComp = Math.Max(0f, _maxTotalCompensation - _accumulatedCompensation);
                    }
                }

                float hComp = 0f;
                if (_horizontalCompensation > 0f && Math.Abs(dx) > 1f)
                {
                    if (!SecureArithmetic.TryMultiply(dx, _horizontalCompensation, out hComp))
                    {
                        Logger.Warn("Horizontal compensation calculation overflow, using clamped value");
                        hComp = Math.Sign(dx) * Math.Min(Math.Abs(dx), 100f) * Math.Min(_horizontalCompensation, 1f);
                    }
                }

                if (!SecureArithmetic.TryAdd(_accumulatedCompensation, vComp, out float newAccumulated))
                {
                    Logger.Warn("Accumulated compensation overflow, capping at maximum");
                    _accumulatedCompensation = Math.Min(_maxTotalCompensation > 0 ? _maxTotalCompensation : 1000f, _accumulatedCompensation + Math.Min(vComp, 100f));
                }
                else
                {
                    _accumulatedCompensation = newAccumulated;
                }
                _lastAppliedCompensation = vComp;
                _lastCompensationTime = now;

                var outDx = dx - hComp;
                var outDy = dy - vComp;
                
                // Validate output values
                if (!SecureArithmetic.IsWithinBounds(outDx, -10000f, 10000f) || !SecureArithmetic.IsWithinBounds(outDy, -10000f, 10000f))
                {
                    Logger.Warn("Output values out of bounds, using original input");
                    return (dx, dy);
                }
                CompensationApplied?.Invoke(-hComp, -vComp);
                return (outDx, outDy);
            }
        }
        #endregion

        #region Status / Diagnostics
        public void Reset()
        {
            if (_disposed) return;
            lock (_lock)
            {
                _isActive = false;
                _isShootingDetected = false;
                _accumulatedCompensation = 0f;
            }
        }
        public string GetStatusInfo()
        {
            if (_disposed) return "Disposed";
            lock (_lock)
            {
                if (!_enabled) return "Disabled";
                if (!_isShootingDetected) return "Standby (waiting for fire button)";
                var ms = (DateTime.UtcNow - _activationStart).TotalMilliseconds;
                if (SecureArithmetic.TryConvertToInt(ms, out int msInt) && msInt < _activationDelayMs) 
                {
                    if (SecureArithmetic.TrySubtract(_activationDelayMs, msInt, out int remainingDelay))
                    {
                        return $"Armed (delay: {remainingDelay}ms)";
                    }
                    else
                    {
                        return "Armed (delay calculation overflow)";
                    }
                }
                if (!_isActive) return "Ready (waiting for recoil)";
                return $"Active (comp: {_accumulatedCompensation:F1})";
            }
        }
        public string GetDiagnosticInfo()
        {
            if (_disposed) return "AntiRecoil: Disposed";
            lock (_lock)
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
                    $"Decay: {_decayPerMs:F3}/ms",
                    $"Recording: {_recording} ({_recordBuffer.Count} samples)",
                    $"Patterns: {_patterns.Count}");
            }
        }
        public bool IsActive { get { if (_disposed) return false; lock (_lock) return _isActive; } }
        public bool IsShootingDetected { get { if (_disposed) return false; lock (_lock) return _isShootingDetected; } }

        /// <summary>
        /// Get a snapshot of recent vertical movements for telemetry display
        /// </summary>
        public IReadOnlyList<float> GetRecentVerticalMovements()
        {
            if (_disposed) return new List<float>();
            lock (_lock) return _recentVerticalBuffer.ToList();
        }
        #endregion

        #region Persistence (Settings & Patterns)
        public void SaveSettings()
        {
            try
            {
                var settings = new AntiRecoilSettings
                {
                    Version = 2,
                    Enabled = _enabled,
                    Strength = _strength,
                    ActivationDelayMs = _activationDelayMs,
                    VerticalThreshold = _verticalThreshold,
                    HorizontalCompensation = _horizontalCompensation,
                    AdaptiveCompensation = _adaptiveCompensation,
                    MaxTickCompensation = _maxTickCompensation,
                    MaxTotalCompensation = _maxTotalCompensation,
                    CooldownMs = _cooldownMs,
                    DecayPerMs = _decayPerMs
                };
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFileName, json);
            }
            catch (Exception ex) { Logger.Error("Failed to save anti-recoil settings", ex); }
        }
        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFileName)) return;
                var json = File.ReadAllText(SettingsFileName);
                var settings = new AntiRecoilSettings();
                settings = SecureJsonDeserializer.DeserializeSecure<AntiRecoilSettings>(json) ?? new AntiRecoilSettings();
                if (settings != null)
                {
                    _enabled = settings.Enabled;
                    _strength = Math.Clamp(settings.Strength, 0f, 1f);
                    _activationDelayMs = Math.Max(0, settings.ActivationDelayMs);
                    _verticalThreshold = Math.Max(0f, settings.VerticalThreshold);
                    _horizontalCompensation = Math.Clamp(settings.HorizontalCompensation, 0f, 1f);
                    _adaptiveCompensation = settings.AdaptiveCompensation;

                    // Load advanced settings with fallback defaults for older versions
                    _maxTickCompensation = Math.Max(0f, settings.MaxTickCompensation);
                    _maxTotalCompensation = Math.Max(0f, settings.MaxTotalCompensation);
                    _cooldownMs = Math.Max(0, settings.CooldownMs);
                    _decayPerMs = Math.Max(0f, settings.DecayPerMs);
                }
            }
            catch (Exception ex) { Logger.Error("Failed to load anti-recoil settings", ex); }
        }
        private void LoadPatterns()
        {
            try
            {
                var patternDir = Path.GetDirectoryName(PatternStoreFile);
                if (patternDir != null && !SecurePathValidator.EnsureSafeDirectory(patternDir))
                {
                    Logger.Error("Failed to create or validate patterns directory: {Directory}", patternDir);
                    return;
                }
                if (!File.Exists(PatternStoreFile)) return;
                var json = File.ReadAllText(PatternStoreFile);
                var list = new List<AntiRecoilPattern>();
                list = SecureJsonDeserializer.DeserializeSecure<List<AntiRecoilPattern>>(json) ?? new List<AntiRecoilPattern>();
                if (list != null) _patterns.AddRange(list.Where(p => p?.Samples != null));
            }
            catch (Exception ex) { Logger.Error("Failed loading anti-recoil patterns", ex); }
        }
        private void SavePatterns()
        {
            try
            {
                var json = JsonSerializer.Serialize(_patterns, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(PatternStoreFile, json);
            }
            catch (Exception ex) { Logger.Error("Failed saving anti-recoil patterns", ex); }
        }

        /// <summary>
        /// Export current settings to a JSON file
        /// </summary>
        public void ExportSettings(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || filePath.Length > 1000)
                throw new ArgumentException("Invalid input");
            
            // Path validation to prevent directory traversal attacks
            var validatedPath = SecurePathValidator.ValidateFilePath(filePath);
            if (validatedPath == null)
                throw new UnauthorizedAccessException("Invalid or unsafe path");

            lock (_lock)
            {
                var settings = new AntiRecoilSettings
                {
                    Enabled = _enabled,
                    Strength = _strength,
                    ActivationDelayMs = _activationDelayMs,
                    VerticalThreshold = _verticalThreshold,
                    HorizontalCompensation = _horizontalCompensation,
                    AdaptiveCompensation = _adaptiveCompensation,
                    MaxTickCompensation = _maxTickCompensation,
                    MaxTotalCompensation = _maxTotalCompensation,
                    CooldownMs = _cooldownMs,
                    DecayPerMs = _decayPerMs
                };

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(validatedPath, json);
                Logger.Info("Settings exported to {FilePath}", validatedPath);

                // Open the exported file with the default application
                if (!SecureProcessLauncher.TryLaunchFile(validatedPath))
                {
                    Logger.Warn("Failed to open exported settings file: {FilePath}", validatedPath);
                }
            }
        }

        /// <summary>
        /// Import settings from a JSON file
        /// </summary>
        public void ImportSettings(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || filePath.Length > 1000)
                throw new ArgumentException("Invalid input");
            
            // Path validation to prevent directory traversal attacks
            var validatedPath = SecurePathValidator.ValidateFilePath(filePath);
            if (validatedPath == null)
                throw new UnauthorizedAccessException("Invalid or unsafe path");

            if (!File.Exists(validatedPath))
                throw new FileNotFoundException($"Settings file not found: {validatedPath}");

            lock (_lock)
            {
                var json = File.ReadAllText(validatedPath);
                AntiRecoilSettings settings;
                var options = new JsonSerializerOptions 
                { 
                    MaxDepth = 5,
                    PropertyNameCaseInsensitive = false,
                    AllowTrailingCommas = false
                };
                settings = SecureJsonDeserializer.DeserializeSecure<AntiRecoilSettings>(json) ?? new AntiRecoilSettings();

                if (settings != null)
                {
                    _enabled = settings.Enabled;
                    _strength = Math.Clamp(settings.Strength, 0f, 1f);
                    _activationDelayMs = Math.Max(0, settings.ActivationDelayMs);
                    _verticalThreshold = Math.Max(0f, settings.VerticalThreshold);
                    _horizontalCompensation = settings.HorizontalCompensation;
                    _adaptiveCompensation = settings.AdaptiveCompensation;
                    _maxTickCompensation = Math.Max(0f, settings.MaxTickCompensation);
                    _maxTotalCompensation = Math.Max(0f, settings.MaxTotalCompensation);
                    _cooldownMs = Math.Max(0, settings.CooldownMs);
                    _decayPerMs = Math.Max(0f, settings.DecayPerMs);

                    SaveSettings();
                    SettingsChanged?.Invoke();
                    Logger.Info("Settings imported from {FilePath}", validatedPath);
                }
            }
        }
        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            lock (_lock)
            {
                _disposed = true;
                Reset();
            }
        }
    }

    #region Pattern Data Classes
    public class AntiRecoilPattern
    {
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public List<AntiRecoilSample> Samples { get; set; } = new();
        public string? Notes { get; set; }
        public List<string> Tags { get; set; } = new();
        public int Version { get; set; } = 1;
    }
    public class AntiRecoilSample { public float Dx { get; set; } public float Dy { get; set; } }
    public class AntiRecoilSimulationResult
    {
        public string PatternName { get; set; } = string.Empty;
        public List<AntiRecoilSimPoint> Points { get; set; } = new();
        public float TotalCompY { get; set; }
        public float AvgCompY { get; set; }
    }
    public class AntiRecoilSimPoint
    {
        public float InputDx { get; set; }
        public float InputDy { get; set; }
        public float OutputDx { get; set; }
        public float OutputDy { get; set; }
        public float CompensationY { get; set; }
    }
    #endregion
}
