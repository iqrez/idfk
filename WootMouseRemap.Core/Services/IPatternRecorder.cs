using WootMouseRemap.Features;

namespace WootMouseRemap.Core.Services;

public interface IPatternRecorder
{
    bool IsRecording { get; }
    int SampleCount { get; }
    string? CurrentRecordingName { get; }
    
    event Action? RecordingStarted;
    event Action? RecordingStopped;
    event Action<AntiRecoilPattern>? PatternRecorded;
    
    bool StartRecording(string name);
    AntiRecoilPattern? StopRecording(bool save = true);
    void RecordSample(float dx, float dy);
    void ClearRecording();
}