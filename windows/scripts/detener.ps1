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

# Primero la app (usa la BD), luego PostgreSQL.
Detener-App
Detener-Postgres

Escribir-Log 'ALXOR Vet detenido.' 'OK'
exit 0
