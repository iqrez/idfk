using System.Collections.ObjectModel;
using System.Text.Json;
// Logger is in WootMouseRemap namespace
using WootMouseRemap.Features;

namespace WootMouseRemap.Core.Services;

public sealed class PatternRepository : IPatternRepository, IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly ObservableCollection<AntiRecoilPattern> _patterns = new();
    private readonly string _filePath;
    private bool _disposed;
    
    public ObservableCollection<AntiRecoilPattern> Patterns => _patterns;
    
    public event Action<AntiRecoilPattern>? PatternAdded;
    public event Action<AntiRecoilPattern>? PatternUpdated;
    public event Action<string>? PatternDeleted;
    
    public PatternRepository(string filePath = "Profiles/anti_recoil_patterns.json")
    {
        _filePath = filePath;
    }
    
    public Task<AntiRecoilPattern?> GetPatternAsync(string name)
    {
        _lock.EnterReadLock();
        try
        {
            var result = _patterns.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(result);
        }
        finally { _lock.ExitReadLock(); }
    }
    
    public async Task<bool> AddPatternAsync(AntiRecoilPattern pattern)
    {
        if (pattern?.Samples == null || pattern.Samples.Count == 0) return false;
        
        _lock.EnterWriteLock();
        try
        {
            var existing = _patterns.FirstOrDefault(p => p.Name.Equals(pattern.Name, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return false;
            
            _patterns.Add(pattern);
            await SavePatternsAsync();
            PatternAdded?.Invoke(pattern);
            return true;
        }
        finally { _lock.ExitWriteLock(); }
    }
    
    public async Task<bool> UpdatePatternAsync(AntiRecoilPattern pattern)
    {
        if (pattern?.Samples == null || pattern.Samples.Count == 0) return false;
        
        _lock.EnterWriteLock();
        try
        {
            var index = _patterns.ToList().FindIndex(p => p.Name.Equals(pattern.Name, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return false;
            
            _patterns[index] = pattern;
            await SavePatternsAsync();
            PatternUpdated?.Invoke(pattern);
            return true;
        }
        finally { _lock.ExitWriteLock(); }
    }
    
    public async Task<bool> DeletePatternAsync(string name)
    {
        _lock.EnterWriteLock();
        try
        {
            var pattern = _patterns.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (pattern == null) return false;
            
            _patterns.Remove(pattern);
            await SavePatternsAsync();
            PatternDeleted?.Invoke(name);
            return true;
        }
        finally { _lock.ExitWriteLock(); }
    }
    
    public async Task<AntiRecoilPattern?> ImportPatternAsync(string filePath)
    {
        try
        {
            var validation = FileValidator.ValidatePatternFile(filePath);
            if (!validation.IsValid)
            {
                Logger.Error("Pattern file validation failed: {ErrorMessage}", validation.ErrorMessage ?? "Unknown error");
                return null;
            }
            
            var json = await File.ReadAllTextAsync(filePath);
            AntiRecoilPattern pattern;
            var options = new JsonSerializerOptions 
            { 
                MaxDepth = 5,
                PropertyNameCaseInsensitive = false,
                AllowTrailingCommas = false
            };
            try 
            {
                pattern = JsonSerializer.Deserialize<AntiRecoilPattern>(json, options) ?? new AntiRecoilPattern();
            }
            catch (JsonException) 
            {
                pattern = new AntiRecoilPattern(); // Safe fallback
            }
            if (pattern?.Samples == null || pattern.Samples.Count == 0) return null;
            
            if (string.IsNullOrWhiteSpace(pattern.Name))
                pattern.Name = Path.GetFileNameWithoutExtension(filePath);
            
            if (pattern.CreatedUtc == DateTime.MinValue)
                pattern.CreatedUtc = DateTime.UtcNow;
            
            pattern.Version = Math.Max(1, pattern.Version);
            pattern.Tags ??= new List<string>();
            
            _lock.EnterWriteLock();
            try
            {
                var existing = _patterns.FirstOrDefault(p => p.Name.Equals(pattern.Name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    var index = _patterns.IndexOf(existing);
                    _patterns[index] = pattern;
                    PatternUpdated?.Invoke(pattern);
                }
                else
                {
                    _patterns.Add(pattern);
                    PatternAdded?.Invoke(pattern);
                }
                
                await SavePatternsAsync();
                return pattern;
            }
            finally { _lock.ExitWriteLock(); }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to import pattern from {FilePath}", filePath, ex);
            return null;
        }
    }
    
    public async Task ExportPatternAsync(AntiRecoilPattern pattern, string filePath)
    {
        // Validate file path to prevent path traversal attacks
        var validation = FileValidator.ValidateExportFilePath(filePath);
        if (!validation.IsValid)
        {
            Logger.Error("Pattern export failed: {ErrorMessage}", validation.ErrorMessage ?? "Unknown error");
            throw new InvalidOperationException(validation.ErrorMessage);
        }
        
        var json = JsonSerializer.Serialize(pattern, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }
    
    public async Task LoadPatternsAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            if (!File.Exists(_filePath)) return;
            
            var json = await File.ReadAllTextAsync(_filePath);
            List<AntiRecoilPattern> patterns;
            var options = new JsonSerializerOptions 
            { 
                MaxDepth = 5,
                PropertyNameCaseInsensitive = false,
                AllowTrailingCommas = false
            };
            try 
            {
                patterns = JsonSerializer.Deserialize<List<AntiRecoilPattern>>(json, options) ?? new List<AntiRecoilPattern>();
            }
            catch (JsonException) 
            {
                patterns = new List<AntiRecoilPattern>(); // Safe fallback
            }
            
            if (patterns != null)
            {
                _lock.EnterWriteLock();
                try
                {
                    _patterns.Clear();
                    foreach (var pattern in patterns.Where(p => p?.Samples != null))
                    {
                        _patterns.Add(pattern);
                    }
                }
                finally { _lock.ExitWriteLock(); }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed loading anti-recoil patterns", ex);
        }
    }
    
    public async Task SavePatternsAsync()
    {
        try
        {
            _lock.EnterReadLock();
            List<AntiRecoilPattern> patternsToSave;
            try
            {
                patternsToSave = _patterns.ToList();
            }
            finally { _lock.ExitReadLock(); }
            
            var json = JsonSerializer.Serialize(patternsToSave, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed saving anti-recoil patterns", ex);
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lock.Dispose();
    }
}