# WootMouseRemap Deep Dive — 2025-09-23T18:23:44.425900 UTC

## Snapshot
- Source root: `/mnt/data/a_unpacked/a`
- C# files: **69** | Total LOC: **20595** | csproj files: **2** | P/Invoke entries: **23**
- WinForms resources (.resx): **1**, Images: **0**, Manifests: **1**

## Key Findings
- **P/Invoke usage:** 23 entries; ensure `SetLastError` and `CharSet` are specified and prefer `SafeHandle`.
- **Unsafe code:** present in 1 file(s); add tests and careful pinning.
- **Nullability suppressions:** found in 1 file(s); remove pragmas and fix annotations.

## Top 25 Largest Files (by LOC)
| File | LOC | Summary |
| --- | --- | --- |
| UI/AntiRecoilConfigForm.cs | 2577 | Types: 4 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 74, Properties: 2. |
| Modes/ControllerOutputMode.cs | 1119 | Types: 5 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 24, Properties: 21. ViGEm submits: Submit=1, SubmitReport=0. |
| Modes/ControllerPassthroughMode.cs | 968 | Types: 5 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 27, Properties: 22. |
| OverlayForm.cs | 923 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 35, Properties: 0. Nullability suppressed pragmas present. |
| UI/AdvancedMouseSettingsForm.cs | 722 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 27, Properties: 0. |
| UI/TrayManager.cs | 670 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 23, Properties: 0. |
| Features/AntiRecoil.cs | 654 | Types: 6 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 17, Properties: 44. |
| UI/CompactAdvancedForm.cs | 625 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 16, Properties: 0. |
| UI/PatternGraphControl.cs | 520 | Types: 3 class(es), 0 struct(s), 0 interface(s), 1 enum(s). Methods: 20, Properties: 11. |
| UI/TelemetryControl.cs | 471 | Types: 3 class(es), 0 struct(s), 0 interface(s), 1 enum(s). Methods: 18, Properties: 20. |
| Gamepad/XInputPassthrough.cs | 457 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 10, Properties: 2. ViGEm submits: Submit=1, SubmitReport=0. |
| Features/PatternTransforms.cs | 453 | Types: 3 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 13, Properties: 16. |
| UI/HelpSystem.cs | 448 | Types: 4 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 9, Properties: 3. |
| Modes/ModeManager.cs | 444 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 13, Properties: 2. |
| Core/BackupManager.cs | 413 | Types: 7 class(es), 0 struct(s), 0 interface(s), 1 enum(s). Methods: 10, Properties: 27. |
| Core/ProfileManager.cs | 383 | Types: 3 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 13, Properties: 17. |
| Diagnostics/PassthroughDiagnostics.cs | 366 | Types: 4 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 7, Properties: 27. |
| Core/ModeTransitionManager.cs | 344 | Types: 3 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 12, Properties: 6. |
| UI/AdvancedFeatures.cs | 332 | Types: 7 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 15, Properties: 11. |
| Core/ThreadSafeModeController.cs | 322 | Types: 2 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 11, Properties: 5. |
| Core/ModeStateValidator.cs | 321 | Types: 4 class(es), 0 struct(s), 0 interface(s), 1 enum(s). Methods: 8, Properties: 6. |
| UI/ValidationSystem.cs | 319 | Types: 6 class(es), 0 struct(s), 0 interface(s), 1 enum(s). Methods: 17, Properties: 11. |
| Input/LowLevelHooks.cs | 303 | Types: 1 class(es), 3 struct(s), 0 interface(s), 0 enum(s). Methods: 11, Properties: 2. P/Invoke: 5 import(s). |
| Tests/BackupManagerTests.cs | 303 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 18, Properties: 0. |
| Tests/ThreadSafetyTests.cs | 302 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 7, Properties: 0. |

