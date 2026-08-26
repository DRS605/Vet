# Instalación de ALXOR Vet en la clínica

Guía práctica para instalar **ALXOR Vet** en el **PC/servidor de la clínica** (instalación local con
Docker). Al terminar tendrás la aplicación funcionando en la red local, con la clínica creada, el
cuadro vacunal cargado y el correo de la clínica configurado.

> Producto: gestión veterinaria (agenda, historial, vacunas, recordatorios y facturación de actos)
> con **Cartilla Viva**, el portal del dueño de la mascota. Corre sobre ALXOR Core (.NET 8 +
> PostgreSQL), todo dentro de contenedores Docker: no hace falta instalar .NET ni PostgreSQL a mano.

---

## 0. Instalación sin Docker (Windows, autoinstalable) — la más sencilla

> **Recomendada para la clínica.** Es un ZIP que se descomprime y arranca de un **doble clic**,
> **sin Docker, sin instalar .NET ni Python**. PostgreSQL va **portable** dentro de la propia
> carpeta. El resto del documento (secciones 1 en adelante) describe la instalación **con Docker**,
> que sigue siendo válida para quien la prefiera o para Linux/macOS.

**Qué es:** un paquete `ALXORVet-Windows.zip` que contiene la app como ejecutable único
(`AlxorCore.Api.exe`, self-contained: lleva dentro el runtime de .NET), la interfaz web (`wwwroot`
con `vet.html`), los lanzadores `.bat` y un `LEEME.txt`.

**Pasos en el PC de la clínica (Windows 10/11 64-bit):**

1. Descomprime el ZIP en **cualquier carpeta**, la que prefieras. Puede ser una carpeta propia
   (`C:\ALXOR-Vet`) **o incluso `C:\Archivos de programa\ALXOR Vet`**: no hace falta ejecutar como
   administrador. La carpeta de instalación se trata como de **solo lectura** (solo se lee el `.exe`
   y su interfaz web); **ningún dato se escribe dentro de ella**.
> **PostgreSQL: dos opciones.** El instalador **pregunta** cómo quieres la base de datos:
>
> - **(A) Usar un PostgreSQL ya instalado en el PC** — *recomendado si la clínica ya tiene
>   PostgreSQL*. Es lo más rápido: **no descarga nada**. Autodetecta si hay algo escuchando en
>   `localhost:5432` y te ofrece usarlo; si no detecta nada, te pregunta igualmente. Si dices que sí,
>   te pide **puerto** (por defecto `5432`), **usuario** (por defecto `postgres`), **contraseña** y
>   **nombre de BD** (por defecto `alxor`), **verifica la conexión** (TCP) y guarda esos datos en la
>   sección `PostgresExistente` de `config\alxor.config.json`. La app **crea la BD `alxor` sola en el
>   primer arranque** (EF Core `Migrate()`), así que el usuario indicado debe tener **permiso para
>   crear bases de datos** (el usuario `postgres` lo tiene). No se descarga ni arranca el portable.
> - **(B) PostgreSQL portable** — si la clínica **no** tiene PostgreSQL. El instalador lo prepara solo
>   (ver más abajo). La primera vez lo **descarga** de la web oficial (puede tardar varios minutos).
>
> La elección se **guarda** y no se vuelve a preguntar en instalaciones/arranques posteriores.

