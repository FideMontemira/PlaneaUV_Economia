let tarjetaActual = null;
let modalBootstrapInstance = null;
let modalInicioInstance = null;
let modalTutoriasInstance = null;
let modalReporteGlobalInstance = null;
let huboCambios = false;

document.addEventListener('DOMContentLoaded', function () {
    const modalInicioEl = document.getElementById('modalInicio');
    if (modalInicioEl) {
        modalInicioInstance = new bootstrap.Modal(modalInicioEl);
        modalInicioInstance.show();
    }

    const modalTutEl = document.getElementById('modalTutorias');
    if (modalTutEl) modalTutoriasInstance = new bootstrap.Modal(modalTutEl);

    const modalReporteEl = document.getElementById('modalReporteGlobal');
    if (modalReporteEl) modalReporteGlobalInstance = new bootstrap.Modal(modalReporteEl);

    cargarListaAlumnosInicio();
    cargarListaAlumnos();
    cargarAlertas();
    actualizarProgreso();

    const modalEl = document.getElementById('modalAvance');
    if (modalEl) modalBootstrapInstance = new bootstrap.Modal(modalEl);

    const tarjetas = document.querySelectorAll('.materia-card');

    tarjetas.forEach(tarjeta => {
        tarjeta.addEventListener('click', function (e) {
            if (this.classList.contains('estado-bloqueada')) {
                const prereq = this.getAttribute('data-prerequisito');
                alert(`Materia bloqueada. Debes aprobar primero su prerrequisito: "${prereq}".`);
                return;
            }

            tarjetaActual = this;
            const nombre = this.getAttribute('data-nombre');
            const creditos = this.getAttribute('data-creditos');
            const esOptativa = this.classList.contains('bg-optativa');

            document.getElementById('modalMateriaNombre').innerText = nombre;
            document.getElementById('modalMateriaCreditos').innerText = creditos;

            const divOptativa = document.getElementById('divOptativa');
            const inputOptativa = document.getElementById('inputOptativa');

            if (esOptativa) {
                divOptativa.classList.remove('d-none');
                inputOptativa.value = this.getAttribute('data-nombre-personalizado') || '';
            } else {
                divOptativa.classList.add('d-none');
            }

            const inscripcionPrevia = this.getAttribute('data-inscripcion') || 'primera';
            const examenPrevio = this.getAttribute('data-examen') || 'ordinario';
            const estadoPrevio = this.getAttribute('data-estado');
            const riesgoPrevio = this.getAttribute('data-riesgo') === 'true';
            const rezagoPrevio = this.getAttribute('data-rezago') === 'true';

            document.getElementById('selectInscripcion').value = inscripcionPrevia;
            actualizarOpcionesExamen(inscripcionPrevia);
            document.getElementById('selectExamen').value = examenPrevio;
            document.getElementById('checkRiesgo').checked = riesgoPrevio;
            document.getElementById('checkRezago').checked = rezagoPrevio;

            if (estadoPrevio === 'aprobada') {
                document.getElementById('selectAprobacion').value = 'si';
            } else if (estadoPrevio === 'reprobada') {
                document.getElementById('selectAprobacion').value = 'no';
            } else if (estadoPrevio === 'cursando') {
                document.getElementById('selectAprobacion').value = 'cursando';
            } else {
                document.getElementById('selectAprobacion').value = 'ninguno';
            }

            evaluarLogicaInscripcion();
            modalBootstrapInstance.show();
        });
    });

    document.getElementById('selectInscripcion').addEventListener('change', function () {
        actualizarOpcionesExamen(this.value);
        document.getElementById('selectAprobacion').value = 'ninguno';
        evaluarLogicaInscripcion();
    });

    document.getElementById('selectExamen').addEventListener('change', evaluarLogicaInscripcion);
    document.getElementById('selectAprobacion').addEventListener('change', evaluarLogicaInscripcion);
});

function abrirModalTutorias() {
    var matricula = document.getElementById('inputMatricula').value.trim();
    if (!matricula) {
        alert("Por favor selecciona un alumno primero.");
        return;
    }
    modalTutoriasInstance.show();
}

function marcarCambios() {
    huboCambios = true;
}


