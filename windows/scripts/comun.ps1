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
$Global:AppPuertoDefecto = 8090

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

# --- PostgreSQL ya instalado en el equipo (opcion) ---------------------------
# Puerto estandar de un PostgreSQL instalado por el usuario (servicio del SO).
$Global:PgPuertoExistenteDefecto = 5432

# Comprueba si hay un servicio TCP escuchando (p. ej. PostgreSQL) usando un
# TcpClient con timeout corto. Es mas rapido y fiable que Test-NetConnection.
function Probar-PuertoTcp {
    param([string]$Equipo = 'localhost', [int]$Puerto = 5432, [int]$TimeoutMs = 1500)
    $cliente = $null
    try {
        $cliente = New-Object System.Net.Sockets.TcpClient
        $iar = $cliente.BeginConnect($Equipo, $Puerto, $null, $null)
        $conectado = $iar.AsyncWaitHandle.WaitOne($TimeoutMs, $false)
        if ($conectado -and $cliente.Connected) {
            $cliente.EndConnect($iar)
            return $true
        }
        return $false
    } catch {
        return $false
    } finally {
        if ($cliente) { $cliente.Close() }
    }
}

# Escapa un valor para una cadena de conexion estilo ADO.NET/Npgsql. Si el valor
# lleva caracteres problematicos (; = ' " o espacios al principio/fin) se envuelve
# entre comillas dobles y las comillas dobles internas se duplican (regla ADO.NET).
# Asi una contrasena con ; o = no rompe la cadena de conexion.
function Escapar-ValorNpgsql {
    param([string]$Valor)
    if ($null -eq $Valor) { return '' }
    if (($Valor -match '[;=''"]') -or ($Valor -ne $Valor.Trim())) {
        return '"' + ($Valor -replace '"', '""') + '"'
    }
    return $Valor
}

# True si la configuracion indica usar un PostgreSQL ya instalado por el usuario.
function Usa-PostgresExistente {
    param($Config)
    if ($null -eq $Config) { return $false }
    if (-not ($Config.PSObject.Properties.Name -contains 'PostgresExistente')) { return $false }
    $pe = $Config.PostgresExistente
    if ($null -eq $pe) { return $false }
    return ($pe.Usar -eq $true)
}

# Construye la cadena de conexion Npgsql a partir de la config: usa el PostgreSQL
# existente si esta configurado, o el portable en caso contrario.
function Nueva-CadenaConexion {
    param([Parameter(Mandatory=$true)] $Config)
    if (Usa-PostgresExistente -Config $Config) {
        $pe = $Config.PostgresExistente
        $equipo  = if ([string]::IsNullOrWhiteSpace([string]$pe.Host)) { 'localhost' } else { [string]$pe.Host }
        $puerto  = if ($pe.Puerto) { [int]$pe.Puerto } else { $PgPuertoExistenteDefecto }
        $bd      = if ([string]::IsNullOrWhiteSpace([string]$pe.Bd)) { 'alxor' } else { [string]$pe.Bd }
        $usuario = if ([string]::IsNullOrWhiteSpace([string]$pe.Usuario)) { 'postgres' } else { [string]$pe.Usuario }
        $clave   = [string]$pe.Clave
        return "Host=$(Escapar-ValorNpgsql $equipo);Port=$puerto;Database=$(Escapar-ValorNpgsql $bd);Username=$(Escapar-ValorNpgsql $usuario);Password=$(Escapar-ValorNpgsql $clave)"
    }
    # Portable: contrasena generada con alfabeto seguro, pero se escapa igualmente.
    return "Host=localhost;Port=$($Config.PgPuerto);Database=$($Config.BaseDatos);Username=$($Config.PgUsuario);Password=$(Escapar-ValorNpgsql ([string]$Config.PgPassword))"
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

# --- Acceso directo de arranque en el Inicio de Windows ----------------------
# (Re)crea el acceso directo 'ALXOR Vet.lnk' en la carpeta Startup del usuario
# apuntando SIEMPRE al 'Arrancar ALXOR Vet.bat' de ESTA carpeta (la que se esta
# ejecutando ahora). Es la clave para que, tras descomprimir una version nueva
# en otra carpeta y arrancarla, Windows deje de relanzar la carpeta ANTIGUA al
# encender: la ultima carpeta que arrancas se convierte en la de autoarranque.
function Actualizar-AccesoInicio {
    try {
        $carpetaInicio = [Environment]::GetFolderPath('Startup')
        if ([string]::IsNullOrWhiteSpace($carpetaInicio)) { return }
        $destino  = Join-Path $carpetaInicio 'ALXOR Vet.lnk'
        $objetivo = Join-Path $RaizPaquete 'Arrancar ALXOR Vet.bat'
        if (-not (Test-Path $objetivo)) { Escribir-Log 'No se encontro el .bat de arranque; no se actualiza el acceso directo de Inicio.' 'AVISO'; return }
        $ws = New-Object -ComObject WScript.Shell
        $lnk = $ws.CreateShortcut($destino)
        $lnk.TargetPath       = $objetivo
        $lnk.WorkingDirectory = $RaizPaquete
        $lnk.WindowStyle      = 7   # minimizado
        $lnk.Description       = 'Arranca ALXOR Vet al iniciar sesion'
        $lnk.Save()
        Escribir-Log "Autoarranque de Windows apuntando a esta carpeta: $RaizPaquete" 'OK'
    } catch {
        Escribir-Log "No se pudo actualizar el acceso directo de Inicio: $_" 'AVISO'
    }
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
