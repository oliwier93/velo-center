@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-VeloCenter.ps1"
exit /b %ERRORLEVEL%