function actualizarProgreso() {
    let creditosTotales = 0;
    let creditosAprobados = 0;
    let materiasEnRiesgo = 0;
    let materiasEnCurso = 0;

    document.querySelectorAll('.materia-card').forEach(t => {
        let cr = parseInt(t.getAttribute('data-creditos')) || 0;
        creditosTotales += cr;

        const estado = t.getAttribute('data-estado');
        if (estado === 'aprobada') {
            creditosAprobados += cr;
        } else if (estado === 'cursando') {
            materiasEnCurso++;
        }

        if (t.getAttribute('data-riesgo') === 'true') {
            materiasEnRiesgo++;
        }
    });

    let porcentaje = creditosTotales === 0 ? 0 : Math.round((creditosAprobados / 441) * 100);

    document.getElementById('textoPorcentaje').innerText = porcentaje + '%';
    document.getElementById('textoCreditos').innerText = creditosAprobados + ' / 441 Cr.';
    document.getElementById('graficaAvance').style.background = `conic-gradient(#28a745 ${porcentaje}%, #e9ecef ${porcentaje}%)`;

    document.getElementById('textoRiesgo').innerText = materiasEnRiesgo;
    document.getElementById('textoCurso').innerText = materiasEnCurso;

    document.querySelectorAll('.materia-card[data-prerequisito]').forEach(tarjeta => {
        const prereqAttr = tarjeta.getAttribute('data-prerequisito');
        const prereqs = prereqAttr.split(',').map(p => p.trim());
        let todasAprobadas = true;

        prereqs.forEach(prereq => {
            const tarjetaPrereq = Array.from(document.querySelectorAll('.materia-card')).find(t =>
                t.getAttribute('data-nombre') === prereq ||
                t.getAttribute('data-nombre-personalizado') === prereq
            );
            if (tarjetaPrereq && tarjetaPrereq.getAttribute('data-estado') !== 'aprobada') {
                todasAprobadas = false;
            }
        });

        const spanTexto = tarjeta.querySelector('.prerequisito-text');

        if (todasAprobadas) {
            if (spanTexto) spanTexto.style.display = 'none';
            tarjeta.classList.remove('estado-bloqueada');
        } else {
            if (spanTexto) spanTexto.style.display = 'block';
            tarjeta.classList.add('estado-bloqueada');
        }
    });
}

function actualizarOpcionesExamen(tipoInscripcion) {
    const selectExamen = document.getElementById('selectExamen');
    selectExamen.innerHTML = '';
    const opcionesPrimera = [
        { val: 'ordinario', text: 'Examen Ordinario' },
        { val: 'extraordinario', text: 'Examen Extraordinario' },
        { val: 'titulo', text: 'Examen a Título de Suficiencia' }
    ];
    const opcionesSegunda = [
        { val: 'ordinario', text: 'Examen Ordinario' },
        { val: 'extraordinario', text: 'Examen Extraordinario' },
        { val: 'ultima', text: 'Examen de Última Oportunidad' }
    ];
    const opcionesMapear = (tipoInscripcion === 'primera') ? opcionesPrimera : opcionesSegunda;
    opcionesMapear.forEach(opc => {
        let nuevaOpcion = new Option(opc.text, opc.val);
        selectExamen.add(nuevaOpcion);
    });
}

function evaluarLogicaInscripcion() {
    var inscripcion = document.getElementById('selectInscripcion').value;
    var examen = document.getElementById('selectExamen').value;
    var aprobacion = document.getElementById('selectAprobacion').value;
    var alerta = document.getElementById('alertaEstatus');

    alerta.className = 'alert mt-3';

    if (aprobacion === 'ninguno') {
        alerta.classList.add('d-none');
        return;
    }

    alerta.classList.remove('d-none');

    if (aprobacion === 'cursando') {
        alerta.classList.add('alert-info');
        alerta.innerHTML = `<strong>Semestre en curso.</strong> Pendiente de calificación final.`;
        return;
    }

    if (aprobacion === 'si') {
        alerta.classList.add('alert-success');
        alerta.innerHTML = `<strong>Materia Aprobada!</strong>`;
        return;
    }

    if (aprobacion === 'no') {
        if (inscripcion === 'primera') {
            if (examen === 'ordinario') {
                alerta.classList.add('alert-warning');
                alerta.innerHTML = 'Reprobó Ordinario. <strong>Debe presentar Extraordinario.</strong>';
            } else if (examen === 'extraordinario') {
                alerta.classList.add('alert-warning');
                alerta.innerHTML = 'Reprobó Extraordinario. <strong>Debe presentar Título.</strong>';
            } else if (examen === 'titulo') {
                alerta.classList.add('alert-danger');
                alerta.innerHTML = 'Reprobó Título. <strong>Pasa a Segunda Inscripción.</strong>';
            }
        } else if (inscripcion === 'segunda') {
            if (examen === 'ordinario') {
                alerta.classList.add('alert-warning');
                alerta.innerHTML = 'Reprobó Ordinario de 2da. <strong>Debe presentar Extraordinario.</strong>';
            } else if (examen === 'extraordinario') {
                alerta.classList.add('alert-warning');
                alerta.innerHTML = 'Reprobó Extra de 2da. <strong>Debe presentar Última Oportunidad.</strong>';
            } else if (examen === 'ultima') {
                alerta.classList.add('alert-danger');
                alerta.innerHTML = '<strong>CANDIDATO A BAJA.</strong> Reprobó Última Oportunidad.';
            }
        }
    }
}

