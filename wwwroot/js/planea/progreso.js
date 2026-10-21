// PlaneaUV · Indicadores: avance en créditos, contadores, gráficas por área y bloqueo por prerrequisitos.
// Se carga como script clásico (ver Views/Home/Index.cshtml): comparte el ámbito global con los demás archivos de esta carpeta.

// ===================== GRÁFICAS POR ÁREA =====================
// Una gráfica circular por cada tipo de materia del plan, igual a la de créditos pero por área.
// El área de cada tarjeta es su clase de color (sale de las columnas Tipo y Area de la plantilla).
const AREAS_DEL_PLAN = [
    { clase: 'bg-general', nombre: 'General', color: '#6c757d' },
    { clase: 'bg-basica', nombre: 'Básica', color: '#f0ad00' },
    { clase: 'bg-disciplinar', nombre: 'Disciplinar', color: '#28a745' },
    { clase: 'bg-terminal', nombre: 'Terminal', color: '#dc3545' },
    { clase: 'bg-optativa', nombre: 'Optativas', color: '#0d6efd' },
    { clase: 'bg-afel', nombre: 'AFEL', color: '#20c997' }
];

function dibujarGraficasPorArea() {
    const contenedor = $('graficasAreas');
    if (!contenedor) return;

    const tarjetas = [...document.querySelectorAll('#contenedor-mapa-dinamico .materia-card')];
    if (tarjetas.length === 0) {
        contenedor.innerHTML = '<span class="text-muted small">Selecciona un alumno para ver su avance por área.</span>';
        return;
    }

    let html = '';
    AREAS_DEL_PLAN.forEach(area => {
        const delArea = tarjetas.filter(t => t.classList.contains(area.clase));
        if (delArea.length === 0) return;   // el plan no tiene materias de esta área

        const creditos = t => parseInt(t.getAttribute('data-creditos')) || 0;
        const aprobadas = delArea.filter(t => t.getAttribute('data-estado') === 'aprobada');
        const total = delArea.reduce((s, t) => s + creditos(t), 0);
        const logrado = aprobadas.reduce((s, t) => s + creditos(t), 0);
        const pct = total > 0 ? Math.round((logrado / total) * 100) : 0;

        html += `<div class="area-grafica" title="${area.nombre}: ${aprobadas.length} de ${delArea.length} materias aprobadas">
                <div class="area-circulo" style="background: conic-gradient(${area.color} ${pct}%, #e9ecef ${pct}%);">
                    <div class="area-circulo-interior">${pct}%</div>
                </div>
                <div class="area-nombre" style="color: ${area.color};">${area.nombre}</div>
                <div class="area-detalle">${logrado} / ${total} Cr.</div>
            </div>`;
    });
    contenedor.innerHTML = html;
}

function actualizarProgreso() {
    aplicarSituacionAlumno();   // periodo que cursa y rezago, tal como los calculó el servidor

    let creditosAprobados = 0, materiasEnRiesgo = 0, materiasEnCurso = 0;

    document.querySelectorAll('.materia-card').forEach(t => {
        const estado = t.getAttribute('data-estado');
        if (estado === 'aprobada') creditosAprobados += parseInt(t.getAttribute('data-creditos')) || 0;
        else if (estado === 'cursando') materiasEnCurso++;

        if (t.getAttribute('data-riesgo') === 'true') materiasEnRiesgo++;
    });

    // El total sale del plan del alumno; sin plan cargado no hay total contra el cual calcular
    let hayMapa = !!document.querySelector('#contenedor-mapa-dinamico .materia-card');
    let total = hayMapa ? creditosTotalesPlan : 0;
    let porcentaje = total > 0 ? Math.min(100, Math.round((creditosAprobados / total) * 100)) : 0;

    let txtPorc = $('textoPorcentaje'); if (txtPorc) txtPorc.innerText = porcentaje + '%';
    let txtCred = $('textoCreditos'); if (txtCred) txtCred.innerText = total > 0 ? `${creditosAprobados} / ${total} Cr.` : `${creditosAprobados} Cr.`;
    let grAv = $('graficaAvance'); if (grAv) grAv.style.background = `conic-gradient(#28a745 ${porcentaje}%, #e9ecef ${porcentaje}%)`;
    let txRiesgo = $('textoRiesgo'); if (txRiesgo) txRiesgo.innerText = materiasEnRiesgo;
    let txCurso = $('textoCurso'); if (txCurso) txCurso.innerText = materiasEnCurso;

    // Rezago: automático (periodos anteriores al que cursa) o marcado a mano, sin contar lo ya aprobado
    let enRezago = [...document.querySelectorAll('.materia-card')].filter(t => t.getAttribute('data-estado') !== 'aprobada' && (t.hasAttribute('data-rezago-auto') || t.getAttribute('data-rezago') === 'true')).length;
    let txRezago = $('textoRezago'); if (txRezago) txRezago.innerText = enRezago;

    dibujarGraficasPorArea();

    document.querySelectorAll('.materia-card[data-prerequisito]').forEach(tarjeta => {
        let todasAprobadas = true;
        tarjeta.getAttribute('data-prerequisito').split(',').map(p => p.trim()).forEach(prereq => {
            const tPrereq = Array.from(document.querySelectorAll('.materia-card')).find(x => x.getAttribute('data-nombre') === prereq || x.getAttribute('data-nombre-personalizado') === prereq);
            if (tPrereq && tPrereq.getAttribute('data-estado') !== 'aprobada') todasAprobadas = false;
        });

        const s = tarjeta.querySelector('.prerequisito-text');
        if (todasAprobadas) {
            if (s) s.style.display = 'none';
            tarjeta.classList.remove('estado-bloqueada');
        } else {
            if (s) s.style.display = 'block';
            tarjeta.classList.add('estado-bloqueada');
        }
    });
}
