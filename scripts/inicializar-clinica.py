#!/usr/bin/env python3
"""
Arranque limpio de una clínica real en ALXOR Vet.

A diferencia de `datos-demo-vet.py` (que siembra clientes y animales de ejemplo), este script deja
la aplicación LISTA PARA PRODUCCIÓN pero VACÍA de datos operativos: crea el usuario administrador,
crea la empresa (la clínica) y carga el **cuadro vacunal por especie** con pautas por defecto
sensatas en español. No crea clientes ni animales.

Es idempotente en lo razonable: reutiliza el usuario y la empresa si ya existen, y solo crea las
pautas vacunales que falten (por especie + nombre), de modo que se puede ejecutar más de una vez.

Uso (parámetros por variables de entorno o argumentos):
    CLINICA_NOMBRE="Clínica Veterinaria San Roque" \
    CLINICA_NIF="B12345674" \
    ADMIN_EMAIL="admin@clinica.com" \
    ADMIN_PASSWORD="CambiaEstaClave1!" \
    python3 scripts/inicializar-clinica.py [URL_BASE]

Variables reconocidas:
    CLINICA_NOMBRE   Razón social / nombre de la clínica            (obligatorio)
    CLINICA_NIF      NIF/CIF de la clínica (validado por la API)    (obligatorio)
    ADMIN_EMAIL      Email del usuario administrador                (obligatorio)
    ADMIN_PASSWORD   Contraseña del administrador                   (obligatorio)
    ADMIN_NOMBRE     Nombre visible del administrador               (opcional; por defecto "Administración")
    ALXOR_URL        URL base de la API                             (opcional; por defecto http://localhost:8080)
    CLINICA_CALLE / CLINICA_CP / CLINICA_POBLACION / CLINICA_PROVINCIA  (opcionales)

El primer argumento posicional, si se indica, tiene prioridad como URL base.
Solo usa la biblioteca estándar (urllib): no requiere instalar nada.
"""
import json
import os
import sys
import urllib.request
import urllib.error

BASE = (sys.argv[1] if len(sys.argv) > 1 else os.environ.get("ALXOR_URL", "http://localhost:8080")).rstrip("/")

NOMBRE_CLINICA = os.environ.get("CLINICA_NOMBRE", "")
NIF = os.environ.get("CLINICA_NIF", "")
ADMIN_EMAIL = os.environ.get("ADMIN_EMAIL", "")
ADMIN_PASSWORD = os.environ.get("ADMIN_PASSWORD", "")
ADMIN_NOMBRE = os.environ.get("ADMIN_NOMBRE", "Administración")

CALLE = os.environ.get("CLINICA_CALLE", "")
CP = os.environ.get("CLINICA_CP", "")
POBLACION = os.environ.get("CLINICA_POBLACION", "")
PROVINCIA = os.environ.get("CLINICA_PROVINCIA", "")

# ---------------------------------------------------------------------------
# Cuadro vacunal por defecto (cuadro maestro de pautas por especie).
#
# Cada entrada: (especie, nombre, carácter, edad de inicio en semanas, periodicidad de refuerzo en
# meses). El carácter es "Legal" (obligatoria por ley, p. ej. la rabia en algunas comunidades),
# "Recomendada" (protocolo clínico habitual) u "Opcional" (según el animal). Son valores de partida
# sensatos y editables después desde la propia aplicación (Vacunas → Pautas).
#
#   Perro:
#     · Polivalente (DHPPi/L)  Recomendada  inicio 6 sem   refuerzo 12 meses
#     · Rabia                  Legal        inicio 12 sem  refuerzo 12 meses
#     · Tos de las perreras    Opcional     inicio 8 sem   refuerzo 12 meses
#     · Leishmania             Recomendada  inicio 26 sem  refuerzo 12 meses
#   Gato:
#     · Trivalente felina      Recomendada  inicio 8 sem   refuerzo 12 meses
#     · Leucemia felina        Recomendada  inicio 8 sem   refuerzo 12 meses
#     · Rabia                  Legal        inicio 12 sem  refuerzo 12 meses
#   Conejo:
#     · Mixomatosis            Recomendada  inicio 5 sem   refuerzo 6 meses
#     · RHD/VHD (hemorrágica)  Recomendada  inicio 10 sem  refuerzo 12 meses
#   Hurón:
#     · Moquillo (Distemper)   Recomendada  inicio 8 sem   refuerzo 12 meses
#     · Rabia                  Legal        inicio 12 sem  refuerzo 12 meses
#
# Ave y Reptil quedan como MARCO ampliable: no existe un calendario vacunal universal, así que se
# dejan sin pautas por defecto para que la clínica añada las suyas según su criterio.
# ---------------------------------------------------------------------------
CUADRO_VACUNAL = [
    # (especie, nombre, caracter, edadInicioSemanas, periodicidadRefuerzoMeses)
    ("Perro", "Polivalente (DHPPi/L)", "Recomendada", 6, 12),
    ("Perro", "Rabia", "Legal", 12, 12),
    ("Perro", "Tos de las perreras", "Opcional", 8, 12),
    ("Perro", "Leishmania", "Recomendada", 26, 12),
    ("Gato", "Trivalente felina", "Recomendada", 8, 12),
    ("Gato", "Leucemia felina", "Recomendada", 8, 12),
    ("Gato", "Rabia", "Legal", 12, 12),
    ("Conejo", "Mixomatosis", "Recomendada", 5, 6),
    ("Conejo", "RHD/VHD (enfermedad hemorrágica)", "Recomendada", 10, 12),
    ("Huron", "Moquillo (Distemper)", "Recomendada", 8, 12),
    ("Huron", "Rabia", "Legal", 12, 12),
]

