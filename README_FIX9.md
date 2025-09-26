# WootMouseRemap (fix9)

Changes:
- Single-instance via mutex.
- Better global exception handling with logging + crash dialog.
- ViGEm status log hooks.
- RawInput explicit Register() during startup.
- Added `app.manifest` (PerMonitorV2 DPI, asInvoker).
- Switched to `Microsoft.NET.Sdk` + bumped ViGEm client to `1.21.256` to silence warnings.

## Build
```powershell
dotnet restore
dotnet build -c Release
# Optional single-file publish
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=false
```


## fix18m_patched3 -> patched4 additions
- Mode toggle is also bound to **Middle Mouse** by default (and F8).
- **OS Suppression** now auto-syncs with mode: ON when outputting to controller, OFF in mouse/keyboard mode. Panic: Ctrl+Alt+Pause.
- **No-motion watchdog**: when you lift the mouse (no deltas for ~18ms), right stick is hard-zeroed and smoothing is reset to prevent drift.
- **Diagonal smoothing**: mapper uses radial processing + vector EMA to eliminate XY stutter at diagonals.

## Mode Facade Simplification (2025-09)

Legacy multi-mode / transition stack (Disabled/Controller/etc.) has been collapsed to two concrete runtime modes:

* `ControllerOutput`
* `ControllerPassthrough`

To decouple UI panels and diagnostics from implementation details, a new `IModeService` facade was introduced (`Core/IModeService.cs`) with a concrete implementation `ModeService` combining persistence (`ModeController`), runtime behavior (`ModeManager`), and suppression flag exposure.

### IModeService Surface
* `InputMode CurrentMode` – authoritative active mode.
* `bool SuppressionActive` – mirrors `LowLevelHooks.Suppress`.
* `event Action<InputMode, InputMode> ModeChanged` – old/new.
* `bool Switch(InputMode)` – idempotent switch.
* `InputMode Toggle()` – convenience two-way toggle.
* `InitializeFromPersistence()` – call once after registering handlers to restore persisted mode (if any).

### Tests
Obsolete tests referencing removed modes and transition managers were removed (`ModeSystemTests.cs`, `ThreadSafetyTests.cs`). A focused harness (`Tests/ModeServiceTests.cs`) validates:
1. Persisted passthrough value is restored on startup.
2. Disconnect path (simplified) switches back to output.

`ThreadSafeModeController` is retained temporarily but slated for deprecation; new code should depend on `IModeService` instead.

### Migration Guidance
Replace direct uses of `ModeController` + manual suppression checks in UI with an injected (or locally constructed) `ModeService` instance; subscribe to `ModeChanged` for updating labels / status lines.

See `MIGRATION.md` for a full step-by-step guide and rationale. For consistent UI strings, use `ModeStatusFormatter.Format(IModeService, extra)`. The old multi-phase transition manager and thread-safe wrapper have been removed.
