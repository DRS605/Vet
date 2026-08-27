@echo off
REM Actualiza ALXOR Vet a la ultima version con un solo clic.
REM Copia el actualizador a %TEMP% y lo ejecuta desde alli (para poder reemplazar
REM esta misma carpeta sin conflictos) pasandole la ruta de instalacion.
setlocal
set "DIR=%~dp0"
set "DIR=%DIR:~0,-1%"
copy /Y "%DIR%\scripts\actualizar-cliente.ps1" "%TEMP%\alxor-actualizar.ps1" >nul
start "ALXOR Vet - Actualizacion" powershell -NoProfile -ExecutionPolicy Bypass -File "%TEMP%\alxor-actualizar.ps1" -Instalacion "%DIR%"
exit
