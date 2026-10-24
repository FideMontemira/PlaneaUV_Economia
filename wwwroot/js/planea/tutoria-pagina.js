// PlaneaUV · Pantalla de tutoría (Views/Home/Tutoria.cshtml): carga al alumno, guarda solo y cambia de periodo.
// El formulario y el recuadro de señales los arma tutorias.js; este archivo le da lo que necesita:
// situacionActual(), marcarCambios(), esperarGuardadoEnCurso() y hayGuardadosPendientes().
//
// Las sesiones se guardan con /Home/GuardarTutorias, que guarda SOLO las tutorías del periodo:
// nunca toca las materias del alumno, aunque el mapa esté abierto en otra pestaña.

let situacionTutoria = null;   // situación del alumno (semáforo, señales...) que manda el servidor

const matriculaTutoria = () => ($('inputMatricula') ? $('inputMatricula').value.trim() : '');
const periodoTutoria = () => ($('selectPeriodoGlobal') ? $('selectPeriodoGlobal').value : '');

function situacionActual() {
    return situacionTutoria && situacionTutoria.matricula === matriculaTutoria() && situacionTutoria.periodo === periodoTutoria() ? situacionTutoria : null;
}

// ===================== GUARDADO AUTOMÁTICO =====================
// Cada cambio guarda una "foto" de las sesiones de ese periodo; se envían poco después (si se cambian varias
// casillas seguidas se manda una sola vez). Si el Excel está abierto se reintenta cada 5 segundos.
const tutoriasPendientes = new Map();   // periodo -> sesiones sin guardar
let guardadoTutoriaEnCurso = null;
let esperaGuardado = null;
let reintentoTutoria = null;
let ultimoGuardadoFallo = false;

function marcarCambios() {
    guardarTutoriasEnMemoria();
    const periodo = periodoTutoria();
    tutoriasPendientes.set(periodo, JSON.parse(JSON.stringify(tutoriasGlobal.filter(t => t.Periodo === periodo))));
    mostrarEstadoTutoria('guardando');
    clearTimeout(esperaGuardado);
    esperaGuardado = setTimeout(() => { esperaGuardado = null; procesarGuardadoTutorias(); }, 400);
}

function hayGuardadosPendientes() {
    return tutoriasPendientes.size > 0 || guardadoTutoriaEnCurso !== null || esperaGuardado !== null;
}

// Manda ya lo pendiente y espera a que termine (para la ficha, el Word, cambiar de periodo o volver al mapa)
function esperarGuardadoEnCurso() {
    if (esperaGuardado) { clearTimeout(esperaGuardado); esperaGuardado = null; }
    procesarGuardadoTutorias();
    if (guardadoTutoriaEnCurso) return guardadoTutoriaEnCurso.then(() => (ultimoGuardadoFallo ? undefined : esperarGuardadoEnCurso()));
    return Promise.resolve();
}

function procesarGuardadoTutorias() {
    if (guardadoTutoriaEnCurso || tutoriasPendientes.size === 0) return;
    clearTimeout(reintentoTutoria);
    reintentoTutoria = null;

    const [periodo, sesiones] = tutoriasPendientes.entries().next().value;
    tutoriasPendientes.delete(periodo);
    mostrarEstadoTutoria('guardando');

    guardadoTutoriaEnCurso = fetch('/Home/GuardarTutorias', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Matricula: matriculaTutoria(), Periodo: periodo, Sesiones: sesiones })
    })
        .then(leerRespuesta)
        .then(() => {
            ultimoGuardadoFallo = false;
            if (tutoriasPendientes.size === 0 && !esperaGuardado) {
                mostrarEstadoTutoria('ok');
                ocultarAviso();
                cargarSituacionTutoria();   // el semáforo y las señales cambian con lo que se registra
            }
        })
        .catch(e => {
            console.error(e);
            ultimoGuardadoFallo = true;
            mostrarEstadoTutoria('error');
            // Datos no válidos: reintentar no lo arregla
            if (e.status === 400 || e.status === 403) { mostrarAviso('No se guardó: ' + e.message); return; }
            if (!tutoriasPendientes.has(periodo)) tutoriasPendientes.set(periodo, sesiones);
            mostrarAviso(e.message + ' (Se reintentará automáticamente.)');
            reintentoTutoria = setTimeout(procesarGuardadoTutorias, 5000);
        })
        .finally(() => {
            guardadoTutoriaEnCurso = null;
            if (!reintentoTutoria) procesarGuardadoTutorias();
        });
}

