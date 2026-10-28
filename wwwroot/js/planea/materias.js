// PlaneaUV · Materias del mapa: ventana de registro de intentos, AFEL extra y dibujo de cada tarjeta.
// Se carga como script clásico (ver Views/Home/Index.cshtml): comparte el ámbito global con los demás archivos de esta carpeta.

let tarjetaActual = null;

let modalBootstrapInstance = null;

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
        alert('Materia bloqueada. ' + (this.getAttribute('data-motivo-bloqueo') || `Debes aprobar primero: "${this.getAttribute('data-prerequisito')}".`));
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
            else if (examen === 'titulo') { alerta.classList.add('alert-danger'); alerta.innerHTML = 'Reprobó Título. <strong>Pasa a 2da Inscripción</strong>, que se registra en el periodo en que la vuelva a cursar.'; }
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

    // Cada periodo guarda un solo registro por materia. La 2.ª inscripción se cursa en un periodo
    // posterior al de la 1.ª: si se registrara en el mismo, sobrescribiría el intento reprobado y se perdería.
    const motivo = motivoInscripcionInvalida(historialArray, index, periodoActual, valInsc);
    if (motivo) {
        alert(motivo);
        return;
    }

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

    historialArray = ordenarIntentos(historialArray);
    tarjetaActual.setAttribute('data-historial', JSON.stringify(historialArray));
    actualizarTarjetaVisual(tarjetaActual, historialArray);

    if (modalBootstrapInstance) modalBootstrapInstance.hide();
    actualizarProgreso();
    marcarCambios();
}

// Revisa que la inscripción elegida tenga sentido con los otros intentos de la materia.
// Devuelve el mensaje para el tutor, o null si se puede guardar.
function motivoInscripcionInvalida(historialArray, indiceActual, periodoActual, tipoInscripcion) {
    const orden = ordenPeriodo(periodoActual);
    const otros = historialArray.filter((_, i) => i !== indiceActual);

    if (tipoInscripcion === 'segunda') {
        const actual = indiceActual !== -1 ? historialArray[indiceActual] : null;
        if (actual && actual.TipoInscripcion === 'primera' && actual.Estado === 'reprobada') {
            return `En ${periodoActual} ya está registrada la 1.ª inscripción como reprobada.\n\n`
                + 'La 2.ª inscripción se cursa en otro periodo: cambia el "Periodo de trabajo" al periodo en que la volvió '
                + 'a inscribir y regístrala ahí. Así se conservan los dos intentos.';
        }
        const primeraPosterior = otros.find(x => x.TipoInscripcion === 'primera' && orden !== null
            && ordenPeriodo(x.Periodo) !== null && ordenPeriodo(x.Periodo) >= orden);
        if (primeraPosterior) {
            return `La 1.ª inscripción está registrada en ${primeraPosterior.Periodo}. `
                + 'La 2.ª inscripción tiene que ser en un periodo posterior.';
        }
    } else if (tipoInscripcion === 'primera') {
        const segundaAnterior = otros.find(x => x.TipoInscripcion === 'segunda' && orden !== null
            && ordenPeriodo(x.Periodo) !== null && ordenPeriodo(x.Periodo) <= orden);
        if (segundaAnterior) {
            return `La 2.ª inscripción está registrada en ${segundaAnterior.Periodo}. `
                + 'La 1.ª inscripción tiene que ser en un periodo anterior.';
        }
    }
    return null;
}

function actualizarTarjetaVisual(tarjeta, historialArray) {
    tarjeta.classList.remove('estado-aprobada', 'estado-reprobada', 'estado-cursando');
    const nombreActual = tarjeta.getAttribute('data-nombre-personalizado') || tarjeta.getAttribute('data-nombre');
    const creditosActual = tarjeta.getAttribute('data-creditos');
    // Se conservan las leyendas "Req:" (prerrequisito y porcentaje de créditos) con su clase; actualizarProgreso decide si se ven
    const leyendas = [...tarjeta.querySelectorAll('.prerequisito-text')];

    let htmlContenido = `<strong>${escapeHTML(nombreActual)}</strong><br/>(${creditosActual} Cr.)`;
    leyendas.forEach(s => { htmlContenido += `<span class="${s.className}">${s.innerHTML}</span>`; });
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
