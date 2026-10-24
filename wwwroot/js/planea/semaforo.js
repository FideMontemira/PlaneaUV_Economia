// PlaneaUV · Semáforo de riesgo: colores, textos y la "pastilla" que lo muestra.
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

// Dirección de la pantalla de tutoría de un alumno en un periodo
function urlTutoria(matricula, periodo) {
    return `/Home/Tutoria?matricula=${encodeURIComponent(matricula)}&periodo=${encodeURIComponent(periodo || '')}`;
}
