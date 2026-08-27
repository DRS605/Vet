# =============================================================================
#  ALXOR Vet - Arranque (arranques posteriores a la instalacion)
# =============================================================================
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'comun.ps1')
. (Join-Path $PSScriptRoot 'postgres.ps1')
. (Join-Path $PSScriptRoot 'app.ps1')

try {
    Asegurar-Carpetas
    Escribir-Log '============================================================'
    Escribir-Log 'ALXOR Vet - ARRANCAR'
    Escribir-Log '============================================================'

    $config = Leer-Config
    if ($null -eq $config) {
        Escribir-Log "No hay configuracion. Ejecuta primero 'Instalar ALXOR Vet.bat'." 'ERROR'
        exit 1
    }
    # Compatibilidad: asegura un secreto JWT persistido (configs antiguas).
    if (-not ($config.PSObject.Properties.Name -contains 'JwtSecreto') -or [string]::IsNullOrWhiteSpace($config.JwtSecreto)) {
        $config | Add-Member -NotePropertyName JwtSecreto -NotePropertyValue (Nuevo-Secreto -Bytes 48) -Force
        Guardar-Config -Config $config
        Escribir-Log 'Se genero y persistio un secreto JWT que faltaba en la configuracion.' 'AVISO'
    }
    if (-not (Test-Path $RutaExe)) {
        Escribir-Log "No se encuentra $RutaExe. Descomprime el paquete completo." 'ERROR'
        exit 1
    }

    if (Usa-PostgresExistente -Config $config) {
        # PostgreSQL EXISTENTE (servicio del usuario): no se arranca ni se para aqui.
        $pe = $config.PostgresExistente
        Escribir-Log "Usando PostgreSQL ya instalado ($($pe.Host):$($pe.Puerto)); no se gestiona Postgres portable." 'OK'
        if (-not (Probar-PuertoTcp -Equipo ([string]$pe.Host) -Puerto ([int]$pe.Puerto) -TimeoutMs 3000)) {
            Escribir-Log "No hay conexion con PostgreSQL en $($pe.Host):$($pe.Puerto). Arrancalo y reintenta." 'ERROR'
            throw "Sin conexion a PostgreSQL en $($pe.Host):$($pe.Puerto)."
        }
        # La app crea/migra la BD sola al arrancar; aqui no tocamos la BD.
    } else {
        if (-not (Test-Path (Join-Path $BinPostgres 'pg_ctl.exe'))) {
            Escribir-Log "Falta PostgreSQL portable. Ejecuta primero 'Instalar ALXOR Vet.bat'." 'ERROR'
            exit 1
        }
        if (-not (Arrancar-Postgres -Puerto $config.PgPuerto)) { throw 'No se pudo arrancar PostgreSQL.' }
        if (-not (Asegurar-BaseDatos -Puerto $config.PgPuerto -Usuario $config.PgUsuario -Password $config.PgPassword -BaseDatos $config.BaseDatos)) { throw 'No se pudo asegurar la base de datos.' }
    }

    if (-not (Arrancar-App -Config $config)) { throw 'La aplicacion no arranco correctamente.' }

    # Reapunta el autoarranque de Windows a ESTA carpeta: si el usuario descomprimio
    # una version nueva en otra carpeta, a partir de ahora el PC arrancara esta y no
    # la antigua. (Combinado con Matar-InstanciasApp, elimina el "sigue igual".)
    Actualizar-AccesoInicio

    $url = "http://localhost:$($config.AppPuerto)/vet.html"
    Escribir-Log "Abriendo el navegador en $url" 'OK'
    try { Start-Process $url } catch { Escribir-Log "Abrelo a mano en: $url" 'AVISO' }

    $ip = Obtener-IpLocal
    Escribir-Log 'ALXOR Vet en marcha.' 'OK'
    if ($ip) { Escribir-Log "Desde otros PCs (LAN): http://$($ip):$($config.AppPuerto)/vet.html" 'OK' }
    exit 0
}
catch {
    Escribir-Log "FALLO EL ARRANQUE: $_" 'ERROR'
    Escribir-Log 'Revisa logs\instalacion.log, logs\postgres.log y logs\app.log.' 'AVISO'
    exit 1
}
