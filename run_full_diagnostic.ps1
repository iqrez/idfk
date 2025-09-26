# Comprehensive diagnostic and fix script
Write-Host "=== WootMouseRemap Full Diagnostic & Fix ===" -ForegroundColor Cyan
Write-Host ""

# 1. Check current application state
$process = Get-Process -Name "WootMouseRemap" -ErrorAction SilentlyContinue
if (-not $process) {
    Write-Host "❌ Application not running" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Application running (PID: $($process.Id))" -ForegroundColor Green

# 2. Check current mode
$currentMode = Get-Content "mode.json" -Raw
$modeNum = [int]$currentMode.Trim()
$modeName = if ($modeNum -eq 0) { "ControllerOutput" } else { "ControllerPassthrough" }
Write-Host "Current Mode: $modeName ($modeNum)" -ForegroundColor Yellow

# 3. Check for ViGEm driver
Write-Host ""
Write-Host "Checking ViGEm Driver..." -ForegroundColor Yellow
try {
    $vigemCheck = Get-PnpDevice -Class "System" | Where-Object {$_.FriendlyName -like "*ViGEm*"}
    if ($vigemCheck) {
        Write-Host "✅ ViGEm driver found" -ForegroundColor Green
    } else {
        Write-Host "❌ ViGEm driver not found - this is likely the main issue!" -ForegroundColor Red
        Write-Host "   Virtual controller cannot work without ViGEm driver" -ForegroundColor Red
        Write-Host "   Download from: https://github.com/ViGEm/ViGEmBus/releases" -ForegroundColor Cyan
    }
} catch {
    Write-Host "⚠️  Could not check ViGEm driver status" -ForegroundColor Yellow
}

# 4. Test XInput detection
Write-Host ""
Write-Host "Checking XInput Controllers..." -ForegroundColor Yellow
$xinputFound = $false
for ($i = 0; $i -lt 4; $i++) {
    # This is a simple test - actual XInput testing would require native calls
    if (Test-Path "C:\Windows\System32\xinput1_4.dll") {
        Write-Host "✅ XInput library available" -ForegroundColor Green
        break
    }
}

# 5. Check raw input system
Write-Host ""
Write-Host "Checking Input System..." -ForegroundColor Yellow
$recentInputLogs = Get-Content "Logs\woot.log" -Tail 20 | Where-Object {
    $_ -like "*mouse*" -or $_ -like "*Mouse*" -or $_ -like "*RawInput*"
}

if ($recentInputLogs) {
    Write-Host "✅ Mouse input detected in logs" -ForegroundColor Green
} else {
    Write-Host "❌ No mouse input in recent logs - input system may not be working" -ForegroundColor Red
}

# 6. Check for initialization errors
Write-Host ""
Write-Host "Checking for Errors..." -ForegroundColor Yellow
$errorLogs = Get-Content "Logs\woot.log" | Where-Object {
    $_ -like "*ERROR*" -or $_ -like "*Error*" -or $_ -like "*WARN*" -or $_ -like "*failed*"
} | Select-Object -Last 5

if ($errorLogs) {
    Write-Host "⚠️  Found recent errors/warnings:" -ForegroundColor Yellow
    foreach ($errorLine in $errorLogs) {
        Write-Host "   $errorLine" -ForegroundColor Red
    }
} else {
    Write-Host "✅ No recent errors found" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== DIAGNOSTIC SUMMARY ===" -ForegroundColor Cyan
Write-Host "The most likely issues are:" -ForegroundColor Yellow
Write-Host "1. ViGEm Bus Driver not installed (virtual controller won't work)" -ForegroundColor Red
Write-Host "2. Mouse input hooks not functioning properly" -ForegroundColor Red
Write-Host "3. Application may need restart after driver installation" -ForegroundColor Yellow

Write-Host ""
Write-Host "=== RECOMMENDED FIXES ===" -ForegroundColor Cyan
Write-Host "1. Install ViGEm Bus Driver from: https://github.com/ViGEm/ViGEmBus/releases" -ForegroundColor Cyan
Write-Host "2. Restart the application after driver installation" -ForegroundColor Cyan
Write-Host "3. Test both modes after driver installation" -ForegroundColor Cyan