# Immediate fixes that don't require admin privileges
Write-Host "=== WootMouseRemap Immediate Fixes ===" -ForegroundColor Cyan

# 1. Test F1 hotkey functionality
Write-Host "Testing F1 Mode Toggle..." -ForegroundColor Yellow
$originalMode = Get-Content "mode.json" -Raw
Write-Host "Current mode: $($originalMode.Trim())" -ForegroundColor White

Write-Host "Press F1 now to test mode switching..." -ForegroundColor Cyan
Write-Host "Waiting 3 seconds for F1 key press..." -ForegroundColor Gray

Start-Sleep -Seconds 3

$newMode = Get-Content "mode.json" -Raw
if ($newMode.Trim() -ne $originalMode.Trim()) {
    Write-Host "✅ F1 hotkey is working! Mode changed!" -ForegroundColor Green
} else {
    Write-Host "⚠️  F1 hotkey may not be working - mode unchanged" -ForegroundColor Yellow
}

# 2. Check application status
Write-Host ""
Write-Host "Checking application status..." -ForegroundColor Yellow

$process = Get-Process -Name "WootMouseRemap" -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "✅ Application running (PID: $($process.Id))" -ForegroundColor Green
    if ($process.Responding) {
        Write-Host "✅ Application responding normally" -ForegroundColor Green
    } else {
        Write-Host "❌ Application may be frozen" -ForegroundColor Red
    }
} else {
    Write-Host "❌ Application not running" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== SUMMARY ===" -ForegroundColor Cyan
Write-Host "Primary issue: ViGEm Bus Driver missing" -ForegroundColor Red
Write-Host "Manual install: https://github.com/ViGEm/ViGEmBus/releases" -ForegroundColor Cyan
Write-Host "Download latest ViGEmBus_x64.msi and run as Administrator" -ForegroundColor White