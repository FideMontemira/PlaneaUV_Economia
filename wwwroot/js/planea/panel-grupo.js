// PlaneaUV · Panel del grupo: todos los alumnos en una tabla ordenable.
// Se carga como script clásico (ver Views/Home/Index.cshtml): comparte el ámbito global con los demás archivos de esta carpeta.

// ===================== PANEL DEL GRUPO =====================
// Todos los alumnos en una tabla: periodo que cursa, avance, materias en curso, rezago y alertas.
let datosPanelGrupo = [];

let ordenPanelGrupo = { columna: 'alertas', ascendente: false };

function abrirPanelGrupo() {
    const selP = $('selectPeriodoGlobal');
    const periodo = selP ? selP.value : '';
    const sub = $('subtituloPanelGrupo'); if (sub) sub.innerText = 'Periodo de trabajo: ' + periodo;
    const tbody = document.querySelector('#tablaPanelGrupo tbody');
    if (tbody) tbody.innerHTML = '<tr><td colspan="9" class="text-center py-4"><div class="spinner-border text-primary" role="status"></div></td></tr>';

    // Encabezados clicables para ordenar (se enlazan una sola vez)
    document.querySelectorAll('#tablaPanelGrupo th[data-columna]').forEach(th => {
        if (th.dataset.enlazado) return;
        th.dataset.enlazado = '1';
        th.addEventListener('click', () => {
            const col = th.getAttribute('data-columna');
            ordenPanelGrupo = { columna: col, ascendente: ordenPanelGrupo.columna === col ? !ordenPanelGrupo.ascendente : true };
            dibujarPanelGrupo();
        });
    });

    bootstrap.Modal.getOrCreateInstance($('modalPanelGrupo')).show();

    // Se espera el guardado en curso para que el panel no muestre datos de antes del último cambio
    esperarGuardadoEnCurso()
        .then(() => fetch(`/Home/ObtenerPanelGrupo?periodo=${encodeURIComponent(periodo)}`))
        .then(leerRespuesta)
        .then(res => { datosPanelGrupo = res.data || []; dibujarPanelGrupo(); })
        .catch(e => {
            console.error(e);
            if (tbody) tbody.innerHTML = `<tr><td colspan="9" class="text-center text-danger py-3">${escapeHTML(e.message)}</td></tr>`;
        });
}

function dibujarPanelGrupo() {
    const tbody = document.querySelector('#tablaPanelGrupo tbody');
    if (!tbody) return;

    const texto = ($('filtroPanelGrupo') ? $('filtroPanelGrupo').value : '').toLowerCase().trim();
    const soloActivos = $('soloActivosPanelGrupo') ? $('soloActivosPanelGrupo').checked : false;

    let filas = datosPanelGrupo.filter(a =>
        (!soloActivos || (a.situacion || 'Activo') === 'Activo') &&
        (!texto || `${a.matricula} ${a.nombre} ${a.programa}`.toLowerCase().includes(texto)));

    const { columna, ascendente } = ordenPanelGrupo;
    filas.sort((a, b) => {
        let x = a[columna], y = b[columna];
        if (x === null || x === undefined) x = typeof y === 'number' ? -1 : '';
        if (y === null || y === undefined) y = typeof x === 'number' ? -1 : '';
        const r = typeof x === 'number' && typeof y === 'number' ? x - y : String(x).localeCompare(String(y), 'es', { sensitivity: 'base' });
        return ascendente ? r : -r;
    });

    // Flecha en la columna por la que está ordenado
    document.querySelectorAll('#tablaPanelGrupo th[data-columna]').forEach(th => {
        th.querySelectorAll('.flecha-orden').forEach(f => f.remove());
        if (th.getAttribute('data-columna') === columna) th.insertAdjacentHTML('beforeend', `<span class="flecha-orden"> ${ascendente ? '▲' : '▼'}</span>`);
    });

    tbody.innerHTML = '';
    if (filas.length === 0) tbody.innerHTML = '<tr><td colspan="9" class="text-center text-muted py-3">No hay alumnos que mostrar.</td></tr>';

    filas.forEach(a => {
        const tr = document.createElement('tr');
        tr.setAttribute('role', 'button');
        const excede = a.periodoQueCursa !== null && a.duracionPlan > 0 && a.periodoQueCursa > a.duracionPlan;
        const periodoTxt = a.periodoQueCursa === null ? '<span class="text-muted">—</span>'
            : a.periodoQueCursa < 1 ? '<span class="text-muted">Aún no ingresa</span>'
            : `<span class="${excede ? 'text-danger fw-bold' : ''}">${a.periodoQueCursa}°${a.duracionPlan ? ' / ' + a.duracionPlan : ''}</span>`;
        const sit = a.situacion || 'Activo';
        tr.innerHTML = `
            <td class="fw-bold">${escapeHTML(a.matricula)}</td>
            <td>${escapeHTML(a.nombre)}</td>
            <td class="small">${escapeHTML(a.programa)}</td>
            <td><span class="badge ${sit === 'Activo' ? 'bg-success' : sit === 'Baja Temporal' ? 'bg-warning text-dark' : 'bg-danger'}">${escapeHTML(sit)}</span></td>
            <td class="text-center">${periodoTxt}</td>
            <td>
                <div class="d-flex align-items-center gap-2" title="${a.creditosAprobados} de ${a.creditosTotales} créditos">
                    <div class="progress flex-grow-1" style="height: 12px;"><div class="progress-bar bg-success" style="width: ${a.avance}%"></div></div>
                    <span class="small fw-bold" style="min-width: 38px;">${a.avance}%</span>
                </div>
            </td>
            <td class="text-center">${a.enCurso}</td>
            <td class="text-center">${a.rezago > 0 ? `<span class="badge bg-secondary">${a.rezago}</span>` : '<span class="text-muted">0</span>'}</td>
            <td class="text-center">${a.alertas > 0 ? `<span class="badge bg-danger">${a.alertas}</span>` : '<span class="text-muted">0</span>'}</td>`;
        tr.addEventListener('click', () => {
            if ($('inputMatricula')) $('inputMatricula').value = a.matricula;
            buscarAlumno();
            bootstrap.Modal.getOrCreateInstance($('modalPanelGrupo')).hide();
            if (modalInicioInstance) modalInicioInstance.hide();
        });
        tbody.appendChild(tr);
    });

    const resumen = $('resumenPanelGrupo');
    if (resumen) {
        const conRezago = filas.filter(a => a.rezago > 0).length, conAlertas = filas.filter(a => a.alertas > 0).length;
        const promedio = filas.length ? Math.round(filas.reduce((s, a) => s + a.avance, 0) / filas.length) : 0;
        resumen.innerText = `${filas.length} alumno(s) · avance promedio ${promedio}% · ${conRezago} con rezago · ${conAlertas} con alertas`;
    }
}
