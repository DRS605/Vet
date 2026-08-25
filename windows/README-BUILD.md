# Cómo regenerar el paquete de Windows (ALXOR Vet, sin Docker)

Este directorio (`windows/`) contiene **solo las fuentes** del paquete de instalación
autoinstalable para Windows: los lanzadores `.bat`, los scripts PowerShell (`scripts/*.ps1`)
y el `LEEME.txt`. **No se versiona ningún binario** (ni el `.exe` publicado ni el ZIP): esos
se generan con los pasos de abajo.

Resultado final: `dist/ALXORVet-Windows.zip`, que el cliente descomprime en un PC Windows y
arranca con un doble clic — sin Docker, sin instalar .NET ni Python.

## Requisitos para construir

- SDK de .NET 8. En este repo se ha usado vía Docker (Linux sin SDK nativo):
  `mcr.microsoft.com/dotnet/sdk:8.0` con `--network host`.
- La compilación en verde: `dotnet build` → **0 warnings / 0 errors**.

## 1. Publicar la app como Windows self-contained (single-file)

```bash
dotnet publish src/AlxorCore.Api \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o dist/publish-win-x64
```

Con el SDK por Docker (equivalente, desde la raíz del repo):

```bash
docker run --rm --network host \
  -e HTTPS_PROXY="$HTTPS_PROXY" -e HTTP_PROXY="$HTTP_PROXY" \
  -e SSL_CERT_FILE=/ca/ca-bundle.crt -v /root/.ccr:/ca:ro \
  -v "$PWD":/src -w /src \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  bash -lc "dotnet publish src/AlxorCore.Api -c Release -r win-x64 --self-contained true \
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
            -o /src/dist/publish-win-x64"
```

Comprobaciones tras publicar:

- Se genera `dist/publish-win-x64/AlxorCore.Api.exe` (~130 MB; incluye el runtime .NET).
- Está `dist/publish-win-x64/wwwroot/` con `vet.html`, `cartilla.html`, `index.html`.
- QuestPDF/SkiaSharp: el nativo win-x64 `QuestPdfSkia.dll` queda **embebido** en el
  single-file (se auto-extrae al arrancar gracias a `IncludeNativeLibrariesForSelfExtract`).

## 2. Armar el ZIP de entrega

Se combina la carpeta `windows/` (scripts + LEEME) con la publicación en `app/`:

```bash
STAGE=$(mktemp -d)/ALXOR-Vet
mkdir -p "$STAGE/app"

# Lanzadores, scripts y LEEME (fuentes de este directorio).
cp "windows/Instalar ALXOR Vet.bat" "windows/Arrancar ALXOR Vet.bat" \
   "windows/Detener ALXOR Vet.bat" "windows/LEEME.txt" "$STAGE/"
cp -r windows/scripts "$STAGE/"

# La app publicada (sin los .pdb para aligerar).
cp -r dist/publish-win-x64/. "$STAGE/app/"
rm -f "$STAGE/app/"*.pdb

# ZIP
mkdir -p dist
( cd "$(dirname "$STAGE")" && zip -r -q ALXORVet-Windows.zip ALXOR-Vet )
mv "$(dirname "$STAGE")/ALXORVet-Windows.zip" dist/ALXORVet-Windows.zip
```

El script `windows/scripts/empaquetar.sh` hace exactamente esto de un solo paso.

## 3. Qué NO se incluye en el ZIP

- **PostgreSQL portable**: por defecto NO se empaqueta. El lanzador lo descarga en el
  primer arranque (zip oficial "binaries only" de EnterpriseDB, win-x64) y lo extrae en
  `postgres/`. Si prefieres incluirlo, descarga ese zip y déjalo dentro de `postgres/`
  del paquete antes de comprimir (el lanzador detecta un zip o unos binarios ya presentes
  y no vuelve a descargar). La versión objetivo está en `scripts/comun.ps1`
  (`$VersionPostgres`).

## Notas de diseño

- **Puertos**: la app escucha en `0.0.0.0:8080`; PostgreSQL en `localhost:5433` (5433 para no
  chocar con un PostgreSQL de sistema en 5432). Se pueden cambiar en `config/alxor.config.json`
  (creado en la instalación) — el `appsettings.Production.json` de `app/` se regenera desde ahí.
- **Secretos**: la contraseña de PostgreSQL y `Jwt:ClaveSecreta` se generan con el RNG
  criptográfico del sistema en el primer arranque. No hay secretos por defecto en el ZIP.
- **Config de la app**: `app/appsettings.Production.json` (cadena de conexión, JWT, sección
  Correo con `Habilitado=false`, `Migraciones:AplicarAlArrancar=true`). `ASPNETCORE_ENVIRONMENT`
  y `ASPNETCORE_URLS` se pasan como variables de entorno del proceso.
- **Migraciones**: la app aplica las migraciones EF Core al arrancar (idempotente).
```
