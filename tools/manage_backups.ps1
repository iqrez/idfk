<#
manage_backups.ps1
Scans repository for common backup file patterns and offers interactive restore/delete options.
Run from repository root in PowerShell (requires .NET file access permissions).
#>

Write-Host "Scanning repository for backup files..." -ForegroundColor Cyan

# Patterns to search for
$patterns = @('*.bak','*.backup','*.orig','*.old','*~','*.bak_*','*.bak*')

$repo = Get-Location
$found = @()
foreach ($p in $patterns) {
    $found += Get-ChildItem -Path $repo -Recurse -Filter $p -ErrorAction SilentlyContinue | Where-Object { -not $_.PSIsContainer }
}

$found = $found | Sort-Object -Property FullName -Unique

if ($found.Count -eq 0) {
    Write-Host "No backup files found." -ForegroundColor Green
    return
}

Write-Host "Found backup files:" -ForegroundColor Yellow
$index = 0
foreach ($f in $found) {
    $index++
    Write-Host "[$index] $($f.FullName) ($(Get-FileHash $f.FullName -Algorithm SHA256).Hash.Substring(0,8))" -ForegroundColor White
}

function Guess-OriginalPath($backupPath) {
    # Common conventions: file.ext.bak -> file.ext, file.ext.backup -> file.ext, file~ -> file, file.ext.orig -> file.ext
    $bn = [System.IO.Path]::GetFileName($backupPath)
    $dir = [System.IO.Path]::GetDirectoryName($backupPath)

    # If contains multiple dots and ends with known suffix, strip the suffix
    foreach ($suf in @('.bak','.backup','.orig','.old')) {
        if ($bn.ToLower().EndsWith($suf)) {
            $origName = $bn.Substring(0, $bn.Length - $suf.Length)
            $cand = Join-Path $dir $origName
            if (Test-Path $cand) { return $cand }
            # Also try if the backup was named like "File.cs.bak" -> "File.cs"
            if ($origName -match '\.cs$|\.txt$|\.json$|\.config$|\.xml$|\.csproj$|\.sln$') { return $cand }
        }
    }

    # tilde-based (e.g. File.cs~)
    if ($bn.EndsWith('~')) {
        $origName = $bn.Substring(0, $bn.Length - 1)
        $cand = Join-Path $dir $origName
        if (Test-Path $cand) { return $cand }
    }

    # fallback: try to find similarly named file in same dir ignoring suffixes
    $base = $bn -replace '\.bak.*$','' -replace '_backup.*$','' -replace '\.orig$','' -replace '~$',''
    $cands = Get-ChildItem -Path $dir -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "$base*" }
    if ($cands.Count -gt 0) { return $cands[0].FullName }

    return $null
}

# Build actionable list
$actions = @()
foreach ($f in $found) {
    $orig = Guess-OriginalPath $f.FullName
    $actions += [PSCustomObject]@{ Backup = $f.FullName; Original = $orig }
}

Write-Host "\nSummary:" -ForegroundColor Cyan
foreach ($i in 0..($actions.Count-1)) {
    $a = $actions[$i]
    Write-Host "[$($i+1)] Backup: $($a.Backup)" -ForegroundColor White
    Write-Host "     Guessed original: $($a.Original ?? '(none)')" -ForegroundColor DarkGray
}

Write-Host "\nChoose an action:" -ForegroundColor Yellow
Write-Host "  R  => Restore selected backups to guessed originals (creates .pre_restore backup for existing originals)" -ForegroundColor White
Write-Host "  RA => Restore All" -ForegroundColor White
Write-Host "  D  => Delete selected backup files" -ForegroundColor White
Write-Host "  DA => Delete All" -ForegroundColor White
Write-Host "  L  => List backup files again" -ForegroundColor White
Write-Host "  Q  => Quit" -ForegroundColor White

while ($true) {
    $choice = Read-Host "Action (e.g. RA or R 1,3 or D 2)"
    if ([string]::IsNullOrWhiteSpace($choice)) { continue }
    $choice = $choice.Trim()
    if ($choice -ieq 'Q') { break }
    if ($choice -ieq 'L') {
        foreach ($i in 0..($actions.Count-1)) { $a = $actions[$i]; Write-Host "[$($i+1)] $($a.Backup) -> $($a.Original ?? '(none)')" }
        continue
    }
    if ($choice -ieq 'DA') {
        foreach ($a in $actions) {
            try { Remove-Item -LiteralPath $a.Backup -Force; Write-Host "Deleted: $($a.Backup)" -ForegroundColor Green } catch { Write-Host "Failed delete: $($a.Backup) - $($_.Exception.Message)" -ForegroundColor Red }
        }
        break
    }
    if ($choice -ieq 'RA') {
        foreach ($a in $actions) { Invoke-Restore $a }
        break
    }

    if ($choice -match '^R\s+(.+)$') {
        $lst = $matches[1].Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^[0-9]+$' }
        foreach ($s in $lst) {
            $idx = [int]$s - 1
            if ($idx -ge 0 -and $idx -lt $actions.Count) { Invoke-Restore $actions[$idx] }
        }
        break
    }
    if ($choice -match '^D\s+(.+)$') {
        $lst = $matches[1].Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^[0-9]+$' }
        foreach ($s in $lst) {
            $idx = [int]$s - 1
            if ($idx -ge 0 -and $idx -lt $actions.Count) { Try { Remove-Item -LiteralPath $actions[$idx].Backup -Force; Write-Host "Deleted: $($actions[$idx].Backup)" -ForegroundColor Green } catch { Write-Host "Failed delete: $($actions[$idx].Backup) - $($_.Exception.Message)" -ForegroundColor Red } }
        }
        break
    }

    Write-Host "Unknown command. Try again." -ForegroundColor Red
}

function Invoke-Restore($entry) {
    if ($null -eq $entry.Original) { Write-Host "Skipping restore: no guessed original for $($entry.Backup)" -ForegroundColor Yellow; return }
    $orig = $entry.Original
    $bak = $entry.Backup
    if (-not (Test-Path $bak)) { Write-Host "Backup disappeared: $bak" -ForegroundColor Yellow; return }

    try {
        if (Test-Path $orig) {
            $pre = "$orig.pre_restore_$(Get-Date -Format yyyyMMdd_HHmmss)"
            Copy-Item -LiteralPath $orig -Destination $pre -Force
            Write-Host "Created pre-restore backup: $pre" -ForegroundColor DarkGray
        }
        Copy-Item -LiteralPath $bak -Destination $orig -Force
        Write-Host "Restored: $bak -> $orig" -ForegroundColor Green
    }
    catch {
        Write-Host "Restore failed for $bak -> $orig : $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "Done." -ForegroundColor Cyan
