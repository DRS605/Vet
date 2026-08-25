# =============================================================================
#  ALXOR Vet - Gestion de PostgreSQL portable (sin instalacion ni servicios)
#  Se carga (dot-source) desde instalar/arrancar/detener. No se ejecuta solo.
# =============================================================================

# Deja disponibles los binarios de PostgreSQL en la carpeta 'postgres\'.
# Prioridad:
#   1) Ya extraidos en postgres\pgsql\bin  -> se usan tal cual.
#   2) Un .zip presente en postgres\       -> se extrae.
#   3) Descargar el zip oficial de EDB     -> se descarga y se extrae.
function Asegurar-BinariosPostgres {
    $pgCtl = Join-Path $BinPostgres 'pg_ctl.exe'
    if (Test-Path $pgCtl) {
        Escribir-Log "PostgreSQL portable ya presente en $BinPostgres" 'OK'
        return $true
    }

    if (-not (Test-Path $DirPostgres)) { New-Item -ItemType Directory -Path $DirPostgres -Force | Out-Null }

    # 2) Buscar un zip ya descargado a mano.
    $zipLocal = Get-ChildItem -Path $DirPostgres -Filter '*.zip' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($zipLocal) {
        Escribir-Log "Extrayendo PostgreSQL desde $($zipLocal.Name)..."
        try {
            Expand-Archive -Path $zipLocal.FullName -DestinationPath $DirPostgres -Force
        } catch {
            Escribir-Log "No se pudo extraer $($zipLocal.Name): $_" 'ERROR'
            return $false
        }
        if (Test-Path $pgCtl) { Escribir-Log 'PostgreSQL portable listo.' 'OK'; return $true }
        Escribir-Log 'El zip no contenia pgsql\bin\pg_ctl.exe.' 'ERROR'
        return $false
    }

    # 3) Descargar.
    Escribir-Log "Descargando PostgreSQL portable (esto requiere internet la primera vez)..."
    Escribir-Log "  URL: $UrlPostgres"
    $destinoZip = Join-Path $DirPostgres "postgresql-portable.zip"
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $progresoPrevio = $ProgressPreference
        $ProgressPreference = 'SilentlyContinue'   # acelera Invoke-WebRequest
        Invoke-WebRequest -Uri $UrlPostgres -OutFile $destinoZip -UseBasicParsing -TimeoutSec 600
        $ProgressPreference = $progresoPrevio
    } catch {
        Escribir-Log "Fallo la descarga de PostgreSQL: $_" 'ERROR'
        Escribir-Log "Solucion: descarga manualmente el zip 'binaries only' de PostgreSQL para Windows x64" 'AVISO'
        Escribir-Log "desde https://www.enterprisedb.com/download-postgresql-binaries y deja el .zip en la carpeta 'postgres\'." 'AVISO'
        return $false
    }

    Escribir-Log 'Extrayendo PostgreSQL...'
    try {
        Expand-Archive -Path $destinoZip -DestinationPath $DirPostgres -Force
        Remove-Item $destinoZip -Force -ErrorAction SilentlyContinue
    } catch {
        Escribir-Log "No se pudo extraer el zip descargado: $_" 'ERROR'
        return $false
    }

    if (Test-Path $pgCtl) { Escribir-Log 'PostgreSQL portable listo.' 'OK'; return $true }
    Escribir-Log 'Tras la extraccion no se encontro pgsql\bin\pg_ctl.exe.' 'ERROR'
    return $false
}

# initdb en 'datos\' la primera vez (idempotente: si ya existe, no hace nada).
function Inicializar-Cluster {
    param([string]$Usuario, [string]$Password)

    if (Test-Path (Join-Path $DirDatos 'PG_VERSION')) {
        Escribir-Log 'El cluster de datos ya estaba inicializado; se conserva.' 'OK'
        return $true
    }

    if (-not (Test-Path $DirDatos)) { New-Item -ItemType Directory -Path $DirDatos -Force | Out-Null }

    $initdb = Join-Path $BinPostgres 'initdb.exe'
    $pwfile = Join-Path $DirLogs '.pwfile.tmp'
    Set-Content -Path $pwfile -Value $Password -Encoding ascii -NoNewline

    Escribir-Log "Inicializando la base de datos en 'datos\' (initdb)..."
    try {
        & $initdb -D $DirDatos -U $Usuario --pwfile=$pwfile --auth=scram-sha-256 --encoding=UTF8 --locale=C 2>&1 |
            ForEach-Object { Escribir-Log "  initdb: $_" }
    } finally {
        Remove-Item $pwfile -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path (Join-Path $DirDatos 'PG_VERSION')) {
        Escribir-Log 'Cluster de datos inicializado.' 'OK'
        return $true
    }
    Escribir-Log 'initdb no dejo un cluster valido (revisa el log).' 'ERROR'
    return $false
}

