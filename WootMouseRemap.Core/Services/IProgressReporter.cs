namespace WootMouseRemap.Core.Services;

/// <summary>
/// Interface for reporting progress of long-running operations
/// </summary>
public interface IProgressReporter
{
    void ReportProgress(int percentage, string? message = null);
    void ReportCompleted(string? message = null);
    void ReportError(string errorMessage);
    bool IsCancellationRequested { get; }
}

/// <summary>
/// Simple progress reporter implementation
/// </summary>
public class ProgressReporter : IProgressReporter
{
    private readonly IProgress<ProgressInfo>? _progress;
    private readonly CancellationToken _cancellationToken;
    
    public bool IsCancellationRequested => _cancellationToken.IsCancellationRequested;
    
    public ProgressReporter(IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default)
    {
        _progress = progress;
        _cancellationToken = cancellationToken;
    }
    
    public void ReportProgress(int percentage, string? message = null)
    {
        _progress?.Report(new ProgressInfo(percentage, message, false, null));
    }
    
    public void ReportCompleted(string? message = null)
    {
        _progress?.Report(new ProgressInfo(100, message, true, null));
    }
    
    public void ReportError(string errorMessage)
    {
        _progress?.Report(new ProgressInfo(0, null, true, errorMessage));
    }
}

public record ProgressInfo(int Percentage, string? Message, bool IsCompleted, string? ErrorMessage);