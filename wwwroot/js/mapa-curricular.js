function escapeHTML(str) {
    if (str === null || str === undefined) return '';
    return String(str).replace(/[&<>'"]/g, tag => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;'
    }[tag]));
}

let tarjetaActual = null;
let modalBootstrapInstance = null;
let modalInicioInstance = null;
let modalTutoriasInstance = null;
let modalReporteGlobalInstance = null;
let modalGestorPlanesInstance = null;
let planActualAlumno = "";
let materiasPlanMaestro = [];
let planDibujadoActual = "";
let huboCambios = false;

let tutoriasGlobal = [];

// Datos del plan de estudios que está dibujado. Todo sale de la plantilla Excel del plan (nada fijo en el código):
// - listaOptativas: catálogo de optativas (hoja "Optativas"); sirve para diferenciar Optativa de AFEL al recrear historial
// - creditosTotalesPlan: total para el porcentaje de avance (hoja "Datos" o, si no viene, la suma de las materias)
// - programaPlan: nombre del programa para el título
let listaOptativas = [];
let creditosTotalesPlan = 0;
let programaPlan = "";

// Aplica a la pantalla los datos del plan recién cargado (título, total de créditos y catálogo de optativas)
function aplicarDatosDelPlan(res, planNombre) {
    const optativas = res.optativas || [];
    listaOptativas = optativas.map(o => o.nombre || o.Nombre).filter(n => n);
    programaPlan = res.programa || planNombre || "";
    creditosTotalesPlan = parseInt(res.creditosTotales) || 0;

    const titulo = $('tituloMapa');
    if (titulo) titulo.innerText = programaPlan ? 'Mapa Curricular - ' + programaPlan : 'Mapa Curricular';

    const sel = $('inputOptativa');
    if (!sel) return;
    sel.innerHTML = '<option value="">-- Seleccione la experiencia educativa --</option>';
    const grupos = {};
    optativas.forEach(o => {
        const nombre = o.nombre || o.Nombre, grupo = o.grupo || o.Grupo || '';
        if (!nombre) return;
        if (!grupo) { sel.add(new Option(nombre, nombre)); return; }
        if (!grupos[grupo]) { grupos[grupo] = document.createElement('optgroup'); grupos[grupo].label = grupo; sel.appendChild(grupos[grupo]); }
        grupos[grupo].appendChild(new Option(nombre, nombre));
    });
}


function selectAEstado(v) {
    return v === 'si' ? 'aprobada' : v === 'no' ? 'reprobada' : v;
}
function estadoASelect(e) {
    return e === 'aprobada' ? 'si' : e === 'reprobada' ? 'no' : e;
}

function $(id) {
    return document.getElementById(id);
}

// ===================== PERIODOS ESCOLARES =====================
// Formato único en todo el sistema: "Agosto - Enero AAAA" y "Febrero - Julio AAAA", donde AAAA es el año
// en que TERMINA el periodo ("Agosto - Enero 2027" va de agosto 2026 a enero 2027).

// Periodo que corresponde a una fecha (por defecto, hoy)
function periodoDeFecha(fecha = new Date()) {
    const mes = fecha.getMonth(), anio = fecha.getFullYear();   // enero = 0
    if (mes >= 7) return `Agosto - Enero ${anio + 1}`;          // agosto a diciembre
    if (mes === 0) return `Agosto - Enero ${anio}`;             // enero
    return `Febrero - Julio ${anio}`;                           // febrero a julio
}

// Llena el selector de Periodo de Trabajo: 8 años hacia atrás, el actual seleccionado y 2 años hacia adelante.
// Antes eran 4 opciones fijas en el HTML que se acababan en 2027.
function llenarSelectorDePeriodos() {
    const sel = $('selectPeriodoGlobal');
    if (!sel) return;
    const anioActual = new Date().getFullYear();
    sel.innerHTML = '';
    for (let anio = anioActual - 8; anio <= anioActual + 2; anio++) {
        sel.add(new Option(`Agosto - Enero ${anio}`, `Agosto - Enero ${anio}`));
        sel.add(new Option(`Febrero - Julio ${anio}`, `Febrero - Julio ${anio}`));
    }
    sel.value = periodoDeFecha();
}

document.addEventListener('DOMContentLoaded', function () {
    try {
        // Debe ir primero: alertas, tutorías y periodo del alumno leen el Periodo de Trabajo
        llenarSelectorDePeriodos();

        const modalInicioEl = $('modalInicio');
        if (modalInicioEl) {
            modalInicioInstance = new bootstrap.Modal(modalInicioEl);
            setTimeout(() => { modalInicioInstance.show(); }, 150);
        }

        const modalTutEl = $('modalTutorias');
        if (modalTutEl) modalTutoriasInstance = new bootstrap.Modal(modalTutEl);

        const modalReporteEl = $('modalReporteGlobal');
        if (modalReporteEl) modalReporteGlobalInstance = new bootstrap.Modal(modalReporteEl);

        cargarListaAlumnosInicio();
        cargarListaAlumnos();
        cargarAlertas();
        cargarPlanesDropdown();

        const modalPlanesEl = $('modalGestorPlanes');
        if (modalPlanesEl) modalGestorPlanesInstance = new bootstrap.Modal(modalPlanesEl);

        const modalEl = $('modalAvance');
        if (modalEl) modalBootstrapInstance = new bootstrap.Modal(modalEl);

        const afelesList = document.querySelectorAll('.bg-afel');
        if (afelesList.length > 0) {
            const lastAfel = afelesList[afelesList.length - 1];
            const contenedor = lastAfel.parentNode;
            contenedor.id = 'contenedor-afeles';

            const btnContainer = document.createElement('div');
            btnContainer.className = 'text-center mt-3';
            btnContainer.innerHTML = '<button type="button" class="btn btn-sm btn-outline-success fw-bold w-100 shadow-sm" onclick="agregarAfelExtra()">+ Agregar AFEL</button>';
            contenedor.appendChild(btnContainer);
        }

        document.querySelectorAll('.materia-card').forEach(tarjeta => {
            tarjeta.addEventListener('click', manejadorClickTarjeta);
        });

        actualizarProgreso();

        const selInsc = $('selectInscripcion');
        if (selInsc) {
            selInsc.addEventListener('change', function () {
                actualizarOpcionesExamen(this.value);
                $('selectAprobacion').value = 'ninguno';
                evaluarLogicaInscripcion();
            });
        }

        const selExamen = $('selectExamen');
        if (selExamen) selExamen.addEventListener('change', evaluarLogicaInscripcion);

        const selAprob = $('selectAprobacion');
        if (selAprob) selAprob.addEventListener('change', evaluarLogicaInscripcion);

        const selPeriodo = $('selectPeriodoGlobal');
        if (selPeriodo) {
            // Cambiar de periodo solo cambia lo que se ve; no modifica datos, así que no se guarda
            selPeriodo.addEventListener('change', () => {
                cargarTutoriasALaVista();
                actualizarPeriodoAlumno();
                cargarAlertas();   // las alertas dependen del periodo que cursa cada alumno
            });
        }

    } catch (err) {
        console.error("Error iniciando script:", err);
    }
});

function agregarAfelExtra(nombrePers = "", creditosPers = 0) {
    const contenedor = $('contenedor-afeles');
    if (!contenedor) return null;

    // Siguiente número libre (si se quitó uno de en medio, no se repite un nombre que ya existe)
    let numAfel = document.querySelectorAll('.bg-afel').length + 1;
    while (document.querySelector(`.materia-card[data-nombre="AFEL ${numAfel}"]`)) numAfel++;
    let nombreBase = "AFEL " + numAfel;
    let crOtorga = creditosPers > 0 ? creditosPers : 6;

    let nuevaTarjeta = document.createElement('div');
    nuevaTarjeta.className = 'materia-card bg-afel afel-extra shadow-sm mb-2';
    nuevaTarjeta.setAttribute('data-nombre', nombreBase);
    nuevaTarjeta.setAttribute('data-creditos', crOtorga);

    if (nombrePers) nuevaTarjeta.setAttribute('data-nombre-personalizado', nombrePers);

    nuevaTarjeta.innerHTML = `<strong>${escapeHTML(nombrePers || nombreBase)}</strong><br/>(${crOtorga} Cr.)` + BOTON_QUITAR_AFEL;
    nuevaTarjeta.addEventListener("click", manejadorClickTarjeta);

    let btnContainer = contenedor.querySelector('.btn-outline-success').parentNode;
    contenedor.insertBefore(nuevaTarjeta, btnContainer);

    return nuevaTarjeta;
}

// Botón "×" de las AFEL agregadas con "+ Agregar AFEL" (las del plan no lo llevan: son lugares del plan)
const BOTON_QUITAR_AFEL = '<button type="button" class="btn-quitar-afel" title="Quitar este AFEL" aria-label="Quitar este AFEL" onclick="quitarAfelExtra(event, this)"><i class="bi bi-x-lg"></i></button>';

