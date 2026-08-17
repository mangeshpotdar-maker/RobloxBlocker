@echo off
:: ============================================================================
:: MPCustom Service - One-Click Standalone Installer Batch Script
:: ============================================================================
setlocal EnableDelayedExpansion

title MPCustom Service Installer

:: Ensure script working directory is preserved under UAC elevation
cd /d "%~dp0"
set "SCRIPT_DIR=%~dp0"

:: Check for Administrator Privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Administrative privileges required. Elevating via UAC...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

:: Re-verify directory after UAC elevation
cd /d "%~dp0"
set "SCRIPT_DIR=%~dp0"

echo ============================================================================
echo                      MPCustom Service Setup Utility
echo ============================================================================
echo.

set "TARGET_DIR=C:\Mangesh\Jules\Roblox"
set "ALT_DIR=C:\Mangesh\Jules"
set "LOG_DIR=%TARGET_DIR%\Logs"
set "LOG_FILE=%LOG_DIR%\install.log"

if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"
if not exist "%ALT_DIR%" mkdir "%ALT_DIR%"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"
if not exist "%TARGET_DIR%\Config" mkdir "%TARGET_DIR%\Config"

echo [%DATE% %TIME%] Starting MPCustom Service Standalone Installation... > "%LOG_FILE%"
echo [%DATE% %TIME%] Script Directory: %SCRIPT_DIR% >> "%LOG_FILE%"
echo [%DATE% %TIME%] Target Directory: %TARGET_DIR% >> "%LOG_FILE%"

:: ============================================================================
:: 1. PREREQUISITE AUDIT & AUTOMATED DEPENDENCY INSTALLATION
:: ============================================================================
echo [1/5] Auditing system prerequisites (.NET 8 Desktop Runtime)...
echo [%DATE% %TIME%] Auditing .NET Desktop Runtime prerequisite... >> "%LOG_FILE%"

reg query "HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App" >nul 2>&1
set DESKTOP_REG_EXISTS=%errorlevel%

if %DESKTOP_REG_EXISTS% neq 0 (
    echo [INFO] .NET 8 Desktop Runtime not detected. Downloading official Microsoft installer...
    echo [%DATE% %TIME%] .NET Desktop Runtime missing. Downloading from Microsoft CDN... >> "%LOG_FILE%"

    set "DOTNET_INSTALLER=%TEMP%\windowsdesktop-runtime-8.0.12-win-x64.exe"
    set "DOTNET_URL=https://dotnetcli.azureedge.net/dotnet/Runtime/8.0.12/windowsdesktop-runtime-8.0.12-win-x64.exe"

    powershell -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; (New-Object System.Net.WebClient).DownloadFile('https://dotnetcli.azureedge.net/dotnet/Runtime/8.0.12/windowsdesktop-runtime-8.0.12-win-x64.exe', '$env:TEMP\windowsdesktop-runtime-8.0.12-win-x64.exe')" >> "%LOG_FILE%" 2>&1

    if exist "%TEMP%\windowsdesktop-runtime-8.0.12-win-x64.exe" (
        echo [INFO] Installing .NET 8 Desktop Runtime silently...
        echo [%DATE% %TIME%] Running .NET Runtime silent installer... >> "%LOG_FILE%"
        start /wait "" "%TEMP%\windowsdesktop-runtime-8.0.12-win-x64.exe" /install /quiet /norestart >> "%LOG_FILE%" 2>&1
        del /f /q "%TEMP%\windowsdesktop-runtime-8.0.12-win-x64.exe" >nul 2>&1
        echo [INFO] .NET 8 Desktop Runtime installed successfully.
    ) else (
        echo [WARNING] Could not download .NET Runtime automatically. Please ensure internet access.
        echo [%DATE% %TIME%] Failed to download .NET Runtime installer. >> "%LOG_FILE%"
    )
) else (
    echo [INFO] Prerequisite check passed: .NET 8 Desktop Runtime is available.
    echo [%DATE% %TIME%] Prerequisite check passed: .NET 8 Desktop Runtime exists. >> "%LOG_FILE%"
)

:: ============================================================================
:: 2. LOCATE & DEPLOY PRE-COMPILED BINARIES (RECURSIVE DISCOVERY)
:: ============================================================================
echo [2/5] Locating and deploying application binaries...
echo [%DATE% %TIME%] Searching for application binaries... >> "%LOG_FILE%"

set "SRC_DIR="

if exist "%SCRIPT_DIR%dist\MPCustom.Service.exe" (
    set "SRC_DIR=%SCRIPT_DIR%dist"
) else if exist "%SCRIPT_DIR%MPCustom.Service.exe" (
    set "SRC_DIR=%SCRIPT_DIR%"
) else (
    for /r "%SCRIPT_DIR%" %%F in (MPCustom.Service.exe) do (
        if exist "%%F" (
            set "SRC_DIR=%%~dpF"
            :: Trim trailing backslash if present
            if "!SRC_DIR:~-1!"=="\" set "SRC_DIR=!SRC_DIR:~0,-1!"
        )
    )
)

