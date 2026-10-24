// PlaneaUV · Alumnos: buscar, crear, eliminar y listas de alumnos registrados.
// Se carga como script clásico (ver Views/Home/Index.cshtml): comparte el ámbito global con los demás archivos de esta carpeta.

let modalInicioInstance = null;

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
            if (!res.success) {
                alert(res.message);
                // Sin alumno en pantalla (p. ej. se volvió de la tutoría de un alumno que ya no existe): se ofrece elegir otro
                if (!document.querySelector('#contenedor-mapa-dinamico .materia-card') && modalInicioInstance) modalInicioInstance.show();
                return;
            }

            // Se usa la matrícula tal como está guardada: si se escribió en minúsculas, los guardados chocarían
            if (res.data.matricula) { matricula = res.data.matricula; inputMat.value = matricula; }
            if ($('inputNombreAlumno')) $('inputNombreAlumno').value = res.data.nombre;
            if ($('selectSituacionAlumno')) $('selectSituacionAlumno').value = res.data.situacion || 'Activo';
            planActualAlumno = res.data.planEstudio || "";

            // ====== LA MAGIA: CARGAMOS EL MAPA PRIMERO ======
            cargarMapaCurricular(planActualAlumno, function () {

                // --- ESTO SE EJECUTA HASTA QUE EL MAPA ESTÉ COMPLETAMENTE DIBUJADO ---
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
                cargarSituacionAlumno();   // periodo que cursa y rezago los calcula el servidor

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
    huboCambios = false;
    alumnoCargado = '';
    periodosSinInscripcion = [];
    situacionAlumno = null;

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
    if (!FORMATO_MATRICULA.test(mat)) {
        alert("La matrícula solo puede tener letras y números (de 3 a 20 caracteres), por ejemplo S21012345.");
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

function filtrarAlumnosModal() {
    let input = $('inputBuscarAlumnoModal');
    if (!input) return;
    const texto = input.value.toLowerCase();
    const filas = document.querySelectorAll('#listaAlumnosModal .fila-alumno-modal');
    filas.forEach(fila => {
        if (fila.innerText.toLowerCase().includes(texto)) { fila.style.setProperty('display', 'flex', 'important'); } else { fila.style.setProperty('display', 'none', 'important'); }
    });
}
