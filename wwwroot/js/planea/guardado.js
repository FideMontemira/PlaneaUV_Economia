// PlaneaUV · Guardado automático: cola de guardado por alumno, reintentos y estado del guardado.
// Se carga como script clásico (ver Views/Home/Index.cshtml): comparte el ámbito global con los demás archivos de esta carpeta.

let huboCambios = false;

// ===================== GUARDADO AUTOMÁTICO =====================
// Cada cambio toma una "foto" de los datos del alumno en ese momento y la deja en una cola por matrícula.
// Así, aunque se cambie de alumno mientras se guarda, cada alumno se guarda con SUS datos y no se pierde
// el último cambio. Si un guardado falla (p. ej. el Excel está abierto) se reintenta cada 5 segundos.
let guardadoEnCurso = null;               // promesa del guardado que se está enviando

const guardadosPendientes = new Map();    // matrícula -> datos más recientes aún sin guardar

const alumnosEliminados = new Set();      // matrículas borradas: sus guardados pendientes se descartan

let reintentoGuardado = null;

let alumnoCargado = '';                   // matrícula cuyos datos están dibujados en el mapa

function marcarCambios() {
    const datos = construirDatosAlumno();
    if (!datos) return;
    huboCambios = true;
    guardadosPendientes.set(datos.Matricula, datos);
    procesarColaGuardado();
}

function hayGuardadosPendientes() {
    return guardadosPendientes.size > 0 || guardadoEnCurso !== null;
}

// Promesa que se resuelve cuando termina el guardado que esté en curso (si hay uno)
function esperarGuardadoEnCurso() {
    return guardadoEnCurso || Promise.resolve();
}

function construirDatosAlumno() {
    let inputMat = $('inputMatricula');
    const matricula = inputMat ? inputMat.value.trim() : '';

    // Solo se guarda si el mapa en pantalla es realmente de este alumno. Si el mapa no cargó (plan
    // inexistente) o todavía se está cargando, guardar mandaría otras materias o ninguna y borraría su avance.
    if (!matricula || alumnoCargado !== matricula || !document.querySelector('#contenedor-mapa-dinamico .materia-card')) return null;

    guardarTutoriasEnMemoria();

    const materias = [];
    document.querySelectorAll('.materia-card').forEach(t => {
        let hist = t.getAttribute('data-historial');
        if (hist) materias.push(...JSON.parse(hist));
    });

    return {
        Matricula: matricula,
        Nombre: $('inputNombreAlumno') ? $('inputNombreAlumno').value.trim() : '',
        Situacion: $('selectSituacionAlumno') ? $('selectSituacionAlumno').value : 'Activo',
        // Respaldo: si por alguna razón no hay plan, usar el que está dibujado en pantalla
        PlanEstudio: planActualAlumno || planDibujadoActual,
        PeriodosSinInscripcion: periodosSinInscripcion.slice(),
        Tutorias: JSON.parse(JSON.stringify(tutoriasGlobal)),
        Materias: materias
    };
}

function procesarColaGuardado() {
    if (guardadoEnCurso || guardadosPendientes.size === 0) return;
    clearTimeout(reintentoGuardado);
    reintentoGuardado = null;

    const [matricula, datos] = guardadosPendientes.entries().next().value;
    guardadosPendientes.delete(matricula);
    if (alumnosEliminados.has(matricula)) { procesarColaGuardado(); return; }

    mostrarEstadoGuardado('guardando');

    guardadoEnCurso = fetch('/Home/ExportarExcel', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(datos) })
        .then(leerRespuesta)
        .then(() => {
            if (guardadosPendientes.size === 0) {
                huboCambios = false;
                mostrarEstadoGuardado('ok');
                ocultarAviso();
                cargarListaAlumnos();
                cargarListaAlumnosInicio();
                cargarAlertas();
                cargarSituacionAlumno();   // aprobar materias o marcar pausas cambia el periodo y el rezago
            }
        })
        .catch(e => {
            console.error(e);
            // Se vuelve a encolar (salvo que ya haya datos más nuevos de ese alumno) y se reintenta más tarde
            if (!guardadosPendientes.has(matricula) && !alumnosEliminados.has(matricula)) guardadosPendientes.set(matricula, datos);
            mostrarEstadoGuardado('error');
            mostrarAviso(e.message + ' (Se reintentará automáticamente.)');
            reintentoGuardado = setTimeout(procesarColaGuardado, 5000);
        })
        .finally(() => {
            guardadoEnCurso = null;
            if (!reintentoGuardado) procesarColaGuardado();
        });
}

// Si se intenta cerrar la pestaña con cambios sin guardar, el navegador pide confirmación
window.addEventListener('beforeunload', e => {
    if (hayGuardadosPendientes()) { e.preventDefault(); e.returnValue = ''; }
});

function mostrarEstadoGuardado(estado) {
    const el = $('estadoGuardado');
    if (!el) return;
    // El indicador vive en la barra superior (fondo azul): los colores los pone el CSS con .ok / .error
    if (estado === 'guardando') { el.className = 'estado-guardado guardando'; el.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Guardando...'; }
    else if (estado === 'ok') { el.className = 'estado-guardado ok'; el.innerHTML = '<i class="bi bi-cloud-check-fill me-1"></i> Guardado'; }
    else { el.className = 'estado-guardado error'; el.innerHTML = '<i class="bi bi-exclamation-triangle-fill me-1"></i> Error al guardar'; }
}
