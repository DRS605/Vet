# =============================================================================
#  ALXOR Vet - Actualizador de UN CLIC para el cliente (Windows, sin conocimientos).
#  Descarga la ultima version publicada, cierra la app, reemplaza los archivos de
#  la instalacion (los DATOS no se tocan: viven en %LOCALAPPDATA%\ALXOR Vet) y
#  vuelve a arrancar. Se ejecuta desde una COPIA en %TEMP% (lo lanza el .bat) para
#  poder sobrescribir su propia carpeta sin conflictos.
#
#  Uso (normalmente via "Actualizar ALXOR Vet.bat"):
#     powershell -ExecutionPolicy Bypass -File actualizar-cliente.ps1 -Instalacion "C:\Vetis\ALXOR-Vet"
# =============================================================================
param([Parameter(Mandatory=$true)][string]$Instalacion)

$ErrorActionPreference = 'Stop'

# URL del paquete publicado (rama 'descargas' del repositorio).
$Url    = 'https://github.com/DRS605/Vet/raw/descargas/ALXORVet-Windows.zip'
$Puerto = 8090

function Log($m, $c = 'Gray') { Write-Host $m -ForegroundColor $c }

Write-Host ''
Log '==============================================' 'Cyan'
Log '   ALXOR Vet - Actualizacion' 'Cyan'
Log '==============================================' 'Cyan'

try {
    $Instalacion = $Instalacion.TrimEnd('\')
    if (-not (Test-Path (Join-Path $Instalacion 'app\AlxorCore.Api.exe'))) {
        throw "No parece una instalacion de ALXOR Vet: $Instalacion"
    }
    Log "Carpeta de instalacion: $Instalacion"

    # 1) Descargar la ultima version.
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $zip = Join-Path $env:TEMP 'ALXORVet-Windows.zip'
    Log 'Descargando la ultima version... (puede tardar segun tu conexion)'
    Invoke-WebRequest -Uri $Url -OutFile $zip -UseBasicParsing

    # 2) Extraer a una carpeta temporal.
    $tmp = Join-Path $env:TEMP 'alxor-actualizacion'
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $tmp -Force
    $src = Join-Path $tmp 'ALXOR-Vet'
    if (-not (Test-Path (Join-Path $src 'app\AlxorCore.Api.exe'))) {
        throw 'El paquete descargado no tiene el formato esperado.'
    }

    # 3) Cerrar la app en marcha (por nombre y liberando el puerto).
    Log 'Cerrando la aplicacion en marcha...'
    Get-Process -Name 'AlxorCore.Api' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    try {
        Get-NetTCPConnection -LocalPort $Puerto -State Listen -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty OwningProcess -Unique |
            ForEach-Object { if ($_ -and $_ -ne 0) { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue } }
    } catch { }
    Start-Sleep -Seconds 2

    # 4) Reemplazar los archivos. Los DATOS del usuario NO estan aqui (viven en
    #    %LOCALAPPDATA%\ALXOR Vet), asi que reemplazar la carpeta es seguro.
    Log 'Instalando los archivos nuevos...'
    & robocopy "$src\app" "$Instalacion\app" /MIR /NFL /NDL /NJH /NJS /R:3 /W:2 | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "No se pudieron copiar los archivos de la app (robocopy $LASTEXITCODE)." }
    & robocopy "$src\scripts" "$Instalacion\scripts" /MIR /NFL /NDL /NJH /NJS /R:3 /W:2 | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "No se pudieron copiar los scripts (robocopy $LASTEXITCODE)." }
    Copy-Item (Join-Path $src '*.bat') $Instalacion -Force -ErrorAction SilentlyContinue
    Copy-Item (Join-Path $src 'LEEME.txt') $Instalacion -Force -ErrorAction SilentlyContinue

    # 5) Arrancar la version nueva (su propio lanzador cierra cualquier resto y
    #    reapunta el autoarranque a esta carpeta).
    Log 'Arrancando la version nueva...' 'Green'
    Start-Process -FilePath (Join-Path $Instalacion 'Arrancar ALXOR Vet.bat') -WorkingDirectory $Instalacion

    # 6) Verificar la version que responde.
    Start-Sleep -Seconds 6
    try {
        $v = Invoke-RestMethod "http://localhost:$Puerto/version.json?x=$(Get-Random)" -TimeoutSec 8
        Log ""
        Log "Actualizado correctamente a: $($v.version)   ($($v.empaquetado))" 'Green'
    } catch {
        Log ''
        Log 'La aplicacion se esta iniciando; en unos segundos estara lista en el navegador.' 'Yellow'
    }

    # Limpieza.
    Remove-Item $zip -Force -ErrorAction SilentlyContinue
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
catch {
    Log ''
    Log "FALLO LA ACTUALIZACION: $_" 'Red'
    Log 'Comprueba tu conexion a internet y vuelve a intentarlo.' 'Yellow'
}

Write-Host ''
Read-Host 'Pulsa ENTER para cerrar esta ventana'