if defined SRC_DIR (
    echo [INFO] Binaries located in: "!SRC_DIR!"
    echo [%DATE% %TIME%] Deploying binaries from "!SRC_DIR!" to "%TARGET_DIR%"... >> "%LOG_FILE%"
    xcopy "!SRC_DIR!\*" "%TARGET_DIR%\" /E /Y /Q >> "%LOG_FILE%" 2>&1
    xcopy "!SRC_DIR!\*" "%ALT_DIR%\" /E /Y /Q >> "%LOG_FILE%" 2>&1
) else (
    echo [ERROR] Application binaries not found in installer directory!
    echo [%DATE% %TIME%] ERROR: MPCustom.Service.exe missing in %SCRIPT_DIR% >> "%LOG_FILE%"
    pause
    exit /b 1
)

:: Verify critical executable existence
if not exist "%TARGET_DIR%\MPCustom.Service.exe" (
    echo [ERROR] MPCustom.Service.exe missing in %TARGET_DIR%!
    echo [%DATE% %TIME%] ERROR: MPCustom.Service.exe missing in %TARGET_DIR% >> "%LOG_FILE%"
    pause
    exit /b 1
)

:: ============================================================================
:: 3. DIRECTORY ACCESS CONTROL PERMISSIONS
:: ============================================================================
echo [3/5] Setting security access control permissions...
echo [%DATE% %TIME%] Setting ACL permissions... >> "%LOG_FILE%"
icacls "%TARGET_DIR%" /grant:r "Administrators":(OI)(CI)F /grant:r "SYSTEM":(OI)(CI)F /inheritance:r >> "%LOG_FILE%" 2>&1
icacls "%ALT_DIR%" /grant:r "Administrators":(OI)(CI)F /grant:r "SYSTEM":(OI)(CI)F /inheritance:r >> "%LOG_FILE%" 2>&1

:: ============================================================================
:: 4. WINDOWS SERVICE REGISTRATION
:: ============================================================================
echo [4/5] Registering and starting Windows Service (MPCustomService)...
echo [%DATE% %TIME%] Registering MPCustomService... >> "%LOG_FILE%"

sc query MPCustomService >nul 2>&1
if %errorlevel% equ 0 (
    sc stop MPCustomService >> "%LOG_FILE%" 2>&1
    sc delete MPCustomService >> "%LOG_FILE%" 2>&1
)

sc create MPCustomService binPath= "%TARGET_DIR%\MPCustom.Service.exe" start= auto DisplayName= "MPCustom Service" >> "%LOG_FILE%" 2>&1
sc failure MPCustomService reset= 86400 actions= restart/60000/restart/60000/restart/60000 >> "%LOG_FILE%" 2>&1
sc start MPCustomService >> "%LOG_FILE%" 2>&1

:: ============================================================================
:: 5. FIREWALL & DOMAIN PROTECTION INITIALIZATION
:: ============================================================================
echo [5/5] Initializing firewall outbound rules and domain blocks...
echo [%DATE% %TIME%] Initializing firewall rules... >> "%LOG_FILE%"
if exist "%TARGET_DIR%\MPCustom.Installer.exe" (
    "%TARGET_DIR%\MPCustom.Installer.exe" --install >> "%LOG_FILE%" 2>&1
)

:: ============================================================================
:: 6. START MENU SHORTCUT & INITIAL LAUNCH
:: ============================================================================
echo Creating Start Menu shortcut...
set "START_MENU=%ProgramData%\Microsoft\Windows\Start Menu\Programs\MPCustom Control.lnk"
set "TARGET_EXE=%TARGET_DIR%\MPCustom.Control.exe"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$s=(New-Object -COM WScript.Shell).CreateShortcut('%START_MENU%');$s.TargetPath='%TARGET_EXE%';$s.Save()" >> "%LOG_FILE%" 2>&1

echo.
echo ============================================================================
echo [SUCCESS] MPCustom Service installation completed successfully!
echo [INFO] Target Directory: %TARGET_DIR%
echo [INFO] Executables Deployed:
echo        - %TARGET_DIR%\MPCustom.Control.exe
echo        - %TARGET_DIR%\MPCustom.Service.exe
echo        - %ALT_DIR%\MPCustom.Control.exe
echo [INFO] Installation log file saved to:
echo        %LOG_FILE%
echo ============================================================================
echo.
echo Launching MPCustom Control Administrator Setup Wizard...
if exist "%TARGET_DIR%\MPCustom.Control.exe" (
    start "" "%TARGET_DIR%\MPCustom.Control.exe"
) else if exist "%ALT_DIR%\MPCustom.Control.exe" (
    start "" "%ALT_DIR%\MPCustom.Control.exe"
)

pause
