# Deploy-Skript fuer treadmin (kein Admin noetig nach einmaliger icacls-Einrichtung)
# Voraussetzung: icacls "C:\inetpub\Schulverwaltung" /grant "treadmin:(OI)(CI)(F)" /T

$iisPath  = "C:\inetpub\Schulverwaltung"
$buildSrc = "C:\schulverwaltung\src"
$offline  = Join-Path $iisPath "app_offline.htm"

Write-Host "=== Schulverwaltung Deploy ===" -ForegroundColor Cyan

# 1. Build
Write-Host "Baue Release..." -ForegroundColor Yellow
& dotnet build-server shutdown 2>&1 | Out-Null
& dotnet build "$buildSrc\Schulverwaltung.Web\Schulverwaltung.Web.csproj" -c Release
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FEHLGESCHLAGEN!" -ForegroundColor Red; exit 1 }

# 2. App offline schalten (IIS-Modul stoppt den Prozess und gibt DLLs frei)
Write-Host "App offline schalten..." -ForegroundColor Yellow
"<html><body>Wartung - bitte kurz warten...</body></html>" | Set-Content $offline
Start-Sleep -Seconds 3

# 3. DLLs kopieren
Write-Host "Kopiere DLLs..." -ForegroundColor Yellow
$binPath = "$buildSrc\Schulverwaltung.Web\bin\Release\net8.0"
$dlls = @(
    "Schulverwaltung.Web.dll",
    "Schulverwaltung.Web.pdb",
    "Schulverwaltung.Infrastructure.dll",
    "Schulverwaltung.Infrastructure.pdb",
    "Schulverwaltung.Application.dll",
    "Schulverwaltung.Domain.dll"
)
foreach ($dll in $dlls) {
    $src = Join-Path $binPath $dll
    if (Test-Path $src) {
        Copy-Item $src $iisPath -Force
        Write-Host "  OK: $dll" -ForegroundColor Green
    }
}

# 4. App wieder online
Write-Host "App wieder online..." -ForegroundColor Yellow
Remove-Item $offline -Force

# 5. Git commit + push
Write-Host "Git commit & push..." -ForegroundColor Yellow
Set-Location "C:\Schulverwaltung"
$geaendert = git status --porcelain
if ($geaendert) {
    $zeitstempel = Get-Date -Format "yyyy-MM-dd HH:mm"
    git add .
    git commit -m "Deploy $zeitstempel"
    git push
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  OK: Aenderungen gepusht" -ForegroundColor Green
    } else {
        Write-Host "  WARNUNG: Git push fehlgeschlagen (Deploy war trotzdem erfolgreich)" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Keine Aenderungen zum Committen" -ForegroundColor Gray
}

Write-Host "=== Deploy abgeschlossen ===" -ForegroundColor Cyan
