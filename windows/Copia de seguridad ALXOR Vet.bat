@echo off
REM Copia de seguridad de la base de datos de ALXOR Vet.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\backup.ps1"
echo.
pause
