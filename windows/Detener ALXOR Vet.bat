@echo off
chcp 65001 >nul
title Detener ALXOR Vet
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\detener.ps1"
echo.
pause