function quitarAfelExtra(e, boton) {
    e.stopPropagation();   // que no abra la ventana de la materia
    const tarjeta = boton.closest('.materia-card');
    if (!tarjeta) return;

    const historial = JSON.parse(tarjeta.getAttribute('data-historial') || '[]');
    const nombre = tarjeta.getAttribute('data-nombre-personalizado') || tarjeta.getAttribute('data-nombre');
    if (historial.length > 0 && !confirm(`"${nombre}" tiene ${historial.length} registro(s). Si lo quitas se borrarán. ¿Continuar?`)) return;

    tarjeta.remove();
    actualizarProgreso();
    marcarCambios();   // se guarda al momento (si no tenía registros no cambia nada en el Excel)
}

function manejadorClickTarjeta(e) {
    if (this.classList.contains('estado-bloqueada')) {
        alert(`Materia bloqueada. Debes aprobar primero: "${this.getAttribute('data-prerequisito')}".`);
        return;
    }

    tarjetaActual = this;
    $('modalMateriaNombre').innerText = this.getAttribute('data-nombre');
    $('modalMateriaCreditos').innerText = this.getAttribute('data-creditos') + ' Cr.';

    const divOptativa = $('divOptativa');
    const inputOptativa = $('inputOptativa');
    const divAfel = $('divAfel');
    const inputNombreAfel = $('inputNombreAfel');
    const inputCreditosAfel = $('inputCreditosAfel');

    if (this.classList.contains('bg-optativa')) {
        // Si el plan no trae catálogo de optativas no hay nada que elegir: la tarjeta conserva su nombre
        if (divOptativa) divOptativa.classList.toggle('d-none', listaOptativas.length === 0);
        if (divAfel) divAfel.classList.add('d-none');
        if (inputOptativa) inputOptativa.value = this.getAttribute('data-nombre-personalizado') || '';
    } else if (this.classList.contains('bg-afel')) {
        if (divOptativa) divOptativa.classList.add('d-none');
        if (divAfel) divAfel.classList.remove('d-none');
        if (inputNombreAfel) inputNombreAfel.value = this.getAttribute('data-nombre-personalizado') || '';
        if (inputCreditosAfel) inputCreditosAfel.value = this.getAttribute('data-creditos') || '6';
    } else {
        if (divOptativa) divOptativa.classList.add('d-none');
        if (divAfel) divAfel.classList.add('d-none');
    }

    let historialJSON = this.getAttribute('data-historial');
    let historialArray = historialJSON ? JSON.parse(historialJSON) : [];
    let periodoSelect = $('selectPeriodoGlobal');
    let periodoActual = periodoSelect ? periodoSelect.value : '';

    let intentoActual = historialArray.find(x => x.Periodo === periodoActual);
    let ultimoIntento = historialArray.length > 0 ? historialArray[historialArray.length - 1] : null;
    let intentoAprobado = historialArray.find(x => x.Estado === 'aprobada');

    if ($('selectInscripcion')) $('selectInscripcion').disabled = false;
    if ($('selectExamen')) $('selectExamen').disabled = false;
    if ($('selectAprobacion')) $('selectAprobacion').disabled = false;
    if ($('checkRiesgo')) $('checkRiesgo').disabled = false;
    if ($('checkRezago')) $('checkRezago').disabled = false;

    let btnGuardar = document.querySelector('#modalAvance .modal-footer .btn-primary');
    if (btnGuardar) btnGuardar.style.display = 'block';

    if (intentoAprobado && intentoAprobado.Periodo !== periodoActual) {
        let modalPerVinc = $('modalPeriodoVinculado');
        if (modalPerVinc) modalPerVinc.innerHTML = `<span class="badge bg-success">Aprobada en: ${escapeHTML(intentoAprobado.Periodo)}</span>`;

        if ($('selectInscripcion')) $('selectInscripcion').value = intentoAprobado.TipoInscripcion;
        actualizarOpcionesExamen(intentoAprobado.TipoInscripcion);

        if ($('selectExamen')) $('selectExamen').value = intentoAprobado.UltimoExamen;
        if ($('selectAprobacion')) $('selectAprobacion').value = 'si';
        if ($('checkRiesgo')) $('checkRiesgo').checked = intentoAprobado.EnRiesgo;
        if ($('checkRezago')) $('checkRezago').checked = intentoAprobado.EnRezago;

        if ($('selectInscripcion')) $('selectInscripcion').disabled = true;
        if ($('selectExamen')) $('selectExamen').disabled = true;
        if ($('selectAprobacion')) $('selectAprobacion').disabled = true;
        if ($('checkRiesgo')) $('checkRiesgo').disabled = true;
        if ($('checkRezago')) $('checkRezago').disabled = true;
        if (btnGuardar) btnGuardar.style.display = 'none';

        let alerta = $('alertaEstatus');
        if (alerta) {
            alerta.className = 'alert alert-success mt-3 py-2 small';
            alerta.innerHTML = `<strong>Bloqueada.</strong> Materia acreditada en <strong>${escapeHTML(intentoAprobado.Periodo)}</strong>. Cambia a ese periodo para editarla.`;
            alerta.classList.remove('d-none');
        }

        renderizarHistorialUI(historialArray, true);
        if (modalBootstrapInstance) modalBootstrapInstance.show();
        return;
    }

    let modalPerVinc = $('modalPeriodoVinculado');
    if (modalPerVinc) modalPerVinc.innerText = 'Periodo a registrar: ' + periodoActual;

    if (intentoActual) {
        if ($('selectInscripcion')) $('selectInscripcion').value = intentoActual.TipoInscripcion;
        actualizarOpcionesExamen(intentoActual.TipoInscripcion);
        if ($('selectExamen')) $('selectExamen').value = intentoActual.UltimoExamen;
        if ($('selectAprobacion')) $('selectAprobacion').value = estadoASelect(intentoActual.Estado);
        if ($('checkRiesgo')) $('checkRiesgo').checked = intentoActual.EnRiesgo;
        if ($('checkRezago')) $('checkRezago').checked = intentoActual.EnRezago;
    } else if (ultimoIntento) {
        if ($('selectInscripcion')) $('selectInscripcion').value = ultimoIntento.TipoInscripcion;
        actualizarOpcionesExamen(ultimoIntento.TipoInscripcion);
        if ($('selectExamen')) $('selectExamen').value = ultimoIntento.UltimoExamen;
        if ($('selectAprobacion')) $('selectAprobacion').value = 'ninguno';
        if ($('checkRiesgo')) $('checkRiesgo').checked = ultimoIntento.EnRiesgo;
        if ($('checkRezago')) $('checkRezago').checked = ultimoIntento.EnRezago;
    } else {
        if ($('selectInscripcion')) $('selectInscripcion').value = 'primera';
        actualizarOpcionesExamen('primera');
        if ($('selectExamen')) $('selectExamen').value = 'ordinario';
        if ($('selectAprobacion')) $('selectAprobacion').value = 'ninguno';
        if ($('checkRiesgo')) $('checkRiesgo').checked = false;
        if ($('checkRezago')) $('checkRezago').checked = false;
    }

    evaluarLogicaInscripcion();
    renderizarHistorialUI(historialArray, false);
    if (modalBootstrapInstance) modalBootstrapInstance.show();
}

function limpiarEstadoMateria() {
    if (!tarjetaActual) return;

    let historialJSON = tarjetaActual.getAttribute('data-historial');
    let historialArray = historialJSON ? JSON.parse(historialJSON) : [];
    let intentoAprobado = historialArray.find(x => x.Estado === 'aprobada');

    let selPeriodo = $('selectPeriodoGlobal');
    let periodoActual = selPeriodo ? selPeriodo.value : '';

    if (intentoAprobado && intentoAprobado.Periodo !== periodoActual) {
        if (!confirm(`Esta materia fue aprobada en ${intentoAprobado.Periodo}. ¿Seguro que deseas borrarla?`)) return;
    } else {
        if (!confirm("¿Seguro que deseas reiniciar esta materia? Se borrará TODO el historial.")) return;
    }

    tarjetaActual.removeAttribute('data-historial');

    if ($('selectInscripcion')) $('selectInscripcion').disabled = false;
    if ($('selectExamen')) $('selectExamen').disabled = false;
    if ($('selectAprobacion')) $('selectAprobacion').disabled = false;
    if ($('checkRiesgo')) $('checkRiesgo').disabled = false;
    if ($('checkRezago')) $('checkRezago').disabled = false;

    let btnGuardar = document.querySelector('#modalAvance .modal-footer .btn-primary');
    if (btnGuardar) btnGuardar.style.display = 'block';

    if ($('selectInscripcion')) $('selectInscripcion').value = 'primera';
    actualizarOpcionesExamen('primera');

    if ($('selectExamen')) $('selectExamen').value = 'ordinario';
    if ($('selectAprobacion')) $('selectAprobacion').value = 'ninguno';
    if ($('checkRiesgo')) $('checkRiesgo').checked = false;
    if ($('checkRezago')) $('checkRezago').checked = false;

    if (tarjetaActual.classList.contains("afel-extra")) {
        tarjetaActual.remove();
        if (modalBootstrapInstance) modalBootstrapInstance.hide();
        actualizarProgreso();
        marcarCambios();
        return;
    }

    evaluarLogicaInscripcion();
    renderizarHistorialUI([], false);
    actualizarTarjetaVisual(tarjetaActual, []);
    actualizarProgreso();
    marcarCambios();
}