function guardarAvance() {
    if (tarjetaActual && tarjetaActual.classList.contains('bg-optativa')) {
        const nuevoNombre = document.getElementById('inputOptativa').value.trim();
        if (nuevoNombre) {
            tarjetaActual.setAttribute('data-nombre-personalizado', nuevoNombre);
            tarjetaActual.setAttribute('data-nombre', nuevoNombre);
        }
    }

    var inscripcion = document.getElementById('selectInscripcion').value;
    var examen = document.getElementById('selectExamen').value;
    var aprobacion = document.getElementById('selectAprobacion').value;
    var enRiesgo = document.getElementById('checkRiesgo').checked;
    var enRezago = document.getElementById('checkRezago').checked;

    if (aprobacion === 'ninguno' && !enRiesgo && !enRezago) {
        alert("Por favor seleccione el estado de la materia o marque riesgo/rezago.");
        return;
    }

    tarjetaActual.classList.remove('estado-aprobada', 'estado-reprobada', 'estado-cursando');

    if (aprobacion === "si") {
        tarjetaActual.classList.add('estado-aprobada');
        tarjetaActual.setAttribute('data-estado', 'aprobada');
        enRiesgo = false;
        enRezago = false;
    } else if (aprobacion === "no") {
        tarjetaActual.classList.add('estado-reprobada');
        tarjetaActual.setAttribute('data-estado', 'reprobada');
    } else if (aprobacion === "cursando") {
        tarjetaActual.classList.add('estado-cursando');
        tarjetaActual.setAttribute('data-estado', 'cursando');
    }

    if (enRiesgo) tarjetaActual.setAttribute('data-riesgo', 'true');
    else tarjetaActual.removeAttribute('data-riesgo');

    if (enRezago) tarjetaActual.setAttribute('data-rezago', 'true');
    else tarjetaActual.removeAttribute('data-rezago');

    tarjetaActual.setAttribute('data-inscripcion', inscripcion);
    tarjetaActual.setAttribute('data-examen', examen);

    const nombreActual = tarjetaActual.getAttribute('data-nombre-personalizado') || tarjetaActual.getAttribute('data-nombre');
    const creditosActual = tarjetaActual.getAttribute('data-creditos');

    const prereqSpan = tarjetaActual.querySelector('.prerequisito-text');
    let htmlContenido = `<strong>${nombreActual}</strong><br/>(${creditosActual} Cr.)`;

    if (aprobacion !== 'ninguno') {
        const textoInscripcionCorto = (inscripcion === 'primera') ? '1ª Insc.' : '2ª Insc.';
        const textosExamenes = { 'ordinario': 'Ord.', 'extraordinario': 'Ext.', 'titulo': 'Título', 'ultima': 'Últ. Op.' };
        let textoExamenCorto = textosExamenes[examen] || examen;
        if (aprobacion === 'cursando') textoExamenCorto = 'En Curso';

        htmlContenido += `<div class="badge-info-materia">${textoInscripcionCorto} • ${textoExamenCorto}</div>`;
    }

    if (prereqSpan) htmlContenido += `<span class="prerequisito-text">${prereqSpan.innerHTML}</span>`;

    if (aprobacion === "si") {
        htmlContenido += `<div class="badge bg-success text-white mt-1 shadow-sm" style="font-size:0.65rem; width:100%">APROBADA</div>`;
    }
    if (enRiesgo) {
        htmlContenido += `<div class="badge bg-warning text-dark mt-1 shadow-sm" style="font-size:0.65rem; width:100%">EN RIESGO</div>`;
    }
    if (enRezago) {
        htmlContenido += `<div class="badge bg-secondary text-white mt-1 shadow-sm" style="font-size:0.65rem; width:100%">EN REZAGO</div>`;
    }

    tarjetaActual.innerHTML = htmlContenido;
    modalBootstrapInstance.hide();

    huboCambios = true;
    actualizarProgreso();
}

