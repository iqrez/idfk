# Switch to Controller Output mode for testing
Write-Host "=== Switching to Controller Output Mode ==="

# Change mode to 0 (ControllerOutput)
Write-Host "Switching mode from Passthrough to Output..."
Set-Content -Path "mode.json" -Value "0"

Write-Host "Mode switched. Waiting for application to detect change..."
Start-Sleep -Seconds 3

# Check if mode was applied
$newMode = Get-Content "mode.json" -Raw
$modeName = switch ([int]$newMode.Trim()) {
    0 { "ControllerOutput" }
    1 { "ControllerPassthrough" }
    default { "Unknown" }
}

Write-Host "Current mode is now: $modeName"
Write-Host ""
Write-Host "Testing Controller Output Mode:"
Write-Host "   - This mode should convert mouse/keyboard input to controller output"
Write-Host "   - Anti-recoil should be active for mouse movement"
Write-Host "   - Move your mouse to generate input"
Write-Host ""
Write-Host "Monitoring logs for 10 seconds..."

# Monitor logs for mouse activity
$startTime = Get-Date
$endTime = $startTime.AddSeconds(10)

while ((Get-Date) -lt $endTime) {
    Start-Sleep -Milliseconds 500

    # Check for new log entries
    $recentLogs = Get-Content "Logs\woot.log" -Tail 5 | Where-Object {
        $_ -like "*Mouse*" -or $_ -like "*Anti-Recoil*" -or $_ -like "*Mode*"
    }

    if ($recentLogs) {
        Write-Host "Found activity:"
        foreach ($log in $recentLogs) {
            Write-Host "   $log"
        }
        break
    }
}

Write-Host ""
Write-Host "Test complete. Check the system tray diagnostics for detailed status."