function renderizarHistorialUI(historialArray, isLocked) {
    const ul = $('listaHistorialIntentos');
    if (!ul) return;

    ul.innerHTML = '';

    if (historialArray.length === 0) {
        ul.innerHTML = '<li class="list-group-item text-center text-muted">No hay intentos registrados.</li>';
    } else {
        historialArray.forEach(intento => {
            let badge = '';
            if (intento.Estado === 'aprobada') badge = 'bg-success';
            else if (intento.Estado === 'reprobada') badge = 'bg-danger';
            else badge = 'bg-info text-dark';

            let txtInsc = intento.TipoInscripcion === 'primera' ? '1ra Insc.' : '2da Insc.';

            ul.innerHTML += `
                <li class="list-group-item d-flex justify-content-between align-items-center bg-light">
                    <div>
                        <strong>${txtInsc} - ${escapeHTML((intento.UltimoExamen || '').toUpperCase())}</strong><br/>
                        <small class="text-muted"><i class="bi bi-calendar-event"></i> ${escapeHTML(intento.Periodo)}</small>
                    </div>
                    <span class="badge ${badge} fs-6">${escapeHTML((intento.Estado || '').toUpperCase())}</span>
                </li>
            `;
        });
    }

    const btnSiguiente = $('btnSiguienteOp');
    if (!btnSiguiente) return;

    if (isLocked) {
        btnSiguiente.classList.add('d-none');
        return;
    }

    let periodoSelect = $('selectPeriodoGlobal');
    let periodoActual = periodoSelect ? periodoSelect.value : '';
    let ultimoIntento = historialArray.length > 0 ? historialArray[historialArray.length - 1] : null;
    let tieneIntentoEstePeriodo = historialArray.some(x => x.Periodo === periodoActual);

    if (ultimoIntento && ultimoIntento.Estado === 'reprobada' && !tieneIntentoEstePeriodo) {
        btnSiguiente.classList.remove('d-none');
    } else {
        btnSiguiente.classList.add('d-none');
    }
}

function prepararSiguienteOportunidad() {
    let historialJSON = tarjetaActual.getAttribute('data-historial');
    let historialArray = historialJSON ? JSON.parse(historialJSON) : [];
    let ultimo = historialArray[historialArray.length - 1];

    if (!ultimo) return;

    let nextInsc = ultimo.TipoInscripcion;
    let nextExam = 'ordinario';

    if (ultimo.TipoInscripcion === 'primera') {
        if (ultimo.UltimoExamen === 'ordinario') nextExam = 'extraordinario';
        else if (ultimo.UltimoExamen === 'extraordinario') nextExam = 'titulo';
        else if (ultimo.UltimoExamen === 'titulo') {
            nextInsc = 'segunda';
            nextExam = 'ordinario';
        }
    } else if (ultimo.TipoInscripcion === 'segunda') {
        if (ultimo.UltimoExamen === 'ordinario') nextExam = 'extraordinario';
        else if (ultimo.UltimoExamen === 'extraordinario') nextExam = 'ultima';
        else if (ultimo.UltimoExamen === 'ultima') {
            alert('Ya agotó sus oportunidades. Candidato a baja.');
            return;
        }
    }

    if ($('selectInscripcion')) $('selectInscripcion').value = nextInsc;
    actualizarOpcionesExamen(nextInsc);

    if ($('selectExamen')) $('selectExamen').value = nextExam;
    if ($('selectAprobacion')) $('selectAprobacion').value = 'ninguno';

    evaluarLogicaInscripcion();
}

function abrirModalTutorias() {
    let inputMat = $('inputMatricula');
    if (!inputMat || !inputMat.value.trim()) {
        alert("Selecciona un alumno primero.");
        return;
    }
    cargarTutoriasALaVista();
    if (modalTutoriasInstance) modalTutoriasInstance.show();
}

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

// Lee la respuesta JSON del servidor; si es un error (p. ej. 409: Excel abierto) lanza el mensaje del servidor
function leerRespuesta(res) {
    return res.json().catch(() => ({})).then(data => {
        if (!res.ok) throw new Error(data.message || `Error del servidor (${res.status}).`);
        return data;
    });
}

// Aviso visible en la parte superior de la pantalla (no bloquea como alert)
function mostrarAviso(mensaje) {
    let el = $('avisoSistema');
    if (!el) {
        el = document.createElement('div');
        el.id = 'avisoSistema';
        el.className = 'alert alert-danger shadow position-fixed top-0 start-50 translate-middle-x mt-2 fw-bold';
        el.style.zIndex = '2000';
        el.style.maxWidth = '90%';
        document.body.appendChild(el);
    }
    el.textContent = mensaje;
    el.classList.remove('d-none');
}

