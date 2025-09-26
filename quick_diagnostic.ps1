# Quick diagnostic script for WootMouseRemap mode issues
Write-Host "=== WootMouseRemap Quick Diagnostics ===" -ForegroundColor Cyan
Write-Host "Timestamp: $(Get-Date)" -ForegroundColor Gray
Write-Host ""

# Check if application is running
$process = Get-Process -Name "WootMouseRemap" -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "✅ Application Status: Running (PID: $($process.Id))" -ForegroundColor Green
} else {
    Write-Host "❌ Application Status: Not Running" -ForegroundColor Red
    exit 1
}

# Check for log files
Write-Host "📋 Log File Analysis:" -ForegroundColor Yellow
if (Test-Path "Logs\woot.log") {
    $logSize = (Get-Item "Logs\woot.log").Length
    Write-Host "   Log file exists: $(($logSize/1KB).ToString('F1')) KB" -ForegroundColor Green

    # Get recent log entries (last 10 lines)
    $recentLogs = Get-Content "Logs\woot.log" -Tail 10
    Write-Host "   Recent log entries:" -ForegroundColor Gray
    foreach ($line in $recentLogs[-3..-1]) {
        Write-Host "   $line" -ForegroundColor DarkGray
    }
} else {
    Write-Host "   ❌ No log file found" -ForegroundColor Red
}

Write-Host ""

# Check for configuration files
Write-Host "📁 Configuration Files:" -ForegroundColor Yellow
$configFiles = @(
    "antirecoil_settings.json",
    "mode.json",
    "Profiles\anti_recoil_patterns.json"
)

foreach ($file in $configFiles) {
    if (Test-Path $file) {
        $size = (Get-Item $file).Length
        Write-Host "   ✅ $file ($(($size).ToString()) bytes)" -ForegroundColor Green
    } else {
        Write-Host "   ❌ $file (missing)" -ForegroundColor Red
    }
}

Write-Host ""

# Check for controller-related processes
Write-Host "🎮 Controller System Check:" -ForegroundColor Yellow

# Check for HID/Gaming services
$hidService = Get-Service -Name "hidserv" -ErrorAction SilentlyContinue
if ($hidService -and $hidService.Status -eq "Running") {
    Write-Host "   ✅ HID Service: Running" -ForegroundColor Green
} else {
    Write-Host "   ❌ HID Service: Not Running" -ForegroundColor Red
}

# Check for connected USB devices (simplified)
$usbDevices = Get-WmiObject -Class Win32_USBControllerDevice -ErrorAction SilentlyContinue
$controllerCount = 0
if ($usbDevices) {
    # This is a simplified check - actual controller detection would be more complex
    $controllerCount = ($usbDevices | Measure-Object).Count
    Write-Host "   📊 USB Devices detected: $controllerCount" -ForegroundColor Gray
}

Write-Host ""

# Check for ViGEm driver
Write-Host "ViGEm Driver Check:" -ForegroundColor Yellow
try {
    $vigemDevices = Get-PnpDevice -Class "System" -ErrorAction SilentlyContinue | Where-Object {$_.FriendlyName -like "*ViGEm*"}
    if ($vigemDevices) {
        Write-Host "   ViGEm devices found" -ForegroundColor Green
    } else {
        Write-Host "   ViGEm driver not detected - this may cause virtual controller issues" -ForegroundColor Yellow
        Write-Host "      Download from: https://github.com/ViGEm/ViGEmBus/releases" -ForegroundColor Gray
    }
} catch {
    Write-Host "   Could not check ViGEm status" -ForegroundColor Yellow
}

Write-Host ""

# Check system resources
Write-Host "💻 System Resources:" -ForegroundColor Yellow
$cpu = Get-Counter "\Processor(_Total)\% Processor Time" -SampleInterval 1 -MaxSamples 1 -ErrorAction SilentlyContinue
if ($cpu) {
    $cpuUsage = [math]::Round($cpu.CounterSamples[0].CookedValue, 1)
    Write-Host "   CPU Usage: $cpuUsage%" -ForegroundColor $(if($cpuUsage -lt 80){"Green"}else{"Yellow"})
}

$memory = Get-WmiObject -Class Win32_OperatingSystem -ErrorAction SilentlyContinue
if ($memory) {
    $memUsage = [math]::Round(((($memory.TotalVisibleMemorySize - $memory.FreePhysicalMemory) / $memory.TotalVisibleMemorySize) * 100), 1)
    Write-Host "   Memory Usage: $memUsage%" -ForegroundColor $(if($memUsage -lt 80){"Green"}else{"Yellow"})
}

Write-Host ""

# Recommendations
Write-Host "Quick Recommendations:" -ForegroundColor Yellow
Write-Host "   1. Right-click the system tray icon to access Mode Diagnostics" -ForegroundColor Cyan
Write-Host "   2. Try switching between Controller Output and Passthrough modes" -ForegroundColor Cyan
Write-Host "   3. Check if a physical controller is connected and recognized by Windows" -ForegroundColor Cyan
Write-Host "   4. Verify ViGEm Bus Driver is installed for virtual controller support" -ForegroundColor Cyan

Write-Host ""
Write-Host "=== Diagnostic Complete ===" -ForegroundColor Cyan