function puedeCambiarDeContexto() {
    if (huboCambios) {
        return confirm("Tienes calificaciones o tutorías sin guardar. Si cambias de alumno ahora, perderás ese avance. ¿Deseas continuar sin guardar?");
    }
    return true;
}

function filtrarAlumnosModal() {
    const texto = document.getElementById('inputBuscarAlumnoModal').value.toLowerCase();
    const botones = document.querySelectorAll('#listaAlumnosModal button');
    botones.forEach(btn => {
        if (btn.innerText.toLowerCase().includes(texto)) btn.style.display = 'block';
        else btn.style.display = 'none';
    });
}

function buscarAlumno(materiaAResaltar = null) {
    var matricula = document.getElementById('inputMatricula').value.trim();
    if (!matricula) return;

    fetch(`/Home/BuscarAlumnoExcel?matricula=${matricula}`)
        .then(response => response.json())
        .then(res => {
            if (!res.success) {
                alert(res.message);
                return;
            }

            document.getElementById('inputNombreAlumno').value = res.data.nombre;

            document.getElementById('tutoria1').value = res.data.tutoria1 || '';
            document.getElementById('fechaTutoria1').value = res.data.fechaTutoria1 || '';
            document.getElementById('asistenciaTutoria1').value = res.data.asistenciaTutoria1 || 'pendiente';

            document.getElementById('tutoria2').value = res.data.tutoria2 || '';
            document.getElementById('fechaTutoria2').value = res.data.fechaTutoria2 || '';
            document.getElementById('asistenciaTutoria2').value = res.data.asistenciaTutoria2 || 'pendiente';

            document.getElementById('tutoria3').value = res.data.tutoria3 || '';
            document.getElementById('fechaTutoria3').value = res.data.fechaTutoria3 || '';
            document.getElementById('asistenciaTutoria3').value = res.data.asistenciaTutoria3 || 'pendiente';

            document.querySelectorAll('.materia-card').forEach(t => {
                t.classList.remove('estado-aprobada', 'estado-reprobada', 'estado-cursando', 'resaltado-alerta', 'estado-bloqueada');
                t.removeAttribute('data-estado');
                t.removeAttribute('data-inscripcion');
                t.removeAttribute('data-examen');
                t.removeAttribute('data-riesgo');
                t.removeAttribute('data-rezago');

                const baseName = t.getAttribute('data-nombre');
                const creds = t.getAttribute('data-creditos');
                const prereqSpan = t.querySelector('.prerequisito-text');

                if (t.classList.contains('bg-optativa')) {
                    t.removeAttribute('data-nombre-personalizado');
                    t.setAttribute('data-nombre', 'Optativa');
                    t.innerHTML = `<strong>Optativa</strong><br/>(${creds} Cr.)`;
                } else {
                    let htmlContenido = `<strong>${baseName}</strong><br/>(${creds} Cr.)`;
                    if (prereqSpan) htmlContenido += `<span class="prerequisito-text">${prereqSpan.innerHTML}</span>`;
                    t.innerHTML = htmlContenido;
                }
            });

            res.data.materias.forEach(mat => {
                let tarjeta = Array.from(document.querySelectorAll('.materia-card')).find(t =>
                    t.getAttribute('data-nombre') === mat.nombre ||
                    t.getAttribute('data-nombre-personalizado') === mat.nombre
                );

                if (!tarjeta) {
                    tarjeta = Array.from(document.querySelectorAll('.materia-card.bg-optativa')).find(t => !t.hasAttribute('data-estado'));
                    if (tarjeta) {
                        tarjeta.setAttribute('data-nombre-personalizado', mat.nombre);
                        tarjeta.setAttribute('data-nombre', mat.nombre);
                    }
                }

                if (tarjeta) {
                    tarjeta.setAttribute('data-inscripcion', mat.tipoInscripcion);
                    tarjeta.setAttribute('data-examen', mat.ultimoExamen);
                    tarjeta.setAttribute('data-estado', mat.estado);

                    if (mat.enRiesgo) tarjeta.setAttribute('data-riesgo', 'true');
                    if (mat.enRezago) tarjeta.setAttribute('data-rezago', 'true');

                    if (mat.estado === 'aprobada') tarjeta.classList.add('estado-aprobada');
                    if (mat.estado === 'reprobada') tarjeta.classList.add('estado-reprobada');
                    if (mat.estado === 'cursando') tarjeta.classList.add('estado-cursando');

                    const creds = tarjeta.getAttribute('data-creditos');
                    let htmlContenido = `<strong>${mat.nombre}</strong><br/>(${creds} Cr.)`;

                    if (mat.estado && mat.estado !== 'ninguno') {
                        const textoInscripcionCorto = (mat.tipoInscripcion === 'primera') ? '1ª Insc.' : '2ª Insc.';
                        const textosExamenes = { 'ordinario': 'Ord.', 'extraordinario': 'Ext.', 'titulo': 'Título', 'ultima': 'Últ. Op.' };
                        let textoExamenCorto = textosExamenes[mat.ultimoExamen] || mat.ultimoExamen;
                        if (mat.estado === 'cursando') textoExamenCorto = 'En Curso';

                        htmlContenido += `<div class="badge-info-materia">${textoInscripcionCorto} • ${textoExamenCorto}</div>`;
                    }

                    const prereqSpan = tarjeta.querySelector('.prerequisito-text');
                    if (prereqSpan) htmlContenido += `<span class="prerequisito-text">${prereqSpan.innerHTML}</span>`;

                    if (mat.estado === 'aprobada') {
                        htmlContenido += `<div class="badge bg-success text-white mt-1 shadow-sm" style="font-size:0.65rem; width:100%">APROBADA</div>`;
                    }
                    if (mat.enRiesgo || tarjeta.getAttribute('data-riesgo') === 'true') {
                        htmlContenido += `<div class="badge bg-warning text-dark mt-1 shadow-sm" style="font-size:0.65rem; width:100%">EN RIESGO</div>`;
                    }
                    if (mat.enRezago || tarjeta.getAttribute('data-rezago') === 'true') {
                        htmlContenido += `<div class="badge bg-secondary text-white mt-1 shadow-sm" style="font-size:0.65rem; width:100%">EN REZAGO</div>`;
                    }

                    tarjeta.innerHTML = htmlContenido;
                }
            });

            huboCambios = false;
            actualizarProgreso();

            if (materiaAResaltar) {
                setTimeout(() => {
                    const tarjetaAnimar = Array.from(document.querySelectorAll('.materia-card')).find(t =>
                        t.getAttribute('data-nombre') === materiaAResaltar ||
                        t.getAttribute('data-nombre-personalizado') === materiaAResaltar
                    );
                    if (tarjetaAnimar) {
                        tarjetaAnimar.scrollIntoView({ behavior: 'smooth', block: 'center' });
                        tarjetaAnimar.classList.add('resaltado-alerta');
                        setTimeout(() => tarjetaAnimar.classList.remove('resaltado-alerta'), 3000);
                    }
                }, 300);
            }
        })
        .catch(error => { console.error('Error:', error); alert("Error de comunicación."); });
}