function ocultarAviso() {
    const el = $('avisoSistema');
    if (el) el.classList.add('d-none');
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

function actualizarOpcionesExamen(tipoInscripcion) {
    const sel = $('selectExamen');
    if (!sel) return;
    sel.innerHTML = '';

    let opts = [];
    if (tipoInscripcion === 'primera') {
        opts = [{ v: 'ordinario', t: 'Examen Ordinario' }, { v: 'extraordinario', t: 'Examen Extraordinario' }, { v: 'titulo', t: 'Examen a Título' }];
    } else {
        opts = [{ v: 'ordinario', t: 'Examen Ordinario' }, { v: 'extraordinario', t: 'Examen Extraordinario' }, { v: 'ultima', t: 'Última Oportunidad' }];
    }

    opts.forEach(o => { sel.add(new Option(o.t, o.v)); });
}

function evaluarLogicaInscripcion() {
    var selInsc = $('selectInscripcion');
    var selExam = $('selectExamen');
    var selAprob = $('selectAprobacion');

    if (!selInsc || !selExam || !selAprob) return;

    var inscripcion = selInsc.value, examen = selExam.value, aprobacion = selAprob.value;
    var alerta = $('alertaEstatus');
    if (!alerta) return;

    alerta.className = 'alert mt-3 py-2 small';

    if (aprobacion === 'ninguno') { alerta.classList.add('d-none'); return; }
    alerta.classList.remove('d-none');

    if (aprobacion === 'cursando') { alerta.classList.add('alert-info'); alerta.innerHTML = `<strong>En curso.</strong> Pendiente de calificar.`; return; }
    if (aprobacion === 'si') { alerta.classList.add('alert-success'); alerta.innerHTML = `<strong>¡Materia Aprobada!</strong>`; return; }

    if (aprobacion === 'no') {
        if (inscripcion === 'primera') {
            if (examen === 'ordinario') { alerta.classList.add('alert-warning'); alerta.innerHTML = 'Reprobó Ordinario. <strong>Sigue Extra.</strong>'; }
            else if (examen === 'extraordinario') { alerta.classList.add('alert-warning'); alerta.innerHTML = 'Reprobó Extra. <strong>Sigue Título.</strong>'; }
            else if (examen === 'titulo') { alerta.classList.add('alert-danger'); alerta.innerHTML = 'Reprobó Título. <strong>Pasa a 2da Inscripción.</strong>'; }
        } else {
            if (examen === 'ordinario') { alerta.classList.add('alert-warning'); alerta.innerHTML = 'Reprobó Ord. de 2da. <strong>Sigue Extra.</strong>'; }
            else if (examen === 'extraordinario') { alerta.classList.add('alert-warning'); alerta.innerHTML = 'Reprobó Extra de 2da. <strong>Sigue Última Op.</strong>'; }
            else if (examen === 'ultima') { alerta.classList.add('alert-danger'); alerta.innerHTML = '<strong>CANDIDATO A BAJA.</strong> Reprobó Última Oportunidad.'; }
        }
    }
}

function guardarAvance() {
    let nuevoNombre = tarjetaActual.getAttribute('data-nombre');
    let creditosGuardar = parseInt(tarjetaActual.getAttribute('data-creditos')) || 0;

    if (tarjetaActual.classList.contains('bg-optativa')) {
        let inputOp = $('inputOptativa');
        let ipt = inputOp ? inputOp.value.trim() : '';
        if (ipt) {
            tarjetaActual.setAttribute('data-nombre-personalizado', ipt);
            nuevoNombre = ipt;
        } else {
            tarjetaActual.removeAttribute('data-nombre-personalizado');
        }
    } else if (tarjetaActual.classList.contains('bg-afel')) {
        let inputNAfel = $('inputNombreAfel');
        let inputCAfel = $('inputCreditosAfel');
        let iptName = inputNAfel ? inputNAfel.value.trim() : '';
        let iptCred = inputCAfel ? parseInt(inputCAfel.value) : 0;

        if (iptName) {
            tarjetaActual.setAttribute('data-nombre-personalizado', iptName);
            nuevoNombre = iptName;
        } else {
            tarjetaActual.removeAttribute('data-nombre-personalizado');
        }

        if (iptCred > 0) {
            tarjetaActual.setAttribute('data-creditos', iptCred);
            creditosGuardar = iptCred;
        }
    }

    let selAprob = $('selectAprobacion');
    var aprobacion = selAprob ? selAprob.value : 'ninguno';

    var chkR = $('checkRiesgo');
    var chkZ = $('checkRezago');
    var enRiesgo = chkR ? chkR.checked : false;
    var enRezago = chkZ ? chkZ.checked : false;

    if (aprobacion === 'ninguno' && !enRiesgo && !enRezago) {
        alert("Seleccione un estado o marque riesgo/rezago.");
        return;
    }

    let historialJSON = tarjetaActual.getAttribute('data-historial');
    let historialArray = historialJSON ? JSON.parse(historialJSON) : [];

    let selPeriodo = $('selectPeriodoGlobal');
    let periodoActual = selPeriodo ? selPeriodo.value : '';

    let intentoAprobado = historialArray.find(x => x.Estado === 'aprobada');
    if (intentoAprobado && intentoAprobado.Periodo !== periodoActual) {
        alert("No se puede modificar: La materia ya fue aprobada en otro periodo.");
        return;
    }

    let index = historialArray.findIndex(x => x.Periodo === periodoActual);
    let selInsc = $('selectInscripcion');
    let selExam = $('selectExamen');

    let valInsc = selInsc ? selInsc.value : 'primera';
    let valExam = selExam ? selExam.value : 'ordinario';
    let estadoGuardar = selectAEstado(aprobacion);

    if (index === -1) {
        historialArray.push({
            Nombre: nuevoNombre,
            Periodo: periodoActual,
            Creditos: creditosGuardar,
            TipoInscripcion: valInsc,
            UltimoExamen: valExam,
            Estado: estadoGuardar,
            EnRiesgo: enRiesgo,
            EnRezago: enRezago
        });
    } else {
        historialArray[index].Nombre = nuevoNombre;
        historialArray[index].TipoInscripcion = valInsc;
        historialArray[index].UltimoExamen = valExam;
        historialArray[index].Estado = estadoGuardar;
        historialArray[index].EnRiesgo = enRiesgo;
        historialArray[index].EnRezago = enRezago;
    }

    tarjetaActual.setAttribute('data-historial', JSON.stringify(historialArray));
    actualizarTarjetaVisual(tarjetaActual, historialArray);

    if (modalBootstrapInstance) modalBootstrapInstance.hide();
    actualizarProgreso();
    marcarCambios();
}

function actualizarTarjetaVisual(tarjeta, historialArray) {
    tarjeta.classList.remove('estado-aprobada', 'estado-reprobada', 'estado-cursando');
    const nombreActual = tarjeta.getAttribute('data-nombre-personalizado') || tarjeta.getAttribute('data-nombre');
    const creditosActual = tarjeta.getAttribute('data-creditos');
    const prereqSpan = tarjeta.querySelector('.prerequisito-text');

    let htmlContenido = `<strong>${escapeHTML(nombreActual)}</strong><br/>(${creditosActual} Cr.)`;
    if (prereqSpan) htmlContenido += `<span class="prerequisito-text">${prereqSpan.innerHTML}</span>`;
    if (tarjeta.classList.contains('afel-extra')) htmlContenido += BOTON_QUITAR_AFEL;

    if (historialArray.length === 0) {
        tarjeta.removeAttribute('data-estado');
        tarjeta.removeAttribute('data-riesgo');
        tarjeta.removeAttribute('data-rezago');
        tarjeta.innerHTML = htmlContenido;
        return;
    }

    let last = historialArray[historialArray.length - 1];

    if (last.Estado && last.Estado !== 'ninguno') {
        tarjeta.setAttribute('data-estado', last.Estado);

        if (last.Estado === "aprobada") tarjeta.classList.add('estado-aprobada');
        else if (last.Estado === "reprobada") tarjeta.classList.add('estado-reprobada');
        else if (last.Estado === "cursando") tarjeta.classList.add('estado-cursando');

        let txInsc = last.TipoInscripcion === 'primera' ? '1ª Insc.' : '2ª Insc.';
        let ex = last.UltimoExamen;
        let txExm = ex === 'ordinario' ? 'Ord.' : ex === 'extraordinario' ? 'Ext.' : ex === 'titulo' ? 'Título' : ex === 'ultima' ? 'Últ. Op.' : '';

        if (last.Estado === 'cursando') txExm = 'En Curso';

        htmlContenido += `<div class="badge-info-materia">${txInsc} • ${txExm}<br/><span style="font-size: 0.60rem; opacity: 0.85;">${escapeHTML(last.Periodo)}</span></div>`;

        if (last.Estado === "aprobada") {
            htmlContenido += `<div class="badge bg-success text-white mt-1 shadow-sm" style="font-size:0.65rem; width:100%">APROBADA</div>`;
        }
    } else {
        tarjeta.removeAttribute('data-estado');
    }

    tarjeta.setAttribute('data-riesgo', last.EnRiesgo);
    tarjeta.setAttribute('data-rezago', last.EnRezago);

    if (last.EnRiesgo) htmlContenido += `<div class="badge bg-warning text-dark mt-1 shadow-sm" style="font-size:0.65rem; width:100%">EN RIESGO</div>`;
    if (last.EnRezago) htmlContenido += `<div class="badge bg-secondary text-white mt-1 shadow-sm" style="font-size:0.65rem; width:100%">EN REZAGO</div>`;

    tarjeta.innerHTML = htmlContenido;
}

// ===================== PERIODO QUE CURSA EL ALUMNO =====================
// La matrícula lleva el año de ingreso: S21... (o zS21...) = entró en agosto de 2021.
// Su 1er periodo es "Agosto - Enero" de ese ingreso, el 2do "Febrero - Julio" siguiente, y así sucesivamente.

// Año de ingreso según la matrícula, o null si la matrícula no tiene el formato S + 2 dígitos
function anioIngresoDeMatricula(matricula) {
    const m = /^z?s(\d{2})/i.exec((matricula || '').trim());
    return m ? 2000 + parseInt(m[1]) : null;
}

// Número de periodo que le toca al alumno en un Periodo de Trabajo dado.
// En el selector el año es el de TÉRMINO: "Agosto - Enero 2027" va de agosto 2026 a enero 2027.
function numeroDePeriodo(anioIngreso, periodoTexto) {
    const m = /(\d{4})/.exec(periodoTexto || '');
    if (!anioIngreso || !m) return null;
    const anio = parseInt(m[1]);
    return /^\s*ago/i.test(periodoTexto)
        ? (anio - 1 - anioIngreso) * 2 + 1   // Agosto - Enero: periodos impares
        : (anio - anioIngreso) * 2;          // Febrero - Julio: periodos pares
}

// Nombre del periodo número i (1, 2, 3...) de un alumno que ingresó en 'anioIngreso'
function nombreDePeriodo(anioIngreso, i) {
    return i % 2 === 1 ? `Agosto - Enero ${anioIngreso + (i + 1) / 2}` : `Febrero - Julio ${anioIngreso + i / 2}`;
}

// Periodos en que el alumno cargado NO estuvo inscrito (bajas temporales). Se guardan con el alumno.
let periodosSinInscripcion = [];

// Periodo que REALMENTE cursa: los periodos corridos desde el ingreso menos los periodos anteriores sin inscripción
function periodoQueCursaAlumno(matricula, periodoTexto) {
    const ingreso = anioIngresoDeMatricula(matricula);
    const corridos = numeroDePeriodo(ingreso, periodoTexto);
    if (corridos === null) return null;
    const pausas = periodosSinInscripcion.map(p => numeroDePeriodo(ingreso, p)).filter(n => n >= 1 && n < corridos).length;
    return corridos - pausas;
}

function abrirModalPausas() {
    const inputMat = $('inputMatricula');
    const matricula = inputMat ? inputMat.value.trim() : '';
    if (!matricula || alumnoCargado !== matricula) { alert("Selecciona un alumno primero."); return; }
    const ingreso = anioIngresoDeMatricula(matricula);
    if (!ingreso) { alert("La matrícula no trae el año de ingreso (ej. S21...), así que no se puede calcular su periodo."); return; }

    // Se listan los periodos desde su ingreso hasta el Periodo de Trabajo
    const selP = $('selectPeriodoGlobal');
    const hasta = Math.max(1, numeroDePeriodo(ingreso, selP ? selP.value : '') || 1);
    const lista = $('listaPausas');
    lista.innerHTML = '';
    for (let i = 1; i <= hasta; i++) {
        const nombre = nombreDePeriodo(ingreso, i);
        const item = document.createElement('label');
        item.className = 'list-group-item d-flex align-items-center gap-2';
        item.innerHTML = `<input class="form-check-input m-0" type="checkbox" ${periodosSinInscripcion.includes(nombre) ? 'checked' : ''}> <span>${escapeHTML(nombre)}</span>`;
        item.querySelector('input').addEventListener('change', function () {
            periodosSinInscripcion = periodosSinInscripcion.filter(p => p !== nombre);
            if (this.checked) periodosSinInscripcion.push(nombre);
            actualizarResumenPausas();
            actualizarProgreso();   // recalcula periodo que cursa y rezago
            marcarCambios();        // se guarda al momento
        });
        lista.appendChild(item);
    }
    actualizarResumenPausas();
    bootstrap.Modal.getOrCreateInstance($('modalPausas')).show();
}

function actualizarResumenPausas() {
    const r = $('resumenPausas');
    if (r) r.innerText = periodosSinInscripcion.length === 0 ? 'Sin periodos marcados.' : `${periodosSinInscripcion.length} periodo(s) sin inscripción.`;
}

function actualizarPeriodoAlumno() {
    const info = $('infoPeriodoAlumno');
    const inputMat = $('inputMatricula');
    const selP = $('selectPeriodoGlobal');
    const matricula = inputMat ? inputMat.value.trim() : '';

    // Se quita el resaltado anterior del mapa y las marcas de rezago automático (se recalculan abajo)
    document.querySelectorAll('.periodo-header.periodo-actual').forEach(h => h.classList.remove('periodo-actual'));
    document.querySelectorAll('.badge-rezago-auto').forEach(b => b.remove());
    document.querySelectorAll('.materia-card[data-rezago-auto]').forEach(t => t.removeAttribute('data-rezago-auto'));

    if (!info) return;
    info.className = 'form-control form-control-sm fw-bold bg-light text-muted';
    info.title = '';
    if (!matricula) { info.innerText = '—'; return; }

    const ingreso = anioIngresoDeMatricula(matricula);
    if (!ingreso) { info.innerText = 'Matrícula sin año'; info.title = 'La matrícula no empieza con S + año de ingreso (ej. S21...)'; return; }

    const periodoTrabajo = selP ? selP.value : '';
    const corridos = numeroDePeriodo(ingreso, periodoTrabajo);
    if (corridos === null) { info.innerText = '—'; return; }
    if (corridos < 1) { info.innerText = `Ingresa en ago. ${ingreso}`; info.title = 'En el Periodo de Trabajo elegido el alumno todavía no había ingresado.'; return; }

    // Se descuentan los periodos sin inscripción (bajas temporales)
    const n = periodoQueCursaAlumno(matricula, periodoTrabajo);
    const pausas = corridos - n;
    const enPausaAhora = periodosSinInscripcion.includes(periodoTrabajo);

    // Columnas de semestre del plan (la de solo AFEL no cuenta)
    const columnas = [...document.querySelectorAll('.periodo-columna[data-semestre]')];
    const ultimoDelPlan = columnas.length ? Math.max(...columnas.map(c => parseInt(c.getAttribute('data-semestre')))) : 0;

    info.title = `Ingresó en agosto de ${ingreso}.` + (pausas > 0 ? ` Se descuentan ${pausas} periodo(s) sin inscripción.` : '');
    if (enPausaAhora) {
        info.innerText = `Sin inscripción (retoma ${n}°)`;
        info.className = 'form-control form-control-sm fw-bold bg-light text-secondary border-secondary';
        info.title += ' En este Periodo de Trabajo está marcado como no inscrito.';
    } else if (ultimoDelPlan && n > ultimoDelPlan) {
        // Ya rebasó la duración del plan: se marca en rojo
        info.innerText = `${n}° periodo (plan: ${ultimoDelPlan})`;
        info.className = 'form-control form-control-sm fw-bold bg-light text-danger border-danger';
        info.title += ` El plan dura ${ultimoDelPlan} periodos: lleva ${n - ultimoDelPlan} de más.`;
    } else {
        info.innerText = `${n}° periodo`;
        info.className = 'form-control form-control-sm fw-bold bg-light text-primary border-primary';
    }

    const columnaActual = enPausaAhora ? null : columnas.find(c => parseInt(c.getAttribute('data-semestre')) === n);
    if (columnaActual) columnaActual.querySelector('.periodo-header').classList.add('periodo-actual');

    // REZAGO AUTOMÁTICO: toda materia de un periodo anterior al que cursa y que no esté aprobada.
    // Solo es visual (no se guarda en el Excel): se recalcula con la matrícula, el plan y el Periodo de Trabajo.
    columnas.filter(c => parseInt(c.getAttribute('data-semestre')) < n).forEach(col => {
        col.querySelectorAll('.materia-card').forEach(t => {
            if (t.classList.contains('bg-afel') || t.getAttribute('data-estado') === 'aprobada') return;
            t.setAttribute('data-rezago-auto', 'true');
            if (t.getAttribute('data-rezago') === 'true') return;   // ya trae la etiqueta de rezago manual
            t.insertAdjacentHTML('beforeend', '<div class="badge bg-secondary text-white mt-1 shadow-sm badge-rezago-auto" style="font-size:0.65rem; width:100%" title="Materia de un periodo anterior al que cursa">EN REZAGO</div>');
        });
    });
}

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
    actualizarPeriodoAlumno();

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

function puedeCambiarDeContexto() {
    // Con la cola de guardado por alumno, cambiar de alumno ya no pierde cambios: los pendientes se guardan solos
    return true;
}

function buscarAlumno(materiaAResaltar = null) {
    let inputMat = $('inputMatricula');
    if (!inputMat) return;

    var matricula = inputMat.value.trim();
    if (!matricula) return;

    // Mientras llega el alumno nuevo, el mapa todavía muestra al anterior: no se permite guardar
    alumnoCargado = '';

    // Se espera a que termine el guardado en curso para no leer datos de antes de ese guardado
    esperarGuardadoEnCurso()
        .then(() => fetch(`/Home/BuscarAlumnoExcel?matricula=${encodeURIComponent(matricula)}`))
        .then(leerRespuesta)
        .then(res => {
            if (!res.success) { alert(res.message); return; }

            if ($('inputNombreAlumno')) $('inputNombreAlumno').value = res.data.nombre;
            if ($('selectSituacionAlumno')) $('selectSituacionAlumno').value = res.data.situacion || 'Activo';
            planActualAlumno = res.data.planEstudio || "";

            // ====== LA MAGIA: CARGAMOS EL MAPA PRIMERO ======
            cargarMapaCurricular(planActualAlumno, function () {

                // --- ESTO SE EJECUTA HASTA QUE EL MAPA ESTÉ COMPLETAMENTE DIBUJADO ---
                tutoriasGlobal = (res.data.tutorias || []).map(t => ({ Periodo: t.periodo, Sesion: t.sesion, Fecha: t.fecha, Asistencia: t.asistencia, Comentarios: t.comentarios }));
                cargarTutoriasALaVista();
                periodosSinInscripcion = (res.data.periodosSinInscripcion || []).slice();

                let materiasAgrupadas = {};
                res.data.materias.forEach(m => {
                    let matParseada = { Nombre: m.nombre, Periodo: m.periodo, Creditos: m.creditos, TipoInscripcion: m.tipoInscripcion, UltimoExamen: m.ultimoExamen, Estado: selectAEstado(m.estado), EnRiesgo: m.enRiesgo, EnRezago: m.enRezago };
                    if (!materiasAgrupadas[matParseada.Nombre]) materiasAgrupadas[matParseada.Nombre] = [];
                    materiasAgrupadas[matParseada.Nombre].push(matParseada);
                });

                document.querySelectorAll(".afel-extra").forEach(t => t.remove());

                document.querySelectorAll('.materia-card').forEach(t => {
                    let baseName = t.getAttribute('data-nombre');
                    let historialArray = materiasAgrupadas[baseName] || [];

                    if (t.classList.contains('bg-optativa') && historialArray.length === 0) {
                        for (let k in materiasAgrupadas) {
                            if (!document.querySelector(`.materia-card[data-nombre="${CSS.escape(k)}"]`)) {
                                if (listaOptativas.includes(k) && !t.hasAttribute('data-historial')) {
                                    historialArray = materiasAgrupadas[k];
                                    t.setAttribute('data-nombre-personalizado', k);
                                    delete materiasAgrupadas[k];
                                    break;
                                }
                            }
                        }
                    }

                    if (t.classList.contains('bg-afel') && historialArray.length === 0) {
                        for (let k in materiasAgrupadas) {
                            if (!document.querySelector(`.materia-card[data-nombre="${CSS.escape(k)}"]`)) {
                                if (!listaOptativas.includes(k) && !t.hasAttribute('data-historial')) {
                                    historialArray = materiasAgrupadas[k];
                                    t.setAttribute('data-nombre-personalizado', k);
                                    if (historialArray[0] && historialArray[0].Creditos) t.setAttribute('data-creditos', historialArray[0].Creditos);
                                    delete materiasAgrupadas[k];
                                    break;
                                }
                            }
                        }
                    }

                    if (historialArray.length > 0) {
                        t.setAttribute('data-historial', JSON.stringify(historialArray));
                    } else {
                        t.removeAttribute('data-historial');
                        if (t.classList.contains('bg-optativa') || t.classList.contains('bg-afel')) t.removeAttribute('data-nombre-personalizado');
                    }

                    actualizarTarjetaVisual(t, historialArray);
                });

                for (let k in materiasAgrupadas) {
                    if (!document.querySelector(`.materia-card[data-nombre="${CSS.escape(k)}"]`) && !document.querySelector(`.materia-card[data-nombre-personalizado="${CSS.escape(k)}"]`) && !listaOptativas.includes(k)) {
                        let historialExtra = materiasAgrupadas[k];
                        let creditosExtra = historialExtra[0] ? historialExtra[0].Creditos : 6;
                        let nuevaTarjeta = agregarAfelExtra(k, creditosExtra);
                        if (nuevaTarjeta) {
                            nuevaTarjeta.setAttribute('data-historial', JSON.stringify(historialExtra));
                            actualizarTarjetaVisual(nuevaTarjeta, historialExtra);
                        }
                    }
                }

                // Desde aquí el mapa en pantalla es de este alumno y ya se pueden guardar sus cambios
                alumnoCargado = matricula;
                if (!hayGuardadosPendientes()) { const estadoG = $('estadoGuardado'); if (estadoG) estadoG.innerHTML = ''; }
                actualizarProgreso();

                if (materiaAResaltar) {
                    setTimeout(() => {
                        const anim = Array.from(document.querySelectorAll('.materia-card')).find(t => t.getAttribute('data-nombre') === materiaAResaltar || t.getAttribute('data-nombre-personalizado') === materiaAResaltar);
                        if (anim) {
                            anim.scrollIntoView({ behavior: 'smooth', block: 'center' });
                            anim.classList.add('resaltado-alerta');
                            setTimeout(() => anim.classList.remove('resaltado-alerta'), 3000);
                        }
                    }, 300);
                }
            });

        }).catch(e => { console.error(e); mostrarAviso(e.message || "Error de comunicación con el sistema."); });
}

function cargarListaAlumnos() {
    fetch('/Home/ObtenerAlumnosRegistrados').then(leerRespuesta).then(alumnos => {
        const ul = $('listaAlumnosRegistrados');
        if (!ul) return;
        ul.innerHTML = '';
        if (alumnos.length === 0) { ul.innerHTML = '<li><span class="dropdown-item-text text-muted">Vacio</span></li>'; }
        else {
            ul.innerHTML = '<li><h6 class="dropdown-header">Alumnos</h6></li>';
            alumnos.forEach(a => {
                let li = document.createElement('li'); let btn = document.createElement('a'); btn.className = 'dropdown-item d-flex justify-content-between'; btn.href = '#';
                let ic = a.situacion === 'Activo' ? '🟢' : (a.situacion === 'Egresado' ? '🎓' : '🔴');
                btn.className += ' align-items-center gap-2';
                btn.innerHTML = `<span><strong>${escapeHTML(a.matricula)}</strong> - ${escapeHTML(a.nombre)}</span> <span class="d-flex align-items-center gap-2">${ic} <span class="btn btn-sm btn-outline-danger py-0 px-1 lh-1 btn-eliminar-alumno" title="Eliminar alumno">${ICONO_ELIMINAR}</span></span>`;
                btn.onclick = (e) => { e.preventDefault(); if (!puedeCambiarDeContexto()) return; if ($('inputMatricula')) $('inputMatricula').value = a.matricula; buscarAlumno(); };
                btn.querySelector('.btn-eliminar-alumno').addEventListener('click', (e) => { e.preventDefault(); e.stopPropagation(); eliminarAlumno(a.matricula, a.nombre); });
                li.appendChild(btn); ul.appendChild(li);
            });
        }
        ul.insertAdjacentHTML('beforeend', '<li><hr class="dropdown-divider"></li><li><a class="dropdown-item text-success fw-bold" href="#" onclick="abrirModalNuevoAlumno(event)">Agregar alumno</a></li>');
        if (!hayGuardadosPendientes()) ocultarAviso();
    }).catch(e => { console.error('Error cargando lista de alumnos:', e); mostrarAviso(e.message); });
}

function cargarListaAlumnosInicio() {
    fetch('/Home/ObtenerAlumnosRegistrados').then(leerRespuesta).then(alumnos => {
        const c = $('listaAlumnosModal');
        if (!c) return;
        c.innerHTML = '';
        alumnos.forEach(a => {
            // Fila: botón para abrir al alumno + botón para eliminarlo (filtrarAlumnosModal oculta la fila completa)
            let fila = document.createElement('div'); fila.className = 'd-flex gap-2 mb-2 fila-alumno-modal';
            let btn = document.createElement('button'); btn.className = 'btn btn-outline-primary text-start fw-bold flex-grow-1 d-flex justify-content-between';
            let ic = a.situacion === 'Activo' ? '🟢' : (a.situacion === 'Egresado' ? '🎓' : '🔴');
            btn.innerHTML = `<span><i class="bi bi-person-fill"></i> ${escapeHTML(a.matricula)} - ${escapeHTML(a.nombre)}</span> <span>${ic}</span>`;
            btn.onclick = () => { if (!puedeCambiarDeContexto()) return; if ($('inputMatricula')) $('inputMatricula').value = a.matricula; buscarAlumno(); if (modalInicioInstance) modalInicioInstance.hide(); };
            let btnEliminar = document.createElement('button'); btnEliminar.className = 'btn btn-outline-danger'; btnEliminar.title = 'Eliminar alumno';
            btnEliminar.innerHTML = ICONO_ELIMINAR;
            btnEliminar.onclick = () => eliminarAlumno(a.matricula, a.nombre);
            fila.appendChild(btn); fila.appendChild(btnEliminar);
            c.appendChild(fila);
        });
    }).catch(e => { console.error('Error cargando alumnos de inicio:', e); mostrarAviso(e.message); });
}

function abrirModalNuevoAlumno(e) {
    e.preventDefault();
    if (!puedeCambiarDeContexto()) return;
    if ($('inputBuscarAlumnoModal')) $('inputBuscarAlumnoModal').value = '';
    const b = $('btnCerrarModalInicio'); if (b) b.classList.remove('d-none');
    if (modalInicioInstance) modalInicioInstance.show();
}

// Ícono de bote de basura en SVG (el proyecto no carga Bootstrap Icons, así que los <i class="bi ..."> no se ven)
const ICONO_ELIMINAR = '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16" aria-hidden="true"><path d="M2.5 1a1 1 0 0 0-1 1v1a1 1 0 0 0 1 1H3v9a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2V4h.5a1 1 0 0 0 1-1V2a1 1 0 0 0-1-1H10a1 1 0 0 0-1-1H7a1 1 0 0 0-1 1zm3 4a.5.5 0 0 1 .5.5v7a.5.5 0 0 1-1 0v-7a.5.5 0 0 1 .5-.5M8 5a.5.5 0 0 1 .5.5v7a.5.5 0 0 1-1 0v-7A.5.5 0 0 1 8 5m3 .5v7a.5.5 0 0 1-1 0v-7a.5.5 0 0 1 1 0"/></svg>';

function eliminarAlumno(matricula, nombre) {
    if (!confirm(`¿Seguro que deseas eliminar al alumno ${matricula} - ${nombre}?\n\nSe borrará todo su avance y tutorías. Esta acción no se puede deshacer.`)) return;

    // Se descartan sus guardados pendientes y se espera el que esté en curso, para que no lo vuelva a crear
    alumnosEliminados.add(matricula);
    guardadosPendientes.delete(matricula);

    esperarGuardadoEnCurso()
        .then(() => fetch(`/Home/EliminarAlumno?matricula=${encodeURIComponent(matricula)}`, { method: 'POST' }))
        .then(leerRespuesta)
        .then(data => {
            if (!data.success) { alumnosEliminados.delete(matricula); alert("No se pudo eliminar: " + (data.message || "error desconocido.")); return; }

            let inputMat = $('inputMatricula');
            if (inputMat && inputMat.value.trim().toLowerCase() === matricula.toLowerCase()) {
                limpiarVistaAlumno();
                const b = $('btnCerrarModalInicio'); if (b) b.classList.add('d-none');
                if (modalInicioInstance) modalInicioInstance.show();
            }

            cargarListaAlumnos();
            cargarListaAlumnosInicio();
            cargarAlertas();
        })
        .catch(e => { console.error(e); alumnosEliminados.delete(matricula); alert("No se pudo eliminar el alumno. " + (e.message || "")); });
}

// Deja la pantalla sin alumno seleccionado (se usa al eliminar al alumno que estaba abierto)
function limpiarVistaAlumno() {
    if ($('inputMatricula')) $('inputMatricula').value = '';
    if ($('inputNombreAlumno')) $('inputNombreAlumno').value = '';
    if ($('selectSituacionAlumno')) $('selectSituacionAlumno').value = 'Activo';
    planActualAlumno = '';
    planDibujadoActual = '';
    tutoriasGlobal = [];
    cargarTutoriasALaVista();
    huboCambios = false;
    alumnoCargado = '';
    periodosSinInscripcion = [];

    const contenedor = $('contenedor-mapa-dinamico');
    if (contenedor) contenedor.innerHTML = '<div class="w-100 text-center text-muted mt-5"><i class="bi bi-map text-secondary" style="font-size: 3rem;"></i><br />Selecciona un alumno para cargar su plan de estudios.</div>';

    const estado = $('estadoGuardado'); if (estado) estado.innerHTML = '';
    const titulo = $('tituloMapa'); if (titulo) titulo.innerText = 'Mapa Curricular';
    actualizarProgreso();
}

function crearNuevoAlumno() {
    let inputMat = $('inputNuevaMatricula');
    let inputNom = $('inputNuevoNombre');
    let selPlan = $('selectPlanNuevoAlumno');

    if (!inputMat || !inputNom || !selPlan) return;

    const mat = inputMat.value.trim();
    const nom = inputNom.value.trim();
    const plan = selPlan.value;

    if (!mat || !nom || !plan) {
        alert("Faltan datos o no has seleccionado el Plan de Estudios.");
        return;
    }

    if (!puedeCambiarDeContexto()) return;

    fetch('/Home/ObtenerAlumnosRegistrados')
        .then(leerRespuesta)
        .then(alumnos => {
            // BLINDAJE 1: (a.matricula || "") evita que crashee si hay una fila vacía en Excel
            if (alumnos.some(a => (a.matricula || "").toLowerCase() === mat.toLowerCase())) {
                alert("Matrícula ya registrada.");
                return;
            }

            if ($('inputMatricula')) $('inputMatricula').value = mat;
            if ($('inputNombreAlumno')) $('inputNombreAlumno').value = nom;
            if ($('selectSituacionAlumno')) $('selectSituacionAlumno').value = 'Activo';

            // planActualAlumno está declarada con 'let', así que window.planActualAlumno sería otra variable distinta:
            // hay que asignar la variable real o los guardados posteriores mandan el plan vacío ("Generico")
            planActualAlumno = plan;

            tutoriasGlobal = [];
            cargarTutoriasALaVista();
            periodosSinInscripcion = [];
            alumnosEliminados.delete(mat);

            // Se fuerza a redibujar el mapa: si el plan es el mismo que el del alumno anterior, sin esto
            // las tarjetas conservaban las materias del alumno anterior y se guardaban en el nuevo
            planDibujadoActual = '';
            alumnoCargado = '';

            // Se dibuja la plantilla del plan elegido y, HASTA QUE TERMINE, se registra al alumno
            cargarMapaCurricular(plan, function () {
                actualizarProgreso();
                alumnoCargado = mat;

                // El registro pasa por la cola de guardado: si el Excel está ocupado se reintenta solo
                marcarCambios();
                if (!document.querySelector('#contenedor-mapa-dinamico .materia-card')) {
                    mostrarAviso("No se pudo cargar el plan de estudios; el alumno no se registró.");
                    return;
                }

                if (modalInicioInstance) modalInicioInstance.hide();
                inputMat.value = '';
                inputNom.value = '';
                selPlan.value = '';
            });

        }).catch(e => {
            console.error("Error al crear alumno:", e);
            mostrarAviso(e.message || "Error de comunicación con el sistema.");
        });
}

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
        { titulo: 'Seriadas Adeudadas (Candados)', color: '#dc3545', id: 'contenedorReporteSeriadas' }
    ];

    if (document.querySelector('#modalReporteGlobal .spinner-border')) { alert("El reporte aún se está cargando. Intenta de nuevo en un momento."); return; }

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