## Top 25 Highest-Risk Files (heuristic)
| File | Risk (1-5) | Summary | Potential Improvements |
| --- | --- | --- | --- |
| HotkeyService.cs | 3 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 9, Properties: 0. P/Invoke: 1 import(s). | DllImport: add SetLastError=true where appropriate and check Marshal.GetLastWin32Error(). DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. |
| OverlayForm.cs | 3 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 35, Properties: 0. Nullability suppressed pragmas present. | Nullability warnings suppressed: remove suppression and fix annotations to prevent NREs. Avoid Environment.Exit in libraries; propagate errors instead. Prefer DateTime.UtcNow for timestamps; use time providers for testability. ViGEm updates without Submit/SubmitReport detected: ensure each update tick submits to the virtual device. |
| Controllers/XInputController.cs | 3 | Types: 1 class(es), 2 struct(s), 0 interface(s), 0 enum(s). Methods: 4, Properties: 1. P/Invoke: 2 import(s). | DllImport: add SetLastError=true where appropriate and check Marshal.GetLastWin32Error(). DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. DllImport: add SetLastError=true where appropriate and check Marshal.GetLastWin32Error(). DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. |
| Gamepad/XInputHelper.cs | 3 | Types: 4 class(es), 3 struct(s), 0 interface(s), 0 enum(s). Methods: 15, Properties: 0. P/Invoke: 6 import(s). | DllImport: add SetLastError=true where appropriate and check Marshal.GetLastWin32Error(). DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. DllImport: add SetLastError=true where appropriate and check Marshal.GetLastWin32Error(). DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. DllImport: add SetLastError=true where appropriate and check Marshal.GetLastWin32Error(). DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. DllImport: add SetLastError=true where appropriate and check Marshal.GetLastWin32Error(). DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. DllImport: add SetLastError=true where appropriate and check Marshal.GetLastWin32Error(). DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. DllImport: add SetLastError=true where appropriate and check Marshal.GetLastWin32Error(). DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. |
| Input/LowLevelHooks.cs | 3 | Types: 1 class(es), 3 struct(s), 0 interface(s), 0 enum(s). Methods: 11, Properties: 2. P/Invoke: 5 import(s). | DllImport: add SetLastError=true where appropriate and check Marshal.GetLastWin32Error(). DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. DllImport: add SetLastError=true where appropriate and check Marshal.GetLastWin32Error(). DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. DllImport: add SetLastError=true where appropriate and check Marshal.GetLastWin32Error(). DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. DllImport: add SetLastError=true where appropriate and check Marshal.GetLastWin32Error(). DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. P/Invoke: prefer SafeHandle/critical handle wrappers over raw IntPtr where possible. |
| Input/RawInput.cs | 3 | Types: 1 class(es), 4 struct(s), 0 interface(s), 0 enum(s). Methods: 7, Properties: 0. P/Invoke: 2 import(s). Contains 'unsafe' code. | DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. P/Invoke: prefer SafeHandle/critical handle wrappers over raw IntPtr where possible. Unsafe code present: ensure bounds checks and pinning are correct; add tests around marshaling. |
| Interop/DeviceHider.cs | 3 | Types: 2 class(es), 3 struct(s), 0 interface(s), 0 enum(s). Methods: 12, Properties: 3. P/Invoke: 7 import(s). | DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. DllImport: specify CharSet (e.g., CharSet.Unicode) to avoid ANSI/Unicode mismatches. P/Invoke: prefer SafeHandle/critical handle wrappers over raw IntPtr where possible. |
| UI/CompactAdvancedForm.cs | 2 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 16, Properties: 0. | Avoid Application.DoEvents(); refactor long-running work off the UI thread. Prefer DateTime.UtcNow for timestamps; use time providers for testability. |
| UI/ModeDiagnosticForm.cs | 2 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 7, Properties: 0. | Avoid Application.DoEvents(); refactor long-running work off the UI thread. Prefer DateTime.UtcNow for timestamps; use time providers for testability. ViGEm updates without Submit/SubmitReport detected: ensure each update tick submits to the virtual device. |
| ControllerKeyMapManager.cs | 1 | Types: 1 class(es), 0 struct(s), 0 interface(s), 2 enum(s). Methods: 9, Properties: 0. | ViGEm updates without Submit/SubmitReport detected: ensure each update tick submits to the virtual device. |
| Enums.cs | 1 | Methods: 0, Properties: 0. | ViGEm updates without Submit/SubmitReport detected: ensure each update tick submits to the virtual device. |
| InputEventHub.cs | 1 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 4, Properties: 0. |  |
| Program.cs | 1 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 0, Properties: 0. | Prefer DateTime.UtcNow for timestamps; use time providers for testability. |
| XInputStatePoller.cs | 1 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 3, Properties: 0. |  |
| Calibration/AutoTuner.cs | 1 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 3, Properties: 0. |  |
| Calibration/FigureEightDriver.cs | 1 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 0, Properties: 0. |  |
| Controllers/ControllerManager.cs | 1 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 0, Properties: 0. |  |
| Controllers/DirectInputController.cs | 1 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 2, Properties: 1. |  |
| Controllers/PhysicalController.cs | 1 | Types: 1 class(es), 1 struct(s), 0 interface(s), 0 enum(s). Methods: 3, Properties: 2. |  |
| Core/BackupManager.cs | 1 | Types: 7 class(es), 0 struct(s), 0 interface(s), 1 enum(s). Methods: 10, Properties: 27. | Prefer DateTime.UtcNow for timestamps; use time providers for testability. |
| Core/IModeService.cs | 1 | Types: 0 class(es), 0 struct(s), 1 interface(s), 0 enum(s). Methods: 0, Properties: 0. |  |
| Core/ModeController.cs | 1 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 3, Properties: 1. |  |
| Core/ModeService.cs | 1 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 5, Properties: 0. |  |
| Core/ModeStateValidator.cs | 1 | Types: 4 class(es), 0 struct(s), 0 interface(s), 1 enum(s). Methods: 8, Properties: 6. |  |
| Core/ModeStatusFormatter.cs | 1 | Types: 1 class(es), 0 struct(s), 0 interface(s), 0 enum(s). Methods: 1, Properties: 0. |  |

