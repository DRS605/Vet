@echo off
chcp 65001 >nul
title Instalar ALXOR Vet
cd /d "%~dp0"
echo.
echo   Instalando ALXOR Vet. La primera vez puede tardar varios minutos
echo   (prepara la base de datos y, si hace falta, la descarga). No cierres
echo   esta ventana hasta que termine.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\instalar.ps1"
echo.
echo   Si algo fallo, revisa la carpeta "logs".
echo.
pause
