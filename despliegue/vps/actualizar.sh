#!/usr/bin/env bash
# =============================================================================
#  ALXOR Vet - Actualizar a la última versión (VPS nativo, SIN Docker).
#  Recompila la app desde el código y reinicia el servicio. Centralizado: una
#  sola vez y toda la clínica ve la versión nueva al recargar el navegador.
#
#  Uso (en el VPS, como root, desde la carpeta del repositorio):
#     git pull
#     sudo bash despliegue/vps/actualizar.sh
# =============================================================================
set -euo pipefail

if [ "$(id -u)" -ne 0 ]; then echo "ERROR: ejecuta con sudo/root." >&2; exit 1; fi

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DESTINO="/opt/alxor-vet/app"
USUARIO="alxorvet"

echo "==> Publicando la última versión en $DESTINO ..."
# Limpia la salida anterior para no dejar ficheros huérfanos entre versiones.
rm -rf "$DESTINO"
mkdir -p "$DESTINO"
dotnet publish "$RAIZ/src/AlxorCore.Api/AlxorCore.Api.csproj" \
  -c Release -r linux-x64 --self-contained true -o "$DESTINO"
chown -R "$USUARIO:$USUARIO" /opt/alxor-vet
chmod +x "$DESTINO/AlxorCore.Api"

echo "==> Reiniciando el servicio..."
systemctl restart alxor-vet
sleep 2
systemctl --no-pager --lines=5 status alxor-vet || true
echo "==> Listo. Recarga el navegador (Ctrl+F5)."
