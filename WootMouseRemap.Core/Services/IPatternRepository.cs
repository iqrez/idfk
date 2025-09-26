using System.Collections.ObjectModel;
using WootMouseRemap.Features;

namespace WootMouseRemap.Core.Services;

public interface IPatternRepository
{
    ObservableCollection<AntiRecoilPattern> Patterns { get; }
    
    event Action<AntiRecoilPattern>? PatternAdded;
    event Action<AntiRecoilPattern>? PatternUpdated;
    event Action<string>? PatternDeleted;
    
    Task<AntiRecoilPattern?> GetPatternAsync(string name);
    Task<bool> AddPatternAsync(AntiRecoilPattern pattern);
    Task<bool> UpdatePatternAsync(AntiRecoilPattern pattern);
    Task<bool> DeletePatternAsync(string name);
    Task<AntiRecoilPattern?> ImportPatternAsync(string filePath);
    Task ExportPatternAsync(AntiRecoilPattern pattern, string filePath);
    Task LoadPatternsAsync();
    Task SavePatternsAsync();
}