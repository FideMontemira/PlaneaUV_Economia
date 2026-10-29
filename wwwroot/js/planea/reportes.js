// PlaneaUV · Alertas y reporte global (incluye su impresión).
// Se carga como script clásico (ver Views/Home/Index.cshtml): comparte el ámbito global con los demás archivos de esta carpeta.

let modalReporteGlobalInstance = null;

function cargarAlertas() {
    const contenedor = $('contenedorTarjetasAlerta');
    if (!contenedor) return;
    contenedor.innerHTML = '<div class="text-center py-4"><div class="spinner-border text-warning" role="status"></div></div>';

    // Si se piden las alertas varias veces seguidas (p. ej. al cambiar de periodo), solo cuenta la última petición
    const numeroPeticion = cargarAlertas.ultimaPeticion = (cargarAlertas.ultimaPeticion || 0) + 1;
    const selPeriodoAlertas = $('selectPeriodoGlobal');
    fetch(`/Home/ObtenerReporteAlertas?periodo=${encodeURIComponent(selPeriodoAlertas ? selPeriodoAlertas.value : '')}`).then(leerRespuesta).then(res => {
        if (numeroPeticion !== cargarAlertas.ultimaPeticion) return;
        const alertas = res.data; const contadorBadge = $('contadorAlertas');

        if (alertas.length === 0) {
            if (contadorBadge) contadorBadge.style.display = 'none';
            contenedor.innerHTML = `<div class="alert alert-success text-center mt-2 shadow-sm border-0">Todo en orden. No hay alumnos en riesgo actualmente.</div>`;
            return;
        }

        if (contadorBadge) { contadorBadge.innerText = alertas.length; contadorBadge.style.display = 'block'; }
        contenedor.innerHTML = '';

        alertas.forEach(alerta => {
            const tarjeta = document.createElement('div'); tarjeta.className = 'card border-0 shadow-sm mb-2';
            tarjeta.innerHTML = `<div class="card-body p-3">
                    <div class="d-flex justify-content-between align-items-start mb-2"><span class="badge ${escapeHTML(alerta.badge)} px-2 py-1">${escapeHTML(alerta.mensaje)}</span></div>
                    <h6 class="card-title fw-bold text-dark mb-1">${escapeHTML(alerta.nombre)}</h6>
                    <p class="card-text text-muted small mb-2"><strong>Matrícula:</strong> ${escapeHTML(alerta.matricula)}<br/><strong>${escapeHTML(alerta.etiqueta || 'Materia')}:</strong> ${escapeHTML(alerta.materia)}</p>
                    <button class="btn btn-sm btn-outline-secondary w-100 fw-bold btn-revisar-mapa">Revisar Mapa</button>
                </div>`;
            tarjeta.querySelector('.btn-revisar-mapa').addEventListener('click', () => { cargarAlumnoDesdeAlerta(alerta.matricula, alerta.nombre, alerta.resaltar !== undefined ? alerta.resaltar : alerta.materia); });
            contenedor.appendChild(tarjeta);
        });
    }).catch(error => {
        console.error('Error cargando alertas:', error);
        contenedor.innerHTML = `<div class="alert alert-danger text-center mt-2 small">${escapeHTML(error.message)}</div>`;
    });
}

function cargarAlumnoDesdeAlerta(matricula, nombre, materia) {
    if (!puedeCambiarDeContexto()) return;
    if ($('inputMatricula')) $('inputMatricula').value = matricula;
    if ($('inputNombreAlumno')) $('inputNombreAlumno').value = nombre;
    buscarAlumno(materia);
    const offcanvasEl = $('offcanvasAlertas');
    if (offcanvasEl) { const offcanvasInstance = bootstrap.Offcanvas.getInstance(offcanvasEl) || new bootstrap.Offcanvas(offcanvasEl); if (offcanvasInstance) offcanvasInstance.hide(); }
}

function abrirReporteGlobal() {
    if (modalReporteGlobalInstance) modalReporteGlobalInstance.show();
    cargarDatosReporteGlobal();
}

