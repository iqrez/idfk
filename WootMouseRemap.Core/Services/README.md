# Anti-Recoil Service Architecture

This document outlines the refactored anti-recoil architecture using dependency injection and service separation.

## Architecture Overview

The original monolithic `AntiRecoil` class has been decomposed into focused services:

### Core Services

1. **IRecoilProcessor** - Handles the core recoil compensation logic
2. **IPatternRepository** - Manages pattern storage with ObservableCollection for data binding
3. **IPatternRecorder** - Handles pattern recording functionality
4. **ISettingsManager<T>** - Generic settings management with async operations

### Key Improvements

#### 1. Dependency Injection
- Services are registered using `ServiceCollectionExtensions.AddAntiRecoilServices()`
- Easy to swap implementations for testing or different platforms
- Clear separation of concerns

#### 2. Async Operations
- All file I/O operations are now async to prevent UI blocking
- Pattern loading/saving uses `File.ReadAllTextAsync` and `File.WriteAllTextAsync`
- Settings import/export operations are non-blocking

#### 3. Better Concurrency
- `ReaderWriterLockSlim` used in `RecoilProcessor` for better read performance
- `ConcurrentQueue<T>` used for telemetry buffers
- Events are fired outside of locks to prevent deadlocks

#### 4. Data Binding Support
- `PatternRepository` exposes `ObservableCollection<AntiRecoilPattern>`
- UI automatically updates when patterns change
- No need for manual list copying

#### 5. Performance Optimizations
- `OptimizedPatternTransforms` uses `Span<T>` for better memory efficiency
- Single-pass statistics calculation
- Reduced allocations in hot paths

## Usage Example

```csharp
// Setup DI container
var services = new ServiceCollection();
services.AddAntiRecoilServices();
services.AddSingleton<AntiRecoilService>();
var serviceProvider = services.BuildServiceProvider();

// Get the orchestrating service
var antiRecoilService = serviceProvider.GetRequiredService<AntiRecoilService>();

// Use the service
antiRecoilService.Enabled = true;
antiRecoilService.Strength = 0.7f;

// Process mouse movement
var (dx, dy) = antiRecoilService.ProcessMouseMovement(inputDx, inputDy);

// Access patterns (automatically updates UI via data binding)
var patterns = antiRecoilService.Patterns; // ObservableCollection
```

## Migration Path

1. **Phase 1**: Update constructors to accept `AntiRecoilService` instead of `AntiRecoil`
2. **Phase 2**: Update UI forms to use data binding with `ObservableCollection`
3. **Phase 3**: Replace synchronous file operations with async equivalents
4. **Phase 4**: Add progress reporting for long-running operations

## Testing Benefits

- Mock individual services for unit testing
- Test recoil logic independently of file I/O
- Test pattern management without UI dependencies
- Concurrent access testing is easier with separated concerns

## Future Enhancements

- Add `IPatternAnalyzer` service for ML-based pattern optimization
- Implement `ICloudPatternSync` for pattern sharing
- Add `IGameDetector` service for automatic profile switching
- Create `ITelemetryService` for advanced monitoring