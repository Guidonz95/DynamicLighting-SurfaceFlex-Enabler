@echo off
cd /d "%~dp0"
net session >nul 2>&1 || (powershell -Command "Start-Process '%~f0' -Verb RunAs" & exit /b)
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\Uninstall.ps1"
pause
