# Desplegar ALXOR Vet en la nube (VPS de OVHcloud, sin Docker)

Publicar la aplicación en internet resuelve de raíz el problema de "descomprimo
una carpeta nueva y sigue viéndose la versión vieja": ya no hay ejecutables ni
carpetas en el PC de la clínica, solo un navegador abriendo **una única URL con
la última versión siempre**. Además, con la app en internet la **Cartilla Viva**
(el enlace que se da al dueño de la mascota) funciona desde cualquier sitio.

Este despliegue es **nativo, sin Docker**: la app corre como servicio de
**systemd**, PostgreSQL se instala por **apt**, y **Caddy** (un binario del
sistema) da HTTPS automático con Let's Encrypt.

---

## Opción A (recomendada): VPS de OVHcloud, instalación nativa

### 1. Crear el VPS y el dominio

1. En OVHcloud, crea un **VPS** con **Ubuntu 22.04/24.04** (el más pequeño vale
   para una clínica).
2. Apunta un **dominio o subdominio** (registro DNS **A**) a la IP del VPS.
   Ejemplo: `vet.tudominio.com → 51.x.x.x`.
3. Deja abiertos los puertos **80** y **443** (si activaste el Firewall de Red de
   OVHcloud, permite esos dos; el 22 para SSH).

### 2. Instalar (un solo comando)

Conéctate por SSH (`ssh ubuntu@LA_IP`) y ejecuta:

```bash
git clone https://github.com/DRS605/Vet.git
cd Vet
sudo bash despliegue/vps/instalar.sh  vet.tudominio.com  tucorreo@tudominio.com
```

El script `instalar.sh` hace todo automáticamente:

- Instala PostgreSQL, `libfontconfig1` (para los PDF), el SDK de .NET 8 y Caddy.
- Crea la base de datos `alxor` y su usuario con una **contraseña aleatoria**.
- **Compila y publica** la app (self-contained) en `/opt/alxor-vet/app`.
- Genera el **secreto JWT** y escribe `/etc/alxor-vet.env`.
- Instala y arranca el **servicio** `alxor-vet` (systemd).
- Configura **Caddy** para tu dominio con **HTTPS automático**.

Cuando termine, abre **https://vet.tudominio.com** — la raíz te lleva a la app.
Pulsa **Crear cuenta**, pon tus datos y el nombre de la clínica: la base de
datos se crea y migra sola en el primer arranque.

> La primera vez, Caddy tarda unos segundos en obtener el certificado. Si abres
> demasiado pronto y ves un aviso de certificado, espera medio minuto y recarga.

### 3. Comprobaciones y registros

```bash
systemctl status alxor-vet      # estado del servicio
journalctl -u alxor-vet -f      # registros en vivo de la app
systemctl status caddy          # estado del proxy/HTTPS
```

### 4. Actualizar a la última versión (centralizado)

Cada vez que haya cambios, en el VPS:

```bash
cd Vet && git pull
sudo bash despliegue/vps/actualizar.sh
```

Recompila y reinicia el servicio. Al recargar el navegador (Ctrl+F5), la clínica
ya tiene la versión nueva. **Una sola vez para todos**, sin tocar los PCs.

### 5. Copias de seguridad de la base de datos

```bash
# Copia
sudo -u postgres pg_dump -Fc alxor > alxor_$(date +%F).dump

# Restaurar
sudo -u postgres pg_restore -d alxor --clean --if-exists alxor_FECHA.dump
```

Puedes automatizarlo con un `cron` diario. Para llevarte la copia fuera del
servidor: `scp ubuntu@LA_IP:~/alxor_*.dump .`

### 6. Traer tus datos actuales (opcional)

Si en tu PC local ya tienes datos que quieras conservar, exporta desde allí y
restaura en el VPS (dímelo y te doy los comandos exactos según uses el
PostgreSQL portable de Windows o uno propio).

---

## La Cartilla Viva

En la ficha de cada cliente hay un botón **📱 Cartilla Viva**: genera un enlace
personal (`https://TU_DOMINIO/cartilla.html?token=...`) que compartes por
WhatsApp o email. El dueño abre esa página —sin contraseña— y ve sus mascotas,
las vacunas al día, las próximas citas y, si es cachorro, su plan de
crecimiento; incluso puede **confirmar citas**. Puedes **regenerar** (invalida
el anterior) o **revocar** el acceso cuando quieras.

> El enlace solo es útil con la app publicada en un dominio (esta guía). En
> `localhost` funcionaría únicamente en ese mismo PC.

## Ficheros de este despliegue

| Fichero | Qué es |
|---------|--------|
| `despliegue/vps/instalar.sh`        | Instalación completa en el VPS (primera vez). |
| `despliegue/vps/actualizar.sh`      | Recompila y reinicia (actualizaciones). |
| `despliegue/vps/alxor-vet.service`  | Unidad systemd del servicio. |
| `despliegue/vps/alxor-vet.env.ejemplo` | Referencia de las variables de entorno. |

## Variables de entorno principales (`/etc/alxor-vet.env`)

| Variable | Para qué |
|----------|----------|
| `ConnectionStrings__AlxorCore` | Conexión a PostgreSQL. |
| `Jwt__ClaveSecreta` | Firma de los tokens de sesión (mín. 32 caracteres). |
| `Migraciones__AplicarAlArrancar` | Crea/migra el esquema al arrancar (`true`). |
| `ASPNETCORE_URLS` | La app escucha en `127.0.0.1:8080`; Caddy la publica con HTTPS. |
| `Correo__*` | SMTP para correos de verificación, recordatorios y facturas. |

---

## Alternativas

- **Azure App Service** admite .NET de forma nativa (sin que toques Docker) si
  prefieres un servicio totalmente gestionado en vez de un VPS.
- El repositorio también incluye `render.yaml` y `despliegue/docker-compose.nube.yml`
  para quien **sí** quiera usar contenedores (Render construye una imagen Docker
  a partir del `Dockerfile`). No es necesario para la Opción A.