function exportarAvance() {
    var matricula = document.getElementById('inputMatricula').value.trim();
    var nombre = document.getElementById('inputNombreAlumno').value.trim();

    if (!matricula || !nombre) {
        alert("Por favor selecciona un alumno del menú de inicio antes de guardar.");
        return;
    }

    var materiasCursadas = [];
    document.querySelectorAll('.materia-card').forEach(t => {
        const estado = t.getAttribute('data-estado');
        const riesgo = t.getAttribute('data-riesgo') === 'true';
        const rezago = t.getAttribute('data-rezago') === 'true';

        if (estado || riesgo || rezago) {
            materiasCursadas.push({
                Nombre: t.getAttribute('data-nombre') || t.getAttribute('data-nombre-personalizado'),
                Creditos: parseInt(t.getAttribute('data-creditos')),
                TipoInscripcion: t.getAttribute('data-inscripcion') || 'primera',
                UltimoExamen: t.getAttribute('data-examen') || 'ordinario',
                Estado: estado || 'ninguno',
                EnRiesgo: riesgo,
                EnRezago: rezago
            });
        }
    });

    var alumnoAvance = {
        Matricula: matricula,
        Nombre: nombre,
        Tutoria1: document.getElementById('tutoria1').value.trim(),
        FechaTutoria1: document.getElementById('fechaTutoria1').value,
        AsistenciaTutoria1: document.getElementById('asistenciaTutoria1').value,
        Tutoria2: document.getElementById('tutoria2').value.trim(),
        FechaTutoria2: document.getElementById('fechaTutoria2').value,
        AsistenciaTutoria2: document.getElementById('asistenciaTutoria2').value,
        Tutoria3: document.getElementById('tutoria3').value.trim(),
        FechaTutoria3: document.getElementById('fechaTutoria3').value,
        AsistenciaTutoria3: document.getElementById('asistenciaTutoria3').value,
        Materias: materiasCursadas
    };

    fetch('/Home/ExportarExcel', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(alumnoAvance)
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                alert("Cambios guardados exitosamente en el servidor!");
                huboCambios = false;
                cargarListaAlumnos();
                cargarListaAlumnosInicio();
                cargarAlertas();
            } else {
                alert("Error al procesar los datos.");
            }
        })
        .catch(error => { console.error('Error:', error); alert("Error de comunicación con el servidor."); });
}

