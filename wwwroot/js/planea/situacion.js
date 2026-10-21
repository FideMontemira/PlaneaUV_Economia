// PlaneaUV · Situación del alumno: periodo que cursa, periodos sin inscripción (pausas) y rezago.
// Se carga como script clásico (ver Views/Home/Index.cshtml): comparte el ámbito global con los demás archivos de esta carpeta.
//
// IMPORTANTE: aquí NO se calcula ninguna regla académica. El periodo que cursa, el descuento de pausas y el
// rezago automático los calcula SOLO el servidor (Services/ReglasTrayectoria.cs) y esta parte solo los muestra.
// Así la pantalla, las alertas, los reportes y el panel del grupo nunca pueden dar resultados distintos.

// Periodos en que el alumno cargado NO estuvo inscrito (bajas temporales). Se guardan con el alumno.
let periodosSinInscripcion = [];

// Última situación recibida del servidor para el alumno y Periodo de Trabajo en pantalla:
// { matricula, periodo, anioIngreso, periodosCorridos, periodoQueCursa, pausasDescontadas, enPausa,
//   duracionPlan, materiasEnRezago: [...], periodosDesdeIngreso: [...] }
let situacionAlumno = null;

// La situación guardada, solo si corresponde al alumno y periodo que se ven ahora (si no, null)
function situacionActual() {
    const inputMat = $('inputMatricula');
    const selP = $('selectPeriodoGlobal');
    const matricula = inputMat ? inputMat.value.trim() : '';
    const periodo = selP ? selP.value : '';
    return situacionAlumno && situacionAlumno.matricula === matricula && situacionAlumno.periodo === periodo ? situacionAlumno : null;
}

// Pide al servidor la situación del alumno en pantalla. Se llama al cargar un alumno, al cambiar el
// Periodo de Trabajo y después de cada guardado (porque aprobar materias o marcar pausas la cambia).
function cargarSituacionAlumno() {
    const inputMat = $('inputMatricula');
    const selP = $('selectPeriodoGlobal');
    const matricula = inputMat ? inputMat.value.trim() : '';
    const periodo = selP ? selP.value : '';

    // Si se pide varias veces seguidas, solo cuenta la última respuesta
    const numeroPeticion = cargarSituacionAlumno.ultimaPeticion = (cargarSituacionAlumno.ultimaPeticion || 0) + 1;
    if (!matricula) { situacionAlumno = null; actualizarProgreso(); return Promise.resolve(); }

    return fetch(`/Home/ObtenerSituacionAlumno?matricula=${encodeURIComponent(matricula)}&periodo=${encodeURIComponent(periodo)}`)
        .then(leerRespuesta)
        .then(res => {
            if (numeroPeticion !== cargarSituacionAlumno.ultimaPeticion) return;
            situacionAlumno = res.success ? Object.assign({ matricula, periodo }, res.data) : null;
            actualizarProgreso();   // vuelve a pintar periodo, rezago y contadores con la respuesta
        })
        .catch(e => console.error('Error obteniendo la situación del alumno:', e));
}

function abrirModalPausas() {
    const inputMat = $('inputMatricula');
    const matricula = inputMat ? inputMat.value.trim() : '';
    if (!matricula || alumnoCargado !== matricula) { alert("Selecciona un alumno primero."); return; }

    const s = situacionActual();
    if (!s) { alert("Se está calculando el periodo del alumno. Intenta de nuevo en un momento."); return; }
    if (!s.anioIngreso) { alert("La matrícula no trae el año de ingreso (ej. S21...), así que no se puede calcular su periodo."); return; }

    // Periodos desde su ingreso hasta el Periodo de Trabajo (los da el servidor)
    const lista = $('listaPausas');
    lista.innerHTML = '';
    if (s.periodosDesdeIngreso.length === 0)
        lista.innerHTML = '<div class="list-group-item text-muted small">En el Periodo de Trabajo elegido el alumno todavía no había ingresado.</div>';

    s.periodosDesdeIngreso.forEach(nombre => {
        const item = document.createElement('label');
        item.className = 'list-group-item d-flex align-items-center gap-2';
        item.innerHTML = `<input class="form-check-input m-0" type="checkbox" ${periodosSinInscripcion.includes(nombre) ? 'checked' : ''}> <span>${escapeHTML(nombre)}</span>`;
        item.querySelector('input').addEventListener('change', function () {
            periodosSinInscripcion = periodosSinInscripcion.filter(p => p !== nombre);
            if (this.checked) periodosSinInscripcion.push(nombre);
            actualizarResumenPausas();
            marcarCambios();   // se guarda al momento; al terminar, el servidor recalcula periodo y rezago
        });
        lista.appendChild(item);
    });
    actualizarResumenPausas();
    bootstrap.Modal.getOrCreateInstance($('modalPausas')).show();
}

