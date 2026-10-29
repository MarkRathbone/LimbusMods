@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0BepInEx\maintenance\Start-ModdedLimbus.ps1" %*
if errorlevel 1 pause
