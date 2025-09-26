# Test input system functionality
Write-Host "=== Input System Test ==="
Write-Host "Testing core input processing..."
Write-Host ""

# Check the most recent logs to see what's actually happening
Write-Host "Recent Application Activity (last 10 entries):"
$recentLogs = Get-Content "Logs\woot.log" -Tail 10
foreach ($log in $recentLogs) {
    Write-Host "   $log"
}

Write-Host ""

# Look for initialization logs
Write-Host "Checking for initialization issues..."
$initLogs = Get-Content "Logs\woot.log" | Where-Object {
    $_ -like "*TrayManager*" -or
    $_ -like "*OverlayForm*" -or
    $_ -like "*initialized*" -or
    $_ -like "*Error*" -or
    $_ -like "*ViGEm*" -or
    $_ -like "*virtual*"
} | Select-Object -Last 10

if ($initLogs) {
    Write-Host "Initialization logs found:"
    foreach ($log in $initLogs) {
        Write-Host "   $log"
    }
} else {
    Write-Host "No initialization logs found - this may indicate an issue"
}

Write-Host ""

# Check what the HotkeyService is actually detecting
Write-Host "Hotkey Service Activity:"
$hotkeyLogs = Get-Content "Logs\woot.log" | Where-Object {
    $_ -like "*HotkeyService*"
} | Select-Object -Last 5

foreach ($log in $hotkeyLogs) {
    Write-Host "   $log"
}

Write-Host ""
Write-Host "Analysis:"
Write-Host "   - If you see HotkeyService activity, the application is receiving input"
Write-Host "   - If no ViGEm/virtual controller logs, there may be a driver issue"
Write-Host "   - If no mouse movement logs, mouse input processing may not be working"
Write-Host ""
Write-Host "Next steps:"
Write-Host "   1. Check system tray for the WootMouseRemap icon"
Write-Host "   2. Right-click tray icon and select 'Mode Diagnostics'"
Write-Host "   3. Try the F1 key to toggle modes (should show in logs)"