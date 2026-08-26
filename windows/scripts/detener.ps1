# =============================================================================
#  ALXOR Vet - Detener (para la aplicacion y PostgreSQL de forma limpia)
# =============================================================================
$ErrorActionPreference = 'Continue'

. (Join-Path $PSScriptRoot 'comun.ps1')
. (Join-Path $PSScriptRoot 'postgres.ps1')
. (Join-Path $PSScriptRoot 'app.ps1')

Asegurar-Carpetas
Escribir-Log '============================================================'
Escribir-Log 'ALXOR Vet - DETENER'
Escribir-Log '============================================================'

# Primero la app (usa la BD), luego PostgreSQL (solo si es el portable).
Detener-App

$config = Leer-Config
if (Usa-PostgresExistente -Config $config) {
    Escribir-Log 'PostgreSQL es un servicio del usuario (existente); no se detiene.' 'OK'
} else {
    Detener-Postgres
}

Escribir-Log 'ALXOR Vet detenido.' 'OK'
exit 0
