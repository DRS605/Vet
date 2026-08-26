# =============================================================================
#  ALXOR Vet - Arranque/parada del proceso de la aplicacion (.exe self-contained)
#  Se carga (dot-source). No se ejecuta solo.
# =============================================================================

# Pasa TODA la configuracion de la app por VARIABLES DE ENTORNO al proceso del
# .exe (heredadas por el proceso hijo). NO se escribe ningun appsettings dentro
# de la carpeta de instalacion (es de solo lectura). ASP.NET Core lee las
# variables con doble subrayado '__' como jerarquia de secciones.
#
# La cadena de conexion, el secreto JWT y (opcionalmente) los ajustes de Correo
# provienen de la config generada bajo la raiz de datos del usuario
# (%LOCALAPPDATA%\ALXOR Vet\config\alxor.config.json), de modo que son
# idempotentes y se reutilizan en cada arranque.
function Aplicar-VariablesEntorno {
    param([Parameter(Mandatory=$true)] $Config)

    # La cadena de conexion la construye una funcion comun: apunta al PostgreSQL
    # existente (si el usuario lo eligio en la instalacion) o al portable.
    $cadena = Nueva-CadenaConexion -Config $Config

    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:ASPNETCORE_URLS        = "http://0.0.0.0:$($Config.AppPuerto)"

    # Cadena de conexion (seccion ConnectionStrings:AlxorCore).
    $env:ConnectionStrings__AlxorCore = $cadena

    # JWT (seccion Jwt). El secreto persiste en la config del usuario.
    $env:Jwt__ClaveSecreta      = $Config.JwtSecreto
    $env:Jwt__Emisor            = 'alxor-vet'
    $env:Jwt__Audiencia         = 'alxor-vet'
    $env:Jwt__MinutosExpiracion = '60'

    # Migraciones: la app aplica la BD sola al arrancar.
    $env:Migraciones__AplicarAlArrancar = 'true'

    # Correo (seccion Correo). Deshabilitado por defecto. Se puede activar
    # editando la seccion "Correo" del fichero de config bajo la raiz de datos.
    $correo = $null
    if ($Config.PSObject.Properties.Name -contains 'Correo') { $correo = $Config.Correo }

    $env:Correo__Habilitado      = (& { if ($correo -and $correo.Habilitado) { 'true' } else { 'false' } })
    $env:Correo__Host            = (& { if ($correo) { [string]$correo.Host } else { '' } })
    $env:Correo__Puerto          = (& { if ($correo -and $correo.Puerto) { [string]$correo.Puerto } else { '587' } })
    $env:Correo__UsarStartTls    = (& { if ($correo -and ($correo.UsarStartTls -eq $false)) { 'false' } else { 'true' } })
    $env:Correo__Usuario         = (& { if ($correo) { [string]$correo.Usuario } else { '' } })
    $env:Correo__Clave           = (& { if ($correo) { [string]$correo.Clave } else { '' } })
    $env:Correo__Remitente       = (& { if ($correo -and $correo.Remitente) { [string]$correo.Remitente } else { 'no-responder@alxor.local' } })
    $env:Correo__RemitenteNombre = (& { if ($correo -and $correo.RemitenteNombre) { [string]$correo.RemitenteNombre } else { 'ALXOR Vet' } })
    $env:Correo__BaseUrl         = "http://localhost:$($Config.AppPuerto)"

    Escribir-Log "Configuracion de la app pasada por variables de entorno (nada se escribe en la carpeta de instalacion)." 'OK'
}

# Devuelve los procesos vivos cuyo ejecutable es NUESTRO exe (robusto ante
# reinicios: no depende solo del PID guardado). Acceder a .Path puede lanzar
# errores no terminantes en procesos protegidos; se silencian.
function Procesos-App {
    return Get-Process -ErrorAction SilentlyContinue | Where-Object {
        try { $_.Path -eq $RutaExe } catch { $false }
    }
}

function App-EnMarcha {
    $procs = Procesos-App
    if ($procs) { return $true }
    return $false
}

function Arrancar-App {
    param([Parameter(Mandatory=$true)] $Config)
    $Puerto = $Config.AppPuerto
    if (App-EnMarcha) {
        Escribir-Log 'La aplicacion ya estaba en marcha.' 'OK'
        return $true
    }
    if (-not (Test-Path $RutaExe)) {
        Escribir-Log "No se encuentra el ejecutable en $RutaExe" 'ERROR'
        return $false
    }

    # Toda la configuracion se pasa por variables de entorno del PROCESO
    # (heredadas por el hijo, incluido cmd). No se escribe nada junto al .exe.
    Aplicar-VariablesEntorno -Config $Config

    Escribir-Log "Arrancando ALXOR Vet en http://0.0.0.0:$Puerto ..."

    # Lanzamos a traves de un cmd OCULTO que hace la redireccion de salida a
    # logs\. Se hace asi (y no con Start-Process -RedirectStandardOutput +
    # -WindowStyle Hidden) porque en Windows PowerShell 5.1 esa combinacion es
    # incompatible (redirigir exige UseShellExecute=false y -WindowStyle exige
    # true). Con cmd la ventana queda oculta, la app queda desacoplada de la
    # consola del .bat y la salida va a fichero.
    $errLog = Join-Path $DirLogs 'app.err.log'
    $argumentos = "/c `"`"$RutaExe`" > `"$LogApp`" 2> `"$errLog`"`""
    Start-Process -FilePath 'cmd.exe' -ArgumentList $argumentos `
        -WorkingDirectory $DirApp -WindowStyle Hidden | Out-Null

    if (Esperar-App -Puerto $Puerto -SegundosMax 120) {
        # Guardar el PID REAL de la app (no el de cmd) para poder pararla.
        $appProc = Procesos-App | Select-Object -First 1
        if ($appProc) { $appProc.Id | Set-Content -Path $PidApp -Encoding ascii }
        Escribir-Log 'La aplicacion responde correctamente (/salud OK).' 'OK'
        return $true
    }
    Escribir-Log 'La aplicacion no respondio a tiempo (revisa logs\app.log y logs\app.err.log).' 'ERROR'
    return $false
}

function Detener-App {
    $procs = Procesos-App
    if ($procs) {
        Escribir-Log 'Deteniendo la aplicacion...'
        foreach ($p in $procs) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
        Escribir-Log 'Aplicacion detenida.' 'OK'
    } else {
        Escribir-Log 'La aplicacion no estaba en marcha.'
    }
    Remove-Item $PidApp -Force -ErrorAction SilentlyContinue
}