function Postgres-EnMarcha {
    param([int]$Puerto)
    $pgCtl = Join-Path $BinPostgres 'pg_ctl.exe'
    if (-not (Test-Path $pgCtl)) { return $false }
    # pg_ctl status: codigo 0 = en marcha, 3 = parado.
    & $pgCtl -D $DirDatos status *> $null
    return ($LASTEXITCODE -eq 0)
}

function Arrancar-Postgres {
    param([int]$Puerto)
    if (Postgres-EnMarcha -Puerto $Puerto) {
        Escribir-Log 'PostgreSQL ya estaba en marcha.' 'OK'
        return $true
    }
    $pgCtl = Join-Path $BinPostgres 'pg_ctl.exe'
    Escribir-Log "Arrancando PostgreSQL en el puerto $Puerto..."
    # -w espera a que acepte conexiones; solo escucha en localhost (no se expone en red).
    & $pgCtl -D $DirDatos -l $LogPostgres -o "-p $Puerto -c listen_addresses=localhost" -w start *>&1 |
        ForEach-Object { Escribir-Log "  pg_ctl: $_" }
    if (Postgres-EnMarcha -Puerto $Puerto) {
        Escribir-Log 'PostgreSQL en marcha.' 'OK'
        return $true
    }
    Escribir-Log 'PostgreSQL no arranco (revisa logs\postgres.log).' 'ERROR'
    return $false
}

function Detener-Postgres {
    $pgCtl = Join-Path $BinPostgres 'pg_ctl.exe'
    if (-not (Test-Path $pgCtl)) { return }
    if (-not (Test-Path (Join-Path $DirDatos 'PG_VERSION'))) { return }
    & $pgCtl -D $DirDatos status *> $null
    if ($LASTEXITCODE -eq 0) {
        Escribir-Log 'Deteniendo PostgreSQL...'
        & $pgCtl -D $DirDatos -m fast -w stop *>&1 | ForEach-Object { Escribir-Log "  pg_ctl: $_" }
        Escribir-Log 'PostgreSQL detenido.' 'OK'
    } else {
        Escribir-Log 'PostgreSQL no estaba en marcha.'
    }
}

# Crea la base de datos 'alxor' si no existe (idempotente).
function Asegurar-BaseDatos {
    param([int]$Puerto, [string]$Usuario, [string]$Password, [string]$BaseDatos)
    $psql = Join-Path $BinPostgres 'psql.exe'
    $env:PGPASSWORD = $Password
    try {
        $existe = & $psql -h localhost -p $Puerto -U $Usuario -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$BaseDatos'" 2>&1
        if ("$existe".Trim() -eq '1') {
            Escribir-Log "La base de datos '$BaseDatos' ya existe." 'OK'
            return $true
        }
        Escribir-Log "Creando la base de datos '$BaseDatos'..."
        $createdb = Join-Path $BinPostgres 'createdb.exe'
        & $createdb -h localhost -p $Puerto -U $Usuario $BaseDatos 2>&1 | ForEach-Object { Escribir-Log "  createdb: $_" }
        # Verificar.
        $existe2 = & $psql -h localhost -p $Puerto -U $Usuario -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$BaseDatos'" 2>&1
        if ("$existe2".Trim() -eq '1') { Escribir-Log "Base de datos '$BaseDatos' creada." 'OK'; return $true }
        Escribir-Log "No se pudo crear la base de datos '$BaseDatos'." 'ERROR'
        return $false
    } finally {
        Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    }
}