function descargarWordTutorias() {
    let inputMat = $('inputMatricula');
    var matricula = inputMat ? inputMat.value.trim() : '';
    if (!matricula) { alert("Selecciona un alumno primero."); return; }
    if (hayGuardadosPendientes()) { alert("Se están guardando los últimos cambios. Intenta de nuevo en un momento."); return; }
    window.location.href = `/Home/DescargarWordTutorias?matricula=${encodeURIComponent(matricula)}`;
}

function filtrarAlumnosModal() {
    let input = $('inputBuscarAlumnoModal');
    if (!input) return;
    const texto = input.value.toLowerCase();
    const filas = document.querySelectorAll('#listaAlumnosModal .fila-alumno-modal');
    filas.forEach(fila => {
        if (fila.innerText.toLowerCase().includes(texto)) { fila.style.setProperty('display', 'flex', 'important'); } else { fila.style.setProperty('display', 'none', 'important'); }
    });
}
function abrirGestorPlanes() {
    if (modalGestorPlanesInstance) modalGestorPlanesInstance.show();
}

function subirPlanEstudio() {
    const input = document.getElementById("inputFilePlan");
    if (!input || input.files.length === 0) {
        alert("Por favor selecciona un archivo Excel (.xlsx) primero.");
        return;
    }

    const archivo = input.files[0];
    if (!archivo.name.endsWith('.xlsx')) {
        alert("El archivo debe tener formato .xlsx obligatoriamente.");
        return;
    }

    // Usamos FormData porque estamos enviando un archivo binario, no JSON
    const formData = new FormData();
    formData.append("archivoPlan", archivo);

    // Ponemos a cargar el botón para que el usuario sepa que está trabajando
    const btn = event.currentTarget;
    const textoOriginal = btn.innerHTML;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span> Subiendo...';
    btn.disabled = true;

    fetch('/Home/SubirPlanEstudio', {
        method: 'POST',
        body: formData
    })
        .then(res => res.json())
        .then(data => {
            btn.innerHTML = textoOriginal;
            btn.disabled = false;

            if (data.success) {
                alert(data.message);
                input.value = ""; // Limpiamos el input
                if (modalGestorPlanesInstance) modalGestorPlanesInstance.hide();
            } else {
                alert("Error: " + data.message);
            }
        })
        .catch(e => {
            console.error(e);
            btn.innerHTML = textoOriginal;
            btn.disabled = false;
            alert("Error de comunicación al subir el plan de estudios.");
        });
}
function cargarPlanesDropdown() {
    fetch('/Home/ObtenerPlanesEstudio')
        .then(res => res.json())
        .then(planes => {
            const sel = $('selectPlanNuevoAlumno');
            if (!sel) return;
            sel.innerHTML = '<option value="">-- Selecciona el Plan --</option>';
            planes.forEach(p => {
                sel.add(new Option(p, p));
            });
        })
        .catch(e => console.error("Error cargando planes:", e));
}
// Cuando el alumno no tiene plan (o su plan ya no existe) se muestra un selector para asignarle uno.
// Como el mapa ya no muestra lo que había, se olvida el plan dibujado para que el siguiente se dibuje de nuevo.
function mostrarSelectorPlan(mensaje) {
    planDibujadoActual = '';
    const titulo = $('tituloMapa'); if (titulo) titulo.innerText = 'Mapa Curricular';
    $('contenedor-mapa-dinamico').innerHTML = `
        <div class="alert alert-warning text-center w-100 mt-4">
            <p class="fw-bold mb-2">${escapeHTML(mensaje)}</p>
            <p class="small mb-2">Asígnale un plan de estudios; su avance y tutorías se conservan.</p>
            <div class="d-flex justify-content-center gap-2">
                <select id="selectAsignarPlan" class="form-select form-select-sm" style="max-width: 260px;"><option value="">-- Selecciona el Plan --</option></select>
                <button type="button" class="btn btn-sm btn-success fw-bold" onclick="asignarPlanAlumno()">Asignar plan</button>
            </div>
        </div>`;
    fetch('/Home/ObtenerPlanesEstudio').then(leerRespuesta).then(planes => {
        const sel = $('selectAsignarPlan');
        if (sel) planes.forEach(p => sel.add(new Option(p, p)));
    }).catch(e => console.error("Error cargando planes:", e));
}

