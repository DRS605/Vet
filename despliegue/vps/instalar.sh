#!/usr/bin/env bash
# =============================================================================
#  ALXOR Vet - Instalación NATIVA en un VPS (p. ej. OVHcloud), SIN Docker.
#  Deja la app corriendo como servicio de systemd, con PostgreSQL por apt y
#  Caddy dando HTTPS automático (Let's Encrypt).
#
#  Uso (en el VPS, como root):
#     sudo bash despliegue/vps/instalar.sh  vet.tudominio.com  tucorreo@tudominio.com
#
#  Requisitos previos:
#     - Ubuntu 22.04/24.04 (o Debian 12).
#     - El dominio del primer argumento debe apuntar por DNS (registro A) a la
#       IP de este VPS, y los puertos 80 y 443 abiertos.
# =============================================================================
set -euo pipefail

DOMINIO="${1:-}"
ACME_EMAIL="${2:-}"
if [ -z "$DOMINIO" ]; then
  echo "ERROR: indica el dominio. Ej: sudo bash $0 vet.tudominio.com tucorreo@tudominio.com" >&2
  exit 1
fi
if [ "$(id -u)" -ne 0 ]; then
  echo "ERROR: ejecuta con sudo/root." >&2
  exit 1
fi

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DESTINO="/opt/alxor-vet/app"
ENV_FILE="/etc/alxor-vet.env"
USUARIO="alxorvet"

echo "==> Repositorio: $RAIZ"
echo "==> Dominio:     $DOMINIO"

# --- 1) Paquetes base --------------------------------------------------------
echo "==> Instalando paquetes base (postgresql, libfontconfig1, curl, git)..."
export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get install -y postgresql libfontconfig1 curl git ca-certificates gnupg debian-keyring debian-archive-keyring apt-transport-https

# --- 2) SDK de .NET 8 (para compilar/publicar) -------------------------------
if ! command -v dotnet >/dev/null 2>&1; then
  echo "==> Instalando el SDK de .NET 8..."
  # Feed oficial de Microsoft (funciona en Ubuntu/Debian).
  source /etc/os-release
  curl -fsSL "https://packages.microsoft.com/config/${ID}/${VERSION_ID}/packages-microsoft-prod.deb" -o /tmp/msprod.deb || true
  if [ -f /tmp/msprod.deb ]; then dpkg -i /tmp/msprod.deb || true; apt-get update -y || true; fi
  apt-get install -y dotnet-sdk-8.0 || {
    echo "==> Fallback: instalando .NET con el script oficial dotnet-install..."
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    bash /tmp/dotnet-install.sh --channel 8.0 --install-dir /usr/local/dotnet
    ln -sf /usr/local/dotnet/dotnet /usr/local/bin/dotnet
  }
fi
dotnet --version

# --- 3) Caddy (proxy con HTTPS automático) -----------------------------------
if ! command -v caddy >/dev/null 2>&1; then
  echo "==> Instalando Caddy..."
  curl -fsSL 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
  curl -fsSL 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' > /etc/apt/sources.list.d/caddy-stable.list
  apt-get update -y
  apt-get install -y caddy
fi

# --- 4) Usuario de servicio --------------------------------------------------
if ! id "$USUARIO" >/dev/null 2>&1; then
  echo "==> Creando usuario de servicio '$USUARIO'..."
  useradd --system --no-create-home --shell /usr/sbin/nologin "$USUARIO"
fi

# --- 5) Base de datos PostgreSQL --------------------------------------------
echo "==> Configurando PostgreSQL (base 'alxor', usuario 'alxor')..."
systemctl enable --now postgresql
DBPASS="$(openssl rand -base64 24 | tr -d '/+=' | cut -c1-24)"
sudo -u postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname='alxor'" | grep -q 1 \
  || sudo -u postgres psql -c "CREATE ROLE alxor LOGIN PASSWORD '$DBPASS';"
sudo -u postgres psql -c "ALTER ROLE alxor PASSWORD '$DBPASS';"
sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='alxor'" | grep -q 1 \
  || sudo -u postgres psql -c "CREATE DATABASE alxor OWNER alxor;"

# --- 6) Publicar la aplicación (self-contained linux-x64) --------------------
echo "==> Publicando la aplicación en $DESTINO ..."
mkdir -p "$DESTINO"
dotnet publish "$RAIZ/src/AlxorCore.Api/AlxorCore.Api.csproj" \
  -c Release -r linux-x64 --self-contained true -o "$DESTINO"
chown -R "$USUARIO:$USUARIO" /opt/alxor-vet
chmod +x "$DESTINO/AlxorCore.Api"

# --- 7) Variables de entorno del servicio ------------------------------------
echo "==> Escribiendo $ENV_FILE ..."
JWT="$(openssl rand -base64 48)"
if [ -f "$ENV_FILE" ]; then cp "$ENV_FILE" "$ENV_FILE.bak.$(date +%s)"; fi
cat > "$ENV_FILE" <<EOF
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:8080
ConnectionStrings__AlxorCore=Host=127.0.0.1;Port=5432;Database=alxor;Username=alxor;Password=$DBPASS
Jwt__ClaveSecreta=$JWT
Jwt__Emisor=alxor-vet
Jwt__Audiencia=alxor-vet
Jwt__MinutosExpiracion=60
Migraciones__AplicarAlArrancar=true
Correo__Habilitado=false
Correo__Puerto=587
Correo__UsarStartTls=true
Correo__Remitente=no-responder@alxor.local
Correo__RemitenteNombre=Clinica Veterinaria
Correo__BaseUrl=https://$DOMINIO
EOF
chown root:"$USUARIO" "$ENV_FILE"
chmod 640 "$ENV_FILE"

# --- 8) Servicio systemd -----------------------------------------------------
echo "==> Instalando el servicio systemd..."
cp "$RAIZ/despliegue/vps/alxor-vet.service" /etc/systemd/system/alxor-vet.service
systemctl daemon-reload
systemctl enable alxor-vet
systemctl restart alxor-vet

# --- 9) Caddy (HTTPS) --------------------------------------------------------
echo "==> Configurando Caddy para $DOMINIO ..."
cat > /etc/caddy/Caddyfile <<EOF
{
$( [ -n "$ACME_EMAIL" ] && echo "	email $ACME_EMAIL" )
}

$DOMINIO {
	encode zstd gzip
	redir / /vet.html
	reverse_proxy 127.0.0.1:8080
}
EOF
systemctl enable --now caddy
systemctl reload caddy || systemctl restart caddy

echo ""
echo "======================================================================="
echo " ALXOR Vet instalado."
echo "   Abre:  https://$DOMINIO"
echo "   Estado app:    systemctl status alxor-vet"
echo "   Registros app: journalctl -u alxor-vet -f"
echo "   (La primera vez, Caddy tarda unos segundos en obtener el certificado.)"
echo "======================================================================="