## Project Properties
| Project | TFM | WinForms | Unsafe | Nullable | Manifest | Root NS |
| --- | --- | --- | --- | --- | --- | --- |
| WootMouseRemap.csproj | net8.0-windows | true | true | enable | app.manifest | WootMouseRemap |
| Tests/WootMouseRemap.Tests.csproj | net8.0-windows | true |  | enable |  |  |

## ViGEm & P/Invoke Map
| File | DLL | Function |
| --- | --- | --- |
| HotkeyService.cs | user32.dll | GetKeyState |
| Controllers/XInputController.cs | xinput1_4.dll | XInputGetState14 |
| Controllers/XInputController.cs | xinput1_3.dll | XInputGetState13 |
| Gamepad/XInputHelper.cs | xinput1_4.dll | GetState |
| Gamepad/XInputHelper.cs | xinput1_4.dll | GetCapabilities |
| Gamepad/XInputHelper.cs | xinput1_3.dll | GetState |
| Gamepad/XInputHelper.cs | xinput1_3.dll | GetCapabilities |
| Gamepad/XInputHelper.cs | xinput9_1_0.dll | GetState |
| Gamepad/XInputHelper.cs | xinput9_1_0.dll | GetCapabilities |
| Input/LowLevelHooks.cs | user32.dll | SetWindowsHookEx |
| Input/LowLevelHooks.cs | user32.dll | UnhookWindowsHookEx |
| Input/LowLevelHooks.cs | user32.dll | CallNextHookEx |
| Input/LowLevelHooks.cs | kernel32.dll | GetModuleHandle |
| Input/LowLevelHooks.cs | user32.dll | GetKeyState |
| Input/RawInput.cs | user32.dll | RegisterRawInputDevices |
| Input/RawInput.cs | user32.dll | GetRawInputData |
| Interop/DeviceHider.cs | setupapi.dll | SetupDiGetClassDevs |
| Interop/DeviceHider.cs | setupapi.dll | SetupDiEnumDeviceInfo |
| Interop/DeviceHider.cs | setupapi.dll | SetupDiGetDeviceInstanceId |
| Interop/DeviceHider.cs | setupapi.dll | SetupDiGetDeviceRegistryProperty |
| Interop/DeviceHider.cs | setupapi.dll | SetupDiSetClassInstallParams |
| Interop/DeviceHider.cs | setupapi.dll | SetupDiCallClassInstaller |
| Interop/DeviceHider.cs | setupapi.dll | SetupDiDestroyDeviceInfoList |

_Full list in_ `PInvoke_Map.csv` (_23 entries_)

## Manifest Settings
| Manifest | Require Admin |
| --- | --- |
| app.manifest | True |

## Next-Step Improvements (Cross-Cutting)
- Add/expand XML docs for public APIs; enforce with analyzers.
- Replace `lock(this)` with a private `readonly object` lock.
- Remove blanket `#nullable disable` / `#pragma` suppressions; fix nullability.
- Strengthen P/Invoke signatures with `SetLastError=true`, explicit `CharSet`, and `SafeHandle`.
- Avoid `Application.DoEvents()` and `Thread.Sleep()` on the UI thread; use async patterns.
- Guard ViGEm state updates with guaranteed submit per tick (already patched).
- Centralize logs to `%LOCALAPPDATA%\WootMouseRemap\Logs` with sensitive-value scrubbing.
- Add unit tests for marshaling boundaries and controller mapping logic.