function asignarPlanAlumno() {
    const sel = $('selectAsignarPlan');
    const plan = sel ? sel.value : '';
    const inputMat = $('inputMatricula');
    const matricula = inputMat ? inputMat.value.trim() : '';
    if (!plan || !matricula) { alert("Selecciona un plan de estudios."); return; }

    // El registro se reescribe a partir de lo guardado en el Excel (no de la pantalla, que no tiene mapa),
    // así no se pierden materias ni tutorías
    esperarGuardadoEnCurso()
        .then(() => fetch(`/Home/BuscarAlumnoExcel?matricula=${encodeURIComponent(matricula)}`))
        .then(leerRespuesta)
        .then(res => {
            if (!res.success) throw new Error(res.message);
            const datos = Object.assign({}, res.data, { planEstudio: plan });
            return fetch('/Home/ExportarExcel', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(datos) });
        })
        .then(leerRespuesta)
        .then(() => buscarAlumno())
        .catch(e => { console.error(e); mostrarAviso("No se pudo asignar el plan. " + (e.message || "")); });
}

function cargarMapaCurricular(planNombre, callback) {
    if (!planNombre || planNombre === "Generico") {
        mostrarSelectorPlan("Este alumno no tiene un plan de estudios asignado.");
        if (callback) setTimeout(callback, 50);
        return;
    }

    if (planDibujadoActual === planNombre) {
        if (callback) setTimeout(callback, 50);
        return;
    }

    $('contenedor-mapa-dinamico').innerHTML = '<div class="text-center w-100 py-5"><div class="spinner-border text-primary" role="status"></div><h5 class="mt-3 text-primary">Cargando Plan de Estudios...</h5></div>';

    fetch(`/Home/ObtenerMapaCurricular?planNombre=${encodeURIComponent(planNombre)}`)
        .then(res => res.json())
        .then(res => {
            if (!res.success) {
                // Plan renombrado/borrado de la carpeta PlanesEstudio: se permite asignar otro
                mostrarSelectorPlan(`No se encontró el plan "${planNombre}". ${res.message || ''}`);
                if (callback) setTimeout(callback, 50);
                return;
            }

            materiasPlanMaestro = res.data;
            aplicarDatosDelPlan(res, planNombre);

            try {
                renderizarMapaVisual();
            } catch (errRender) {
                console.error("Error al dibujar las tarjetas:", errRender);
                alert("Error al dibujar el mapa. Verifica los datos.");
            }

            planDibujadoActual = planNombre;

            // ✨ LA MAGIA OCURRE AQUÍ ✨
            // Sacamos el 'callback' (el proceso de guardar alumno) fuera de la cadena 
            // de promesas usando setTimeout. Esto obliga al navegador a pintar los colores
            // en pantalla PRIMERO, y luego intentar guardar al alumno de forma independiente.
            if (callback) {
                setTimeout(callback, 150);
            }
        })
        .catch(e => {
            // Este catch AHORA SÍ es exclusivo para errores reales de internet/servidor.
            console.error("Error de conexión real:", e);
            planDibujadoActual = '';
            $('contenedor-mapa-dinamico').innerHTML = '<div class="alert alert-danger w-100 text-center mt-4">Error de conexión con el sistema. ¿Está abierta la ventana negra del programa?</div>';
            if (callback) setTimeout(callback, 50);
        });
}

