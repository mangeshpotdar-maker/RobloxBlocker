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

echo [1/6] Creating installation directories...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
if not exist "%DATA_DIR%" mkdir "%DATA_DIR%"
if not exist "%DATA_DIR%\Config" mkdir "%DATA_DIR%\Config"
if not exist "%DATA_DIR%\Logs" mkdir "%DATA_DIR%\Logs"

echo [2/6] Building MPCustom solution...
dotnet publish MPCustom.sln -c Release -o "%INSTALL_DIR%" --nologo
if %errorlevel% neq 0 (
    echo [ERROR] Build failed. Please ensure .NET SDK is installed.
    pause
    exit /b 1
)

echo [3/6] Setting directory access control permissions...
icacls "%INSTALL_DIR%" /grant:r "Administrators":(OI)(CI)F /grant:r "SYSTEM":(OI)(CI)F /inheritance:r >nul 2>&1
icacls "%DATA_DIR%" /grant:r "Administrators":(OI)(CI)F /grant:r "SYSTEM":(OI)(CI)F /inheritance:r >nul 2>&1

echo [4/6] Registering and starting Windows Service (MPCustomService)...
sc stop MPCustomService >nul 2>&1
sc delete MPCustomService >nul 2>&1

sc create MPCustomService binPath= "%INSTALL_DIR%\MPCustom.Service.exe" start= auto DisplayName= "MPCustom Service" >nul 2>&1
sc failure MPCustomService reset= 86400 actions= restart/60000/restart/60000/restart/60000 >nul 2>&1
sc start MPCustomService >nul 2>&1

echo [5/6] Initializing firewall outbound rules and domain blocks...
"%INSTALL_DIR%\MPCustom.Installer.exe" --install >nul 2>&1

echo [6/6] Creating Start Menu shortcut...
set "START_MENU=%ProgramData%\Microsoft\Windows\Start Menu\Programs\MPCustom Control.lnk"
set "TARGET_EXE=%INSTALL_DIR%\MPCustom.Control.exe"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$s=(New-Object -COM WScript.Shell).CreateShortcut('%START_MENU%');$s.TargetPath='%TARGET_EXE%';$s.Save()" >nul 2>&1

echo.
echo ============================================================================
echo [SUCCESS] MPCustom Service installation completed successfully!
echo ============================================================================
echo.
echo Launching MPCustom Control Administrator Setup Wizard...
start "" "%TARGET_EXE%"

pause
