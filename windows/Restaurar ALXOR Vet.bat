@echo off
REM Restaura una copia de seguridad de ALXOR Vet (sustituye los datos actuales).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\restaurar.ps1"
echo.
pause
