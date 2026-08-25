# =============================================================================
#  ALXOR Vet - Arranque/parada del proceso de la aplicacion (.exe self-contained)
#  Se carga (dot-source). No se ejecuta solo.
# =============================================================================

# Escribe app\appsettings.Production.json a partir de la configuracion generada.
# Contiene la cadena de conexion, el secreto JWT y la seccion Correo (deshabilitada
# por defecto, con placeholders). ASPNETCORE_* se pasan como variables de entorno.
function Escribir-AppSettings {
    param([Parameter(Mandatory=$true)] $Config, [Parameter(Mandatory=$true)] [string]$JwtSecreto)

    $cadena = "Host=localhost;Port=$($Config.PgPuerto);Database=$($Config.BaseDatos);Username=$($Config.PgUsuario);Password=$($Config.PgPassword)"

    $settings = [ordered]@{
        ConnectionStrings = [ordered]@{ AlxorCore = $cadena }
        Jwt = [ordered]@{
            Emisor           = 'alxor-vet'
            Audiencia        = 'alxor-vet'
            ClaveSecreta     = $JwtSecreto
            MinutosExpiracion= 60
        }
        Correo = [ordered]@{
            Habilitado      = $false
            Host            = ''
            Puerto          = 587
            UsarStartTls    = $true
            Usuario         = ''
            Clave           = ''
            Remitente       = 'no-responder@alxor.local'
            RemitenteNombre = 'ALXOR Vet'
            BaseUrl         = "http://localhost:$($Config.AppPuerto)"
        }
        Migraciones = [ordered]@{ AplicarAlArrancar = $true }
    }

    $settings | ConvertTo-Json -Depth 8 | Set-Content -Path $RutaAppSettings -Encoding UTF8
    Escribir-Log "Configuracion escrita en app\appsettings.Production.json" 'OK'
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
    param([int]$Puerto)
    if (App-EnMarcha) {
        Escribir-Log 'La aplicacion ya estaba en marcha.' 'OK'
        return $true
    }
    if (-not (Test-Path $RutaExe)) {
        Escribir-Log "No se encuentra el ejecutable en $RutaExe" 'ERROR'
        return $false
    }

    # Variables de entorno del PROCESO (heredadas por el hijo, incluido cmd).
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:ASPNETCORE_URLS        = "http://0.0.0.0:$Puerto"

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