function cargarListaAlumnos() {
    fetch('/Home/ObtenerAlumnosRegistrados')
        .then(response => response.json())
        .then(alumnos => {
            const ul = document.getElementById('listaAlumnosRegistrados');
            ul.innerHTML = '';
            if (alumnos.length === 0) {
                ul.innerHTML = '<li><span class="dropdown-item-text text-muted">No hay alumnos guardados</span></li>';
            } else {
                ul.innerHTML = '<li><h6 class="dropdown-header">Seleccionar Alumno</h6></li>';
                alumnos.forEach(alum => {
                    const li = document.createElement('li');
                    const a = document.createElement('a');
                    a.className = 'dropdown-item';
                    a.href = '#';
                    a.innerHTML = `<strong>${alum.matricula}</strong> - ${alum.nombre}`;
                    a.onclick = (e) => {
                        e.preventDefault();
                        if (!puedeCambiarDeContexto()) return;
                        document.getElementById('inputMatricula').value = alum.matricula;
                        document.getElementById('inputNombreAlumno').value = alum.nombre;
                        buscarAlumno();
                    };
                    li.appendChild(a);
                    ul.appendChild(li);
                });
            }
            const divider = document.createElement('li');
            divider.innerHTML = '<hr class="dropdown-divider">';
            ul.appendChild(divider);
            const liNuevo = document.createElement('li');
            const aNuevo = document.createElement('a');
            aNuevo.className = 'dropdown-item text-success fw-bold';
            aNuevo.href = '#';
            aNuevo.innerHTML = 'Agregar alumno';
            aNuevo.onclick = (e) => {
                e.preventDefault();
                if (!puedeCambiarDeContexto()) return;
                document.getElementById('inputBuscarAlumnoModal').value = '';
                filtrarAlumnosModal();
                modalInicioInstance.show();
            };
            liNuevo.appendChild(aNuevo);
            ul.appendChild(liNuevo);
        })
        .catch(error => console.error('Error cargando alumnos:', error));
}

function cargarListaAlumnosInicio() {
    fetch('/Home/ObtenerAlumnosRegistrados')
        .then(response => response.json())
        .then(alumnos => {
            const contenedor = document.getElementById('listaAlumnosModal');
            contenedor.innerHTML = '';
            if (alumnos.length === 0) {
                contenedor.innerHTML = '<p class="text-warning fw-bold">No hay alumnos registrados aún.</p>';
                return;
            }
            alumnos.forEach(alum => {
                const btn = document.createElement('button');
                btn.className = 'btn btn-outline-primary text-start fw-bold shadow-sm mb-2 w-100';
                btn.innerHTML = `${alum.matricula} - ${alum.nombre}`;
                btn.onclick = () => {
                    if (!puedeCambiarDeContexto()) return;
                    document.getElementById('inputMatricula').value = alum.matricula;
                    document.getElementById('inputNombreAlumno').value = alum.nombre;
                    buscarAlumno();
                    modalInicioInstance.hide();
                };
                contenedor.appendChild(btn);
            });
        })
        .catch(error => {
            console.error('Error cargando alumnos:', error);
            document.getElementById('listaAlumnosModal').innerHTML = '<p class="text-danger">Error al cargar la lista.</p>';
        });
}

