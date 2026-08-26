# =============================================================================
#  ALXOR Vet - Funciones comunes (Windows, sin Docker)
#  Este fichero lo cargan (dot-source) el resto de scripts. No se ejecuta solo.
# =============================================================================

# --- Carpeta de INSTALACION (SOLO LECTURA) -----------------------------------
# La carpeta del paquete es la carpeta PADRE de \scripts (donde estan los .bat).
# IMPORTANTE: se trata como de SOLO LECTURA. Solo se LEE de aqui: el ejecutable
# self-contained y su wwwroot. NADA se escribe dentro (funciona instalada en
# 'C:\Archivos de programa', que no es escribible sin administrador).
$Global:RaizPaquete = Split-Path -Parent $PSScriptRoot
$Global:DirApp      = Join-Path $RaizPaquete 'app'
$Global:RutaExe     = Join-Path $DirApp 'AlxorCore.Api.exe'

# --- Raiz de DATOS escribible (perfil del usuario) ---------------------------
# TODO el estado escribible (base de datos, binarios de PostgreSQL portable,
# logs, secretos/config) vive AQUI, en el perfil del usuario, que SIEMPRE es
# escribible. Asi el paquete se puede instalar en cualquier carpeta, incluida
# 'Archivos de programa', sin pedir permisos de administrador.
$baseLocal = $env:LOCALAPPDATA
if ([string]::IsNullOrWhiteSpace($baseLocal)) {
    # Muy raro (LOCALAPPDATA no definido); caemos al perfil del usuario.
    $baseLocal = Join-Path $env:USERPROFILE 'AppData\Local'
}
$Global:RaizDatos   = Join-Path $baseLocal 'ALXOR Vet'
$Global:DirDatos    = Join-Path $RaizDatos 'datos'
$Global:DirLogs     = Join-Path $RaizDatos 'logs'
$Global:DirConfig   = Join-Path $RaizDatos 'config'
$Global:DirPostgres = Join-Path $RaizDatos 'postgres'

$Global:RutaConfig     = Join-Path $DirConfig 'alxor.config.json'
$Global:LogInstalacion = Join-Path $DirLogs 'instalacion.log'
$Global:LogApp         = Join-Path $DirLogs 'app.log'
$Global:LogPostgres    = Join-Path $DirLogs 'postgres.log'
$Global:PidApp         = Join-Path $DirLogs 'app.pid'

# Binarios de PostgreSQL portable (el zip oficial de EDB extrae una carpeta pgsql\).
$Global:BinPostgres = Join-Path $DirPostgres 'pgsql\bin'

# --- Version de PostgreSQL portable a descargar (editable) -------------------
# Binarios "binaries only" oficiales de EnterpriseDB para Windows x64.
# Si esta URL diera error 404 en el futuro, cambia la version aqui o deja el
# zip descargado a mano dentro de la carpeta 'postgres\'.
$Global:VersionPostgres = '16.4-1'
$Global:UrlPostgres = "https://get.enterprisedb.com/postgresql/postgresql-$VersionPostgres-windows-x64-binaries.zip"

# --- Parametros por defecto --------------------------------------------------
# Puerto de PostgreSQL: 5433 para NO chocar con un PostgreSQL de sistema (5432).
$Global:PgPuertoDefecto  = 5433
$Global:AppPuertoDefecto = 8080

function Asegurar-Carpetas {
    # Solo se crean carpetas bajo la raiz de datos escribible (perfil del
    # usuario). NUNCA se crea nada en la carpeta de instalacion.
    foreach ($d in @($RaizDatos, $DirDatos, $DirLogs, $DirConfig, $DirPostgres)) {
        if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
    }
}

# --- Registro (log) ----------------------------------------------------------
function Escribir-Log {
    param(
        [string]$Mensaje,
        [ValidateSet('INFO','AVISO','ERROR','OK')] [string]$Nivel = 'INFO'
    )
    if (-not (Test-Path $DirLogs)) { New-Item -ItemType Directory -Path $DirLogs -Force | Out-Null }
    $marca = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $linea = "[$marca] [$Nivel] $Mensaje"
    Add-Content -Path $LogInstalacion -Value $linea -Encoding UTF8
    switch ($Nivel) {
        'ERROR' { Write-Host $Mensaje -ForegroundColor Red }
        'AVISO' { Write-Host $Mensaje -ForegroundColor Yellow }
        'OK'    { Write-Host $Mensaje -ForegroundColor Green }
        default { Write-Host $Mensaje }
    }
}

# --- Configuracion (config\alxor.config.json) --------------------------------
function Leer-Config {
    if (Test-Path $RutaConfig) {
        try { return (Get-Content -Path $RutaConfig -Raw -Encoding UTF8 | ConvertFrom-Json) }
        catch { Escribir-Log "No se pudo leer $RutaConfig ($_)" 'ERROR'; return $null }
    }
    return $null
}

function Guardar-Config {
    param([Parameter(Mandatory=$true)] $Config)
    if (-not (Test-Path $DirConfig)) { New-Item -ItemType Directory -Path $DirConfig -Force | Out-Null }
    $Config | ConvertTo-Json -Depth 8 | Set-Content -Path $RutaConfig -Encoding UTF8
}

# --- Secretos aleatorios fuertes --------------------------------------------
# Usa el generador criptografico del sistema (System.Security).
function Nuevo-Secreto {
    param([int]$Bytes = 48)
    $buffer = New-Object 'System.Byte[]' $Bytes
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($buffer) } finally { $rng.Dispose() }
    return [Convert]::ToBase64String($buffer)
}

# Contrasena apta para PostgreSQL/URL (sin caracteres problematicos como / + =).
function Nueva-Password {
    param([int]$Longitud = 32)
    $alfabeto = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
    $sb = New-Object System.Text.StringBuilder
    $bytes = New-Object 'System.Byte[]' $Longitud
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    foreach ($b in $bytes) { [void]$sb.Append($alfabeto[[int]$b % $alfabeto.Length]) }
    return $sb.ToString()
}

# --- Comprobacion de administrador ------------------------------------------
function Es-Administrador {
    try {
        $id = [System.Security.Principal.WindowsIdentity]::GetCurrent()
        $pr = New-Object System.Security.Principal.WindowsPrincipal($id)
        return $pr.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
    } catch { return $false }
}

# --- IP local (para el acceso desde otros PCs de la clinica) -----------------
function Obtener-IpLocal {
    try {
        $ips = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
            Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
            Select-Object -ExpandProperty IPAddress
        if ($ips) { return ($ips | Select-Object -First 1) }
    } catch { }
    return $null
}

# --- Esperar a que la app responda en /salud --------------------------------
function Esperar-App {
    param([int]$Puerto, [int]$SegundosMax = 90)
    $url = "http://localhost:$Puerto/salud"
    for ($i = 0; $i -lt $SegundosMax; $i++) {
        try {
            $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
            if ($r.StatusCode -eq 200) { return $true }
        } catch { Start-Sleep -Seconds 1 }
    }
    return $false
}
