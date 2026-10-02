// Función global para sanitizar entradas y evitar inyección de código (XSS)
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
let huboCambios = false;

// Variables Globales Históricas
let tutoriasGlobal = [];

// =========================================================
// MAPEO DE ESTADOS
// El <select> usa si/no, pero el historial, el Excel y el backend
// siempre usan aprobada/reprobada/cursando/ninguno.
// =========================================================
function selectAEstado(v) {
    return v === 'si' ? 'aprobada' : v === 'no' ? 'reprobada' : v;
}
function estadoASelect(e) {
    return e === 'aprobada' ? 'si' : e === 'reprobada' ? 'no' : e;
}

// Helper para no repetir getElementById en cada línea
function $(id) {
    return document.getElementById(id);
}

document.addEventListener('DOMContentLoaded', function () {
    try {
        const modalInicioEl = $('modalInicio');
        if (modalInicioEl) {
            modalInicioInstance = new bootstrap.Modal(modalInicioEl);
            setTimeout(() => {
                modalInicioInstance.show();
            }, 150);
        }

        const modalTutEl = $('modalTutorias');
        if (modalTutEl) {
            modalTutoriasInstance = new bootstrap.Modal(modalTutEl);
        }

        const modalReporteEl = $('modalReporteGlobal');
        if (modalReporteEl) {
            modalReporteGlobalInstance = new bootstrap.Modal(modalReporteEl);
        }

        cargarListaAlumnosInicio();
        cargarListaAlumnos();
        cargarAlertas();
        actualizarProgreso();

        const modalEl = $('modalAvance');
        if (modalEl) {
            modalBootstrapInstance = new bootstrap.Modal(modalEl);
        }

        const tarjetas = document.querySelectorAll('.materia-card');

        tarjetas.forEach(tarjeta => {
            tarjeta.addEventListener('click', function (e) {
                if (this.classList.contains('estado-bloqueada')) {
                    alert(`Materia bloqueada. Debes aprobar primero: "${this.getAttribute('data-prerequisito')}".`);
                    return;
                }

                tarjetaActual = this;
                $('modalMateriaNombre').innerText = this.getAttribute('data-nombre');
                $('modalMateriaCreditos').innerText = this.getAttribute('data-creditos') + ' Cr.';

                const divOptativa = $('divOptativa');
                const inputOptativa = $('inputOptativa');

                if (this.classList.contains('bg-optativa')) {
                    if (divOptativa) divOptativa.classList.remove('d-none');
                    if (inputOptativa) inputOptativa.value = this.getAttribute('data-nombre-personalizado') || '';
                } else {
                    if (divOptativa) divOptativa.classList.add('d-none');
                }

                let historialJSON = this.getAttribute('data-historial');
                let historialArray = historialJSON ? JSON.parse(historialJSON) : [];
                let periodoSelect = $('selectPeriodoGlobal');
                let periodoActual = periodoSelect ? periodoSelect.value : '';

                let intentoActual = historialArray.find(x => x.Periodo === periodoActual);
                let ultimoIntento = historialArray.length > 0 ? historialArray[historialArray.length - 1] : null;
                let intentoAprobado = historialArray.find(x => x.Estado === 'aprobada');

                // Desbloquear controles
                if ($('selectInscripcion')) $('selectInscripcion').disabled = false;
                if ($('selectExamen')) $('selectExamen').disabled = false;
                if ($('selectAprobacion')) $('selectAprobacion').disabled = false;
                if ($('checkRiesgo')) $('checkRiesgo').disabled = false;
                if ($('checkRezago')) $('checkRezago').disabled = false;

                let btnGuardar = document.querySelector('#modalAvance .modal-footer .btn-primary');
                if (btnGuardar) btnGuardar.style.display = 'block';

                // =========================================================
                // BLOQUEO: Aprobada en el pasado
                // =========================================================
                if (intentoAprobado && intentoAprobado.Periodo !== periodoActual) {
                    let modalPerVinc = $('modalPeriodoVinculado');
                    if (modalPerVinc) modalPerVinc.innerHTML = `<span class="badge bg-success">Aprobada en: ${escapeHTML(intentoAprobado.Periodo)}</span>`;

                    if ($('selectInscripcion')) $('selectInscripcion').value = intentoAprobado.TipoInscripcion;
                    actualizarOpcionesExamen(intentoAprobado.TipoInscripcion);

                    if ($('selectExamen')) $('selectExamen').value = intentoAprobado.UltimoExamen;
                    if ($('selectAprobacion')) $('selectAprobacion').value = 'si';
                    if ($('checkRiesgo')) $('checkRiesgo').checked = intentoAprobado.EnRiesgo;
                    if ($('checkRezago')) $('checkRezago').checked = intentoAprobado.EnRezago;

                    // Bloqueos UI
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

                // =========================================================
                // LÓGICA NORMAL
                // =========================================================
                let modalPerVinc = $('modalPeriodoVinculado');
                if (modalPerVinc) modalPerVinc.innerText = 'Periodo a registrar: ' + periodoActual;

                if (intentoActual) {
                    if ($('selectInscripcion')) $('selectInscripcion').value = intentoActual.TipoInscripcion;
                    actualizarOpcionesExamen(intentoActual.TipoInscripcion);
                    if ($('selectExamen')) $('selectExamen').value = intentoActual.UltimoExamen;
                    // Convierte aprobada/reprobada -> si/no para el select
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
            });
        });

        const selInsc = $('selectInscripcion');
        if (selInsc) {
            selInsc.addEventListener('change', function () {
                actualizarOpcionesExamen(this.value);
                $('selectAprobacion').value = 'ninguno';
                evaluarLogicaInscripcion();
            });
        }

        const selExamen = $('selectExamen');
        if (selExamen) {
            selExamen.addEventListener('change', evaluarLogicaInscripcion);
        }

        const selAprob = $('selectAprobacion');
        if (selAprob) {
            selAprob.addEventListener('change', evaluarLogicaInscripcion);
        }

        const selPeriodo = $('selectPeriodoGlobal');
        if (selPeriodo) {
            selPeriodo.addEventListener('change', () => {
                cargarTutoriasALaVista();
                marcarCambios();
            });
        }

    } catch (err) {
        console.error("Error iniciando script:", err);
    }
});

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

    evaluarLogicaInscripcion();
    renderizarHistorialUI([], false);
    actualizarTarjetaVisual(tarjetaActual, []);
    huboCambios = true;
    actualizarProgreso();
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

    if ($('selectInscripcion')) {
        $('selectInscripcion').value = nextInsc;
    }
    actualizarOpcionesExamen(nextInsc);

    if ($('selectExamen')) {
        $('selectExamen').value = nextExam;
    }
    if ($('selectAprobacion')) {
        $('selectAprobacion').value = 'ninguno';
    }
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

function marcarCambios() {
    huboCambios = true;
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
        opts = [
            { v: 'ordinario', t: 'Examen Ordinario' },
            { v: 'extraordinario', t: 'Examen Extraordinario' },
            { v: 'titulo', t: 'Examen a Título' }
        ];
    } else {
        opts = [
            { v: 'ordinario', t: 'Examen Ordinario' },
            { v: 'extraordinario', t: 'Examen Extraordinario' },
            { v: 'ultima', t: 'Última Oportunidad' }
        ];
    }

    opts.forEach(o => {
        sel.add(new Option(o.t, o.v));
    });
}

function evaluarLogicaInscripcion() {
    var selInsc = $('selectInscripcion');
    var selExam = $('selectExamen');
    var selAprob = $('selectAprobacion');

    if (!selInsc || !selExam || !selAprob) return;

    var inscripcion = selInsc.value;
    var examen = selExam.value;
    var aprobacion = selAprob.value;
    var alerta = $('alertaEstatus');

    if (!alerta) return;

    alerta.className = 'alert mt-3 py-2 small';

    if (aprobacion === 'ninguno') {
        alerta.classList.add('d-none');
        return;
    }

    alerta.classList.remove('d-none');

    if (aprobacion === 'cursando') {
        alerta.classList.add('alert-info');
        alerta.innerHTML = `<strong>En curso.</strong> Pendiente de calificar.`;
        return;
    }

    if (aprobacion === 'si') {
        alerta.classList.add('alert-success');
        alerta.innerHTML = `<strong>¡Materia Aprobada!</strong>`;
        return;
    }

    if (aprobacion === 'no') {
        if (inscripcion === 'primera') {
            if (examen === 'ordinario') {
                alerta.classList.add('alert-warning');
                alerta.innerHTML = 'Reprobó Ordinario. <strong>Sigue Extra.</strong>';
            } else if (examen === 'extraordinario') {
                alerta.classList.add('alert-warning');
                alerta.innerHTML = 'Reprobó Extra. <strong>Sigue Título.</strong>';
            } else if (examen === 'titulo') {
                alerta.classList.add('alert-danger');
                alerta.innerHTML = 'Reprobó Título. <strong>Pasa a 2da Inscripción.</strong>';
            }
        } else {
            if (examen === 'ordinario') {
                alerta.classList.add('alert-warning');
                alerta.innerHTML = 'Reprobó Ord. de 2da. <strong>Sigue Extra.</strong>';
            } else if (examen === 'extraordinario') {
                alerta.classList.add('alert-warning');
                alerta.innerHTML = 'Reprobó Extra de 2da. <strong>Sigue Última Op.</strong>';
            } else if (examen === 'ultima') {
                alerta.classList.add('alert-danger');
                alerta.innerHTML = '<strong>CANDIDATO A BAJA.</strong> Reprobó Última Oportunidad.';
            }
        }
    }
}

function guardarAvance() {
    let nuevoNombre = tarjetaActual.getAttribute('data-nombre');

    if (tarjetaActual.classList.contains('bg-optativa')) {
        let inputOp = $('inputOptativa');
        // Se guarda el texto tal cual; el escape se hace al renderizar
        let ipt = inputOp ? inputOp.value.trim() : '';
        if (ipt) {
            tarjetaActual.setAttribute('data-nombre-personalizado', ipt);
            nuevoNombre = ipt;
        } else {
            tarjetaActual.removeAttribute('data-nombre-personalizado');
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
            Creditos: parseInt(tarjetaActual.getAttribute('data-creditos')),
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
    huboCambios = true;
    actualizarProgreso();
}

function actualizarTarjetaVisual(tarjeta, historialArray) {
    tarjeta.classList.remove('estado-aprobada', 'estado-reprobada', 'estado-cursando');
    const nombreActual = tarjeta.getAttribute('data-nombre-personalizado') || tarjeta.getAttribute('data-nombre');
    const creditosActual = tarjeta.getAttribute('data-creditos');
    const prereqSpan = tarjeta.querySelector('.prerequisito-text');

    let htmlContenido = `<strong>${escapeHTML(nombreActual)}</strong><br/>(${creditosActual} Cr.)`;
    if (prereqSpan) {
        htmlContenido += `<span class="prerequisito-text">${prereqSpan.innerHTML}</span>`;
    }

    if (historialArray.length === 0) {
        // Limpia atributos de estado para que no queden datos viejos
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

        if (last.Estado === 'cursando') {
            txExm = 'En Curso';
        }

        htmlContenido += `<div class="badge-info-materia">${txInsc} • ${txExm}<br/><span style="font-size: 0.60rem; opacity: 0.85;">${escapeHTML(last.Periodo)}</span></div>`;

        if (last.Estado === "aprobada") {
            htmlContenido += `<div class="badge bg-success text-white mt-1 shadow-sm" style="font-size:0.65rem; width:100%">APROBADA</div>`;
        }
    } else {
        tarjeta.removeAttribute('data-estado');
    }

    tarjeta.setAttribute('data-riesgo', last.EnRiesgo);
    tarjeta.setAttribute('data-rezago', last.EnRezago);

    if (last.EnRiesgo) {
        htmlContenido += `<div class="badge bg-warning text-dark mt-1 shadow-sm" style="font-size:0.65rem; width:100%">EN RIESGO</div>`;
    }
    if (last.EnRezago) {
        htmlContenido += `<div class="badge bg-secondary text-white mt-1 shadow-sm" style="font-size:0.65rem; width:100%">EN REZAGO</div>`;
    }

    tarjeta.innerHTML = htmlContenido;
}

function actualizarProgreso() {
    let creditosAprobados = 0;
    let materiasEnRiesgo = 0;
    let materiasEnCurso = 0;

    document.querySelectorAll('.materia-card').forEach(t => {
        const estado = t.getAttribute('data-estado');
        if (estado === 'aprobada') creditosAprobados += parseInt(t.getAttribute('data-creditos')) || 0;
        else if (estado === 'cursando') materiasEnCurso++;

        if (t.getAttribute('data-riesgo') === 'true') materiasEnRiesgo++;
    });

    let porcentaje = Math.round((creditosAprobados / 441) * 100);

    let txtPorc = $('textoPorcentaje');
    if (txtPorc) txtPorc.innerText = porcentaje + '%';

    let txtCred = $('textoCreditos');
    if (txtCred) txtCred.innerText = creditosAprobados + ' / 441 Cr.';

    let grAv = $('graficaAvance');
    if (grAv) grAv.style.background = `conic-gradient(#28a745 ${porcentaje}%, #e9ecef ${porcentaje}%)`;

    let txRiesgo = $('textoRiesgo');
    if (txRiesgo) txRiesgo.innerText = materiasEnRiesgo;

    let txCurso = $('textoCurso');
    if (txCurso) txCurso.innerText = materiasEnCurso;

    document.querySelectorAll('.materia-card[data-prerequisito]').forEach(tarjeta => {
        let todasAprobadas = true;

        tarjeta.getAttribute('data-prerequisito').split(',').map(p => p.trim()).forEach(prereq => {
            const tPrereq = Array.from(document.querySelectorAll('.materia-card')).find(x => x.getAttribute('data-nombre') === prereq || x.getAttribute('data-nombre-personalizado') === prereq);
            if (tPrereq && tPrereq.getAttribute('data-estado') !== 'aprobada') {
                todasAprobadas = false;
            }
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
    if (huboCambios) {
        return confirm("Tienes cambios sin guardar. Si cambias de alumno perderás ese avance. ¿Continuar?");
    }
    return true;
}

function buscarAlumno(materiaAResaltar = null) {
    let inputMat = $('inputMatricula');
    if (!inputMat) return;

    var matricula = inputMat.value.trim();
    if (!matricula) return;

    fetch(`/Home/BuscarAlumnoExcel?matricula=${encodeURIComponent(matricula)}`)
        .then(response => response.json())
        .then(res => {
            if (!res.success) {
                alert(res.message);
                return;
            }

            if ($('inputNombreAlumno')) {
                $('inputNombreAlumno').value = res.data.nombre;
            }
            if ($('selectSituacionAlumno')) {
                $('selectSituacionAlumno').value = res.data.situacion || 'Activo';
            }

            tutoriasGlobal = (res.data.tutorias || []).map(t => ({
                Periodo: t.periodo,
                Sesion: t.sesion,
                Fecha: t.fecha,
                Asistencia: t.asistencia,
                Comentarios: t.comentarios
            }));

            cargarTutoriasALaVista();

            let materiasAgrupadas = {};
            res.data.materias.forEach(m => {
                let matParseada = {
                    Nombre: m.nombre,
                    Periodo: m.periodo,
                    Creditos: m.creditos,
                    TipoInscripcion: m.tipoInscripcion,
                    UltimoExamen: m.ultimoExamen,
                    // Normaliza datos viejos (si/no) a aprobada/reprobada
                    Estado: selectAEstado(m.estado),
                    EnRiesgo: m.enRiesgo,
                    EnRezago: m.enRezago
                };
                if (!materiasAgrupadas[matParseada.Nombre]) {
                    materiasAgrupadas[matParseada.Nombre] = [];
                }
                materiasAgrupadas[matParseada.Nombre].push(matParseada);
            });

            document.querySelectorAll('.materia-card').forEach(t => {
                let baseName = t.getAttribute('data-nombre');
                let historialArray = materiasAgrupadas[baseName] || [];

                if (t.classList.contains('bg-optativa') && historialArray.length === 0) {
                    for (let k in materiasAgrupadas) {
                        if (!document.querySelector(`.materia-card[data-nombre="${CSS.escape(k)}"]`)) {
                            if (!t.hasAttribute('data-historial')) {
                                historialArray = materiasAgrupadas[k];
                                t.setAttribute('data-nombre-personalizado', k);
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
                    if (t.classList.contains('bg-optativa')) {
                        t.removeAttribute('data-nombre-personalizado');
                    }
                }

                actualizarTarjetaVisual(t, historialArray);
            });

            huboCambios = false;
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
        }).catch(e => {
            console.error(e);
            alert("Error de comunicación.");
        });
}

function exportarAvance() {
    let inputMat = $('inputMatricula');
    if (!inputMat) return;

    var matricula = inputMat.value.trim();
    if (!matricula) {
        alert("Selecciona un alumno antes de guardar.");
        return;
    }

    guardarTutoriasEnMemoria();

    var materiasAExportar = [];
    document.querySelectorAll('.materia-card').forEach(t => {
        let hist = t.getAttribute('data-historial');
        if (hist) {
            materiasAExportar.push(...JSON.parse(hist));
        }
    });

    var alumnoData = {
        Matricula: matricula,
        Nombre: $('inputNombreAlumno') ? $('inputNombreAlumno').value.trim() : '',
        Situacion: $('selectSituacionAlumno') ? $('selectSituacionAlumno').value : 'Activo',
        Tutorias: tutoriasGlobal,
        Materias: materiasAExportar
    };

    fetch('/Home/ExportarExcel', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(alumnoData)
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                alert("¡Cambios guardados exitosamente!");
                huboCambios = false;
                cargarListaAlumnos();
                cargarListaAlumnosInicio();
                cargarAlertas();
            } else {
                alert("Error al procesar datos.");
            }
        }).catch(e => {
            console.error(e);
            alert("Error de servidor.");
        });
}

function exportarAvanceSilencioso(matricula, nombre) {
    let selSit = $('selectSituacionAlumno');
    let situacion = selSit ? selSit.value : 'Activo';

    // Se envía el texto tal cual; el escape se hace al mostrarlo
    let reqBody = {
        Matricula: matricula,
        Nombre: nombre,
        Situacion: situacion,
        Tutorias: [],
        Materias: []
    };

    fetch('/Home/ExportarExcel', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(reqBody)
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                cargarListaAlumnos();
                cargarListaAlumnosInicio();
            } else {
                alert("Error al registrar el alumno.");
            }
        })
        .catch(e => {
            console.error(e);
            alert("Error de servidor.");
        });
}

function cargarListaAlumnos() {
    fetch('/Home/ObtenerAlumnosRegistrados')
        .then(res => res.json())
        .then(alumnos => {
            const ul = $('listaAlumnosRegistrados');
            if (!ul) return;

            ul.innerHTML = '';
            if (alumnos.length === 0) {
                ul.innerHTML = '<li><span class="dropdown-item-text text-muted">Vacio</span></li>';
            } else {
                ul.innerHTML = '<li><h6 class="dropdown-header">Alumnos</h6></li>';
                alumnos.forEach(a => {
                    let li = document.createElement('li');
                    let btn = document.createElement('a');
                    btn.className = 'dropdown-item d-flex justify-content-between';
                    btn.href = '#';

                    let ic = a.situacion === 'Activo' ? '🟢' : (a.situacion === 'Egresado' ? '🎓' : '🔴');
                    btn.innerHTML = `<span><strong>${escapeHTML(a.matricula)}</strong> - ${escapeHTML(a.nombre)}</span> <span>${ic}</span>`;

                    btn.onclick = (e) => {
                        e.preventDefault();
                        if (!puedeCambiarDeContexto()) return;
                        if ($('inputMatricula')) {
                            $('inputMatricula').value = a.matricula;
                        }
                        buscarAlumno();
                    };

                    li.appendChild(btn);
                    ul.appendChild(li);
                });
            }
            ul.insertAdjacentHTML('beforeend', '<li><hr class="dropdown-divider"></li><li><a class="dropdown-item text-success fw-bold" href="#" onclick="abrirModalNuevoAlumno(event)">Agregar alumno</a></li>');
        })
        .catch(e => console.error('Error cargando lista de alumnos:', e));
}

function cargarListaAlumnosInicio() {
    fetch('/Home/ObtenerAlumnosRegistrados')
        .then(res => res.json())
        .then(alumnos => {
            const c = $('listaAlumnosModal');
            if (!c) return;

            c.innerHTML = '';
            alumnos.forEach(a => {
                let btn = document.createElement('button');
                btn.className = 'btn btn-outline-primary text-start fw-bold mb-2 w-100 d-flex justify-content-between';

                let ic = a.situacion === 'Activo' ? '🟢' : (a.situacion === 'Egresado' ? '🎓' : '🔴');
                btn.innerHTML = `<span><i class="bi bi-person-fill"></i> ${escapeHTML(a.matricula)} - ${escapeHTML(a.nombre)}</span> <span>${ic}</span>`;

                btn.onclick = () => {
                    if (!puedeCambiarDeContexto()) return;
                    if ($('inputMatricula')) {
                        $('inputMatricula').value = a.matricula;
                    }
                    buscarAlumno();
                    if (modalInicioInstance) modalInicioInstance.hide();
                };
                c.appendChild(btn);
            });
        })
        .catch(e => console.error('Error cargando alumnos de inicio:', e));
}

function abrirModalNuevoAlumno(e) {
    e.preventDefault();
    if (!puedeCambiarDeContexto()) return;
    if ($('inputBuscarAlumnoModal')) {
        $('inputBuscarAlumnoModal').value = '';
    }
    const b = $('btnCerrarModalInicio');
    if (b) b.classList.remove('d-none');
    if (modalInicioInstance) modalInicioInstance.show();
}

function crearNuevoAlumno() {
    let inputMat = $('inputNuevaMatricula');
    let inputNom = $('inputNuevoNombre');
    if (!inputMat || !inputNom) return;

    const mat = inputMat.value.trim();
    const nom = inputNom.value.trim();
    if (!mat || !nom) {
        alert("Faltan datos.");
        return;
    }
    if (!puedeCambiarDeContexto()) return;

    fetch('/Home/ObtenerAlumnosRegistrados')
        .then(res => res.json())
        .then(alumnos => {
            if (alumnos.some(a => a.matricula.toLowerCase() === mat.toLowerCase())) {
                alert("Matrícula ya registrada.");
                return;
            }
            if ($('inputMatricula')) $('inputMatricula').value = mat;
            if ($('inputNombreAlumno')) $('inputNombreAlumno').value = nom;
            if ($('selectSituacionAlumno')) $('selectSituacionAlumno').value = 'Activo';

            tutoriasGlobal = [];
            cargarTutoriasALaVista();

            document.querySelectorAll('.materia-card').forEach(t => {
                t.removeAttribute('data-historial');
                if (t.classList.contains('bg-optativa')) {
                    t.removeAttribute('data-nombre-personalizado');
                }
                actualizarTarjetaVisual(t, []);
            });

            huboCambios = false;
            actualizarProgreso();
            exportarAvanceSilencioso(mat, nom);
            if (modalInicioInstance) modalInicioInstance.hide();
            inputMat.value = '';
            inputNom.value = '';
        })
        .catch(e => {
            console.error(e);
            alert("Error de comunicación.");
        });
}

function cargarAlertas() {
    const contenedor = $('contenedorTarjetasAlerta');
    if (!contenedor) return;

    contenedor.innerHTML = '<div class="text-center py-4"><div class="spinner-border text-warning" role="status"></div></div>';

    fetch('/Home/ObtenerReporteAlertas')
        .then(response => response.json())
        .then(res => {
            if (!res.success) return;

            const alertas = res.data;
            const contadorBadge = $('contadorAlertas');

            if (alertas.length === 0) {
                if (contadorBadge) contadorBadge.style.display = 'none';
                contenedor.innerHTML = `
                    <div class="alert alert-success text-center mt-2 shadow-sm border-0">
                        Todo en orden. No hay alumnos en riesgo actualmente.
                    </div>`;
                return;
            }

            if (contadorBadge) {
                contadorBadge.innerText = alertas.length;
                contadorBadge.style.display = 'block';
            }

            contenedor.innerHTML = '';

            alertas.forEach(alerta => {
                const tarjeta = document.createElement('div');
                tarjeta.className = 'card border-0 shadow-sm mb-2';

                tarjeta.innerHTML = `
                    <div class="card-body p-3">
                        <div class="d-flex justify-content-between align-items-start mb-2">
                            <span class="badge ${escapeHTML(alerta.badge)} px-2 py-1">${escapeHTML(alerta.mensaje)}</span>
                        </div>
                        <h6 class="card-title fw-bold text-dark mb-1">${escapeHTML(alerta.nombre)}</h6>
                        <p class="card-text text-muted small mb-2">
                            <strong>Matrícula:</strong> ${escapeHTML(alerta.matricula)}<br/>
                            <strong>Materia:</strong> ${escapeHTML(alerta.materia)}
                        </p>
                        <button class="btn btn-sm btn-outline-secondary w-100 fw-bold btn-revisar-mapa">
                            Revisar Mapa
                        </button>
                    </div>
                `;

                // Listener en lugar de onclick inline: evita que nombres con comillas rompan el JS
                tarjeta.querySelector('.btn-revisar-mapa').addEventListener('click', () => {
                    cargarAlumnoDesdeAlerta(alerta.matricula, alerta.nombre, alerta.materia);
                });

                contenedor.appendChild(tarjeta);
            });
        })
        .catch(error => console.error('Error cargando alertas:', error));
}

function cargarAlumnoDesdeAlerta(matricula, nombre, materia) {
    if (!puedeCambiarDeContexto()) return;

    if ($('inputMatricula')) $('inputMatricula').value = matricula;
    if ($('inputNombreAlumno')) $('inputNombreAlumno').value = nombre;
    buscarAlumno(materia);

    const offcanvasEl = $('offcanvasAlertas');
    if (offcanvasEl) {
        const offcanvasInstance = bootstrap.Offcanvas.getInstance(offcanvasEl) || new bootstrap.Offcanvas(offcanvasEl);
        if (offcanvasInstance) offcanvasInstance.hide();
    }
}

function abrirReporteGlobal() {
    if (modalReporteGlobalInstance) modalReporteGlobalInstance.show();
    cargarDatosReporteGlobal();
}

function cargarDatosReporteGlobal() {
    let rRiesgo = $('contenedorReporteRiesgo');
    let rRezago = $('contenedorReporteRezago');
    let rSeriada = $('contenedorReporteSeriadas');

    if (rRiesgo) rRiesgo.innerHTML = '<div class="text-center py-3"><div class="spinner-border text-warning" role="status"></div></div>';
    if (rRezago) rRezago.innerHTML = '<div class="text-center py-3"><div class="spinner-border text-secondary" role="status"></div></div>';
    if (rSeriada) rSeriada.innerHTML = '<div class="text-center py-3"><div class="spinner-border text-danger" role="status"></div></div>';

    fetch('/Home/ObtenerReporteGlobal')
        .then(response => response.json())
        .then(res => {
            if (!res.success) return;

            let htmlRiesgo = renderizarListaAgrupada(res.data.riesgos, "warning");
            if (rRiesgo) rRiesgo.innerHTML = htmlRiesgo || '<p class="text-muted small">No hay alumnos en riesgo.</p>';

            let htmlRezago = renderizarListaAgrupada(res.data.rezagos, "secondary");
            if (rRezago) rRezago.innerHTML = htmlRezago || '<p class="text-muted small">No hay alumnos en rezago.</p>';

            let htmlSeriadas = renderizarListaAgrupada(res.data.seriadas, "danger");
            if (rSeriada) rSeriada.innerHTML = htmlSeriadas || '<p class="text-muted small">No hay adeudos de materias seriadas.</p>';
        })
        .catch(error => console.error('Error cargando reporte global:', error));
}

function renderizarListaAgrupada(arreglo, colorClase) {
    if (!arreglo || arreglo.length === 0) return '';
    let html = '';
    arreglo.forEach(item => {
        let alumnos = Array.from(item.alumnos);
        let listaAlumnosHtml = alumnos.map(a => `<li class="small">${escapeHTML(a.nombre)} (${escapeHTML(a.matricula)})</li>`).join('');
        html += `
            <div class="border rounded mb-2 bg-white p-2 border-${colorClase}">
                <div class="d-flex justify-content-between align-items-center mb-1">
                    <strong class="text-dark" style="font-size: 0.85rem;">${escapeHTML(item.materia)}</strong>
                    <span class="badge bg-${colorClase} rounded-pill">${alumnos.length}</span>
                </div>
                <ul class="list-unstyled mb-0 ms-2 text-muted" style="font-size: 0.8rem;">
                    ${listaAlumnosHtml}
                </ul>
            </div>`;
    });
    return html;
}

function imprimirReporteGlobal() {
    window.print();
}

function descargarWordTutorias() {
    let inputMat = $('inputMatricula');
    var matricula = inputMat ? inputMat.value.trim() : '';

    if (!matricula) {
        alert("Selecciona un alumno primero.");
        return;
    }
    if (huboCambios) {
        alert("⚠️ Tienes cambios o comentarios sin guardar. Haz clic en 'Guardar' antes de exportar el reporte.");
        return;
    }

    window.location.href = `/Home/DescargarWordTutorias?matricula=${encodeURIComponent(matricula)}`;
}

function filtrarAlumnosModal() {
    let input = $('inputBuscarAlumnoModal');
    if (!input) return;

    const texto = input.value.toLowerCase();
    const botones = document.querySelectorAll('#listaAlumnosModal button');

    botones.forEach(btn => {
        if (btn.innerText.toLowerCase().includes(texto)) {
            btn.style.display = 'flex';
        } else {
            btn.style.display = 'none';
        }
    });
}