function crearNuevoAlumno() {
    const mat = document.getElementById('inputNuevaMatricula').value.trim();
    const nom = document.getElementById('inputNuevoNombre').value.trim();
    if (!mat || !nom) {
        alert("Debes ingresar la matrícula y el nombre para registrar a un alumno nuevo.");
        return;
    }
    if (!puedeCambiarDeContexto()) return;

    fetch('/Home/ObtenerAlumnosRegistrados')
        .then(response => response.json())
        .then(alumnos => {
            const existe = alumnos.some(a => a.matricula.toLowerCase() === mat.toLowerCase());
            if (existe) {
                alert("Esta matrícula ya está registrada. Por favor, búscala en la lista superior.");
            } else {
                document.querySelectorAll('.materia-card').forEach(t => {
                    t.classList.remove('estado-aprobada', 'estado-reprobada', 'estado-cursando', 'resaltado-alerta', 'estado-bloqueada');
                    t.removeAttribute('data-estado');
                    t.removeAttribute('data-inscripcion');
                    t.removeAttribute('data-examen');
                    t.removeAttribute('data-riesgo');
                    t.removeAttribute('data-rezago');
                    const baseName = t.getAttribute('data-nombre');
                    const creds = t.getAttribute('data-creditos');
                    const prereqSpan = t.querySelector('.prerequisito-text');

                    if (t.classList.contains('bg-optativa')) {
                        t.removeAttribute('data-nombre-personalizado');
                        t.setAttribute('data-nombre', 'Optativa');
                        t.innerHTML = `<strong>Optativa</strong><br/>(${creds} Cr.)`;
                    } else {
                        let htmlContenido = `<strong>${baseName}</strong><br/>(${creds} Cr.)`;
                        if (prereqSpan) htmlContenido += `<span class="prerequisito-text">${prereqSpan.innerHTML}</span>`;
                        t.innerHTML = htmlContenido;
                    }
                });

                document.getElementById('inputMatricula').value = mat;
                document.getElementById('inputNombreAlumno').value = nom;

                document.getElementById('tutoria1').value = '';
                document.getElementById('fechaTutoria1').value = '';
                document.getElementById('asistenciaTutoria1').value = 'pendiente';

                document.getElementById('tutoria2').value = '';
                document.getElementById('fechaTutoria2').value = '';
                document.getElementById('asistenciaTutoria2').value = 'pendiente';

                document.getElementById('tutoria3').value = '';
                document.getElementById('fechaTutoria3').value = '';
                document.getElementById('asistenciaTutoria3').value = 'pendiente';

                huboCambios = false;
                actualizarProgreso();
                exportarAvanceSilencioso(mat, nom);
                modalInicioInstance.hide();
                document.getElementById('inputNuevaMatricula').value = '';
                document.getElementById('inputNuevoNombre').value = '';
            }
        });
}

function exportarAvanceSilencioso(matricula, nombre) {
    var alumnoData = { Matricula: matricula, Nombre: nombre, Materias: [] };
    fetch('/Home/ExportarExcel', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(alumnoData)
    })
        .then(() => {
            cargarListaAlumnos();
            cargarListaAlumnosInicio();
        })
        .catch(e => console.error(e));
}

