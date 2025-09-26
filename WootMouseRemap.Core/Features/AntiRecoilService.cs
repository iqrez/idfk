using System.Collections.ObjectModel;
using WootMouseRemap.Core.Services;
using WootMouseRemap.Features;

namespace WootMouseRemap.Core.Features;

/// <summary>
/// Orchestrates anti-recoil functionality using dependency injection and service separation
/// </summary>
public sealed class AntiRecoilService : IDisposable
{
    private readonly IRecoilProcessor _processor;
    private readonly IPatternRepository _patternRepository;
    private readonly IPatternRecorder _recorder;
    private readonly ISettingsManager<AntiRecoilSettings> _settingsManager;
    private bool _disposed;
    
    public ObservableCollection<AntiRecoilPattern> Patterns => _patternRepository.Patterns;
    
    // Events
    public event Action<float, float>? CompensationApplied;
    public event Action<AntiRecoilPattern>? PatternRecorded;
    public event Action? SettingsChanged;
    public event Action? RecordingStarted;
    public event Action? RecordingStopped;
    
    public AntiRecoilService(
        IRecoilProcessor processor,
        IPatternRepository patternRepository,
        IPatternRecorder recorder,
        ISettingsManager<AntiRecoilSettings> settingsManager)
    {
        _processor = processor;
        _patternRepository = patternRepository;
        _recorder = recorder;
        _settingsManager = settingsManager;
        
        // Wire up events
        _processor.CompensationApplied += OnCompensationApplied;
        _processor.SettingsChanged += OnSettingsChanged;
        _recorder.PatternRecorded += OnPatternRecorded;
        _recorder.RecordingStarted += OnRecordingStarted;
        _recorder.RecordingStopped += OnRecordingStopped;
        
        // Initialize
        _ = InitializeAsync();
    }
    
    private async Task InitializeAsync()
    {
        await _settingsManager.LoadSettingsAsync();
        await _patternRepository.LoadPatternsAsync();
        
        // Apply loaded settings to processor
        var settings = _settingsManager.Settings;
        _processor.Enabled = settings.Enabled;
        _processor.Strength = settings.Strength;
        _processor.ActivationDelayMs = settings.ActivationDelayMs;
        _processor.VerticalThreshold = settings.VerticalThreshold;
        _processor.HorizontalCompensation = settings.HorizontalCompensation;
        _processor.AdaptiveCompensation = settings.AdaptiveCompensation;
        _processor.MaxTickCompensation = settings.MaxTickCompensation;
        _processor.MaxTotalCompensation = settings.MaxTotalCompensation;
        _processor.CooldownMs = settings.CooldownMs;
        _processor.DecayPerMs = settings.DecayPerMs;
    }
    
    #region Processor Properties
    public bool Enabled
    {
        get => _processor.Enabled;
        set { _processor.Enabled = value; UpdateSettings(s => s.Enabled = value); }
    }
    
    public float Strength
    {
        get => _processor.Strength;
        set { _processor.Strength = value; UpdateSettings(s => s.Strength = value); }
    }
    
    public int ActivationDelayMs
    {
        get => _processor.ActivationDelayMs;
        set { _processor.ActivationDelayMs = value; UpdateSettings(s => s.ActivationDelayMs = value); }
    }
    
    public float VerticalThreshold
    {
        get => _processor.VerticalThreshold;
        set { _processor.VerticalThreshold = value; UpdateSettings(s => s.VerticalThreshold = value); }
    }
    
    public float HorizontalCompensation
    {
        get => _processor.HorizontalCompensation;
        set { _processor.HorizontalCompensation = value; UpdateSettings(s => s.HorizontalCompensation = value); }
    }
    
    public bool AdaptiveCompensation
    {
        get => _processor.AdaptiveCompensation;
        set { _processor.AdaptiveCompensation = value; UpdateSettings(s => s.AdaptiveCompensation = value); }
    }
    
    public float MaxTickCompensation
    {
        get => _processor.MaxTickCompensation;
        set { _processor.MaxTickCompensation = value; UpdateSettings(s => s.MaxTickCompensation = value); }
    }
    
    public float MaxTotalCompensation
    {
        get => _processor.MaxTotalCompensation;
        set { _processor.MaxTotalCompensation = value; UpdateSettings(s => s.MaxTotalCompensation = value); }
    }
    
