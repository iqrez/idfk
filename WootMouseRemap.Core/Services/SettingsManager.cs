using System.Text.Json;
// Logger is in WootMouseRemap namespace

namespace WootMouseRemap.Core.Services;

public sealed class SettingsManager<T> : ISettingsManager<T> where T : class, new()
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly string _filePath;
    private T _settings = new();
    private bool _disposed;
    
    public T Settings
    {
        get
        {
            _lock.EnterReadLock();
            try { return _settings; }
            finally { _lock.ExitReadLock(); }
        }
    }
    
    public event Action<T>? SettingsChanged;
    
    public SettingsManager(string filePath)
    {
        _filePath = filePath;
    }
    
    public async Task LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            
            var json = await File.ReadAllTextAsync(_filePath);
            
            // Handle migration for AntiRecoilSettings
            T? settings;
            if (typeof(T) == typeof(AntiRecoilSettings))
            {
                var migratedSettings = SettingsMigrator.MigrateSettings(json);
                settings = (T)(object)migratedSettings;
            }
            else
            {
                var options = new JsonSerializerOptions 
                { 
                    MaxDepth = 5,
                    PropertyNameCaseInsensitive = false,
                    AllowTrailingCommas = false
                };
                try 
                {
                    settings = JsonSerializer.Deserialize<T>(json, options) ?? new T();
                }
                catch (JsonException) 
                {
                    settings = new T(); // Safe fallback
                }
            }
            
            if (settings != null)
            {
                _lock.EnterWriteLock();
                try
                {
                    _settings = settings;
                }
                finally { _lock.ExitWriteLock(); }
                
                SettingsChanged?.Invoke(_settings);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load settings from {FilePath}", _filePath, ex);
        }
    }
    
    public async Task SaveSettingsAsync()
    {
        try
        {
            T settingsToSave;
            _lock.EnterReadLock();
            try
            {
                settingsToSave = _settings;
            }
            finally { _lock.ExitReadLock(); }
            
            var json = JsonSerializer.Serialize(settingsToSave, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to save settings to {FilePath}", _filePath, ex);
        }
    }
    
    public async Task<bool> ImportSettingsAsync(string filePath)
    {
        try
        {
            var validation = FileValidator.ValidateSettingsFile<T>(filePath);
            if (!validation.IsValid)
            {
                Logger.Error("Settings file validation failed: {ErrorMessage}", validation.ErrorMessage ?? "Unknown error");
                return false;
            }
            
            var json = await File.ReadAllTextAsync(filePath);
            T settings;
            var options = new JsonSerializerOptions 
            { 
                MaxDepth = 5,
                PropertyNameCaseInsensitive = false,
                AllowTrailingCommas = false
            };
            try 
            {
                settings = JsonSerializer.Deserialize<T>(json, options) ?? new T();
            }
            catch (JsonException) 
            {
                settings = new T(); // Safe fallback
            }
            
            if (settings != null)
            {
                _lock.EnterWriteLock();
                try
                {
                    _settings = settings;
                }
                finally { _lock.ExitWriteLock(); }
                
                await SaveSettingsAsync();
                SettingsChanged?.Invoke(_settings);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to import settings from {FilePath}", filePath, ex);
            return false;
        }
    }
    
    public async Task ExportSettingsAsync(string filePath)
    {
        T settingsToExport;
        _lock.EnterReadLock();
        try
        {
            settingsToExport = _settings;
        }
        finally { _lock.ExitReadLock(); }
        
        var json = JsonSerializer.Serialize(settingsToExport, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }
    
    public void UpdateSettings(Action<T> updateAction)
    {
        _lock.EnterWriteLock();
        try
        {
            updateAction(_settings);
        }
        finally { _lock.ExitWriteLock(); }
        
        _ = Task.Run(SaveSettingsAsync);
        SettingsChanged?.Invoke(_settings);
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lock.Dispose();
    }
}