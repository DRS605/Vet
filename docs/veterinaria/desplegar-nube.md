# Desplegar ALXOR Vet en la nube

Publicar la aplicación en internet resuelve de raíz el problema de "descomprimo
una carpeta nueva y sigue viéndose la versión vieja": ya no hay ejecutables ni
carpetas en el PC de la clínica, solo un navegador abriendo **una única URL con
la última versión siempre**. Además, con la app en internet la **Cartilla Viva**
(el enlace que se da al dueño de la mascota) funciona desde cualquier sitio.

El código es portátil: contenedor **Docker**, configuración por **variables de
entorno**, base de datos por `DATABASE_URL` o `ConnectionStrings__AlxorCore`, y
puerto por `PORT` (o el fijo 8080). Funciona en OVHcloud, Render, Railway,
Fly.io, Azure, etc. Aquí van las dos vías recomendadas.

---

## Opción A (recomendada): VPS de OVHcloud con Docker + HTTPS

Una sola máquina con Docker corre la app + PostgreSQL + un proxy **Caddy** que
da HTTPS automático (Let's Encrypt). Todo está preparado en
`despliegue/docker-compose.nube.yml` y `despliegue/Caddyfile`.

### 1. Crear el VPS y el dominio

1. En OVHcloud, crea un **VPS** (el más pequeño vale para una clínica) con
   **Ubuntu 22.04/24.04**.
2. Apunta un **dominio o subdominio** (registro DNS de tipo **A**) a la IP del
   VPS. Ejemplo: `vet.tudominio.com  →  51.x.x.x`.
3. Asegúrate de que los puertos **80** y **443** están abiertos (en OVHcloud el
   firewall del VPS suele venir abierto; si activaste el Firewall de Red,
   permite 80 y 443).

### 2. Instalar Docker en el VPS

Conéctate por SSH (`ssh ubuntu@LA_IP`) y:

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER   # cierra sesión y vuelve a entrar tras esto
```

### 3. Traer el proyecto y configurarlo

```bash
git clone https://github.com/DRS605/Vet.git
cd Vet
cp despliegue/.env.ejemplo despliegue/.env
nano despliegue/.env
```

En el `.env` rellena como mínimo:

```
DOMINIO_PUBLICO=vet.tudominio.com
ACME_EMAIL=tucorreo@tudominio.com
POSTGRES_PASSWORD=(una contraseña robusta)
JWT_CLAVE_SECRETA=(un secreto largo; genera con: openssl rand -base64 48)
CORREO_BASE_URL=https://vet.tudominio.com
```

### 4. Arrancar

```bash
docker compose -f despliegue/docker-compose.nube.yml --env-file despliegue/.env up -d --build
```

La primera vez compila la app (unos minutos) y Caddy pide el certificado TLS.
Cuando termine, abre **https://vet.tudominio.com** — la raíz te lleva a la app.
Pulsa **Crear cuenta**, pon tus datos y el nombre de la clínica: la base de
datos se crea y migra sola en el primer arranque.

### 5. Actualizar a la última versión (centralizado)

Cada vez que haya cambios, en el VPS:

```bash
cd Vet && git pull
docker compose -f despliegue/docker-compose.nube.yml --env-file despliegue/.env up -d --build
```

Al recargar el navegador, la clínica ya tiene la versión nueva. **Una sola vez
para todos**, sin tocar nada en los PCs.

### 6. Copias de seguridad de la base de datos

```bash
# Copia (guarda el .dump en el servidor o descárgalo con scp)
docker exec alxor-vet-postgres pg_dump -U postgres -Fc alxor > alxor_$(date +%F).dump

# Restaurar
cat alxor_FECHA.dump | docker exec -i alxor-vet-postgres pg_restore -U postgres -d alxor --clean --if-exists
```

Puedes automatizarlo con un `cron` diario en el VPS.

---

## Opción B: Render (todo desde un fichero, sin servidor propio)

Si prefieres no administrar un VPS, el repositorio incluye `render.yaml`, que
define la web (Docker) + PostgreSQL gestionado.

1. Entra en <https://dashboard.render.com> → **New** → **Blueprint**.
2. Conecta el repositorio **DRS605/Vet** (rama `main`). Render lee `render.yaml`.
3. Pulsa **Apply**. Al quedar **Live**, abre `https://alxor-vet.onrender.com/vet.html`.
4. Con `autoDeploy: true`, cada `git push` a `main` redesplega solo.

Coste orientativo: web ~7 $/mes + PostgreSQL ~6 $/mes (hay plan `free` solo para
probar; caduca a los 30 días).

---

## La Cartilla Viva en la nube

En la ficha de cada cliente hay un botón **📱 Cartilla Viva**: genera un enlace
personal (`https://TU_DOMINIO/cartilla.html?token=...`) que puedes compartir por
WhatsApp o email. El dueño abre esa página —sin contraseña— y ve sus mascotas,
las vacunas al día, las próximas citas y, si es cachorro, su plan de
crecimiento; incluso puede **confirmar citas** desde ahí. Puedes **regenerar**
(invalida el anterior) o **revocar** el acceso cuando quieras.

> Para que los enlaces se abran bien desde fuera, la app debe estar publicada
> con un dominio (Opción A o B). En local (`localhost`) el enlace solo funciona
> en ese mismo PC.

## Variables de entorno principales

| Variable | Para qué |
|----------|----------|
| `ConnectionStrings__AlxorCore` **o** `DATABASE_URL` | Conexión a PostgreSQL. |
| `Jwt__ClaveSecreta` | Firma de los tokens de sesión (mín. 32 caracteres). |
| `Migraciones__AplicarAlArrancar` | Crea/migra el esquema al arrancar (por defecto `true`). |
| `PORT` | Puerto (lo inyecta el PaaS). Si no está, se usa `ASPNETCORE_URLS` (8080). |
| `Correo__*` | SMTP para correos de verificación, recordatorios y facturas. |
