@echo off
:: ============================================================================
:: MPCustom Service - One-Click Administrative Uninstaller Batch Script
:: ============================================================================
setlocal EnableDelayedExpansion

title MPCustom Service Uninstaller

:: Check for Administrator Privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Administrative privileges required. Elevating via UAC...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

echo ============================================================================
echo                     MPCustom Service Uninstaller
echo ============================================================================
echo.

set "INSTALL_DIR=%ProgramFiles%\MPCustom"
set "DATA_DIR=%ProgramData%\MPCustom"

echo [1/4] Stopping and removing MPCustomService...
sc stop MPCustomService >nul 2>&1
sc delete MPCustomService >nul 2>&1

echo [2/4] Removing MPCustom firewall rules and domain blocks...
if exist "%INSTALL_DIR%\MPCustom.Installer.exe" (
    "%INSTALL_DIR%\MPCustom.Installer.exe" --uninstall >nul 2>&1
)
netsh advfirewall firewall delete rule name="MPCustom_Block_*" >nul 2>&1

echo [3/4] Removing Start Menu shortcuts...
set "START_MENU=%ProgramData%\Microsoft\Windows\Start Menu\Programs\MPCustom Control.lnk"
if exist "%START_MENU%" del /f /q "%START_MENU%" >nul 2>&1

echo [4/4] Removing application directories...
if exist "%INSTALL_DIR%" rmdir /s /q "%INSTALL_DIR%" >nul 2>&1
if exist "%DATA_DIR%" rmdir /s /q "%DATA_DIR%" >nul 2>&1

echo.
echo ============================================================================
echo [SUCCESS] MPCustom Service cleanly uninstalled!
echo ============================================================================
echo.
pause