function cargarAlertas() {
    const contenedor = document.getElementById('contenedorTarjetasAlerta');
    contenedor.innerHTML = '<div class="text-center py-4"><div class="spinner-border text-warning" role="status"></div></div>';

    fetch('/Home/ObtenerReporteAlertas')
        .then(response => response.json())
        .then(res => {
            if (!res.success) return;

            const alertas = res.data;
            const contadorBadge = document.getElementById('contadorAlertas');

            if (alertas.length === 0) {
                contadorBadge.style.display = 'none';
                contenedor.innerHTML = `
                    <div class="alert alert-success text-center mt-2 shadow-sm border-0">
                        Todo en orden. No hay alumnos en riesgo actualmente.
                    </div>`;
                return;
            }

            contadorBadge.innerText = alertas.length;
            contadorBadge.style.display = 'block';
            contenedor.innerHTML = '';

            alertas.forEach(alerta => {
                const tarjeta = document.createElement('div');
                tarjeta.className = 'card border-0 shadow-sm mb-2';
                tarjeta.innerHTML = `
                    <div class="card-body p-3">
                        <div class="d-flex justify-content-between align-items-start mb-2">
                            <span class="badge ${alerta.badge} px-2 py-1">${alerta.mensaje}</span>
                        </div>
                        <h6 class="card-title fw-bold text-dark mb-1">${alerta.nombre}</h6>
                        <p class="card-text text-muted small mb-2">
                            <strong>Matrícula:</strong> ${alerta.matricula}<br/>
                            <strong>Materia:</strong> ${alerta.materia}
                        </p>
                        <button class="btn btn-sm btn-outline-secondary w-100 fw-bold" onclick="cargarAlumnoDesdeAlerta('${alerta.matricula}', '${alerta.nombre}', '${alerta.materia}')">
                            Revisar Mapa
                        </button>
                    </div>
                `;
                contenedor.appendChild(tarjeta);
            });
        })
        .catch(error => console.error('Error cargando alertas:', error));
}

function cargarAlumnoDesdeAlerta(matricula, nombre, materia) {
    if (!puedeCambiarDeContexto()) return;

    document.getElementById('inputMatricula').value = matricula;
    document.getElementById('inputNombreAlumno').value = nombre;
    buscarAlumno(materia);

    const offcanvasEl = document.getElementById('offcanvasAlertas');
    const offcanvasInstance = bootstrap.Offcanvas.getInstance(offcanvasEl) || new bootstrap.Offcanvas(offcanvasEl);
    offcanvasInstance.hide();
}

function abrirReporteGlobal() {
    modalReporteGlobalInstance.show();
    cargarDatosReporteGlobal();
}

function cargarDatosReporteGlobal() {
    document.getElementById('contenedorReporteRiesgo').innerHTML = '<div class="text-center py-3"><div class="spinner-border text-warning" role="status"></div></div>';
    document.getElementById('contenedorReporteRezago').innerHTML = '<div class="text-center py-3"><div class="spinner-border text-secondary" role="status"></div></div>';
    document.getElementById('contenedorReporteSeriadas').innerHTML = '<div class="text-center py-3"><div class="spinner-border text-danger" role="status"></div></div>';

    fetch('/Home/ObtenerReporteGlobal')
        .then(response => response.json())
        .then(res => {
            if (!res.success) return;

            let htmlRiesgo = renderizarListaAgrupada(res.data.riesgos, "warning");
            document.getElementById('contenedorReporteRiesgo').innerHTML = htmlRiesgo || '<p class="text-muted small">No hay alumnos en riesgo.</p>';

            let htmlRezago = renderizarListaAgrupada(res.data.rezagos, "secondary");
            document.getElementById('contenedorReporteRezago').innerHTML = htmlRezago || '<p class="text-muted small">No hay alumnos en rezago.</p>';

            let htmlSeriadas = renderizarListaAgrupada(res.data.seriadas, "danger");
            document.getElementById('contenedorReporteSeriadas').innerHTML = htmlSeriadas || '<p class="text-muted small">No hay adeudos de materias seriadas.</p>';
        })
        .catch(error => console.error('Error cargando reporte global:', error));
}

function renderizarListaAgrupada(arreglo, colorClase) {
    if (!arreglo || arreglo.length === 0) return '';
    let html = '';
    arreglo.forEach(item => {
        let listaAlumnosHtml = item.alumnos.map(a => `<li class="small">${a.nombre} (${a.matricula})</li>`).join('');
        html += `
            <div class="border rounded mb-2 bg-white p-2 border-${colorClase}">
                <div class="d-flex justify-content-between align-items-center mb-1">
                    <strong class="text-dark" style="font-size: 0.85rem;">${item.materia}</strong>
                    <span class="badge bg-${colorClase} rounded-pill">${item.alumnos.length}</span>
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
    var matricula = document.getElementById('inputMatricula').value.trim();
    if (!matricula) {
        alert("Selecciona un alumno primero.");
        return;
    }
    if (huboCambios) {
        alert("⚠️ Tienes cambios o comentarios sin guardar. Haz clic en 'Guardar' (Botón verde superior) antes de exportar el reporte para que aparezca la información más reciente.");
        return;
    }

    // Si todo está guardado, solicitamos el archivo al backend
    window.location.href = `/Home/DescargarWordTutorias?matricula=${matricula}`;
}