function cargarDatosReporteGlobal() {
    let rRiesgo = $('contenedorReporteRiesgo'), rRezago = $('contenedorReporteRezago'), rSeriada = $('contenedorReporteSeriadas');
    if (rRiesgo) rRiesgo.innerHTML = '<div class="text-center py-3"><div class="spinner-border text-warning" role="status"></div></div>';
    if (rRezago) rRezago.innerHTML = '<div class="text-center py-3"><div class="spinner-border text-secondary" role="status"></div></div>';
    if (rSeriada) rSeriada.innerHTML = '<div class="text-center py-3"><div class="spinner-border text-danger" role="status"></div></div>';

    cargarMateriasCriticas();

    const selPeriodoReporte = $('selectPeriodoGlobal');
    fetch(`/Home/ObtenerReporteGlobal?periodo=${encodeURIComponent(selPeriodoReporte ? selPeriodoReporte.value : '')}`).then(leerRespuesta).then(res => {
        let htmlRiesgo = renderizarListaAgrupada(res.data.riesgos, "warning"); if (rRiesgo) rRiesgo.innerHTML = htmlRiesgo || '<p class="text-muted small">No hay alumnos en riesgo.</p>';
        let htmlRezago = renderizarListaAgrupada(res.data.rezagos, "secondary"); if (rRezago) rRezago.innerHTML = htmlRezago || '<p class="text-muted small">No hay alumnos en rezago.</p>';
        let htmlSeriadas = renderizarListaAgrupada(res.data.seriadas, "danger"); if (rSeriada) rSeriada.innerHTML = htmlSeriadas || '<p class="text-muted small">No hay adeudos de materias seriadas.</p>';
    }).catch(error => {
        // Sin esto los contenedores se quedaban con el indicador de carga para siempre
        console.error('Error cargando reporte global:', error);
        const msg = `<p class="text-danger small">${escapeHTML(error.message)}</p>`;
        [rRiesgo, rRezago, rSeriada].forEach(c => { if (c) c.innerHTML = msg; });
    });
}

// Materias con mayor reprobación entre los alumnos registrados (ReglasMateriasCriticas.cs)
function cargarMateriasCriticas() {
    const c = $('contenedorReporteCriticas');
    if (!c) return;
    c.innerHTML = '<div class="text-center py-3"><div class="spinner-border text-dark" role="status"></div></div>';
    const selP = $('selectPeriodoGlobal');
    fetch(`/Home/ObtenerMateriasCriticas?periodo=${encodeURIComponent(selP ? selP.value : '')}`).then(leerRespuesta).then(res => {
        const criterio = $('criterioMateriasCriticas');
        if (criterio) criterio.innerText = `Crítica: ${res.porcentajeCritico} % o más de reprobación con al menos ${res.minimoIntentos} intentos registrados`;
        const lista = res.data || [];
        if (lista.length === 0) { c.innerHTML = '<p class="text-muted small mb-0">No hay materias reprobadas ni en riesgo registradas.</p>'; return; }

        const nombres = alumnos => alumnos.map(a => escapeHTML(a.nombre || a.matricula)).join(', ');
        const quienes = m => [
            m.reprobaron.length ? `<li><strong>Reprobaron:</strong> ${nombres(m.reprobaron)}</li>` : '',
            m.cursando.length ? `<li><strong>La cursan ahora:</strong> ${nombres(m.cursando)}</li>` : '',
            m.enRiesgo.length ? `<li class="text-danger"><strong>En riesgo ahora:</strong> ${nombres(m.enRiesgo)}</li>` : ''
        ].join('');
        // Lo que se muestra en la etiqueta: el % solo cuando hay casos suficientes para que signifique algo
        const etiqueta = m => m.critica ? `${m.porcentaje} % reprueba`
            : m.motivo === 'sin-reprobaciones' ? 'Sin reprobaciones'
            : `${m.reprobaciones} de ${m.intentos} reprobó`;
        const explicacion = m => m.critica ? `Reprobaron ${m.reprobaciones} de ${m.intentos} intentos registrados.`
            : m.motivo === 'pocos-casos' ? `Aún son pocos casos para saber si es crítica (se necesitan al menos ${res.minimoIntentos} intentos con resultado).`
            : m.motivo === 'sin-reprobaciones' ? 'Nadie la ha reprobado; aparece porque hay alumnos en riesgo en ella.'
            : `Reprobación de ${m.porcentaje} %, por debajo del ${res.porcentajeCritico} %.`;
        // Misma estructura que las otras secciones (tarjeta > encabezado + lista) para que se imprima igual
        const tarjeta = m => `
            <div class="border rounded bg-white p-2 ${m.critica ? 'border-danger' : ''}">
                <div class="d-flex justify-content-between align-items-center mb-1 gap-2">
                    <strong class="text-dark" style="font-size: 0.85rem;">${m.critica ? '<i class="bi bi-fire text-danger me-1"></i>' : ''}${escapeHTML(m.materia)}</strong>
                    <span class="badge ${m.critica ? 'bg-danger' : 'bg-light text-dark border'} rounded-pill text-nowrap">${etiqueta(m)}</span>
                </div>
                <ul class="list-unstyled mb-0 ms-2 text-muted" style="font-size: 0.8rem;">
                    <li class="fst-italic">${explicacion(m)}</li>
                    ${quienes(m)}
                </ul>
            </div>`;

        const criticas = lista.filter(m => m.critica), observacion = lista.filter(m => !m.critica);
        c.innerHTML =
            `<h6 class="fw-bold text-danger mb-0">Materias críticas (${criticas.length})</h6>`
            + (criticas.length ? criticas.map(tarjeta).join('') : '<p class="text-muted small mb-0">Ninguna materia cumple el criterio todavía.</p>')
            + (observacion.length ? `<h6 class="fw-bold text-secondary mb-0 mt-2">En observación (${observacion.length})</h6>` + observacion.map(tarjeta).join('') : '');
    }).catch(e => { c.innerHTML = `<p class="text-danger small">${escapeHTML(e.message)}</p>`; });
}

