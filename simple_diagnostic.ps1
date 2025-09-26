# Simple diagnostic script for WootMouseRemap
Write-Host "=== WootMouseRemap Quick Diagnostics ==="
Write-Host "Timestamp: $(Get-Date)"
Write-Host ""

# Check if application is running
$process = Get-Process -Name "WootMouseRemap" -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "Application Status: Running (PID: $($process.Id))"
} else {
    Write-Host "Application Status: Not Running"
    exit 1
}

# Check for log files
Write-Host ""
Write-Host "Log File Analysis:"
if (Test-Path "Logs\woot.log") {
    $logSize = (Get-Item "Logs\woot.log").Length
    Write-Host "   Log file exists: $(($logSize/1KB).ToString('F1')) KB"

    # Get recent log entries
    $recentLogs = Get-Content "Logs\woot.log" -Tail 5
    Write-Host "   Recent log entries:"
    foreach ($line in $recentLogs) {
        Write-Host "   $line"
    }
} else {
    Write-Host "   No log file found"
}

# Check for configuration files
Write-Host ""
Write-Host "Configuration Files:"
$configFiles = @(
    "antirecoil_settings.json",
    "mode.json"
)

foreach ($file in $configFiles) {
    if (Test-Path $file) {
        $size = (Get-Item $file).Length
        Write-Host "   $file exists ($size bytes)"
    } else {
        Write-Host "   $file missing"
    }
}

# Check for HID service
Write-Host ""
Write-Host "System Services:"
$hidService = Get-Service -Name "hidserv" -ErrorAction SilentlyContinue
if ($hidService -and $hidService.Status -eq "Running") {
    Write-Host "   HID Service: Running"
} else {
    Write-Host "   HID Service: Not Running"
}

Write-Host ""
Write-Host "Next Steps:"
Write-Host "   1. Right-click the system tray icon for diagnostics"
Write-Host "   2. Try switching modes via tray menu"
Write-Host "   3. Check controller connection in Windows"
Write-Host ""
Write-Host "=== Diagnostic Complete ==="