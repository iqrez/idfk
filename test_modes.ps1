# Mode testing script for WootMouseRemap
Write-Host "=== WootMouseRemap Mode Testing ==="
Write-Host "Timestamp: $(Get-Date)"
Write-Host ""

# Check current mode from config
Write-Host "Current Configuration:"
if (Test-Path "mode.json") {
    $modeContent = Get-Content "mode.json" -Raw
    $mode = [int]$modeContent.Trim()
    $modeName = switch ($mode) {
        0 { "ControllerOutput" }
        1 { "ControllerPassthrough" }
        default { "Unknown ($mode)" }
    }
    Write-Host "   Current Mode: $modeName"
} else {
    Write-Host "   Mode config file not found"
}

# Check anti-recoil settings
if (Test-Path "antirecoil_settings.json") {
    $settings = Get-Content "antirecoil_settings.json" | ConvertFrom-Json
    Write-Host "   Anti-Recoil Enabled: $($settings.Enabled)"
    Write-Host "   Anti-Recoil Strength: $($settings.Strength * 100)%"
    Write-Host "   Threshold: $($settings.VerticalThreshold)"
} else {
    Write-Host "   Anti-recoil settings not found"
}

Write-Host ""

# Check for connected game controllers in Windows
Write-Host "Windows Controller Detection:"
try {
    # Check registry for game controllers
    $controllers = Get-ChildItem "HKLM:\SYSTEM\CurrentControlSet\Control\MediaProperties\PrivateProperties\Joystick\OEM" -ErrorAction SilentlyContinue
    if ($controllers) {
        Write-Host "   Found $($controllers.Count) controller entries in registry"
        foreach ($controller in $controllers[0..2]) {  # Show first 3
            $name = (Get-ItemProperty $controller.PSPath -Name "OEMName" -ErrorAction SilentlyContinue).OEMName
            if ($name) {
                Write-Host "   - $name"
            }
        }
    } else {
        Write-Host "   No controllers found in registry"
    }
} catch {
    Write-Host "   Could not check controller registry"
}

Write-Host ""

# Check for XInput capable devices
Write-Host "XInput Device Check:"
try {
    # Look for XInput-related processes or services
    $xinputProcs = Get-Process | Where-Object {$_.ProcessName -like "*xinput*" -or $_.ProcessName -like "*controller*"} -ErrorAction SilentlyContinue
    if ($xinputProcs) {
        Write-Host "   Found XInput-related processes: $($xinputProcs.Count)"
    } else {
        Write-Host "   No XInput processes detected"
    }

    # Check for XInput DLLs in system
    $xinputDll = Test-Path "$env:SystemRoot\System32\xinput1_4.dll"
    Write-Host "   XInput DLL available: $xinputDll"
} catch {
    Write-Host "   Could not check XInput status"
}

Write-Host ""

# Test recommendations based on current mode
Write-Host "Mode-Specific Tests:"
if ($modeName -eq "ControllerPassthrough") {
    Write-Host "   Current mode: Passthrough"
    Write-Host "   - This mode requires a physical controller to be connected"
    Write-Host "   - Physical controller input should pass through to virtual controller"
    Write-Host "   - Test: Connect Xbox controller and verify it works in games"
} elseif ($modeName -eq "ControllerOutput") {
    Write-Host "   Current mode: Output"
    Write-Host "   - This mode converts mouse/keyboard to controller input"
    Write-Host "   - Anti-recoil should be active for mouse movement"
    Write-Host "   - Test: Move mouse and check if virtual controller responds"
} else {
    Write-Host "   Unknown mode - check application status"
}

Write-Host ""

# Check for common issues
Write-Host "Common Issue Checklist:"
Write-Host "   1. Is a physical Xbox controller connected? (Required for Passthrough mode)"
Write-Host "   2. Is ViGEm Bus Driver installed? (Required for virtual controller)"
Write-Host "   3. Are other controller software/drivers conflicting?"
Write-Host "   4. Try switching modes via the system tray icon"

Write-Host ""

# Show recent relevant log entries
Write-Host "Recent Controller Activity:"
if (Test-Path "Logs\woot.log") {
    $recentLogs = Get-Content "Logs\woot.log" -Tail 50 | Where-Object {
        $_ -notlike "*HotkeyService*" -and
        ($_ -like "*Controller*" -or $_ -like "*XInput*" -or $_ -like "*Mode*" -or $_ -like "*ViGEm*")
    }

    if ($recentLogs) {
        foreach ($log in $recentLogs | Select-Object -Last 5) {
            Write-Host "   $log"
        }
    } else {
        Write-Host "   No recent controller activity in logs"
        Write-Host "   This suggests the modes may not be processing controller input"
    }
}

Write-Host ""
Write-Host "=== Test Complete ==="
Write-Host "Next: Use the system tray Mode Diagnostics for detailed real-time analysis"