# Check if ViGEm is installed
Write-Host "Checking for ViGEm Bus Driver..." -ForegroundColor Yellow

try {
    $vigemDevices = Get-PnpDevice | Where-Object {$_.FriendlyName -like "*ViGEm*"}

    if ($vigemDevices) {
        Write-Host "✅ ViGEm devices found:" -ForegroundColor Green
        foreach ($device in $vigemDevices) {
            Write-Host "   $($device.FriendlyName) - Status: $($device.Status)" -ForegroundColor White
        }
    } else {
        Write-Host "❌ No ViGEm devices found" -ForegroundColor Red
        Write-Host "Checking for any virtual or HID devices..." -ForegroundColor Yellow

        $virtualDevices = Get-PnpDevice | Where-Object {
            $_.FriendlyName -like "*Virtual*" -or
            $_.FriendlyName -like "*Xbox*" -or
            $_.Class -eq "HIDClass"
        } | Select-Object -First 5

        if ($virtualDevices) {
            Write-Host "Found some virtual/HID devices:" -ForegroundColor Cyan
            foreach ($device in $virtualDevices) {
                Write-Host "   $($device.FriendlyName)" -ForegroundColor Gray
            }
        }
    }
} catch {
    Write-Host "Error checking devices: $($_.Exception.Message)" -ForegroundColor Red
}

# Also check if the application needs restart
Write-Host ""
Write-Host "Application restart recommended after driver installation" -ForegroundColor Yellow