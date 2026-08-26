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

function Leer-Si-No {
    param([string]$Pregunta, [bool]$PorDefecto = $false)
    $resp = Read-Host $Pregunta
    if ([string]::IsNullOrWhiteSpace($resp)) { return $PorDefecto }
    return (($resp.Trim().ToLower()) -in @('s', 'si', 'sí', 'y', 'yes'))
}

# Pide de forma interactiva los datos del PostgreSQL ya instalado y los devuelve
# como objeto ordenado (Usar=true). Enmascara la contrasena al teclearla.
function Pedir-DatosPostgresExistente {
    Escribir-Log 'Introduce los datos de tu PostgreSQL (Enter = valor por defecto):'
    $equipo = Read-Host '  Host [localhost]'
    if ([string]::IsNullOrWhiteSpace($equipo)) { $equipo = 'localhost' }

    $puertoTxt = Read-Host "  Puerto [$PgPuertoExistenteDefecto]"
    $puerto = $PgPuertoExistenteDefecto
    if (-not [string]::IsNullOrWhiteSpace($puertoTxt)) {
        $tmp = 0
        if ([int]::TryParse($puertoTxt.Trim(), [ref]$tmp) -and $tmp -gt 0 -and $tmp -le 65535) { $puerto = $tmp }
        else { Escribir-Log "Puerto '$puertoTxt' no valido; se usa $PgPuertoExistenteDefecto." 'AVISO' }
    }

    $usuario = Read-Host '  Usuario [postgres]'
    if ([string]::IsNullOrWhiteSpace($usuario)) { $usuario = 'postgres' }

    # Contrasena enmascarada; se convierte a texto plano (se guarda tal cual).
    $claveSegura = Read-Host '  Contrasena' -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($claveSegura)
    try { $clave = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    if ($null -eq $clave) { $clave = '' }

    $bd = Read-Host '  Nombre de la base de datos [alxor]'
    if ([string]::IsNullOrWhiteSpace($bd)) { $bd = 'alxor' }

    return [ordered]@{
        Usar    = $true
        Host    = $equipo
        Puerto  = $puerto
        Usuario = $usuario
        Clave   = $clave
        Bd      = $bd
    }
}

# Decide y persiste como se usa PostgreSQL. Devuelve $true si se usa uno existente.
# Idempotente: si la config ya trae 'PostgresExistente', se respeta sin preguntar.
function Resolver-Postgres {
    param([Parameter(Mandatory=$true)] $Config)

    # Ya decidido en una instalacion previa: respetar la eleccion guardada.
    if ($Config.PSObject.Properties.Name -contains 'PostgresExistente') {
        if (Usa-PostgresExistente -Config $Config) {
            $pe = $Config.PostgresExistente
            Escribir-Log "Configuracion previa: se usa el PostgreSQL ya instalado ($($pe.Host):$($pe.Puerto), BD '$($pe.Bd)')." 'OK'
            return $true
        }
        Escribir-Log 'Configuracion previa: se usa el PostgreSQL portable.' 'OK'
        return $false
    }

    # Primera vez: detectar y preguntar.
    $detectado = Probar-PuertoTcp -Equipo 'localhost' -Puerto $PgPuertoExistenteDefecto -TimeoutMs 1500
    if ($detectado) {
        Escribir-Log "Se ha detectado algo escuchando en localhost:$PgPuertoExistenteDefecto (posible PostgreSQL)." 'OK'
        $usar = Leer-Si-No 'Se ha detectado PostgreSQL en este equipo. Quieres usarlo? (S/N)' $true
    } else {
        Escribir-Log "No se ha detectado PostgreSQL en localhost:$PgPuertoExistenteDefecto." 'INFO'
        $usar = Leer-Si-No 'Usar un PostgreSQL ya instalado? (S/N) [N = preparar uno portable]' $false
    }

    if (-not $usar) {
        # Se persiste la eleccion (portable) para no volver a preguntar al reinstalar.
        $Config | Add-Member -NotePropertyName PostgresExistente -NotePropertyValue ([ordered]@{ Usar = $false }) -Force
        Guardar-Config -Config $Config
        return $false
    }

    $pe = Pedir-DatosPostgresExistente
    $Config | Add-Member -NotePropertyName PostgresExistente -NotePropertyValue $pe -Force
    Guardar-Config -Config $Config
    Escribir-Log "Datos de PostgreSQL existente guardados en config\alxor.config.json ($($pe.Host):$($pe.Puerto), usuario '$($pe.Usuario)', BD '$($pe.Bd)')." 'OK'
    return $true
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
    #    TODO (incluido el secreto JWT) se guarda bajo la raiz de datos del
    #    usuario (%LOCALAPPDATA%\ALXOR Vet\config\alxor.config.json), NUNCA en la
    #    carpeta de instalacion. Los secretos se reutilizan en cada arranque.
    $config = Leer-Config
    if ($null -eq $config) {
        Escribir-Log 'Generando configuracion y secretos (contrasena de PostgreSQL y clave JWT)...'
        $config = [ordered]@{
            PgPuerto   = $PgPuertoDefecto
            AppPuerto  = $AppPuertoDefecto
            PgUsuario  = 'postgres'
            PgPassword = (Nueva-Password -Longitud 32)
            BaseDatos  = 'alxor'
            JwtSecreto = (Nuevo-Secreto -Bytes 48)   # >= 32 caracteres, base64 de 48 bytes
            Correo     = [ordered]@{
                Habilitado      = $false
                Host            = ''
                Puerto          = 587
                UsarStartTls    = $true
                Usuario         = ''
                Clave           = ''
                Remitente       = 'no-responder@alxor.local'
                RemitenteNombre = 'ALXOR Vet'
            }
        }
        Guardar-Config -Config $config
        Escribir-Log 'Secretos generados y guardados en la raiz de datos del usuario (config\alxor.config.json).' 'OK'
    } else {
        Escribir-Log 'Ya existia configuracion previa; se reutiliza (no se regeneran secretos).' 'OK'
        # Compatibilidad: si la config es de una version antigua sin JwtSecreto,
        # se genera uno y se persiste para reutilizarlo (idempotente).
        if (-not ($config.PSObject.Properties.Name -contains 'JwtSecreto') -or [string]::IsNullOrWhiteSpace($config.JwtSecreto)) {
            $config | Add-Member -NotePropertyName JwtSecreto -NotePropertyValue (Nuevo-Secreto -Bytes 48) -Force
            Guardar-Config -Config $config
            Escribir-Log 'Se genero y persistio un secreto JWT que faltaba en la configuracion previa.' 'OK'
        }
    }

    # Normalizamos la config a objeto (PSCustomObject) releyendola del disco, para
    # poder anadir/leer la seccion 'PostgresExistente' de forma uniforme.
    $config = Leer-Config

    # 3) PostgreSQL: usar uno ya instalado (opcion del usuario) o el portable.
    if (Resolver-Postgres -Config $config) {
        # PostgreSQL EXISTENTE: es un servicio del propio usuario. NO se descarga ni
        # se arranca el portable. La app creara la BD sola al migrar (EF Migrate()).
        $config = Leer-Config   # recargar con la seccion PostgresExistente recien guardada
        $pe = $config.PostgresExistente
        Escribir-Log "Verificando conexion TCP con PostgreSQL en $($pe.Host):$($pe.Puerto)..."
        if (-not (Probar-PuertoTcp -Equipo ([string]$pe.Host) -Puerto ([int]$pe.Puerto) -TimeoutMs 3000)) {
            Escribir-Log "No hay conexion TCP con PostgreSQL en $($pe.Host):$($pe.Puerto)." 'ERROR'
            Escribir-Log 'Comprueba que PostgreSQL esta arrancado, que el puerto es correcto y que acepta conexiones locales.' 'ERROR'
            throw "Sin conexion a PostgreSQL en $($pe.Host):$($pe.Puerto)."
        }
        Escribir-Log "Conexion TCP con PostgreSQL OK. La app creara la BD '$($pe.Bd)' al arrancar si no existe (requiere permiso para crear bases de datos)." 'OK'
    } else {
        # PostgreSQL PORTABLE: binarios, cluster y arranque como hasta ahora.
        if (-not (Asegurar-BinariosPostgres)) { throw 'No se pudo preparar PostgreSQL portable.' }
        if (-not (Inicializar-Cluster -Usuario $config.PgUsuario -Password $config.PgPassword)) { throw 'No se pudo inicializar el cluster de datos.' }
        if (-not (Arrancar-Postgres -Puerto $config.PgPuerto)) { throw 'No se pudo arrancar PostgreSQL.' }
        if (-not (Asegurar-BaseDatos -Puerto $config.PgPuerto -Usuario $config.PgUsuario -Password $config.PgPassword -BaseDatos $config.BaseDatos)) { throw 'No se pudo crear la base de datos.' }
    }

    # 4) Regla de firewall para acceso LAN (requiere admin; si no, se documenta).
    Configurar-Firewall -Puerto $config.AppPuerto

    # 5) Acceso directo en el Inicio de Windows (arranca al encender).
    Crear-AccesoInicio

    # 6) Arrancar la aplicacion (migra la BD sola al arrancar).
    if (-not (Arrancar-App -Config $config)) { throw 'La aplicacion no arranco correctamente.' }

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
