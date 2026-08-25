@echo off
chcp 65001 >nul
title Arrancar ALXOR Vet
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\arrancar.ps1"
echo.
echo   Si algo fallo, revisa la carpeta "logs".
echo.
timeout /t 6 >nul