function renderizarMapaVisual() {
    const contenedor = $('contenedor-mapa-dinamico');
    if (!contenedor) return;
    contenedor.innerHTML = '';

    const ordinal = n => n + (n === 1 || n === 3 ? 'er' : n === 2 ? 'do' : n === 7 || n === 10 ? 'mo' : n === 8 ? 'vo' : n === 9 ? 'no' : 'to');
    const clasesArea = { general: 'bg-general', 'básica': 'bg-basica', basica: 'bg-basica', disciplinar: 'bg-disciplinar', terminal: 'bg-terminal' };
    const tipoDe = m => ((m.tipo || m.Tipo) || '').toLowerCase();

    let semestresAgrupados = {};
    materiasPlanMaestro.forEach(m => {
        // Aceptamos tanto 'semestre' (ASP.NET) como 'Semestre'
        let sem = m.semestre || m.Semestre;
        if (!semestresAgrupados[sem]) semestresAgrupados[sem] = [];
        semestresAgrupados[sem].push(m);
    });

    // Misma estructura visual que el mapa anterior (mapa-grid / periodo-columna / periodo-header)
    const grid = document.createElement('div');
    grid.className = 'mapa-grid w-100';

    Object.keys(semestresAgrupados).map(Number).sort((a, b) => a - b).forEach(numSemestre => {
        const materias = semestresAgrupados[numSemestre];
        const soloAfel = materias.every(m => tipoDe(m) === 'afel');
        const tieneAfel = materias.some(m => tipoDe(m) === 'afel');

        const col = document.createElement('div');
        col.className = 'periodo-columna';
        if (tieneAfel) col.id = 'contenedor-afeles';
        // Sirve para resaltar la columna del periodo que cursa el alumno (la de solo AFEL no es un periodo)
        if (!soloAfel) col.setAttribute('data-semestre', numSemestre);

        const header = document.createElement('div');
        header.className = soloAfel ? 'periodo-header bg-success' : 'periodo-header';
        header.innerText = soloAfel ? 'Área Libre (AFEL)' : ordinal(numSemestre) + ' Periodo';
        col.appendChild(header);

        materias.forEach(mat => {
            const nombreMat = mat.nombre || mat.Nombre || "Sin Nombre";
            const creditosMat = mat.creditos || mat.Creditos || 0;
            const tipoMat = tipoDe(mat);
            const areaMat = (mat.area || mat.Area || "").toLowerCase();
            const prerreqMat = mat.prerrequisitos || mat.Prerrequisitos || "";

            // El color sale de la columna Área del Excel (Optativa y AFEL tienen su propio color)
            const card = document.createElement('div');
            card.className = 'materia-card ' + (tipoMat === 'optativa' ? 'bg-optativa'
                : tipoMat === 'afel' ? 'bg-afel shadow-sm'
                : (clasesArea[areaMat] || 'bg-general'));
            card.setAttribute('data-nombre', nombreMat);
            card.setAttribute('data-creditos', creditosMat);

            let prerreqHtml = '';
            if (prerreqMat) {
                card.setAttribute('data-prerequisito', prerreqMat);
                prerreqHtml = `<span class="prerequisito-text">Req: ${escapeHTML(prerreqMat)}</span>`;
                card.classList.add('estado-bloqueada');
            }

            card.innerHTML = `<strong>${escapeHTML(nombreMat)}</strong><br/>(${creditosMat} Cr.)${prerreqHtml}`;
            card.addEventListener('click', manejadorClickTarjeta);
            col.appendChild(card);
        });

        if (tieneAfel) {
            const btnContainer = document.createElement('div');
            btnContainer.className = 'text-center mt-3';
            btnContainer.innerHTML = '<button type="button" class="btn btn-sm btn-outline-success fw-bold w-100 shadow-sm" onclick="agregarAfelExtra()">+ Agregar AFEL</button>';
            col.appendChild(btnContainer);
        }

        grid.appendChild(col);
    });

    // Una columna por semestre del plan (antes eran 8 fijas). La columna de solo AFEL no cuenta,
    // así baja a una segunda fila igual que en el diseño original.
    const columnasSemestre = Math.max(1, grid.querySelectorAll('.periodo-columna').length - grid.querySelectorAll('.periodo-header.bg-success').length);
    grid.style.gridTemplateColumns = `repeat(${columnasSemestre}, 1fr)`;
    grid.style.minWidth = (columnasSemestre * 150) + 'px';

    contenedor.appendChild(grid);
}

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

    const inputMat = $('inputMatricula');
    const selP = $('selectPeriodoGlobal');
    const n = periodoQueCursaAlumno(inputMat ? inputMat.value.trim() : '', selP ? selP.value : '');   // null si la matrícula no trae año
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
    if (bloqueadas > 0) html += `<p class="small text-muted mb-0"><i class="bi bi-lock-fill me-1"></i>${bloqueadas} materia(s) no se sugieren porque les falta aprobar un prerrequisito.</p>`;
    contenedor.innerHTML = html;

    const total = $('totalSugerencia');
    if (total) total.innerText = `Carga sugerida: ${usados} créditos` + (creditosEnCurso > 0 ? ` + ${creditosEnCurso} en curso = ${usados + creditosEnCurso}` : '') + ` (máximo ${maximo})`;
}


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
