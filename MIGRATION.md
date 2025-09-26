# Migration Guide: Legacy Mode System -> IModeService

## Summary
The previous layered system (`ThreadSafeModeController`, `ModeTransitionManager`, ad-hoc persistence) has been removed. Runtime now uses:

- `ModeManager` for handler dispatch (two concrete modes)
- `ModeController` (internal to `ModeService`) only for persistence
- `IModeService` / `ModeService` as the single façade for UI & diagnostics

## Removed Components
- `ThreadSafeModeController.cs`
- `ModeTransitionManager.cs`
- Legacy test harnesses (`ModeSystemTests`, `ThreadSafetyTests`)

## Keep / Replace Mapping
| Old Usage | New Equivalent |
|-----------|----------------|
| Direct `ModeController` for saving + current | `IModeService.CurrentMode` + automatic persistence |
| Manual suppression decisions | `IModeService.SuppressionActive` (reflects handlers) |
| Transition orchestration / validation layers | Simpler `ModeManager.SwitchMode()` via `IModeService.Switch()` |

## Adopting IModeService
1. Construct a `ModeManager` and register the two handlers (`ControllerOutputMode`, `ControllerPassthroughMode`).
2. Create `var modeService = new ModeService("mode.json", modeManager);`
3. After registration call `modeService.InitializeFromPersistence();`
4. Subscribe to `modeService.ModeChanged` for UI updates.
5. Use `modeService.Toggle()` for hotkeys (F1/middle mouse).

## Diagnostics
`ModeStatusFormatter.Format(modeService, extra)` returns a consistent string.
`ModeService.IsPersistedMatch` helps detect startup race conditions.

## Tests
Use `ModeServiceTests` as a template for basic persistence and event validation. Focus on real mode handler effects if needed (suppression toggling and passthrough start/stop).

## Rationale
The removed classes added complexity (multi-phase async transitions, rollback) not required for two idempotent, synchronous mode handlers. Simplifying improves determinism and reduces surface for race conditions.

## Input Pipeline (Updated)

As of the RawInput migration (Sept 2025):

- **Mouse movement deltas** are sourced exclusively from RawInput. Low-level WH_MOUSE_LL hook no longer emits movement; any attempt to reintroduce it triggers a debug assertion.
- **Buttons & Keys**: Both RawInput and low-level hooks can see them, but routing for mode logic now comes from `InputEventHub` (which normalizes RawInput). The low-level hook remains for: suppression (blocking), panic hotkey, and as a resilient fallback for button events if RawInput were to fail.
- **Wheel**: Provided through RawInput only (low-level path ignores wheel events on purpose).
- **Suppression**: Still toggled by mode handlers; when enabled it prevents OS propagation while internal RawInput deltas continue (since RawInput is device-level, not affected by suppression of the low-level hook).

### Why Remove Low-Level Movement?
1. RawInput delivers high fidelity per-device deltas (multi-mouse safe) unaffected by Display Pointer Precision / acceleration.
2. Eliminates duplicate event streams and jitter caused by mixing hook and RawInput timing.
3. Prevents accidental double-application of anti-recoil or smoothing logic.
4. Simplifies reasoning about idle detection (single source of truth).

### Guardrails Added
- `LowLevelHooks` now contains a debug assertion (and throttled warning in Release) if `WM_MOUSEMOVE` ever reaches dispatch. This is a maintenance trapdoor to discourage regressions.
- `OverlayForm` no longer constructs a parallel `RawInput`; it subscribes to `InputEventHub` events instead.

### Extending Later
If you need high-resolution pointer capture for relative motion while the cursor is hidden, keep using RawInput. Only consider reinstating low-level movement if a platform-specific RawInput quirk requires it; in that case gate it behind a feature flag (`EnableLegacyLowLevelMove`) and ensure deltas are not double-counted.

---

## FAQ
**Q: How do I force-write a new persisted mode manually?**  
A: Just call `modeService.Switch(desiredMode)`; the persistence layer is updated automatically on the event path.

**Q: Do I still need to manually set `LowLevelHooks.Suppress`?**  
A: No. Each mode handler sets suppression in `OnModeEntered/OnModeExited`.

**Q: Can I add a third mode later?**  
A: Yes—add another `IModeHandler`, register it, and extend `Toggle()` (or introduce a rotation method). Persistence will automatically include the new enum value.

---
If you need an example integration snippet for a different UI, ping and we can add one.
