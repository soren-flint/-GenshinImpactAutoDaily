@echo off
chcp 65001 >nul
title BetterGI Uninstall
echo ============================================
echo   BetterGI Idle Trigger - Uninstall
echo ============================================
echo.

echo [1/2] Removing startup launcher...
del /f /q "C:\Users\32683\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\BetterGI_IdleLauncher.vbs" 2>nul
if exist "C:\Users\32683\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\BetterGI_IdleLauncher.vbs" (
    echo   FAIL - Could not remove startup file. Try running as Administrator.
) else (
    echo   OK - Startup launcher removed.
)

echo.
echo [2/2] Removing project files...
del /f /q "D:\MIHOYO\GenShinTool\每日驱动\IdleMonitor.ps1" 2>nul
del /f /q "D:\MIHOYO\GenShinTool\每日驱动\Execution-Log.md" 2>nul
del /f /q "D:\MIHOYO\GenShinTool\每日驱动\.bettergi_last_run" 2>nul

echo   OK - Project files removed.

:: Try to remove the folder if empty
rd "D:\MIHOYO\GenShinTool\每日驱动" 2>nul
if not exist "D:\MIHOYO\GenShinTool\每日驱动" (
    echo   OK - Folder removed.
) else (
    echo   INFO - Folder not empty, kept.
)

echo.
echo ============================================
echo   Uninstall complete
echo ============================================
pause