2. Doble clic en **`Instalar ALXOR Vet.bat`**. La primera vez:
   - crea la **raíz de datos** del usuario en **`%LOCALAPPDATA%\ALXOR Vet\`** (p. ej.
     `C:\Users\<tu-usuario>\AppData\Local\ALXOR Vet\`), que **siempre es escribible**;
   - genera los **secretos** (contraseña de PostgreSQL del portable y clave JWT, con el RNG del
     sistema) y los guarda en `config\alxor.config.json` **bajo esa raíz de datos** (se **reutilizan**
     en cada arranque, no se regeneran);
   - **pregunta por la base de datos** (opción A o B, ver recuadro anterior). Con la **opción B
     (portable)**: prepara **PostgreSQL portable** en `%LOCALAPPDATA%\ALXOR Vet\postgres\` (lo
     **descarga** de la web oficial la primera vez), hace `initdb` en `%LOCALAPPDATA%\ALXOR Vet\datos\`,
     lo arranca en `localhost:5433` y crea la BD `alxor`. Con la **opción A (existente)**: **no
     descarga ni arranca** nada de eso; solo verifica que hay conexión con tu PostgreSQL;
   - arranca la app en `http://0.0.0.0:8090` pasándole **toda la configuración por variables de
     entorno** (`ConnectionStrings__AlxorCore`, `Jwt__ClaveSecreta`, `ASPNETCORE_ENVIRONMENT=Production`,
     `ASPNETCORE_URLS`, `Migraciones__AplicarAlArrancar=true` y la sección `Correo__*`, deshabilitada
     por defecto); **no escribe ningún `appsettings` en la carpeta de instalación**. Aplica las
     migraciones sola y **abre el navegador** en `http://localhost:8090/vet.html` (el **asistente de
     primer arranque**: empresa + admin + vacunas);
   - deja un acceso directo en el **Inicio de Windows** (apuntando al `Arrancar ALXOR Vet.bat` de la
     carpeta de instalación) para arrancar al encender.
3. Uso diario: **`Arrancar ALXOR Vet.bat`** y **`Detener ALXOR Vet.bat`**.

**Acceso desde otros PCs de la clínica (LAN):** avería la IP con `ipconfig` y entra en
`http://<IP-del-PC>:8090/vet.html`. Si ejecutaste el instalador **como administrador**, la regla de
firewall del puerto 8090 se crea sola; si no, ábrela una vez con:

```powershell
netsh advfirewall firewall add rule name="ALXOR Vet (8090)" dir=in action=allow protocol=TCP localport=8090
```

