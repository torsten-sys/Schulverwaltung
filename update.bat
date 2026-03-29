@echo off
setlocal
set APPPOOL=SchulverwaltungPool
set SITE=Schulverwaltung
set PUBDIR=C:\inetpub\Schulverwaltung
set SRCDIR=C:\Schulverwaltung
set APPCMD=%windir%\system32\inetsrv\appcmd.exe

echo.
echo ================================================
echo  Schulverwaltung - Update
echo ================================================
echo.

:: Administrator-Check
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo FEHLER: Bitte als Administrator ausfuehren!
    echo Rechtsklick auf update.bat -^> Als Administrator ausfuehren
    pause
    exit /b 1
)

:: 1. Website stoppen falls sie laeuft
echo [1/4] Website wird gestoppt...
"%APPCMD%" stop site /site.name:"%SITE%" >nul 2>&1
"%APPCMD%" stop apppool /apppool.name:"%APPPOOL%" >nul 2>&1
timeout /t 2 /nobreak >nul
echo        OK

:: 2. Publizieren
echo [2/4] Code wird kompiliert und publiziert...
cd /d "%SRCDIR%"
dotnet publish src/Schulverwaltung.Web -c Release -o "%PUBDIR%" --nologo -v quiet
if %errorlevel% neq 0 (
    echo.
    echo FEHLER beim Publizieren! Website wird wieder gestartet...
    "%APPCMD%" start apppool /apppool.name:"%APPPOOL%" >nul 2>&1
    "%APPCMD%" start site /site.name:"%SITE%" >nul 2>&1
    pause
    exit /b 1
)
echo        OK

:: 3. App-Pool und Website starten
echo [3/4] Website wird gestartet...
"%APPCMD%" start apppool /apppool.name:"%APPPOOL%" >nul 2>&1
"%APPCMD%" start site /site.name:"%SITE%" >nul 2>&1
timeout /t 2 /nobreak >nul
echo        OK

:: 4. Status pruefen
echo [4/4] Status wird geprueft...
"%APPCMD%" list site /site.name:"%SITE%" | find "Started" >nul 2>&1
if %errorlevel% equ 0 (
    echo        Website laeuft.
) else (
    echo        WARNUNG: Website scheint nicht zu laufen - bitte IIS-Manager pruefen!
)

echo.
echo ================================================
echo  Fertig!  http://localhost:8080
echo ================================================
echo.
pause