function renderizarListaAgrupada(arreglo, colorClase) {
    if (!arreglo || arreglo.length === 0) return '';
    let html = '';
    arreglo.forEach(item => {
        let alumnos = Array.from(item.alumnos);
        let listaAlumnosHtml = alumnos.map(a => `<li class="small">${escapeHTML(a.nombre)} (${escapeHTML(a.matricula)})</li>`).join('');
        html += `<div class="border rounded mb-2 bg-white p-2 border-${colorClase}">
                <div class="d-flex justify-content-between align-items-center mb-1"><strong class="text-dark" style="font-size: 0.85rem;">${escapeHTML(item.materia)}</strong><span class="badge bg-${colorClase} rounded-pill">${alumnos.length}</span></div>
                <ul class="list-unstyled mb-0 ms-2 text-muted" style="font-size: 0.8rem;">${listaAlumnosHtml}</ul>
            </div>`;
    });
    return html;
}

// Imprime solo el reporte (no la página completa) en un iframe oculto con un formato limpio para papel
function imprimirReporteGlobal() {
    const secciones = [
        { titulo: 'Materias en Riesgo', color: '#ffc107', id: 'contenedorReporteRiesgo' },
        { titulo: 'Materias en Rezago', color: '#6c757d', id: 'contenedorReporteRezago' },
        { titulo: 'Seriadas Adeudadas (Candados)', color: '#dc3545', id: 'contenedorReporteSeriadas' },
        { titulo: 'Reprobación por materia', color: '#212529', id: 'contenedorReporteCriticas' }
    ];

    if (document.querySelector('#modalReporteGlobal .spinner-border')) { avisar("El reporte aún se está cargando. Intenta de nuevo en un momento."); return; }

    const fecha = new Date().toLocaleDateString('es-MX', { year: 'numeric', month: 'long', day: 'numeric' });
    const selP = $('selectPeriodoGlobal');
    const periodo = selP ? selP.value : '';

    let cuerpo = '';
    secciones.forEach(s => {
        const el = $(s.id);
        cuerpo += `<section><h2 style="border-left-color:${s.color}">${s.titulo}</h2>${el ? el.innerHTML : ''}</section>`;
    });

    const html = `<!DOCTYPE html><html lang="es"><head><meta charset="utf-8"><title>Reporte Global de Trayectorias</title>
        <style>
            @page { size: letter; margin: 1.5cm; }
            body { font-family: "Segoe UI", Arial, sans-serif; color: #212529; font-size: 11pt; margin: 0; }
            header { border-bottom: 2px solid #6f42c1; padding-bottom: 6px; margin-bottom: 14px; }
            header h1 { font-size: 16pt; margin: 0; color: #6f42c1; }
            header p { margin: 2px 0 0; font-size: 9pt; color: #555; }
            section { margin-bottom: 16px; }
            h2 { font-size: 12pt; margin: 0 0 8px; padding: 3px 8px; border-left: 6px solid; background: #f3f3f3; }
            section > div { border: 1px solid #ccc; border-radius: 4px; padding: 6px 10px; margin-bottom: 6px; page-break-inside: avoid; break-inside: avoid; }
            section > div > div { display: flex; justify-content: space-between; align-items: center; }
            .badge { border: 1px solid #555; border-radius: 10px; padding: 0 8px; font-size: 9pt; font-weight: bold; }
            ul { margin: 4px 0 0 16px; padding: 0; columns: 2; column-gap: 24px; }
            li { font-size: 9.5pt; list-style: disc; }
            p { font-size: 10pt; color: #666; font-style: italic; margin: 0; }
        </style></head><body>
        <header><h1>Reporte Global de Trayectorias</h1><p>Periodo de trabajo: ${escapeHTML(periodo)} · Impreso el ${fecha}</p></header>
        ${cuerpo}</body></html>`;

    let frame = $('frameImpresionReporte');
    if (frame) frame.remove();
    frame = document.createElement('iframe');
    frame.id = 'frameImpresionReporte';
    frame.style.cssText = 'position:fixed; width:0; height:0; border:0; visibility:hidden;';
    document.body.appendChild(frame);

    const doc = frame.contentWindow.document;
    doc.open(); doc.write(html); doc.close();
    setTimeout(() => { frame.contentWindow.focus(); frame.contentWindow.print(); }, 200);
}
