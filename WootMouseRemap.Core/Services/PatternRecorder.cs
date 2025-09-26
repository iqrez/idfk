using WootMouseRemap.Features;

namespace WootMouseRemap.Core.Services;

public sealed class PatternRecorder : IPatternRecorder
{
    private readonly object _lock = new();
    private readonly List<(float dx, float dy)> _recordBuffer = new();
    private bool _isRecording;
    private string? _currentRecordingName;
    
    public bool IsRecording
    {
        get { lock (_lock) return _isRecording; }
    }
    
    public int SampleCount
    {
        get { lock (_lock) return _recordBuffer.Count; }
    }
    
    public string? CurrentRecordingName
    {
        get { lock (_lock) return _currentRecordingName; }
    }
    
    public event Action? RecordingStarted;
    public event Action? RecordingStopped;
    public event Action<AntiRecoilPattern>? PatternRecorded;
    
    public bool StartRecording(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            name = $"Pattern_{DateTime.Now:HHmmss}";
        
        lock (_lock)
        {
            if (_isRecording) return false;
            
            _isRecording = true;
            _currentRecordingName = name.Trim();
            _recordBuffer.Clear();
        }
        
        RecordingStarted?.Invoke();
        return true;
    }
    
    public AntiRecoilPattern? StopRecording(bool save = true)
    {
        AntiRecoilPattern? pattern = null;
        
        lock (_lock)
        {
            if (!_isRecording) return null;
            
            _isRecording = false;
            
            if (_recordBuffer.Count == 0)
            {
                _currentRecordingName = null;
                RecordingStopped?.Invoke();
                return null;
            }
            
            pattern = new AntiRecoilPattern
            {
                Name = _currentRecordingName!,
                CreatedUtc = DateTime.UtcNow,
                Samples = _recordBuffer.Select(p => new AntiRecoilSample { Dx = p.dx, Dy = p.dy }).ToList()
            };
            
            _currentRecordingName = null;
            _recordBuffer.Clear();
        }
        
        RecordingStopped?.Invoke();
        
        if (save && pattern != null)
        {
            PatternRecorded?.Invoke(pattern);
        }
        
        return pattern;
    }
    
    public void RecordSample(float dx, float dy)
    {
        lock (_lock)
        {
            if (!_isRecording || _recordBuffer.Count >= 5000) return;
            _recordBuffer.Add((dx, dy));
        }
    }
    
    public void ClearRecording()
    {
        lock (_lock)
        {
            _recordBuffer.Clear();
        }
    }
}