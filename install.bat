@echo off
:: ============================================================================
:: MPCustom Service - One-Click Administrative Installer Batch Script
:: ============================================================================
setlocal EnableDelayedExpansion

title MPCustom Service Installer

:: Check for Administrator Privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Administrative privileges required. Elevating via UAC...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

echo ============================================================================
echo                      MPCustom Service Setup Utility
echo ============================================================================
echo.

set "TARGET_DIR=C:\Mangesh\Jules\Roblox"
set "LOG_DIR=%TARGET_DIR%\Logs"
set "LOG_FILE=%LOG_DIR%\install.log"

if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"
if not exist "%TARGET_DIR%\Config" mkdir "%TARGET_DIR%\Config"

echo [%DATE% %TIME%] Starting MPCustom Service Batch Installation... >> "%LOG_FILE%"

echo [1/6] Creating installation directories...
echo [%DATE% %TIME%] Target directory: %TARGET_DIR% >> "%LOG_FILE%"

echo [2/6] Building MPCustom solution...
echo [%DATE% %TIME%] Building MPCustom.sln... >> "%LOG_FILE%"
dotnet publish MPCustom.sln -c Release -o "%TARGET_DIR%" --nologo >> "%LOG_FILE%" 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Build failed. Please check log file at "%LOG_FILE%".
    pause
    exit /b 1
)

echo [3/6] Setting directory access control permissions...
echo [%DATE% %TIME%] Setting ACL permissions on %TARGET_DIR%... >> "%LOG_FILE%"
icacls "%TARGET_DIR%" /grant:r "Administrators":(OI)(CI)F /grant:r "SYSTEM":(OI)(CI)F /inheritance:r >> "%LOG_FILE%" 2>&1

echo [4/6] Registering and starting Windows Service (MPCustomService)...
echo [%DATE% %TIME%] Registering MPCustomService... >> "%LOG_FILE%"
sc stop MPCustomService >> "%LOG_FILE%" 2>&1
sc delete MPCustomService >> "%LOG_FILE%" 2>&1

sc create MPCustomService binPath= "%TARGET_DIR%\MPCustom.Service.exe" start= auto DisplayName= "MPCustom Service" >> "%LOG_FILE%" 2>&1
sc failure MPCustomService reset= 86400 actions= restart/60000/restart/60000/restart/60000 >> "%LOG_FILE%" 2>&1
sc start MPCustomService >> "%LOG_FILE%" 2>&1

echo [5/6] Initializing firewall outbound rules and domain blocks...
echo [%DATE% %TIME%] Executing Installer helper... >> "%LOG_FILE%"
"%TARGET_DIR%\MPCustom.Installer.exe" --install >> "%LOG_FILE%" 2>&1

echo [6/6] Creating Start Menu shortcut...
set "START_MENU=%ProgramData%\Microsoft\Windows\Start Menu\Programs\MPCustom Control.lnk"
set "TARGET_EXE=%TARGET_DIR%\MPCustom.Control.exe"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$s=(New-Object -COM WScript.Shell).CreateShortcut('%START_MENU%');$s.TargetPath='%TARGET_EXE%';$s.Save()" >> "%LOG_FILE%" 2>&1

echo.
echo ============================================================================
echo [SUCCESS] MPCustom Service installation completed successfully!
echo [INFO] Target Location: %TARGET_DIR%
echo [INFO] Comprehensive Log generated at:
echo        %LOG_FILE%
echo ============================================================================
echo.
echo Launching MPCustom Control Administrator Setup Wizard...
start "" "%TARGET_EXE%"

pause
