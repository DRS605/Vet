# =============================================================================
#  ALXOR Vet - Restaurar una copia de seguridad (.dump) con pg_restore --clean
#  Detiene la app, restaura sobre la base actual y avisa de reiniciar.
#  ATENCIÓN: sustituye los datos actuales por los de la copia elegida.
# =============================================================================
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun.ps1')
. (Join-Path $PSScriptRoot 'app.ps1')

function Ruta-Binario {
    param([string]$Nombre)
    $portable = Join-Path $BinPostgres $Nombre
    if (Test-Path $portable) { return $portable }
    return $Nombre
}

try {
    Asegurar-Carpetas
    $config = Leer-Config
    if ($null -eq $config) { Escribir-Log "No hay configuracion. Ejecuta primero 'Instalar ALXOR Vet.bat'." 'ERROR'; exit 1 }

    $dirBackups = Join-Path $RaizDatos 'backups'
    $copias = @(Get-ChildItem -Path $dirBackups -Filter 'alxor-*.dump' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending)
    if ($copias.Count -eq 0) { Escribir-Log "No hay copias de seguridad en $dirBackups." 'ERROR'; exit 1 }

    Write-Host ''
    Write-Host 'Copias de seguridad disponibles (de la más reciente a la más antigua):' -ForegroundColor Cyan
    for ($i = 0; $i -lt [Math]::Min($copias.Count, 20); $i++) {
        $c = $copias[$i]; Write-Host ("  [{0}] {1}  ({2:yyyy-MM-dd HH:mm})" -f $i, $c.Name, $c.LastWriteTime)
    }
    $sel = Read-Host 'Número de copia a restaurar (Enter = la más reciente [0])'
    $idx = 0
    if (-not [string]::IsNullOrWhiteSpace($sel)) { [int]::TryParse($sel.Trim(), [ref]$idx) | Out-Null }
    if ($idx -lt 0 -or $idx -ge $copias.Count) { Escribir-Log "Selección no válida." 'ERROR'; exit 1 }
    $fichero = $copias[$idx].FullName

    $conf = Read-Host "Esto SUSTITUIRÁ los datos actuales por los de '$($copias[$idx].Name)'. Escribe SI para continuar"
    if ($conf.Trim().ToUpper() -ne 'SI') { Escribir-Log 'Restauración cancelada.' 'AVISO'; exit 0 }

    if (Usa-PostgresExistente -Config $config) {
        $pe = $config.PostgresExistente
        $equipo = [string]$pe.Host; $puerto = [int]$pe.Puerto; $usuario = [string]$pe.Usuario; $clave = [string]$pe.Clave; $bd = [string]$pe.Bd
    } else {
        $equipo = 'localhost'; $puerto = [int]$config.PgPuerto; $usuario = [string]$config.PgUsuario; $clave = [string]$config.PgPassword; $bd = [string]$config.BaseDatos
    }

    Escribir-Log 'Deteniendo la aplicación antes de restaurar...'
    try { Detener-App } catch { }

    $pgRestore = Ruta-Binario 'pg_restore.exe'
    Escribir-Log "Restaurando '$fichero' en la base '$bd'..."
    $env:PGPASSWORD = $clave
    try {
        & $pgRestore -h $equipo -p $puerto -U $usuario -d $bd --clean --if-exists --no-owner $fichero 2>&1 | ForEach-Object { Escribir-Log "  pg_restore: $_" }
    } finally {
        Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    }

    Escribir-Log 'Restauración terminada. Arranca de nuevo con "Arrancar ALXOR Vet.bat".' 'OK'
    exit 0
}
catch {
    Escribir-Log "FALLO LA RESTAURACIÓN: $_" 'ERROR'
    exit 1
}
