// PlaneaUV · Indicadores: avance en créditos, contadores, gráficas por área y bloqueo por prerrequisitos.
// Se carga como script clásico (ver Views/Home/Index.cshtml): comparte el ámbito global con los demás archivos de esta carpeta.

// ===================== GRÁFICAS POR ÁREA =====================
// Una gráfica circular por cada tipo de materia del plan, igual a la de créditos pero por área.
// El área de cada tarjeta es su clase de color (sale de las columnas Tipo y Area de la plantilla).
const AREAS_DEL_PLAN = [
    // Mismos tonos que las tarjetas del mapa (wwwroot/css/tema-uv.css)
    { clase: 'bg-general', nombre: 'General', color: '#8a97a8' },
    { clase: 'bg-basica', nombre: 'Básica', color: '#e0a800' },
    { clase: 'bg-disciplinar', nombre: 'Disciplinar', color: '#28ad56' },
    { clase: 'bg-terminal', nombre: 'Terminal', color: '#d9534f' },
    { clase: 'bg-optativa', nombre: 'Optativas', color: '#2a6cc0' },
    { clase: 'bg-afel', nombre: 'AFEL', color: '#17a589' }
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
    pintarTrayectoria(creditosAprobados);
    let grAv = $('graficaAvance'); if (grAv) grAv.style.background = `conic-gradient(#28ad56 ${porcentaje}%, #e6ebf2 ${porcentaje}%)`;
    let txRiesgo = $('textoRiesgo'); if (txRiesgo) txRiesgo.innerText = materiasEnRiesgo;
    let txCurso = $('textoCurso'); if (txCurso) txCurso.innerText = materiasEnCurso;

    // Rezago: automático (periodos anteriores al que cursa) o marcado a mano, sin contar lo ya aprobado
    let enRezago = [...document.querySelectorAll('.materia-card')].filter(t => t.getAttribute('data-estado') !== 'aprobada' && (t.hasAttribute('data-rezago-auto') || t.getAttribute('data-rezago') === 'true')).length;
    let txRezago = $('textoRezago'); if (txRezago) txRezago.innerText = enRezago;

    dibujarGraficasPorArea();

    // Bloqueos: prerrequisitos sin aprobar y porcentaje de créditos sin cubrir (lo segundo, misma regla que el servidor:
    // 70 % de 369 = 258.3, se piden 259). Una materia que ya tiene registro no se bloquea por créditos.
    document.querySelectorAll('.materia-card[data-prerequisito], .materia-card[data-creditos-minimos]').forEach(tarjeta => {
        const faltan = [];
        (tarjeta.getAttribute('data-prerequisito') || '').split(',').map(p => p.trim()).filter(p => p).forEach(prereq => {
            const tPrereq = Array.from(document.querySelectorAll('.materia-card')).find(x => x.getAttribute('data-nombre') === prereq || x.getAttribute('data-nombre-personalizado') === prereq);
            if (tPrereq && tPrereq.getAttribute('data-estado') !== 'aprobada') faltan.push(prereq);
        });

        let motivoCreditos = '';
        const minimo = parseInt(tarjeta.getAttribute('data-creditos-minimos')) || 0;
        const yaRegistrada = !!tarjeta.getAttribute('data-estado');
        if (minimo > 0 && total > 0 && !yaRegistrada) {
            const necesarios = Math.ceil(total * minimo / 100 - 1e-9);
            if (creditosAprobados < necesarios)
                motivoCreditos = `Requiere el ${minimo} % de los créditos (${necesarios} de ${total}). Lleva ${creditosAprobados}: le faltan ${necesarios - creditosAprobados}.`;
        }

        const sPrereq = tarjeta.querySelector('.prerequisito-text:not(.requisito-creditos-text)');
        const sCreditos = tarjeta.querySelector('.requisito-creditos-text');
        if (sPrereq) sPrereq.style.display = faltan.length > 0 ? 'block' : 'none';
        if (sCreditos) sCreditos.style.display = motivoCreditos ? 'block' : 'none';

        const motivos = [];
        if (faltan.length > 0) motivos.push(`Debes aprobar primero: "${faltan.join('", "')}".`);
        if (motivoCreditos) motivos.push(motivoCreditos);
        if (motivos.length > 0) {
            tarjeta.classList.add('estado-bloqueada');
            tarjeta.setAttribute('data-motivo-bloqueo', motivos.join(' '));
        } else {
            tarjeta.classList.remove('estado-bloqueada');
            tarjeta.removeAttribute('data-motivo-bloqueo');
        }
    });
}
