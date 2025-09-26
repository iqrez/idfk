# Test hotkey mode switching
Write-Host "=== Testing Hotkey Mode Switch ==="
Write-Host ""

Write-Host "Current mode.json content:"
$currentMode = Get-Content "mode.json" -Raw
Write-Host "   Mode: $($currentMode.Trim())"
Write-Host ""

Write-Host "Looking for F1 hotkey functionality in recent logs..."
# Look for any mode-related or F1-related logs
$modeLogs = Get-Content "Logs\woot.log" | Where-Object {
    $_ -like "*Mode*" -or $_ -like "*F1*" -or $_ -like "*toggle*" -or $_ -like "*switch*"
} | Select-Object -Last 10

if ($modeLogs) {
    Write-Host "Found mode-related logs:"
    foreach ($log in $modeLogs) {
        Write-Host "   $log"
    }
} else {
    Write-Host "No mode switching logs found"
}

Write-Host ""
Write-Host "Testing sequence:"
Write-Host "1. Try pressing F1 key (should toggle modes)"
Write-Host "2. Check if any mode change appears in logs"
Write-Host "3. Monitor for 5 seconds..."

# Monitor for new logs
$startTime = Get-Date
$endTime = $startTime.AddSeconds(5)
$lastLogCount = (Get-Content "Logs\woot.log" | Measure-Object -Line).Lines

Write-Host ""
Write-Host "Monitoring logs..."

while ((Get-Date) -lt $endTime) {
    Start-Sleep -Milliseconds 500

    $currentLogCount = (Get-Content "Logs\woot.log" | Measure-Object -Line).Lines
    if ($currentLogCount -gt $lastLogCount) {
        $newLogs = Get-Content "Logs\woot.log" -Tail ($currentLogCount - $lastLogCount)
        foreach ($log in $newLogs) {
            if ($log -like "*Mode*" -or $log -like "*F1*" -or $log -like "*112*") {
                Write-Host "Mode activity detected: $log"
            }
        }
        $lastLogCount = $currentLogCount
    }
}

Write-Host ""
Write-Host "Check completed. If F1 doesn't work, try:"
Write-Host "   - Right-click system tray icon to switch modes"
Write-Host "   - Use the Mode Diagnostics tool"