#!/usr/bin/env bash
# =============================================================================
#  Arma dist/ALXORVet-Windows.zip = windows/ (scripts + LEEME) + app publicada.
#  Requisitos previos: haber ejecutado el 'dotnet publish' win-x64 self-contained
#  en dist/publish-win-x64 (ver windows/README-BUILD.md).
#  Uso:  bash windows/scripts/empaquetar.sh
# =============================================================================
set -euo pipefail

# Raiz del repo = dos niveles por encima de este script (windows/scripts/..).
RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PUB="$RAIZ/dist/publish-win-x64"
WIN="$RAIZ/windows"
DIST="$RAIZ/dist"

if [ ! -f "$PUB/AlxorCore.Api.exe" ]; then
  echo "ERROR: no existe $PUB/AlxorCore.Api.exe. Ejecuta antes el 'dotnet publish' win-x64 (README-BUILD.md)." >&2
  exit 1
fi

STAGE="$(mktemp -d)/ALXOR-Vet"
mkdir -p "$STAGE/app"

# Fuentes del paquete (este repo).
cp "$WIN/Instalar ALXOR Vet.bat" "$WIN/Arrancar ALXOR Vet.bat" \
   "$WIN/Detener ALXOR Vet.bat" "$WIN/Copia de seguridad ALXOR Vet.bat" \
   "$WIN/Restaurar ALXOR Vet.bat" "$WIN/LEEME.txt" "$STAGE/"
mkdir -p "$STAGE/scripts"
# Solo los .ps1 (el .sh de empaquetado no va en el paquete de cliente).
cp "$WIN/scripts/"*.ps1 "$STAGE/scripts/"

# App publicada (sin .pdb, para aligerar).
cp -r "$PUB/." "$STAGE/app/"
rm -f "$STAGE/app/"*.pdb

# Sello de versión: se escribe en wwwroot/version.json y la SPA lo muestra en la
# barra lateral. Así, tras actualizar, se ve de un vistazo qué build está corriendo.
FECHA_UTC="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
SHA="$(git -C "$RAIZ" rev-parse --short HEAD 2>/dev/null || echo local)"
VERSION="$(date -u +v%Y.%m.%d)-$SHA"
mkdir -p "$STAGE/app/wwwroot"
cat > "$STAGE/app/wwwroot/version.json" <<JSON
{ "version": "$VERSION", "empaquetado": "$FECHA_UTC", "commit": "$SHA" }
JSON
echo "Sello de version: $VERSION ($FECHA_UTC)"

mkdir -p "$DIST"
rm -f "$DIST/ALXORVet-Windows.zip"
( cd "$(dirname "$STAGE")" && zip -r -q ALXORVet-Windows.zip ALXOR-Vet )
mv "$(dirname "$STAGE")/ALXORVet-Windows.zip" "$DIST/ALXORVet-Windows.zip"
rm -rf "$(dirname "$STAGE")"

echo "OK -> $DIST/ALXORVet-Windows.zip"
ls -lh "$DIST/ALXORVet-Windows.zip"
