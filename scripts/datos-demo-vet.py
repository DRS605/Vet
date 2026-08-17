#!/usr/bin/env python3
"""
Datos de demostración para ALXOR Vet (SPA veterinaria, /vet.html).

Registra (o reutiliza) una cuenta y una empresa de demo, y siembra vía API una
clínica completa: pautas vacunales de perro y gato, varios clientes con animales
(incluido un cachorro), consultas, vacunaciones con próxima dosis, una cirugía con
revisión, citas (varias confirmadas para que el KPI luzca) y actos pendientes de
facturar. Idempotente: si la empresa ya tiene animales, no vuelve a sembrar.

Uso:
    python3 scripts/datos-demo-vet.py                       # contra http://localhost:8080
    python3 scripts/datos-demo-vet.py http://localhost:5000 # otra URL base

Solo usa la biblioteca estándar (urllib): no requiere instalar nada.
Pensado para una base de datos de desarrollo/demo, no para producción.
"""
import json
import sys
import urllib.request
import urllib.error
from datetime import date, datetime, timedelta

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:8080").rstrip("/")

# Credenciales de la cuenta de demo (te servirán para entrar en /vet.html).
EMAIL = "demo-vet@alxorcore.es"
NOMBRE = "Dra. Marta Vidal"
PASS = "Demo1234!"

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


def iso(d):
    return d.isoformat()


def dtiso(d, hora, minuto=0):
    """Marca de tiempo local con offset (para citas)."""
    return datetime(d.year, d.month, d.day, hora, minuto).astimezone().isoformat()


