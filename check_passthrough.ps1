# Check passthrough mode logs
Write-Host "Checking for passthrough-related logs..." -ForegroundColor Yellow

$logFile = "Logs\woot.log"
if (Test-Path $logFile) {
    # Check for passthrough logs
    $passthroughLogs = Get-Content $logFile | Where-Object {
        $_ -like "*passthrough*" -or
        $_ -like "*XInputPassthrough*" -or
        $_ -like "*Virtual pad*" -or
        $_ -like "*ViGEm*"
    } | Select-Object -Last 10

    if ($passthroughLogs) {
        Write-Host "Found passthrough logs:" -ForegroundColor Green
        foreach ($log in $passthroughLogs) {
            Write-Host "   $log" -ForegroundColor White
        }
    } else {
        Write-Host "No passthrough logs found" -ForegroundColor Red
    }

    # Check for controller detection
    $controllerLogs = Get-Content $logFile | Where-Object {
        $_ -like "*controller*" -or
        $_ -like "*Controller*" -or
        $_ -like "*physical*"
    } | Select-Object -Last 5

    if ($controllerLogs) {
        Write-Host ""
        Write-Host "Controller detection logs:" -ForegroundColor Cyan
        foreach ($log in $controllerLogs) {
            Write-Host "   $log" -ForegroundColor White
        }
    }

    # Check recent errors
    $errorLogs = Get-Content $logFile | Where-Object {
        $_ -like "*ERR*" -or $_ -like "*WARN*"
    } | Select-Object -Last 5

    if ($errorLogs) {
        Write-Host ""
        Write-Host "Recent errors/warnings:" -ForegroundColor Red
        foreach ($log in $errorLogs) {
            Write-Host "   $log" -ForegroundColor Yellow
        }
    }

} else {
    Write-Host "Log file not found: $logFile" -ForegroundColor Red
}