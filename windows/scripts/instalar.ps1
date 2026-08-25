# =============================================================================
#  ALXOR Vet - Instalacion / primer arranque (Windows, SIN Docker)
# =============================================================================
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'comun.ps1')
. (Join-Path $PSScriptRoot 'postgres.ps1')
. (Join-Path $PSScriptRoot 'app.ps1')

# --- Funciones locales del instalador (definir antes de usarlas) -------------
function Configurar-Firewall {
    param([int]$Puerto)
    $nombreRegla = "ALXOR Vet ($Puerto)"
    if (-not (Es-Administrador)) {
        Escribir-Log "Sin permisos de administrador: NO se ha creado la regla de firewall para el puerto $Puerto." 'AVISO'
        Escribir-Log "Para el acceso desde otros PCs, ejecuta esto UNA vez en un PowerShell 'como administrador':" 'AVISO'
        Escribir-Log "  netsh advfirewall firewall add rule name=`"$nombreRegla`" dir=in action=allow protocol=TCP localport=$Puerto" 'AVISO'
        return
    }
    try {
        $existe = netsh advfirewall firewall show rule name="$nombreRegla" 2>$null | Select-String -Pattern $nombreRegla -Quiet
        if ($existe) { Escribir-Log 'La regla de firewall ya existia.' 'OK'; return }
        netsh advfirewall firewall add rule name="$nombreRegla" dir=in action=allow protocol=TCP localport=$Puerto | Out-Null
        Escribir-Log "Regla de firewall creada para el puerto $Puerto (acceso desde la LAN)." 'OK'
    } catch {
        Escribir-Log "No se pudo crear la regla de firewall: $_" 'AVISO'
    }
}

function Crear-AccesoInicio {
    try {
        $carpetaInicio = [Environment]::GetFolderPath('Startup')
        $destino = Join-Path $carpetaInicio 'ALXOR Vet.lnk'
        $objetivo = Join-Path $RaizPaquete 'Arrancar ALXOR Vet.bat'
        if (-not (Test-Path $objetivo)) { Escribir-Log 'No se encontro el .bat de arranque; no se crea el acceso directo.' 'AVISO'; return }
        $ws = New-Object -ComObject WScript.Shell
        $lnk = $ws.CreateShortcut($destino)
        $lnk.TargetPath = $objetivo
        $lnk.WorkingDirectory = $RaizPaquete
        $lnk.WindowStyle = 7   # minimizado
        $lnk.Description = 'Arranca ALXOR Vet al iniciar sesion'
        $lnk.Save()
        Escribir-Log "Acceso directo creado en el Inicio de Windows: $destino" 'OK'
    } catch {
        Escribir-Log "No se pudo crear el acceso directo de inicio: $_" 'AVISO'
    }
}

# --- Instalacion -------------------------------------------------------------
try {
    Asegurar-Carpetas
    Escribir-Log '============================================================'
    Escribir-Log 'ALXOR Vet - INSTALACION (primer arranque)'
    Escribir-Log '============================================================'

    # 1) Comprobar que el ejecutable esta presente.
    if (-not (Test-Path $RutaExe)) {
        Escribir-Log "No se encuentra $RutaExe. Descomprime el paquete completo y no muevas la carpeta 'app'." 'ERROR'
        exit 1
    }

    # 2) Configuracion + secretos (solo la primera vez).
    $config = Leer-Config
    if ($null -eq $config) {
        Escribir-Log 'Generando configuracion y secretos (contrasena de PostgreSQL y clave JWT)...'
        $config = [ordered]@{
            PgPuerto   = $PgPuertoDefecto
            AppPuerto  = $AppPuertoDefecto
            PgUsuario  = 'postgres'
            PgPassword = (Nueva-Password -Longitud 32)
            BaseDatos  = 'alxor'
        }
        Guardar-Config -Config $config
        $jwt = (Nuevo-Secreto -Bytes 48)   # >= 32 caracteres, base64 de 48 bytes
        Escribir-AppSettings -Config $config -JwtSecreto $jwt
        Escribir-Log 'Secretos generados y guardados (config\alxor.config.json + app\appsettings.Production.json).' 'OK'
    } else {
        Escribir-Log 'Ya existia configuracion previa; se reutiliza (no se regeneran secretos).' 'OK'
        if (-not (Test-Path $RutaAppSettings)) {
            $jwt = (Nuevo-Secreto -Bytes 48)
            Escribir-AppSettings -Config $config -JwtSecreto $jwt
        }
    }

    # 3) PostgreSQL portable: binarios, cluster y arranque.
    if (-not (Asegurar-BinariosPostgres)) { throw 'No se pudo preparar PostgreSQL portable.' }
    if (-not (Inicializar-Cluster -Usuario $config.PgUsuario -Password $config.PgPassword)) { throw 'No se pudo inicializar el cluster de datos.' }
    if (-not (Arrancar-Postgres -Puerto $config.PgPuerto)) { throw 'No se pudo arrancar PostgreSQL.' }
    if (-not (Asegurar-BaseDatos -Puerto $config.PgPuerto -Usuario $config.PgUsuario -Password $config.PgPassword -BaseDatos $config.BaseDatos)) { throw 'No se pudo crear la base de datos.' }

    # 4) Regla de firewall para acceso LAN (requiere admin; si no, se documenta).
    Configurar-Firewall -Puerto $config.AppPuerto

    # 5) Acceso directo en el Inicio de Windows (arranca al encender).
    Crear-AccesoInicio

    # 6) Arrancar la aplicacion (migra la BD sola al arrancar).
    if (-not (Arrancar-App -Puerto $config.AppPuerto)) { throw 'La aplicacion no arranco correctamente.' }

    # 7) Abrir el navegador en el asistente de primer arranque.
    $url = "http://localhost:$($config.AppPuerto)/vet.html"
    Escribir-Log "Abriendo el navegador en $url" 'OK'
    try { Start-Process $url } catch { Escribir-Log "No se pudo abrir el navegador solo. Abrelo a mano en: $url" 'AVISO' }

    $ip = Obtener-IpLocal
    Escribir-Log '------------------------------------------------------------' 'OK'
    Escribir-Log 'INSTALACION COMPLETADA.' 'OK'
    Escribir-Log "En este PC:            http://localhost:$($config.AppPuerto)/vet.html" 'OK'
    if ($ip) { Escribir-Log "Desde otros PCs (LAN): http://$($ip):$($config.AppPuerto)/vet.html" 'OK' }
    Escribir-Log 'Sigue el asistente de la pantalla: empresa + usuario administrador + vacunas.' 'OK'
    Escribir-Log '------------------------------------------------------------' 'OK'
    exit 0
}
catch {
    Escribir-Log "FALLO LA INSTALACION: $_" 'ERROR'
    Escribir-Log 'Revisa el detalle en logs\instalacion.log, logs\postgres.log y logs\app.log.' 'AVISO'
    exit 1
}
