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
  la **raíz de datos del usuario** `%LOCALAPPDATA%\ALXOR Vet\postgres\`. Si prefieres evitar
  la descarga, deja ese zip dentro de esa carpeta `postgres\` (el lanzador detecta un zip o
  unos binarios ya presentes y no vuelve a descargar). La versión objetivo está en
  `scripts/comun.ps1` (`$VersionPostgres`).

## Notas de diseño

- **Carpeta de instalación de SOLO LECTURA**: el ZIP se puede descomprimir en cualquier ruta,
  incluida `C:\Archivos de programa\`, sin ser administrador. **Nada** se escribe dentro de la
  carpeta de instalación (solo se leen el `.exe` y su `wwwroot`).
- **Raíz de datos escribible**: TODO el estado (base de datos, PostgreSQL portable, logs,
  secretos/config) vive en `%LOCALAPPDATA%\ALXOR Vet\` (`datos\`, `postgres\`, `logs\`,
  `config\`), que siempre es escribible. Copia de seguridad = copiar `…\ALXOR Vet\datos` (con la
  app detenida) o `pg_dump`.
- **Puertos**: la app escucha en `0.0.0.0:8080`; PostgreSQL en `localhost:5433` (5433 para no
  chocar con un PostgreSQL de sistema en 5432). Se pueden cambiar en
  `%LOCALAPPDATA%\ALXOR Vet\config\alxor.config.json`, de donde el lanzador toma la config en
  cada arranque.
- **Secretos**: la contraseña de PostgreSQL y la clave JWT se generan con el RNG criptográfico
  del sistema en el primer arranque y se persisten en
  `%LOCALAPPDATA%\ALXOR Vet\config\alxor.config.json` (se reutilizan; no hay secretos por
  defecto en el ZIP).
- **Config de la app por variables de entorno**: el lanzador pasa toda la configuración al
  proceso del `.exe` como variables de entorno (ASP.NET lee `__` como jerarquía de secciones):
  `ConnectionStrings__AlxorCore`, `Jwt__ClaveSecreta` (+`Jwt__Emisor`/`Jwt__Audiencia`/
  `Jwt__MinutosExpiracion`), `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_URLS`,
  `Migraciones__AplicarAlArrancar=true` y `Correo__*` (deshabilitado por defecto). **No** se
  escribe ningún `appsettings.Production.json`.
- **Migraciones**: la app aplica las migraciones EF Core al arrancar (idempotente).
```