    public int CooldownMs
    {
        get => _processor.CooldownMs;
        set { _processor.CooldownMs = value; UpdateSettings(s => s.CooldownMs = value); }
    }
    
    public float DecayPerMs
    {
        get => _processor.DecayPerMs;
        set { _processor.DecayPerMs = value; UpdateSettings(s => s.DecayPerMs = value); }
    }
    
    public bool IsActive => _processor.IsActive;
    public bool IsShootingDetected => _processor.IsShootingDetected;
    public float LastAppliedCompensation => _processor.LastAppliedCompensation;
    public float AccumulatedCompensation => _processor.AccumulatedCompensation;
    #endregion
    
    #region Processing
    public void OnShootingStarted() => _processor.OnShootingStarted();
    public void OnShootingStopped() => _processor.OnShootingStopped();
    
    public (float dx, float dy) ProcessMouseMovement(float dx, float dy)
    {
        if (_recorder.IsRecording)
            _recorder.RecordSample(dx, dy);
        
        return _processor.ProcessMouseMovement(dx, dy);
    }
    
    public void Reset() => _processor.Reset();
    public string GetStatusInfo() => _processor.GetStatusInfo();
    public string GetDiagnosticInfo() => _processor.GetDiagnosticInfo();
    public IReadOnlyList<float> GetRecentVerticalMovements() => _processor.GetRecentVerticalMovements();
    #endregion
    
    #region Pattern Management
    public async Task<AntiRecoilPattern?> GetPatternAsync(string name) => 
        await _patternRepository.GetPatternAsync(name);
    
    public async Task<bool> DeletePatternAsync(string name) => 
        await _patternRepository.DeletePatternAsync(name);
    
    public async Task ExportPatternAsync(AntiRecoilPattern pattern, string filePath) => 
        await _patternRepository.ExportPatternAsync(pattern, filePath);
    
    public async Task<AntiRecoilPattern?> ImportPatternAsync(string filePath) => 
        await _patternRepository.ImportPatternAsync(filePath);
    
    public AntiRecoilSimulationResult SimulatePattern(AntiRecoilPattern pattern)
    {
        var result = new AntiRecoilSimulationResult { PatternName = pattern.Name };
        float total = 0f;
        
        foreach (var s in pattern.Samples)
        {
            if (s.Dy <= _processor.VerticalThreshold)
            {
                result.Points.Add(new AntiRecoilSimPoint 
                { 
                    InputDx = s.Dx, InputDy = s.Dy, 
                    OutputDx = s.Dx, OutputDy = s.Dy, 
                    CompensationY = 0 
                });
                continue;
            }
            
            float vComp = s.Dy * _processor.Strength;
            float hComp = _processor.HorizontalCompensation > 0 ? s.Dx * _processor.HorizontalCompensation : 0f;
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
    
    #region Recording
    public bool IsRecordingPattern => _recorder.IsRecording;
    public int RecordingSampleCount => _recorder.SampleCount;
    
    public bool StartPatternRecording(string name) => _recorder.StartRecording(name);
    public AntiRecoilPattern? StopPatternRecording(bool save) => _recorder.StopRecording(save);
    #endregion
    
    #region Settings
    public async Task ExportSettingsAsync(string filePath) => 
        await _settingsManager.ExportSettingsAsync(filePath);
    
    public async Task ImportSettingsAsync(string filePath)
    {
        if (await _settingsManager.ImportSettingsAsync(filePath))
        {
            await InitializeAsync(); // Reapply settings to processor
        }
    }
    
    private void UpdateSettings(Action<AntiRecoilSettings> updateAction)
    {
        _settingsManager.UpdateSettings(updateAction);
    }
    #endregion
    
    #region Event Handlers
    private void OnCompensationApplied(float dx, float dy) => CompensationApplied?.Invoke(dx, dy);
    private void OnSettingsChanged() => SettingsChanged?.Invoke();
    private void OnPatternRecorded(AntiRecoilPattern pattern) => PatternRecorded?.Invoke(pattern);
    private void OnRecordingStarted() => RecordingStarted?.Invoke();
    private void OnRecordingStopped() => RecordingStopped?.Invoke();
    #endregion
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        if (_processor is IDisposable disposableProcessor)
            disposableProcessor.Dispose();
        if (_patternRepository is IDisposable disposableRepo)
            disposableRepo.Dispose();
        if (_settingsManager is IDisposable disposableSettings)
            disposableSettings.Dispose();
    }
}