function mostrarEstadoTutoria(estado) {
    const el = $('estadoGuardado');
    if (!el) return;
    if (estado === 'guardando') { el.className = 'estado-guardado guardando'; el.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Guardando...'; }
    else if (estado === 'ok') { el.className = 'estado-guardado ok'; el.innerHTML = '<i class="bi bi-cloud-check-fill me-1"></i> Guardado'; }
    else { el.className = 'estado-guardado error'; el.innerHTML = '<i class="bi bi-exclamation-triangle-fill me-1"></i> Error al guardar'; }
}

// Si se intenta cerrar o salir con cambios sin guardar, el navegador pide confirmación
window.addEventListener('beforeunload', e => {
    if (hayGuardadosPendientes()) { e.preventDefault(); e.returnValue = ''; }
});

// ===================== CARGA DEL ALUMNO =====================
function cargarSituacionTutoria() {
    const matricula = matriculaTutoria(), periodo = periodoTutoria();
    const numero = cargarSituacionTutoria.ultima = (cargarSituacionTutoria.ultima || 0) + 1;
    return fetch(`/Home/ObtenerSituacionAlumno?matricula=${encodeURIComponent(matricula)}&periodo=${encodeURIComponent(periodo)}`)
        .then(leerRespuesta)
        .then(res => {
            if (numero !== cargarSituacionTutoria.ultima || !res.success) return;
            situacionTutoria = Object.assign({ matricula, periodo }, res.data);
            pintarSenalesTutoria();
            pintarEncabezadoTutoria();
        })
        .catch(e => console.error('Error obteniendo la situación del alumno:', e));
}

let datosAlumnoTutoria = null;
let programaTutoria = '';

function pintarEncabezadoTutoria() {
    const s = situacionActual();
    const partes = [escapeHTML(matriculaTutoria())];
    if (programaTutoria) partes.push(escapeHTML(programaTutoria));
    if (s && s.periodoQueCursa >= 1) partes.push(`cursa el ${s.periodoQueCursa}° periodo`);
    if (datosAlumnoTutoria && datosAlumnoTutoria.situacion && datosAlumnoTutoria.situacion !== 'Activo') partes.push(escapeHTML(datosAlumnoTutoria.situacion));
    $('tutoriaDetalle').innerHTML = partes.join(' · ');
    $('tutoriaSemaforo').innerHTML = s && s.semaforo ? pastillaSemaforo(s.semaforo.final) : '';
}

// Enlace de regreso: abre el mapa con este alumno y periodo
function actualizarEnlaceVolver() {
    const enlace = $('enlaceVolverMapa');
    if (enlace) enlace.href = `/?matricula=${encodeURIComponent(matriculaTutoria())}&periodo=${encodeURIComponent(periodoTutoria())}`;
}

function mostrarErrorTutoria(mensaje) {
    $('tutoriaNombre').innerText = 'No se pudo abrir la tutoría';
    $('avisoTutoria').innerHTML = `<div class="alert alert-warning">${escapeHTML(mensaje)} <a href="/" class="alert-link">Ir al mapa curricular</a></div>`;
    $('senalesTutoria').innerHTML = '';
    $('contenidoTutorias').innerHTML = '';
}

document.addEventListener('DOMContentLoaded', () => {
    llenarSelectorDePeriodos();
    const sel = $('selectPeriodoGlobal');
    const inicial = $('periodoInicial') ? $('periodoInicial').value : '';
    if (inicial && [...sel.options].some(o => o.value === inicial)) sel.value = inicial;
    actualizarEnlaceVolver();

    // Volver al mapa: primero se termina de guardar (así no aparece el aviso de "cambios sin guardar")
    const enlaceVolver = $('enlaceVolverMapa');
    if (enlaceVolver) enlaceVolver.addEventListener('click', e => {
        if (!hayGuardadosPendientes()) return;
        e.preventDefault();
        esperarGuardadoEnCurso().then(() => {
            if (hayGuardadosPendientes()) { mostrarAviso('No se pudo guardar lo último. Revisa el aviso antes de salir.'); return; }
            window.location.href = enlaceVolver.href;
        });
    });

    const matricula = matriculaTutoria();
    if (!matricula) { mostrarErrorTutoria('Falta indicar el alumno.'); return; }

    // Cambiar de periodo: se guarda lo pendiente del periodo anterior y se muestran las sesiones del nuevo
    sel.addEventListener('change', () => {
        esperarGuardadoEnCurso().then(() => {
            history.replaceState(null, '', urlTutoria(matriculaTutoria(), periodoTutoria()));
            actualizarEnlaceVolver();
            cargarTutoriasALaVista();
            cargarSituacionTutoria();
        });
    });

    fetch(`/Home/BuscarAlumnoExcel?matricula=${encodeURIComponent(matricula)}`)
        .then(leerRespuesta)
        .then(res => {
            if (!res.success) { mostrarErrorTutoria(res.message || 'Alumno no encontrado.'); return; }
            datosAlumnoTutoria = res.data;
            // La matrícula tal como está guardada (por si en la dirección venía en minúsculas)
            if (res.data.matricula && $('inputMatricula')) { $('inputMatricula').value = res.data.matricula; actualizarEnlaceVolver(); }
            document.title = `Tutoría · ${res.data.nombre} - PlaneaUV`;
            $('tutoriaNombre').innerText = res.data.nombre || matricula;
            tutoriasGlobal = (res.data.tutorias || []).map(tutoriaDesdeServidor);

            // Materias para "Materia con dificultad": las registradas por el alumno y las de su plan
            const nombres = new Set((res.data.materias || []).map(m => m.nombre).filter(n => n));
            const plan = res.data.planEstudio || '';
            const delPlan = plan
                ? fetch(`/Home/ObtenerMapaCurricular?planNombre=${encodeURIComponent(plan)}`).then(leerRespuesta).then(p => {
                    if (!p.success) return;
                    programaTutoria = p.programa || '';
                    (p.data || []).forEach(m => { const tipo = (m.tipo || '').toLowerCase(); if (tipo !== 'afel' && tipo !== 'optativa') nombres.add(m.nombre); });
                }).catch(() => { })
                : Promise.resolve();

            return delPlan.then(() => {
                materiasDelAlumno = [...nombres];
                pintarEncabezadoTutoria();
                return Promise.all([cargarCatalogoTutorias(), cargarSituacionTutoria()]);
            });
        })
        .catch(e => { console.error(e); mostrarErrorTutoria(e.message || 'Error de comunicación con el sistema.'); });
});