**Dónde viven los datos:** con el **PostgreSQL portable**, todo el estado (base de datos, binarios de
PostgreSQL, logs, secretos y config) vive en **`%LOCALAPPDATA%\ALXOR Vet\`**, **no** en la carpeta de
instalación. Así el paquete funciona instalado en cualquier ruta, incluida `Archivos de programa`, sin
permisos de administrador. Con la **opción A (PostgreSQL ya instalado)**, la **base de datos** vive en
**tu propia instalación de PostgreSQL** (no en la carpeta `datos\`); en `%LOCALAPPDATA%\ALXOR Vet\`
quedan solo logs y `config` (incluida la sección `PostgresExistente` con los datos de conexión).

**Correo:** desactivado por defecto. Con la app detenida, edita la sección `Correo` del fichero
`%LOCALAPPDATA%\ALXOR Vet\config\alxor.config.json` (`"Habilitado": true`, `Host`, `Puerto` 587,
`Usuario`, `Clave`, `Remitente`) y vuelve a arrancar: al arrancar, esos valores se pasan a la app como
variables de entorno `Correo__*`.

**Copia de seguridad:** detén la app (**`Detener ALXOR Vet.bat`**) y copia la carpeta
**`%LOCALAPPDATA%\ALXOR Vet\datos`** completa; o, con la app en marcha, usa `pg_dump`:

```powershell
$cfg = Get-Content "$env:LOCALAPPDATA\ALXOR Vet\config\alxor.config.json" | ConvertFrom-Json
$env:PGPASSWORD = $cfg.PgPassword
& "$env:LOCALAPPDATA\ALXOR Vet\postgres\pgsql\bin\pg_dump.exe" -h localhost -p 5433 -U postgres alxor > copia_alxor.sql
```

Guarda la copia **fuera del PC** (nube, disco externo o unidad de red). *(Si usas la **opción A**, tu
PostgreSQL ya instalado, esta copia no aplica: usa el `pg_dump` de tu propia instalación con los datos
de conexión de la sección `PostgresExistente`, o la herramienta de copias de ese servidor.)*

**Si algo falla:** todo queda registrado en **`%LOCALAPPDATA%\ALXOR Vet\logs\`** (`instalacion.log`,
`postgres.log`, `app.log`).

**Regenerar el paquete** (para el equipo técnico): ver `windows/README-BUILD.md`. En resumen,
`dotnet publish src/AlxorCore.Api -c Release -r win-x64 --self-contained true
-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true` y luego
`bash windows/scripts/empaquetar.sh`, que produce `dist/ALXORVet-Windows.zip`.

---

## 1. Requisitos

- Un **PC o servidor de la clínica** encendido durante el horario de trabajo (idealmente siempre).
  Con Windows es el caso más común; también sirve Linux o macOS.
- **Docker Desktop** (Windows/macOS) o **Docker Engine + Docker Compose** (Linux).
- El **código de ALXOR Vet** (esta carpeta del repositorio), copiado al PC.
- Los datos de la clínica a mano: nombre, NIF, email y contraseña del administrador y, si se va a
  usar el correo, los datos del **SMTP** de la clínica.

---

## 2. Instalación en Windows (Docker Desktop) — caso principal

### 2.1. Instalar Docker Desktop

1. Descarga **Docker Desktop para Windows** desde <https://www.docker.com/products/docker-desktop/>
   e instálalo (requiere WSL2; el instalador lo activa por ti si hace falta y pedirá reiniciar).
2. Abre **Docker Desktop** y espera a que el icono de la ballena indique *Running*.
3. **Que arranque con el sistema**: en Docker Desktop → *Settings → General* marca
   **"Start Docker Desktop when you sign in"**. Así la app estará disponible cada vez que se
   encienda el PC. (En los servicios del compose ya está puesto `restart: unless-stopped`, que hace
   que los contenedores se reinicien solos.)

### 2.2. Copiar el proyecto y configurar

1. Copia la carpeta del proyecto al PC (por ejemplo a `C:\ALXOR\ERP`).
2. Abre **PowerShell** y sitúate en esa carpeta:

   ```powershell
   cd C:\ALXOR\ERP
   ```

3. Crea el fichero de configuración a partir del ejemplo y edítalo:

   ```powershell
   copy despliegue\.env.ejemplo despliegue\.env
   notepad despliegue\.env
   ```

   Rellena **como mínimo**:
   - `POSTGRES_PASSWORD`: una contraseña robusta para la base de datos.
   - `JWT_CLAVE_SECRETA`: un secreto largo y aleatorio (mínimo 32 caracteres). Puedes generarlo en
     PowerShell con:

     ```powershell
     [Convert]::ToBase64String((1..48 | % { Get-Random -Max 256 }))
     ```

   - Si vas a enviar correos (recordatorios/facturas por email), pon `CORREO_HABILITADO=true` y los
     datos del **SMTP de la clínica** (`CORREO_HOST`, `CORREO_USUARIO`, `CORREO_CLAVE`,
     `CORREO_REMITENTE`...). Ver la sección [6. Correo SMTP](#6-correo-smtp-de-la-clínica).

### 2.3. Levantar la aplicación

```powershell
docker compose -f despliegue\docker-compose.produccion.yml --env-file despliegue\.env up -d --build
```

La primera vez tarda unos minutos (compila la API). Cuando termine, comprueba que responde:

```powershell
curl http://localhost:8090/salud
```

Debe devolver `{"estado":"ok"}`.

### 2.4. Inicializar la clínica (crea la empresa y el cuadro vacunal)

Necesitas **Python 3** en el PC (o ejecútalo desde otro equipo apuntando a la IP del servidor). Con
PowerShell, en la carpeta del proyecto:

```powershell
$env:CLINICA_NOMBRE   = "Clínica Veterinaria San Roque"
$env:CLINICA_NIF      = "B12345674"
$env:ADMIN_EMAIL      = "admin@clinica.com"
$env:ADMIN_PASSWORD   = "CambiaEstaClave1!"
python scripts\inicializar-clinica.py http://localhost:8090
```

Al terminar imprime el acceso (URL de `/vet.html` y usuario administrador). El script es
**idempotente**: si lo ejecutas otra vez no duplica ni la empresa ni las pautas.

> Si no tienes Python en el servidor, puedes ejecutar el script desde tu portátil cambiando la URL
> por `http://<IP-del-PC>:8090` (ver la sección siguiente para averiguar la IP).

