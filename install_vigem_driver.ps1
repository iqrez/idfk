# ViGEm Driver Installation Script
Write-Host "=== ViGEm Bus Driver Installation ===" -ForegroundColor Cyan
Write-Host ""

# Check if running as administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")

if (-not $isAdmin) {
    Write-Host "❌ This script requires administrator privileges" -ForegroundColor Red
    Write-Host "Please run PowerShell as Administrator and try again" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Alternative: Manual installation steps:" -ForegroundColor Cyan
    Write-Host "1. Go to: https://github.com/ViGEm/ViGEmBus/releases" -ForegroundColor White
    Write-Host "2. Download the latest ViGEmBus_x64.msi" -ForegroundColor White
    Write-Host "3. Run the installer as Administrator" -ForegroundColor White
    Write-Host "4. Restart WootMouseRemap application" -ForegroundColor White
    exit 1
}

Write-Host "✅ Running with administrator privileges" -ForegroundColor Green
Write-Host ""

# Check if ViGEm is already installed
$vigem = Get-PnpDevice -Class "System" | Where-Object {$_.FriendlyName -like "*ViGEm*"}
if ($vigem) {
    Write-Host "✅ ViGEm driver already installed" -ForegroundColor Green
    Write-Host "Driver may need to be updated or there may be another issue" -ForegroundColor Yellow
    exit 0
}

Write-Host "Attempting to download and install ViGEm Bus Driver..." -ForegroundColor Yellow
Write-Host ""

try {
    # Create temp directory
    $tempDir = "$env:TEMP\ViGEmInstall"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

    # Download the latest release info
    Write-Host "Checking for latest ViGEm release..." -ForegroundColor Yellow
    $apiUrl = "https://api.github.com/repos/ViGEm/ViGEmBus/releases/latest"
    $release = Invoke-RestMethod -Uri $apiUrl

    # Find the x64 MSI download
    $msiAsset = $release.assets | Where-Object { $_.name -like "*x64*.msi" } | Select-Object -First 1

    if (-not $msiAsset) {
        Write-Host "❌ Could not find x64 MSI in latest release" -ForegroundColor Red
        Write-Host "Please download manually from: https://github.com/ViGEm/ViGEmBus/releases" -ForegroundColor Cyan
        exit 1
    }

    $downloadUrl = $msiAsset.browser_download_url
    $fileName = $msiAsset.name
    $filePath = Join-Path $tempDir $fileName

    Write-Host "Downloading: $fileName" -ForegroundColor Yellow
    Write-Host "From: $downloadUrl" -ForegroundColor Gray

    # Download the file
    Invoke-WebRequest -Uri $downloadUrl -OutFile $filePath -UseBasicParsing

    if (-not (Test-Path $filePath)) {
        Write-Host "❌ Download failed" -ForegroundColor Red
        exit 1
    }

    Write-Host "✅ Download completed" -ForegroundColor Green
    Write-Host ""

    # Install the MSI
    Write-Host "Installing ViGEm Bus Driver..." -ForegroundColor Yellow
    Write-Host "This may take a few moments..." -ForegroundColor Gray

    $installArgs = @(
        "/i"
        "`"$filePath`""
        "/quiet"
        "/norestart"
    )

    $process = Start-Process -FilePath "msiexec.exe" -ArgumentList $installArgs -Wait -PassThru

    if ($process.ExitCode -eq 0) {
        Write-Host "✅ ViGEm Bus Driver installed successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Cyan
        Write-Host "1. Restart the WootMouseRemap application" -ForegroundColor White
        Write-Host "2. Test both Controller Output and Passthrough modes" -ForegroundColor White
        Write-Host "3. Use the system tray diagnostics to verify functionality" -ForegroundColor White
    } else {
        Write-Host "❌ Installation failed with exit code: $($process.ExitCode)" -ForegroundColor Red
        Write-Host "Try manual installation from: https://github.com/ViGEm/ViGEmBus/releases" -ForegroundColor Cyan
    }

    # Cleanup
    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue

} catch {
    Write-Host "❌ Error during installation: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install manually:" -ForegroundColor Cyan
    Write-Host "1. Go to: https://github.com/ViGEm/ViGEmBus/releases" -ForegroundColor White
    Write-Host "2. Download ViGEmBus_x64.msi" -ForegroundColor White
    Write-Host "3. Run as Administrator" -ForegroundColor White
}