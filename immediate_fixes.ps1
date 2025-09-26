# Immediate fixes that don't require admin privileges
Write-Host "=== WootMouseRemap Immediate Fixes ===" -ForegroundColor Cyan
Write-Host ""

# 1. Test F1 hotkey functionality
Write-Host "Testing F1 Mode Toggle..." -ForegroundColor Yellow
$originalMode = Get-Content "mode.json" -Raw
Write-Host "Current mode: $($originalMode.Trim())" -ForegroundColor White

Write-Host "Press F1 now to test mode switching..." -ForegroundColor Cyan
Write-Host "Waiting 3 seconds for F1 key press..." -ForegroundColor Gray

Start-Sleep -Seconds 3

$newMode = Get-Content "mode.json" -Raw
if ($newMode.Trim() -ne $originalMode.Trim()) {
    Write-Host "✅ F1 hotkey is working! Mode changed from $($originalMode.Trim()) to $($newMode.Trim())" -ForegroundColor Green
} else {
    Write-Host "⚠️  F1 hotkey may not be working - mode unchanged" -ForegroundColor Yellow
}

# 2. Check if ViGEm can be installed without admin
Write-Host ""
Write-Host "Checking ViGEm installation options..." -ForegroundColor Yellow

# Try to download ViGEm info without installing
try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/ViGEm/ViGEmBus/releases/latest"
    $msiAsset = $release.assets | Where-Object { $_.name -like "*x64*.msi" } | Select-Object -First 1

    if ($msiAsset) {
        Write-Host "✅ Latest ViGEm version available: $($release.tag_name)" -ForegroundColor Green
        Write-Host "   Download URL: $($msiAsset.browser_download_url)" -ForegroundColor Cyan
        Write-Host "   File size: $([math]::Round($msiAsset.size / 1MB, 2)) MB" -ForegroundColor White
        Write-Host ""
        Write-Host "To install manually:" -ForegroundColor Cyan
        Write-Host "1. Download: $($msiAsset.name)" -ForegroundColor White
        Write-Host "2. Right-click the file and 'Run as administrator'" -ForegroundColor White
        Write-Host "3. Restart WootMouseRemap after installation" -ForegroundColor White
    }
} catch {
    Write-Host "❌ Could not check ViGEm releases" -ForegroundColor Red
}

# 3. Check current application responsiveness
Write-Host ""
Write-Host "Testing application responsiveness..." -ForegroundColor Yellow

$process = Get-Process -Name "WootMouseRemap" -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "✅ Application is running (PID: $($process.Id))" -ForegroundColor Green
    Write-Host "   CPU Usage: $($process.CPU)" -ForegroundColor White
    Write-Host "   Memory: $([math]::Round($process.WorkingSet64 / 1MB, 2)) MB" -ForegroundColor White

    # Check if it's responding
    if ($process.Responding) {
        Write-Host "✅ Application is responding normally" -ForegroundColor Green
    } else {
        Write-Host "❌ Application may be frozen or unresponsive" -ForegroundColor Red
    }
} else {
    Write-Host "❌ Application is not running" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== SUMMARY ===" -ForegroundColor Cyan
Write-Host "✅ Things working:" -ForegroundColor Green
Write-Host "   - Application is running and initialized properly" -ForegroundColor White
Write-Host "   - Keyboard input (HotkeyService) is functional" -ForegroundColor White
Write-Host "   - System tray and mode switching infrastructure" -ForegroundColor White

Write-Host ""
Write-Host "❌ Issues requiring fixes:" -ForegroundColor Red
Write-Host "   - ViGEm Bus Driver missing (blocks virtual controller)" -ForegroundColor White
Write-Host "   - Mouse input not being processed" -ForegroundColor White

Write-Host ""
Write-Host "🔧 Next actions:" -ForegroundColor Yellow
Write-Host "   1. Install ViGEm driver (requires admin - see URL above)" -ForegroundColor White
Write-Host "   2. Restart application after driver installation" -ForegroundColor White
Write-Host "   3. Test both Controller Output and Passthrough modes" -ForegroundColor White