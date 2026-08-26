# =============================================================================
#  ALXOR Vet - Copia de seguridad de la base de datos (pg_dump, formato custom)
#  Genera un fichero .dump en %LOCALAPPDATA%\ALXOR Vet\backups y conserva los 30
#  más recientes. Funciona tanto con el PostgreSQL portable como con uno propio.
# =============================================================================
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun.ps1')

function Ruta-Binario {
    param([string]$Nombre)
    $portable = Join-Path $BinPostgres $Nombre
    if (Test-Path $portable) { return $portable }
    return $Nombre  # se confía en el PATH (PostgreSQL instalado por el usuario)
}

try {
    Asegurar-Carpetas
    $config = Leer-Config
    if ($null -eq $config) {
        Escribir-Log "No hay configuracion. Ejecuta primero 'Instalar ALXOR Vet.bat'." 'ERROR'
        exit 1
    }

    if (Usa-PostgresExistente -Config $config) {
        $pe = $config.PostgresExistente
        $equipo = [string]$pe.Host; $puerto = [int]$pe.Puerto; $usuario = [string]$pe.Usuario
        $clave = [string]$pe.Clave; $bd = [string]$pe.Bd
    } else {
        $equipo = 'localhost'; $puerto = [int]$config.PgPuerto; $usuario = [string]$config.PgUsuario
        $clave = [string]$config.PgPassword; $bd = [string]$config.BaseDatos
    }

    $dirBackups = Join-Path $RaizDatos 'backups'
    if (-not (Test-Path $dirBackups)) { New-Item -ItemType Directory -Path $dirBackups -Force | Out-Null }
    $marca = Get-Date -Format 'yyyyMMdd-HHmmss'
    $destino = Join-Path $dirBackups "alxor-$marca.dump"

    $pgDump = Ruta-Binario 'pg_dump.exe'
    Escribir-Log "Copia de seguridad de la base '$bd' ($equipo`:$puerto) -> $destino"
    $env:PGPASSWORD = $clave
    try {
        & $pgDump -h $equipo -p $puerto -U $usuario -d $bd -Fc -f $destino 2>&1 | ForEach-Object { Escribir-Log "  pg_dump: $_" }
    } finally {
        Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    }

    if (-not (Test-Path $destino) -or (Get-Item $destino).Length -eq 0) {
        Escribir-Log 'La copia de seguridad no se generó correctamente (revisa el log).' 'ERROR'
        exit 1
    }

    # Conservar solo las 30 copias más recientes.
    Get-ChildItem -Path $dirBackups -Filter 'alxor-*.dump' | Sort-Object LastWriteTime -Descending |
        Select-Object -Skip 30 | Remove-Item -Force -ErrorAction SilentlyContinue

    $mb = [math]::Round((Get-Item $destino).Length / 1MB, 2)
    Escribir-Log "Copia de seguridad completada: $destino ($mb MB)." 'OK'
    Escribir-Log "Carpeta de copias: $dirBackups" 'OK'
    exit 0
}
catch {
    Escribir-Log "FALLO LA COPIA DE SEGURIDAD: $_" 'ERROR'
    exit 1
}