def main():
    global _token
    print(f"ALXOR Vet · sembrando clínica de demo en {BASE}\n")

    # Comprobación de vida.
    try:
        llamar("GET", "/salud", auth=False)
    except Exception as e:
        print(f"No se llega a la API en {BASE}. ¿Está arrancada? ({e})")
        sys.exit(1)

    # 1) Cuenta: registrar (o reutilizar) e iniciar sesión.
    print("Cuenta")
    try:
        llamar("POST", "/auth/registro", {"email": EMAIL, "nombre": NOMBRE, "contrasena": PASS}, auth=False)
        paso("usuario de demo creado")
    except RuntimeError:
        paso("el usuario de demo ya existía, reutilizándolo")
    login = llamar("POST", "/auth/login", {"email": EMAIL, "contrasena": PASS}, auth=False)
    _token = login["token"]
    paso("sesión iniciada")

    # 2) Empresa: crear si no hay ninguna, y seleccionarla.
    print("Clínica")
    empresas = llamar("GET", "/empresas")
    if empresas:
        emp = empresas[0]
        paso(f"reutilizando «{emp.get('razonSocial', emp['id'])}»")
    else:
        llamar("POST", "/empresas", {
            "nif": "B12345674", "razonSocial": "Clínica Veterinaria Demo",
            "calle": "Avinguda del Port 88", "codigoPostal": "46023",
            "poblacion": "Valencia", "provincia": "Valencia",
        })
        emp = llamar("GET", "/empresas")[0]
        paso(f"creada «{emp['razonSocial']}»")
    _token = llamar("POST", f"/empresas/{emp['id']}/seleccionar")["token"]

    # Idempotencia: si ya hay animales, no volvemos a sembrar, pero sí aseguramos el enlace de la
    # Cartilla Viva de la dueña de Nala para poder abrir la demo del portal.
    if llamar("GET", "/animales"):
        print("\nLa clínica ya tiene animales: no se vuelve a sembrar para no duplicar.")
        credenciales(enlace_cartilla_de("Laura Giménez Ortí"))
        return

    hoy = date.today()

    # 3) Pautas vacunales (cuadro maestro por especie).
    print("Pautas vacunales")
    pautas = {}
    plan_pautas = [
        ("Perro", "Polivalente (DHPPi/L)", "Recomendada", 6, 12),
        ("Perro", "Rabia", "Legal", 12, 12),
        ("Perro", "Tos de las perreras", "Opcional", 8, 12),
        ("Gato", "Trivalente felina", "Recomendada", 8, 12),
        ("Gato", "Leucemia felina", "Recomendada", 8, 12),
        ("Gato", "Rabia", "Legal", 12, 12),
    ]
    for especie, nombre, caracter, inicio, refuerzo in plan_pautas:
        p = llamar("POST", "/vacunas/pautas", {
            "especie": especie, "nombre": nombre, "caracter": caracter,
            "edadInicioSemanas": inicio, "periodicidadRefuerzoMeses": refuerzo,
        })
        pautas[(especie, nombre)] = p["id"]
    paso(f"{len(pautas)} pautas (perro y gato)")

    # 4) Clientes con animales.
    print("Clientes y animales")
    clientes = {}
    for nombre, nif, email in [
        ("Laura Giménez Ortí", "48291774K", "laura.gimenez@example.com"),
        ("Marcos Ruiz Sanz", "44531218T", "marcos.ruiz@example.com"),
        ("Ana Torres Belda", "20155884P", "ana.torres@example.com"),
        ("Javier Peña Lloret", "73991002S", "javier.pena@example.com"),
    ]:
        c = llamar("POST", "/clientes", {"nombre": nombre, "nifFiscal": nif, "email": email})
        clientes[nombre] = c["id"]
    paso(f"{len(clientes)} clientes")

    def nac(anios=0, meses=0):
        d = hoy.replace(day=1) - timedelta(days=anios * 365 + meses * 30)
        return iso(d)

    # (cliente, nombre, especie, sexo, raza, fechaNac, microchip, esterilizado, pesoKg)
    animales_def = [
        ("Laura Giménez Ortí", "Nala", "Perro", "Hembra", "Golden Retriever", nac(4), "941000024587207", True, 28.4),
        ("Laura Giménez Ortí", "Simba", "Gato", "Macho", "Europeo común", nac(2), "941000024587553", False, 4.6),
        ("Ana Torres Belda", "Coco", "Perro", "Macho", "Beagle", nac(0, 4), "941000024591180", False, 6.2),
        ("Marcos Ruiz Sanz", "Kira", "Perro", "Hembra", "Border Collie", nac(3), "941000024580042", True, 17.1),
        ("Javier Peña Lloret", "Rocky", "Perro", "Macho", "Bulldog francés", nac(2), "941000024588890", False, 12.8),
        ("Marcos Ruiz Sanz", "Luna", "Gato", "Hembra", "Siamés", nac(1), "941000024587991", True, 3.9),
    ]
    animales = {}
    for cliente, nombre, especie, sexo, raza, fnac, chip, ester, peso in animales_def:
        a = llamar("POST", "/animales", {
            "clienteId": clientes[cliente], "nombre": nombre, "especie": especie, "sexo": sexo,
            "raza": raza, "fechaNacimiento": fnac, "microchip": chip, "esterilizado": ester, "pesoKg": peso,
        })
        animales[nombre] = a["id"]
    paso(f"{len(animales)} animales (incluido el cachorro Coco)")

    # 5) Consultas (historial clínico).
    print("Consultas")
    consultas = [
        ("Nala", 34, "Cojera pata trasera", "Contractura muscular leve",
         "Antiinflamatorio (Meloxicam) 5 días y reposo. Revisión en 2 semanas.", 28.4),
        ("Nala", 160, "Revisión anual", "Estado general excelente", "Sin hallazgos. Peso estable.", 28.1),
        ("Simba", 45, "Dermatitis", "Dermatitis alérgica estacional",
         "Champú medicado y antihistamínico 10 días.", 4.6),
        ("Kira", 20, "Control de tratamiento", "Evolución favorable", "Continuar pauta 1 semana más.", 17.1),
    ]
    for nombre, hace, motivo, diag, trat, peso in consultas:
        llamar("POST", f"/animales/{animales[nombre]}/consultas", {
            "fecha": iso(hoy - timedelta(days=hace)), "motivo": motivo,
            "diagnostico": diag, "tratamiento": trat, "pesoKg": peso, "veterinario": NOMBRE,
        })
    paso(f"{len(consultas)} consultas")

    # 6) Vacunaciones (algunas con próxima dosis dentro de 7 días para el KPI de vacunas).
    print("Vacunaciones")
    vacunas = [
        ("Nala", "Perro", "Polivalente (DHPPi/L)", 210, 3, "L-4471B"),   # próxima en ~3 días
        ("Nala", "Perro", "Rabia", 210, 40, "R-2210"),
        ("Simba", "Gato", "Trivalente felina", 30, 6, "T-8890"),          # próxima en ~6 días
        ("Coco", "Perro", "Polivalente (DHPPi/L)", 40, 20, "L-5521"),
        ("Kira", "Perro", "Rabia", 120, 60, "R-2251"),
    ]
    for nombre, especie, pauta, hace, prox, lote in vacunas:
        llamar("POST", f"/animales/{animales[nombre]}/vacunas", {
            "fechaAplicacion": iso(hoy - timedelta(days=hace)),
            "pautaVacunalId": pautas[(especie, pauta)],
            "lote": lote, "proximaDosis": iso(hoy + timedelta(days=prox)), "veterinario": NOMBRE,
        })
    paso(f"{len(vacunas)} vacunaciones (2 con próxima dosis en la próxima semana)")

    # 7) Cirugía con próxima revisión.
    print("Cirugías")
    llamar("POST", f"/animales/{animales['Nala']}/cirugias", {
        "fecha": iso(hoy - timedelta(days=270)), "nombre": "Esterilización (OVH)",
        "descripcion": "Sin complicaciones. Alta con collar isabelino y analgesia 4 días.",
        "cirujano": NOMBRE, "anestesia": "Isoflurano",
        "proximaRevision": iso(hoy + timedelta(days=5)),
    })
    paso("1 cirugía con revisión próxima")

    # 8) Citas de hoy (varias confirmadas/atendidas para que el KPI dé un número bonito).
    print("Agenda")
    # (animal, horaInicio(h,m), duración, tipo, motivo, acción)
    citas_hoy = [
        ("Nala", (9, 30), 30, "Revision", "Revisión + vacuna polivalente", "confirmar"),
        ("Simba", (10, 15), 20, "Consulta", "Consulta dermatología", "atender"),
        ("Coco", (11, 0), 30, "Vacuna", "2ª dosis primovacunación", "confirmar"),
        ("Kira", (12, 30), 20, "Revision", "Control de tratamiento", "atender"),
        ("Rocky", (16, 0), 40, "Cirugia", "Preoperatorio esterilización", None),
        ("Luna", (17, 30), 20, "Consulta", "Revisión anual", "confirmar"),
    ]
    n_conf = 0
    for nombre, (h, m), dur, tipo, motivo, accion in citas_hoy:
        cita = llamar("POST", "/citas", {
            "animalId": animales[nombre], "inicio": dtiso(hoy, h, m),
            "duracionMinutos": dur, "tipo": tipo, "motivo": motivo, "veterinario": NOMBRE,
        })
        if accion == "confirmar":
            llamar("POST", f"/citas/{cita['id']}/confirmar")
            n_conf += 1
        elif accion == "atender":
            llamar("POST", f"/citas/{cita['id']}/confirmar")
            llamar("POST", f"/citas/{cita['id']}/atender")
            n_conf += 1
    # Algunas citas pasadas atendidas, para nutrir la serie mensual del gráfico.
    n_hist = 0
    for k in range(1, 5):
        d = hoy - timedelta(days=k * 32)
        for nombre, (h, m) in [("Nala", (10, 0)), ("Kira", (11, 0)), ("Simba", (12, 0))]:
            cita = llamar("POST", "/citas", {
                "animalId": animales[nombre], "inicio": dtiso(d, h, m),
                "duracionMinutos": 30, "tipo": "Consulta", "motivo": "Visita", "veterinario": NOMBRE,
            })
            llamar("POST", f"/citas/{cita['id']}/confirmar")
            if (k + h) % 3 != 0:
                llamar("POST", f"/citas/{cita['id']}/atender")
            n_hist += 1
    # Citas próximas (días siguientes): algunas «solicitadas» para que el dueño las confirme desde la
    # Cartilla Viva, y alguna ya confirmada. Son las que aparecen en el portal como «tu próxima cita».
    proximas = [
        ("Nala", 3, (10, 0), 30, "Revision", "Revisión anual + vacuna polivalente", None),
        ("Simba", 6, (11, 30), 20, "Vacuna", "Refuerzo trivalente felina", None),
        ("Coco", 4, (12, 0), 30, "Vacuna", "2ª dosis primovacunación", None),
        ("Kira", 5, (9, 30), 20, "Revision", "Control de tratamiento", "confirmar"),
    ]
    for nombre, dias, (h, m), dur, tipo, motivo, accion in proximas:
        cita = llamar("POST", "/citas", {
            "animalId": animales[nombre], "inicio": dtiso(hoy + timedelta(days=dias), h, m),
            "duracionMinutos": dur, "tipo": tipo, "motivo": motivo, "veterinario": NOMBRE,
        })
        if accion == "confirmar":
            llamar("POST", f"/citas/{cita['id']}/confirmar")
    paso(f"{len(citas_hoy)} citas hoy ({n_conf} confirmadas/atendidas) + {len(proximas)} próximas + {n_hist} citas históricas para el gráfico")

    # 9) Actos clínicos pendientes de facturar.
    print("Facturación")
    actos = [
        ("Nala", "Consulta + vacuna polivalente", 42.0, 21),
        ("Nala", "Vacuna antirrábica", 18.0, 21),
        ("Simba", "Consulta dermatología", 35.0, 21),
        ("Coco", "2ª dosis primovacunación", 28.0, 21),
        ("Kira", "Revisión de tratamiento", 25.0, 21),
    ]
    for nombre, concepto, importe, iva in actos:
        llamar("POST", f"/animales/{animales[nombre]}/actos", {
            "concepto": concepto, "importe": importe, "porcentajeIva": iva,
        })
    paso(f"{len(actos)} actos pendientes de facturar")

    # 10) Recordatorios: generarlos desde los vencimientos de vacunas/revisiones.
    print("Recordatorios")
    try:
        llamar("POST", "/recordatorios/generar?dias=30")
        pend = llamar("GET", "/recordatorios?estado=Pendiente")
        paso(f"{len(pend)} recordatorios pendientes generados")
    except RuntimeError as e:
        paso(f"omitidos ({e})")

    # 11) Cartilla Viva: enlace del portal del dueño para la dueña de Nala (Laura).
    print("Cartilla Viva")
    acceso = llamar("POST", f"/clientes/{clientes['Laura Giménez Ortí']}/portal")
    enlace_cartilla = BASE + acceso["enlace"]
    paso("enlace de la Cartilla Viva de Laura (dueña de Nala) generado")

    print("\n¡Listo! Clínica de demo cargada.")
    credenciales(enlace_cartilla)


def enlace_cartilla_de(nombre_cliente):
    """Genera (o regenera) el acceso de portal de un cliente por su nombre y devuelve la URL completa."""
    try:
        clientes = llamar("GET", "/clientes")
        cliente = next((c for c in clientes if c.get("nombre") == nombre_cliente), None)
        if not cliente:
            return None
        acceso = llamar("POST", f"/clientes/{cliente['id']}/portal")
        return BASE + acceso["enlace"]
    except RuntimeError:
        return None


def credenciales(enlace_cartilla=None):
    print("\n" + "─" * 52)
    print(f"  Abre:       {BASE}/vet.html")
    print(f"  Usuario:    {EMAIL}")
    print(f"  Contraseña: {PASS}")
    if enlace_cartilla:
        print("  " + "·" * 48)
        print("  Cartilla Viva (portal del dueño, sin login):")
        print(f"  {enlace_cartilla}")
    print("─" * 52)


if __name__ == "__main__":
    main()
