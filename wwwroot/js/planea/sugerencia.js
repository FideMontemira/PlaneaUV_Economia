// PlaneaUV · Sugerencia de carga: materias que le conviene inscribir al alumno.
// Se carga como script clásico (ver Views/Home/Index.cshtml): comparte el ámbito global con los demás archivos de esta carpeta.

// ===================== SUGERENCIA DE CARGA =====================
// Propone qué materias le conviene inscribir al alumno, con lo que ya está en pantalla (plan + avance):
//  1) atrasadas que son prerrequisito de otras (candados)   2) el resto del rezago
//  3) las de su periodo                                     4) adelantar, si aún caben créditos
// Nunca sugiere materias aprobadas, en curso o bloqueadas por prerrequisito.
function abrirSugerenciaCarga() {
    const inputMat = $('inputMatricula');
    const matricula = inputMat ? inputMat.value.trim() : '';
    if (!matricula || alumnoCargado !== matricula || !document.querySelector('#contenedor-mapa-dinamico .materia-card')) {
        alert("Selecciona un alumno con plan de estudios primero.");
        return;
    }

    // Tope por defecto: el promedio de créditos por periodo de su plan
    const columnas = document.querySelectorAll('.periodo-columna[data-semestre]').length || 1;
    const inputMax = $('inputMaxCreditos');
    if (inputMax) inputMax.value = Math.max(1, Math.round((creditosTotalesPlan || 0) / columnas)) || 40;

    const selP = $('selectPeriodoGlobal');
    const sub = $('subtituloSugerencia');
    if (sub) sub.innerText = `${$('inputNombreAlumno') ? $('inputNombreAlumno').value : ''} · ${selP ? selP.value : ''}`;

    calcularSugerenciaCarga();
    bootstrap.Modal.getOrCreateInstance($('modalSugerencia')).show();
}

function calcularSugerenciaCarga() {
    const contenedor = $('contenedorSugerencia');
    if (!contenedor) return;

    // Periodo que cursa según el servidor (null si la matrícula no trae año o aún no llega la respuesta)
    const s = situacionActual();
    const n = s && s.periodosCorridos >= 1 ? s.periodoQueCursa : null;
    const maximo = parseInt($('inputMaxCreditos') ? $('inputMaxCreditos').value : '0') || 0;

    // Cuántas materias desbloquea cada una (aparece como prerrequisito de otras)
    const desbloquea = {};
    document.querySelectorAll('.materia-card[data-prerequisito]').forEach(t => {
        t.getAttribute('data-prerequisito').split(',').map(p => p.trim()).filter(p => p).forEach(p => { desbloquea[p] = (desbloquea[p] || 0) + 1; });
    });

    let creditosEnCurso = 0, enCurso = 0, bloqueadas = 0;
    const candidatas = [];
    document.querySelectorAll('.periodo-columna[data-semestre] .materia-card').forEach(t => {
        if (t.classList.contains('bg-afel')) return;
        const estado = t.getAttribute('data-estado');
        const creditos = parseInt(t.getAttribute('data-creditos')) || 0;
        if (estado === 'aprobada') return;
        if (estado === 'cursando') { enCurso++; creditosEnCurso += creditos; return; }
        if (t.classList.contains('estado-bloqueada')) { bloqueadas++; return; }

        const semestre = parseInt(t.closest('.periodo-columna').getAttribute('data-semestre'));
        const nombreBase = t.getAttribute('data-nombre');
        const abre = desbloquea[nombreBase] || 0;
        let prioridad, motivo, color;
        if (n !== null && semestre < n && abre > 0) { prioridad = 1; motivo = `Atrasada · desbloquea ${abre}`; color = 'bg-danger'; }
        else if (n !== null && semestre < n) { prioridad = 2; motivo = 'Atrasada'; color = 'bg-secondary'; }
        else if (n === null || semestre === n) { prioridad = 3; motivo = n === null ? 'Disponible' : 'De su periodo'; color = 'bg-primary'; }
        else { prioridad = 4; motivo = 'Adelantar'; color = 'bg-info text-dark'; }

        candidatas.push({ nombre: t.getAttribute('data-nombre-personalizado') || nombreBase, semestre, creditos, prioridad, motivo, color, abre, reprobada: estado === 'reprobada' });
    });

    candidatas.sort((a, b) => a.prioridad - b.prioridad || a.semestre - b.semestre || b.abre - a.abre);

    // Se llenan los créditos disponibles en orden de prioridad; lo que no cabe queda como "también puede cursar"
    const disponible = Math.max(0, maximo - creditosEnCurso);
    let usados = 0;
    const sugeridas = [], fuera = [];
    candidatas.forEach(c => {
        if (usados + c.creditos <= disponible) { sugeridas.push(c); usados += c.creditos; } else fuera.push(c);
    });

    const fila = c => `<tr>
            <td class="fw-bold">${escapeHTML(c.nombre)}${c.reprobada ? ' <span class="badge bg-warning text-dark">Recursa</span>' : ''}</td>
            <td class="text-center">${c.semestre}°</td>
            <td class="text-center">${c.creditos}</td>
            <td><span class="badge ${c.color}">${escapeHTML(c.motivo)}</span></td>
        </tr>`;
    const tabla = (lista) => `<div class="table-responsive bg-white rounded shadow-sm mb-3"><table class="table table-sm align-middle mb-0">
            <thead class="table-light"><tr><th>Materia</th><th class="text-center">Periodo del plan</th><th class="text-center">Créditos</th><th>Motivo</th></tr></thead>
            <tbody>${lista.map(fila).join('')}</tbody></table></div>`;

    let html = '';
    if (enCurso > 0) html += `<div class="alert alert-info small py-2">Ya tiene <strong>${enCurso} materia(s) en curso</strong> (${creditosEnCurso} créditos); se descuentan del máximo.</div>`;
    if (sugeridas.length > 0) html += `<h6 class="fw-bold text-success"><i class="bi bi-check2-circle me-1"></i>Sugeridas (${sugeridas.length})</h6>` + tabla(sugeridas);
    else html += `<div class="alert alert-warning">No hay materias que sugerir con ese máximo de créditos${candidatas.length === 0 ? ': no le quedan materias disponibles para inscribir.' : '.'}</div>`;
    if (fuera.length > 0) html += `<h6 class="fw-bold text-muted"><i class="bi bi-plus-circle me-1"></i>También puede cursar (no caben en el máximo)</h6>` + tabla(fuera);
    if (bloqueadas > 0) html += `<p class="small text-muted mb-0"><i class="bi bi-lock-fill me-1"></i>${bloqueadas} materia(s) no se sugieren porque les falta aprobar un prerrequisito o cubrir el porcentaje de créditos que piden.</p>`;
    contenedor.innerHTML = html;

    const total = $('totalSugerencia');
    if (total) total.innerText = `Carga sugerida: ${usados} créditos` + (creditosEnCurso > 0 ? ` + ${creditosEnCurso} en curso = ${usados + creditosEnCurso}` : '') + ` (máximo ${maximo})`;
}