---

## 3. Averiguar la IP local del PC (acceso desde otros equipos)

Para que otros ordenadores de la clínica entren en la aplicación necesitas la **IP local** del PC
donde corre ALXOR Vet.

- **Windows**: en PowerShell o CMD ejecuta `ipconfig` y busca **"Dirección IPv4"** de tu adaptador
  de red (algo como `192.168.1.50`).
- **Linux/macOS**: `ip addr` o `ifconfig` (o `hostname -I` en Linux).

Desde cualquier equipo de la **misma red** de la clínica, abre entonces:

```
http://<IP-del-PC>:8090/vet.html
```

Por ejemplo: `http://192.168.1.50:8090/vet.html`. Entra con el usuario administrador que configuraste.

> Consejo: reserva una **IP fija** para ese PC en el router (o configúrala como estática) para que la
> dirección no cambie y los accesos directos de los demás equipos sigan funcionando.

---

## 4. Instalación en Linux / macOS (equivalente)

1. Instala **Docker Engine + Docker Compose** (Linux) o **Docker Desktop** (macOS).
2. Copia el proyecto, prepara el `.env` y levanta la pila:

   ```bash
   cp despliegue/.env.ejemplo despliegue/.env
   nano despliegue/.env        # rellena POSTGRES_PASSWORD, JWT_CLAVE_SECRETA, SMTP...
   docker compose -f despliegue/docker-compose.produccion.yml --env-file despliegue/.env up -d --build
   ```

3. Inicializa la clínica:

   ```bash
   CLINICA_NOMBRE="Clínica Veterinaria San Roque" \
   CLINICA_NIF="B12345674" \
   ADMIN_EMAIL="admin@clinica.com" \
   ADMIN_PASSWORD="CambiaEstaClave1!" \
   python3 scripts/inicializar-clinica.py http://localhost:8090
   ```

   Para que Docker arranque al encender: en Linux `sudo systemctl enable docker`; en macOS marca el
   inicio automático de Docker Desktop. `restart: unless-stopped` reinicia los contenedores solos.

---

## 5. Cuadro vacunal que se carga

`inicializar-clinica.py` deja la clínica **vacía de clientes y animales** pero con el cuadro vacunal
por especie ya cargado (editable después en **Vacunas → Pautas**):

| Especie | Pauta | Carácter | Inicio | Refuerzo |
|---------|-------|----------|--------|----------|
| Perro | Polivalente (DHPPi/L) | Recomendada | 6 semanas | 12 meses |
| Perro | Rabia | Legal | 12 semanas | 12 meses |
| Perro | Tos de las perreras | Opcional | 8 semanas | 12 meses |
| Perro | Leishmania | Recomendada | 26 semanas | 12 meses |
| Gato | Trivalente felina | Recomendada | 8 semanas | 12 meses |
| Gato | Leucemia felina | Recomendada | 8 semanas | 12 meses |
| Gato | Rabia | Legal | 12 semanas | 12 meses |
| Conejo | Mixomatosis | Recomendada | 5 semanas | 6 meses |
| Conejo | RHD/VHD (enfermedad hemorrágica) | Recomendada | 10 semanas | 12 meses |
| Hurón | Moquillo (Distemper) | Recomendada | 8 semanas | 12 meses |
| Hurón | Rabia | Legal | 12 semanas | 12 meses |

**Ave** y **Reptil** quedan como **marco ampliable** (sin pautas por defecto): no hay un calendario
vacunal universal, así que la clínica las añade según su criterio.

---

## 6. Correo SMTP de la clínica

El envío de correos (recordatorios, facturas y presupuestos por email) usa el **SMTP de la clínica**.
Se configura en `despliegue/.env`:

```
CORREO_HABILITADO=true
CORREO_HOST=smtp.tudominio.com     # o smtp.gmail.com
CORREO_PUERTO=587                  # 587 con STARTTLS (recomendado)
CORREO_STARTTLS=true
CORREO_USUARIO=clinica@tudominio.com
CORREO_CLAVE=xxxxxxxx              # en Gmail/Workspace: "contraseña de aplicación"
CORREO_REMITENTE=clinica@tudominio.com
CORREO_REMITENTE_NOMBRE=Clínica Veterinaria San Roque
CORREO_BASE_URL=http://localhost:8090
```

