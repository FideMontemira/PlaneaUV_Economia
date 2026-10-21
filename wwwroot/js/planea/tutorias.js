// PlaneaUV · Tutorías: sesiones del periodo y exportación a Word.
// Se carga como script clásico (ver Views/Home/Index.cshtml): comparte el ámbito global con los demás archivos de esta carpeta.

let modalTutoriasInstance = null;

let tutoriasGlobal = [];

function abrirModalTutorias() {
    let inputMat = $('inputMatricula');
    if (!inputMat || !inputMat.value.trim()) {
        alert("Selecciona un alumno primero.");
        return;
    }
    cargarTutoriasALaVista();
    if (modalTutoriasInstance) modalTutoriasInstance.show();
}

function guardarTutoriasEnMemoria() {
    let selP = $('selectPeriodoGlobal');
    let periodoActual = selP ? selP.value : '';
    tutoriasGlobal = tutoriasGlobal.filter(t => t.Periodo !== periodoActual);

    for (let n = 1; n <= 3; n++) {
        tutoriasGlobal.push({
            Periodo: periodoActual,
            Sesion: n,
            Fecha: $('fechaTutoria' + n) ? $('fechaTutoria' + n).value : '',
            Asistencia: $('asistenciaTutoria' + n) ? $('asistenciaTutoria' + n).value : 'pendiente',
            Comentarios: $('tutoria' + n) ? $('tutoria' + n).value : ''
        });
    }
}

function cargarTutoriasALaVista() {
    let selectP = $('selectPeriodoGlobal');
    let periodoActual = selectP ? selectP.value : '';
    let tuts = tutoriasGlobal.filter(t => t.Periodo === periodoActual);

    for (let n = 1; n <= 3; n++) {
        let t = tuts.find(x => x.Sesion === n);
        if ($('fechaTutoria' + n)) $('fechaTutoria' + n).value = t ? t.Fecha : '';
        if ($('asistenciaTutoria' + n)) $('asistenciaTutoria' + n).value = t ? t.Asistencia : 'pendiente';
        if ($('tutoria' + n)) $('tutoria' + n).value = t ? t.Comentarios : '';
    }
}

function descargarWordTutorias() {
    let inputMat = $('inputMatricula');
    var matricula = inputMat ? inputMat.value.trim() : '';
    if (!matricula) { alert("Selecciona un alumno primero."); return; }
    if (hayGuardadosPendientes()) { alert("Se están guardando los últimos cambios. Intenta de nuevo en un momento."); return; }
    window.location.href = `/Home/DescargarWordTutorias?matricula=${encodeURIComponent(matricula)}`;
}
