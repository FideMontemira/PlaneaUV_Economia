// SITRA-UV · Semáforo de riesgo: colores, textos y la "pastilla" que lo muestra.
// Lo usan el mapa del alumno, el panel del grupo y la pantalla de tutoría.
// El color lo calcula SOLO el servidor (Services/ReglasSemaforo.cs); aquí solo se dibuja.

const SEMAFORO = {
    rojo: { texto: 'Riesgo alto', clase: 'semaforo-rojo' },
    amarillo: { texto: 'Atención', clase: 'semaforo-amarillo' },
    verde: { texto: 'Sin riesgo', clase: 'semaforo-verde' },
    sin: { texto: 'Sin semáforo', clase: 'semaforo-sin' }
};
const infoSemaforo = color => SEMAFORO[color] || SEMAFORO.sin;

function pastillaSemaforo(color) {
    const i = infoSemaforo(color);
    return `<span class="semaforo-pastilla ${i.clase}"><span class="semaforo-punto"></span>${i.texto}</span>`;
}

const TEXTO_VALORACION = { confirma: 'confirmó el semáforo', eleva: 'indicó que el riesgo es mayor', descarta: 'descartó el riesgo' };

// Dirección del reporte de seguimiento: un periodo o el historial de todos los periodos
function urlReportePeriodo(matricula, periodo, historial) {
    return `/Home/ReportePeriodo?matricula=${encodeURIComponent(matricula)}&periodo=${encodeURIComponent(periodo || '')}${historial ? '&historial=true' : ''}`;
}

// Abre una página en otra pestaña en el mismo clic (si no, el navegador puede bloquearla) y la carga cuando
// termine lo que haya que esperar (p. ej. el guardado en curso). Si el navegador la bloquea, se abre aquí mismo.
function abrirEnPestana(url, esperar) {
    const pestana = window.open('', '_blank');
    if (!pestana) { (esperar || Promise.resolve()).then(() => { window.location.href = url; }); return; }
    pestana.document.write('<p style="font-family:sans-serif;color:#555;padding:20px">Preparando el reporte...</p>');
    (esperar || Promise.resolve()).then(() => { pestana.location.href = url; });
}

// Dirección de la pantalla de tutoría de un alumno en un periodo
function urlTutoria(matricula, periodo) {
    return `/Home/Tutoria?matricula=${encodeURIComponent(matricula)}&periodo=${encodeURIComponent(periodo || '')}`;
}