Notas:
- Con `CORREO_HABILITADO=false` (o `CORREO_HOST` vacío) **no se envían correos**: es el modo por
  defecto, seguro para pruebas.
- La implementación usa **SMTP estándar de .NET** (`System.Net.Mail`) sobre **puerto 587 con
  STARTTLS**, que cubre Gmail y la mayoría de dominios. El puerto **465 (SSL implícito) no está
  soportado**; usa 587.
- Tras cambiar el `.env`, aplica los cambios con:

  ```
  docker compose -f despliegue/docker-compose.produccion.yml --env-file despliegue/.env up -d
  ```

### Recordatorios automáticos (opcional)

Por defecto los recordatorios se generan y envían **a mano** desde la aplicación. Si quieres que se
envíen **solos una vez al día**, activa en el `.env`:

```
RECORDATORIOS_AUTOMATICO=true
RECORDATORIOS_HORA=08:00     # hora local de ejecución
RECORDATORIOS_DIAS=30        # ventana de vencimientos a cubrir
```

Requiere tener el correo SMTP configurado. Con `RECORDATORIOS_AUTOMATICO=false` (valor por defecto)
el proceso no se ejecuta.

---

## 7. Copia de seguridad de la base de datos

Todos los datos de la clínica viven en el volumen de PostgreSQL. Haz copias periódicas con `pg_dump`.

**Windows (PowerShell):**

```powershell
docker exec -t alxor-vet-postgres pg_dump -U postgres alxor > C:\ALXOR\copias\alxor_$(Get-Date -Format yyyyMMdd).sql
```

**Linux/macOS:**

```bash
docker exec -t alxor-vet-postgres pg_dump -U postgres alxor > ~/copias/alxor_$(date +%Y%m%d).sql
```

Recomendaciones:
- Guarda la copia en una carpeta que **ya se respalde** (unidad de red, nube de la clínica, disco
  externo). Un backup en el mismo PC no protege ante una avería del equipo.
- Programa la copia (Programador de tareas de Windows / `cron` en Linux) para que se ejecute a diario.

**Restaurar** una copia (con la pila levantada):

```bash
docker exec -i alxor-vet-postgres psql -U postgres -d alxor < copia.sql
```

---

## 8. Cartilla Viva (portal del dueño) — fase 2

