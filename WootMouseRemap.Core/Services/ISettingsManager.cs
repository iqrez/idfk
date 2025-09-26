using WootMouseRemap.Features;

namespace WootMouseRemap.Core.Services;

public interface ISettingsManager<T> where T : class, new()
{
    T Settings { get; }
    
    event Action<T>? SettingsChanged;
    
    Task LoadSettingsAsync();
    Task SaveSettingsAsync();
    Task<bool> ImportSettingsAsync(string filePath);
    Task ExportSettingsAsync(string filePath);
    void UpdateSettings(Action<T> updateAction);
}