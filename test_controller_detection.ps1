# Test controller detection and XInput
Write-Host "=== Controller Detection Test ===" -ForegroundColor Cyan

# Check recent controller detection logs
Write-Host "Recent controller detection activity:" -ForegroundColor Yellow
$detectionLogs = Get-Content "Logs\woot.log" | Where-Object {
    $_ -like "*ControllerDetector*" -or
    $_ -like "*Physical controller*" -or
    $_ -like "*Connected:*" -or
    $_ -like "*XInput*"
} | Select-Object -Last 5

if ($detectionLogs) {
    foreach ($log in $detectionLogs) {
        Write-Host "   $log" -ForegroundColor White
    }
} else {
    Write-Host "   No controller detection logs found" -ForegroundColor Red
}

Write-Host ""
Write-Host "Current mode and status:" -ForegroundColor Yellow
$recentStatus = Get-Content "Logs\woot.log" | Where-Object {
    $_ -like "*Mode*" -or $_ -like "*started*" -or $_ -like "*Physical:*"
} | Select-Object -Last 5

foreach ($log in $recentStatus) {
    Write-Host "   $log" -ForegroundColor White
}

Write-Host ""
Write-Host "Physical Controller Test:" -ForegroundColor Cyan
Write-Host "1. Please connect a physical Xbox/XInput controller" -ForegroundColor White
Write-Host "2. Move the controller sticks or press buttons" -ForegroundColor White
Write-Host "3. Switch to passthrough mode using F1 or tray menu" -ForegroundColor White
Write-Host "4. Watch for input in games or XInput test apps" -ForegroundColor White

Write-Host ""
Write-Host "If passthrough still doesn't work:" -ForegroundColor Yellow
Write-Host "   - Check Windows Game Controller settings" -ForegroundColor White
Write-Host "   - Verify the controller works in other games" -ForegroundColor White
Write-Host "   - Try using Steam's controller test" -ForegroundColor White