> La **Cartilla Viva** (el portal del dueño de la mascota, con confirmación de cita desde casa) es
> una funcionalidad de **fase 2**. Requiere publicar la app en internet, cosa que **no se hace en
> esta instalación local (fase 1)**: aquí la app vive solo en la red de la clínica (LAN), así que la
> Cartilla no se activa. Sigue estando en el producto; simplemente no forma parte de esta primera
> fase. Si más adelante se quiere ofrecer, ver el [Apéndice · Fase 2](#apéndice--fase-2-publicar-la-app-en-internet).

---

## 9. Seguridad (léelo)

- **Cambia el secreto JWT** (`JWT_CLAVE_SECRETA`) por uno propio, largo y aleatorio. No dejes el de
  ejemplo.
- **Cambia las contraseñas por defecto**: la de PostgreSQL (`POSTGRES_PASSWORD`) y la del usuario
  administrador tras el primer acceso.
- La base de datos **no se publica** al exterior (solo la usa la API dentro del compose); mantenlo así.
- **RLS en producción**: el aislamiento multiempresa se refuerza con Row-Level Security de
  PostgreSQL, que **solo actúa si la app se conecta con un rol de BD sin privilegios de superusuario
  y sin BYPASSRLS**. Por defecto se usa el rol `postgres` (superusuario), cómodo pero sin esa red de
  seguridad. Para la instalación monoclínica de fase 1 esto es **suficiente** (el aislamiento por
  empresa lo garantiza además el filtro global de EF Core), así que es **opcional**.

  Para **endurecer** (recomendado si más adelante hay varias empresas o se expone la app), usa un
  rol de aplicación restringido:

  1. **Aplica primero las migraciones con el rol admin/owner** (arranca la API una vez con el usuario
     `postgres`, que crea el esquema solo). Con eso ya existen todas las tablas.
  2. Crea el rol restringido ejecutando el script incluido (una sola vez), como `postgres`:

     ```powershell
     docker exec -i alxor-vet-postgres psql -U postgres -d alxor -v clave="UNA_CLAVE_FUERTE" < despliegue\rls-rol-restringido.sql
     ```

     Crea el rol `alxor_app` **sin** superusuario y **sin** BYPASSRLS, con solo los permisos mínimos
     (CONNECT, USAGE en los esquemas y SELECT/INSERT/UPDATE/DELETE en las tablas de negocio), de modo
     que **sí queda sujeto a las políticas RLS**.
  3. Apunta la conexión de la app a ese rol: en `despliegue/.env`, cambia el usuario/clave que usa
     `ConnectionStrings__AlxorCore` (o define `POSTGRES_USER`/`POSTGRES_PASSWORD` del rol restringido)
     y **desactiva la migración automática** para que la app no intente aplicar DDL con el rol sin
     privilegios: añade `Migraciones__AplicarAlArrancar=false` al entorno de la API. Reinicia con
     `up -d`. (Las migraciones futuras se aplican con el rol admin/owner, no con `alxor_app`.)
- Si en el futuro expones la app a internet (ver [Apéndice · Fase 2](#apéndice--fase-2-publicar-la-app-en-internet)),
  hazlo **siempre con HTTPS** (Cloudflare Tunnel lo da automáticamente).

---

## 10. Operación diaria (chuleta)

| Acción | Comando |
|--------|---------|
| Ver estado | `docker compose -f despliegue/docker-compose.produccion.yml ps` |
| Ver logs de la API | `docker logs -f alxor-vet-api` |
| Parar | `docker compose -f despliegue/docker-compose.produccion.yml down` |
| Arrancar | `docker compose -f despliegue/docker-compose.produccion.yml --env-file despliegue/.env up -d` |
| Actualizar tras cambios de código | añadir `--build` al arranque |
| Copia de seguridad | ver [sección 7](#7-copia-de-seguridad-de-la-base-de-datos) |

Acceso de la clínica: **http://\<IP-del-PC\>:8090/vet.html**

---

## Apéndice · Fase 2: publicar la app en internet (opcional, más adelante)

Todo lo anterior es **fase 1** (instalación local) y basta para el día a día de la clínica. Este
apéndice es **opcional** y solo aplica si en el futuro se quiere activar la **Cartilla Viva** (portal
del dueño) para que los propietarios accedan y confirmen citas **desde casa**. Para eso hay que
exponer la app con una dirección pública. No es necesario para arrancar.

### Opción A (recomendada): Cloudflare Tunnel

Un túnel de Cloudflare publica la app en internet **sin abrir puertos del router** y con HTTPS
automático. A grandes rasgos:

1. Crea una cuenta en Cloudflare y añade un dominio (o usa un subdominio gratuito de prueba).
2. Instala **cloudflared** en el PC de la clínica y autentícalo (`cloudflared login`).
3. Crea el túnel y apúntalo a la app local:

   ```bash
   cloudflared tunnel create alxor-vet
   cloudflared tunnel route dns alxor-vet vet.tudominio.com
   cloudflared tunnel run --url http://localhost:8090 alxor-vet
   ```

4. Configura `cloudflared` como servicio para que arranque con el sistema.
5. Pon esa URL pública (`https://vet.tudominio.com`) en `CORREO_BASE_URL` del `.env` y reinicia la
   API, para que los enlaces de los correos y de la Cartilla usen la dirección pública.

### Opción B: redirección de puertos en el router

Redirige el puerto 8090 (o el 443 con un proxy inverso y certificado) del router hacia el PC de la
clínica. Es más frágil (IP pública que puede cambiar, sin HTTPS de serie, expone el equipo) y
requiere conocimientos de red; **Cloudflare Tunnel es preferible**.