function actualizarResumenPausas() {
    const r = $('resumenPausas');
    if (r) r.innerText = periodosSinInscripcion.length === 0 ? 'Sin periodos marcados.' : `${periodosSinInscripcion.length} periodo(s) sin inscripción.`;
}

// Pinta la situación recibida del servidor: recuadro "Periodo que cursa", columna resaltada y etiquetas EN REZAGO.
// No calcula nada; si todavía no llega la respuesta muestra "Calculando...".
function aplicarSituacionAlumno() {
    const info = $('infoPeriodoAlumno');
    const inputMat = $('inputMatricula');
    const matricula = inputMat ? inputMat.value.trim() : '';

    // Se quita el resaltado anterior del mapa y las marcas de rezago automático
    document.querySelectorAll('.periodo-header.periodo-actual').forEach(h => h.classList.remove('periodo-actual'));
    document.querySelectorAll('.badge-rezago-auto').forEach(b => b.remove());
    document.querySelectorAll('.materia-card[data-rezago-auto]').forEach(t => t.removeAttribute('data-rezago-auto'));

    if (!info) return;
    info.className = 'form-control form-control-sm fw-bold bg-light text-muted';
    info.title = '';
    if (!matricula) { info.innerText = '—'; return; }

    const s = situacionActual();
    if (!s) { info.innerText = 'Calculando...'; return; }
    if (!s.anioIngreso) { info.innerText = 'Matrícula sin año'; info.title = 'La matrícula no empieza con S + año de ingreso (ej. S21...)'; return; }
    if (s.periodosCorridos === null) { info.innerText = '—'; return; }
    if (s.periodosCorridos < 1) { info.innerText = `Ingresa en ago. ${s.anioIngreso}`; info.title = 'En el Periodo de Trabajo elegido el alumno todavía no había ingresado.'; return; }

    const n = s.periodoQueCursa;
    info.title = `Ingresó en agosto de ${s.anioIngreso}.` + (s.pausasDescontadas > 0 ? ` Se descuentan ${s.pausasDescontadas} periodo(s) sin inscripción.` : '');
    if (s.enPausa) {
        info.innerText = `Sin inscripción (retoma ${n}°)`;
        info.className = 'form-control form-control-sm fw-bold bg-light text-secondary border-secondary';
        info.title += ' En este Periodo de Trabajo está marcado como no inscrito.';
    } else if (s.duracionPlan && n > s.duracionPlan) {
        // Ya rebasó la duración del plan: se marca en rojo
        info.innerText = `${n}° periodo (plan: ${s.duracionPlan})`;
        info.className = 'form-control form-control-sm fw-bold bg-light text-danger border-danger';
        info.title += ` El plan dura ${s.duracionPlan} periodos: lleva ${n - s.duracionPlan} de más.`;
    } else {
        info.innerText = `${n}° periodo`;
        info.className = 'form-control form-control-sm fw-bold bg-light text-primary border-primary';
    }

    // Columna del periodo que cursa
    if (!s.enPausa) {
        const columnaActual = document.querySelector(`.periodo-columna[data-semestre="${n}"]`);
        if (columnaActual) columnaActual.querySelector('.periodo-header').classList.add('periodo-actual');
    }

    // Etiqueta EN REZAGO en las materias que indicó el servidor. Si la materia se acaba de aprobar y el
    // servidor todavía no responde con la lista nueva, no se marca (evita un parpadeo de la etiqueta).
    const enRezago = new Set(s.materiasEnRezago || []);
    document.querySelectorAll('#contenedor-mapa-dinamico .materia-card').forEach(t => {
        if (!enRezago.has(t.getAttribute('data-nombre')) || t.getAttribute('data-estado') === 'aprobada') return;
        t.setAttribute('data-rezago-auto', 'true');
        if (t.getAttribute('data-rezago') === 'true') return;   // ya trae la etiqueta de rezago manual
        t.insertAdjacentHTML('beforeend', '<div class="badge bg-secondary text-white mt-1 shadow-sm badge-rezago-auto" style="font-size:0.65rem; width:100%" title="Materia de un periodo anterior al que cursa">EN REZAGO</div>');
    });
}
