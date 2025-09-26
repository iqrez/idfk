# Implementation Plan

[Overview]
Comprehensive analysis and enhancement of the WootMouseRemap mode toggle system, addressing current functionality, potential improvements, and technical debt resolution.

Multiple paragraphs outlining the scope, context, and high-level approach. The current system provides basic two-mode toggling via F1 hotkey between ControllerOutput and ControllerPassthrough modes. This plan will enhance the system with better mode management, additional toggle options, improved hotkey handling, and robust error recovery while maintaining backward compatibility.

[Types]
Enhanced enumeration system for input modes with validation and state management.

Detailed type definitions, interfaces, enums, or data structures with complete specifications. The InputMode enum will be extended with additional states and validation rules. New interfaces will be introduced for mode cycling strategies and hotkey management. Enhanced state tracking will include mode transition history and validation context.

[Files]
Comprehensive file modification strategy for mode system enhancement.

Detailed breakdown:
- New files to be created (with full paths and purpose)
  - `Core/EnhancedModeService.cs`: Advanced mode service with cycling and validation
  - `Core/ModeCycleStrategy.cs`: Strategy pattern for different toggle behaviors
  - `Core/HotkeyManager.cs`: Centralized hotkey management system
  - `Modes/ThreeModeHandler.cs`: Support for three-mode cycling
  - `Diagnostics/ModeTransitionLogger.cs`: Enhanced transition logging
- Existing files to be modified (with specific changes)
  - `Enums.cs`: Add new mode states and hotkey definitions
  - `HotkeyService.cs`: Refactor to use new hotkey manager
  - `Core/ModeService.cs`: Enhance with new toggle strategies
  - `Core/ModeController.cs`: Add three-mode support
  - `OverlayForm.cs`: Update to use enhanced mode system
  - `Program.cs`: Integrate new hotkey manager
- Files to be deleted or moved
  - None required
- Configuration file updates
  - `mode.json`: Add new mode configuration options

[Functions]
Enhanced function architecture for robust mode management.

Detailed breakdown:
- New functions (name, signature, file path, purpose)
  - `CycleNextMode()`: `InputMode CycleNextMode(bool includePassthrough)` in `Core/EnhancedModeService.cs`: Cycles through available modes
  - `SetToggleStrategy()`: `void SetToggleStrategy(IModeToggleStrategy strategy)` in `Core/ModeService.cs`: Sets toggle behavior
  - `RegisterHotkey()`: `void RegisterHotkey(Keys key, Action callback)` in `Core/HotkeyManager.cs`: Registers hotkey combinations
  - `ValidateTransition()`: `bool ValidateTransition(InputMode from, InputMode to)` in `Core/ModeController.cs`: Validates mode transitions
- Modified functions (exact name, current file path, required changes)
  - `Toggle()`: `Core/ModeService.cs`: Add strategy pattern support and validation
  - `OnKey()`: `HotkeyService.cs`: Refactor to use centralized hotkey manager
  - `OnModeToggle()`: `OverlayForm.cs`: Add support for different toggle modes
  - `ToggleNext()`: `Core/ModeController.cs`: Enhance with three-mode support
- Removed functions (name, file path, reason, migration strategy)
  - None identified for removal

[Classes]
Enhanced class architecture with improved separation of concerns.

Detailed breakdown:
- New classes (name, file path, key methods, inheritance)
  - `EnhancedModeService`: `Core/EnhancedModeService.cs`: Advanced mode management with strategies
  - `HotkeyManager`: `Core/HotkeyManager.cs`: Centralized hotkey handling
  - `ThreeModeHandler`: `Modes/ThreeModeHandler.cs`: Three-mode cycling support
  - `ModeTransitionLogger`: `Diagnostics/ModeTransitionLogger.cs`: Enhanced logging
- Modified classes (exact name, file path, specific modifications)
  - `ModeService`: `Core/ModeService.cs`: Add strategy pattern and validation
  - `HotkeyService`: `Core/HotkeyService.cs`: Refactor to use HotkeyManager
  - `ModeController`: `Core/ModeController.cs`: Add three-mode cycling support
  - `OverlayForm`: `OverlayForm.cs`: Integrate enhanced mode system
- Removed classes (name, file path, replacement strategy)
  - None required

[Dependencies]
Minimal dependency changes for enhanced functionality.

Details of new packages, version changes, and integration requirements. No new external dependencies required. All enhancements will use existing .NET framework capabilities and internal libraries.

[Testing]
Comprehensive testing strategy for mode system enhancements.

Test file requirements, existing test modifications, and validation strategies. Enhanced unit tests for new toggle strategies, integration tests for hotkey management, and validation tests for mode transitions. Existing tests in `Tests/` directory will be updated to cover new functionality.

[Implementation Order]
Sequential implementation to minimize conflicts and ensure successful integration.

Numbered steps showing the logical order of changes to minimize conflicts and ensure successful integration.
1. **Analysis Phase**: Complete current system documentation and identify specific issues
2. **Core Enhancement**: Implement EnhancedModeService and ModeCycleStrategy
3. **Hotkey Management**: Create centralized HotkeyManager system
4. **Three-Mode Support**: Add three-mode cycling capability
5. **Integration**: Update existing classes to use new systems
6. **Testing**: Comprehensive testing of all new functionality
7. **Documentation**: Update user-facing documentation and help text