# Especies dejadas como marco (sin pautas por defecto); la clínica las completará.
ESPECIES_MARCO = ["Ave", "Reptil"]

_token = None


def llamar(metodo, ruta, cuerpo=None, auth=True):
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
    req = urllib.request.Request(BASE + ruta, data=datos, method=metodo)
    req.add_header("Content-Type", "application/json")
    if auth and _token:
        req.add_header("Authorization", "Bearer " + _token)
    try:
        with urllib.request.urlopen(req) as r:
            texto = r.read().decode()
            return json.loads(texto) if texto else {}
    except urllib.error.HTTPError as e:
        detalle = e.read().decode()
        raise RuntimeError(f"{metodo} {ruta} -> {e.code}: {detalle}") from None


def paso(msg):
    print("  " + msg)


def exigir_parametros():
    faltan = []
    if not NOMBRE_CLINICA:
        faltan.append("CLINICA_NOMBRE")
    if not NIF:
        faltan.append("CLINICA_NIF")
    if not ADMIN_EMAIL:
        faltan.append("ADMIN_EMAIL")
    if not ADMIN_PASSWORD:
        faltan.append("ADMIN_PASSWORD")
    if faltan:
        print("Faltan parámetros obligatorios: " + ", ".join(faltan))
        print("Consulta la cabecera de este script para ver cómo pasarlos.")
        sys.exit(2)


def main():
    global _token
    exigir_parametros()
    print(f"ALXOR Vet · inicializando clínica en {BASE}\n")

    # Comprobación de vida.
    try:
        llamar("GET", "/salud", auth=False)
    except Exception as e:
        print(f"No se llega a la API en {BASE}. ¿Está arrancada? ({e})")
        sys.exit(1)

    # 1) Usuario administrador: registrar (o reutilizar) e iniciar sesión.
    print("Usuario administrador")
    try:
        llamar("POST", "/auth/registro", {"email": ADMIN_EMAIL, "nombre": ADMIN_NOMBRE, "contrasena": ADMIN_PASSWORD}, auth=False)
        paso("usuario administrador creado")
    except RuntimeError:
        paso("el usuario ya existía, reutilizándolo")
    login = llamar("POST", "/auth/login", {"email": ADMIN_EMAIL, "contrasena": ADMIN_PASSWORD}, auth=False)
    _token = login["token"]
    paso("sesión iniciada")

    # 2) Empresa (la clínica): crear si no hay ninguna; si ya existe, reutilizar.
    print("Clínica")
    empresas = llamar("GET", "/empresas")
    emp = None
    if empresas:
        emp = next((e for e in empresas if e.get("nif") == NIF), empresas[0])
        paso(f"reutilizando «{emp.get('razonSocial', emp['id'])}»")
    else:
        cuerpo = {"nif": NIF, "razonSocial": NOMBRE_CLINICA}
        if CALLE:
            cuerpo["calle"] = CALLE
        if CP:
            cuerpo["codigoPostal"] = CP
        if POBLACION:
            cuerpo["poblacion"] = POBLACION
        if PROVINCIA:
            cuerpo["provincia"] = PROVINCIA
        llamar("POST", "/empresas", cuerpo)
        emp = llamar("GET", "/empresas")[0]
        paso(f"creada «{emp['razonSocial']}»")
    _token = llamar("POST", f"/empresas/{emp['id']}/seleccionar")["token"]

    # 2.5) Maestro de especies: al crear la empresa se siembran las especies por defecto (Perro,
    # Gato, Conejo, Ave, Hurón, Reptil, Otro). Aquí solo verificamos que existen; son editables
    # después desde Ajustes → Especies (dar de alta/editar/baja).
    print("Especies")
    especies = llamar("GET", "/especies")
    if especies:
        paso(f"maestro con {len(especies)} especie(s): " + ", ".join(e["nombre"] for e in especies))
    else:
        paso("el maestro de especies está vacío (revisa la instalación)")

    # 3) Cuadro vacunal por especie: crear solo las pautas que falten (idempotente).
    print("Cuadro vacunal")
    existentes = {(p.get("especie"), p.get("nombre")) for p in llamar("GET", "/vacunas/pautas")}
    creadas = 0
    for especie, nombre, caracter, inicio, refuerzo in CUADRO_VACUNAL:
        if (especie, nombre) in existentes:
            continue
        llamar("POST", "/vacunas/pautas", {
            "especie": especie, "nombre": nombre, "caracter": caracter,
            "edadInicioSemanas": inicio, "periodicidadRefuerzoMeses": refuerzo,
        })
        creadas += 1
    if creadas:
        paso(f"{creadas} pauta(s) vacunal(es) cargada(s)")
    else:
        paso("las pautas ya estaban cargadas (no se duplican)")
    paso("especies con marco ampliable (sin pautas por defecto): " + ", ".join(ESPECIES_MARCO))

    # 4) Verificación: la clínica no tiene clientes ni animales (arranque limpio).
    clientes = llamar("GET", "/clientes")
    animales = llamar("GET", "/animales")
    print(f"\nArranque limpio verificado: {len(clientes)} clientes, {len(animales)} animales.")

    print("¡Listo! La clínica está inicializada y vacía, con el cuadro vacunal cargado.")
    credenciales()


def credenciales():
    print("\n" + "─" * 56)
    print("  ACCESO A ALXOR VET")
    print(f"  Abre:        {BASE}/vet.html")
    print(f"  Usuario:     {ADMIN_EMAIL}")
    print(f"  Contraseña:  (la que has configurado en ADMIN_PASSWORD)")
    print("  " + "·" * 52)
    print("  Cambia la contraseña del administrador tras el primer acceso.")
    print("─" * 56)


if __name__ == "__main__":
    main()
