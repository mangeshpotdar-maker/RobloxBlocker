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

set "INSTALL_DIR=%ProgramFiles%\MPCustom"
set "DATA_DIR=%ProgramData%\MPCustom"
set "LOG_DIR=%DATA_DIR%\Logs"
set "LOG_FILE=%LOG_DIR%\install.log"

if not exist "%DATA_DIR%" mkdir "%DATA_DIR%"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo [%DATE% %TIME%] Starting MPCustom Service Batch Installation... >> "%LOG_FILE%"

echo [1/6] Creating installation directories...
echo [%DATE% %TIME%] Creating directories... >> "%LOG_FILE%"
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%" >> "%LOG_FILE%" 2>&1
if not exist "%DATA_DIR%\Config" mkdir "%DATA_DIR%\Config" >> "%LOG_FILE%" 2>&1

echo [2/6] Building MPCustom solution...
echo [%DATE% %TIME%] Building MPCustom.sln... >> "%LOG_FILE%"
dotnet publish MPCustom.sln -c Release -o "%INSTALL_DIR%" --nologo >> "%LOG_FILE%" 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Build failed. Please check log file at "%LOG_FILE%".
    pause
    exit /b 1
)

echo [3/6] Setting directory access control permissions...
echo [%DATE% %TIME%] Setting ACL permissions... >> "%LOG_FILE%"
icacls "%INSTALL_DIR%" /grant:r "Administrators":(OI)(CI)F /grant:r "SYSTEM":(OI)(CI)F /inheritance:r >> "%LOG_FILE%" 2>&1
icacls "%DATA_DIR%" /grant:r "Administrators":(OI)(CI)F /grant:r "SYSTEM":(OI)(CI)F /inheritance:r >> "%LOG_FILE%" 2>&1

echo [4/6] Registering and starting Windows Service (MPCustomService)...
echo [%DATE% %TIME%] Registering MPCustomService... >> "%LOG_FILE%"
sc stop MPCustomService >> "%LOG_FILE%" 2>&1
sc delete MPCustomService >> "%LOG_FILE%" 2>&1

sc create MPCustomService binPath= "%INSTALL_DIR%\MPCustom.Service.exe" start= auto DisplayName= "MPCustom Service" >> "%LOG_FILE%" 2>&1
sc failure MPCustomService reset= 86400 actions= restart/60000/restart/60000/restart/60000 >> "%LOG_FILE%" 2>&1
sc start MPCustomService >> "%LOG_FILE%" 2>&1

echo [5/6] Initializing firewall outbound rules and domain blocks...
echo [%DATE% %TIME%] Executing Installer helper... >> "%LOG_FILE%"
"%INSTALL_DIR%\MPCustom.Installer.exe" --install >> "%LOG_FILE%" 2>&1

echo [6/6] Creating Start Menu shortcut...
set "START_MENU=%ProgramData%\Microsoft\Windows\Start Menu\Programs\MPCustom Control.lnk"
set "TARGET_EXE=%INSTALL_DIR%\MPCustom.Control.exe"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$s=(New-Object -COM WScript.Shell).CreateShortcut('%START_MENU%');$s.TargetPath='%TARGET_EXE%';$s.Save()" >> "%LOG_FILE%" 2>&1

echo.
echo ============================================================================
echo [SUCCESS] MPCustom Service installation completed successfully!
echo [INFO] Comprehensive Installation Log generated at:
echo        %LOG_FILE%
echo ============================================================================
echo.
echo Launching MPCustom Control Administrator Setup Wizard...
start "" "%TARGET_EXE%"

pause
