using System.Collections.ObjectModel;

namespace WootMouseRemap.Core.Services;

public interface IRecoilProcessor
{
    bool Enabled { get; set; }
    float Strength { get; set; }
    int ActivationDelayMs { get; set; }
    float VerticalThreshold { get; set; }
    float HorizontalCompensation { get; set; }
    bool AdaptiveCompensation { get; set; }
    float MaxTickCompensation { get; set; }
    float MaxTotalCompensation { get; set; }
    int CooldownMs { get; set; }
    float DecayPerMs { get; set; }
    
    bool IsActive { get; }
    bool IsShootingDetected { get; }
    float LastAppliedCompensation { get; }
    float AccumulatedCompensation { get; }
    
    event Action<float, float>? CompensationApplied;
    event Action? SettingsChanged;
    
    void OnShootingStarted();
    void OnShootingStopped();
    (float dx, float dy) ProcessMouseMovement(float dx, float dy);
    void Reset();
    string GetStatusInfo();
    string GetDiagnosticInfo();
    IReadOnlyList<float> GetRecentVerticalMovements();
}