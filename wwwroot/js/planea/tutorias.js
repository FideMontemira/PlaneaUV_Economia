// PlaneaUV · Tutorías: reporte guiado por sesión en 4 pasos y reporte en Word.
// Se usa en la pantalla de tutoría (Views/Home/Tutoria.cshtml, junto con tutoria-pagina.js, que carga al alumno
// y guarda). Es un script clásico: comparte el ámbito global con los demás archivos de esta carpeta.
//
//   1. La sesión (fecha, asistencia, modalidad)
//   2. Valoración del semáforo que calculó el sistema (confirma / es mayor / no hay riesgo con motivo)
//   3. Lo que se trató: temas y problemáticas por categoría (las personales y de salud son confidenciales)
//   4. Canalización, acuerdo principal con fecha de revisión y estado del caso
//
// Las opciones de las casillas NO están aquí: las da el servidor (Services/CatalogoTutorias.cs), que también las
// imprime en el Word. El semáforo, las señales y los temas sugeridos los calcula el servidor (ReglasSemaforo.cs).

// Sesiones de tutoría del alumno cargado (todas sus sesiones de todos los periodos)
let tutoriasGlobal = [];

const SESIONES_POR_PERIODO = 3;
const NOMBRES_SESION = ['Primera sesión', 'Segunda sesión', 'Tercera sesión'];

// Catálogo recibido del servidor y si el formulario ya se armó. Mientras no esté armado, guardar NO debe
// tocar las tutorías (si no, se guardarían vacías y se perdería lo registrado).
let catalogoTutorias = null;
let formularioTutoriasListo = false;

// Color que marcaba el sistema cuando el tutor hizo su valoración en cada sesión (se guarda junto con ella)
let semaforoAlValorar = {};

function cargarCatalogoTutorias() {
    return fetch('/Home/ObtenerCatalogoTutorias')
        .then(leerRespuesta)
        .then(catalogo => {
            catalogoTutorias = catalogo;
            armarFormularioTutorias();
            cargarTutoriasALaVista();
        })
        .catch(e => {
            console.error('Error cargando el catálogo de tutorías:', e);
            const c = $('contenidoTutorias');
            if (c) c.innerHTML = `<div class="alert alert-danger mb-0">No se pudo cargar el formato de tutoría. ${escapeHTML(e.message)}</div>`;
        });
}

// Casillas de un grupo (temas, situaciones o canalizaciones) en columnas
function casillasTutoria(sesion, grupo, opciones, sufijo = '') {
    return `<div class="row row-cols-1 row-cols-md-2 g-1">${opciones.map((op, i) => `
        <div class="col"><div class="form-check">
            <input class="form-check-input" type="checkbox" id="tut-${grupo}${sufijo}-${sesion}-${i}" data-grupo="${grupo}" data-valor="${escapeHTML(op)}" onchange="marcarCambios()">
            <label class="form-check-label small" for="tut-${grupo}${sufijo}-${sesion}-${i}">${escapeHTML(op)}</label>
        </div></div>`).join('')}</div>`;
}

const tituloPaso = (numero, texto, ayuda = '') =>
    `<div class="paso-tutoria-titulo"><span class="paso-numero">${numero}</span>${texto}${ayuda ? `<span class="small text-muted fw-normal ms-1">${ayuda}</span>` : ''}</div>`;

function armarFormularioTutorias() {
    const tabs = $('tabsTutorias'), contenido = $('contenidoTutorias');
    if (!tabs || !contenido || !catalogoTutorias) return;
    tabs.innerHTML = '';
    contenido.innerHTML = '';

    for (let n = 1; n <= SESIONES_POR_PERIODO; n++) {
        const activa = n === 1;
        tabs.insertAdjacentHTML('beforeend', `
            <li class="nav-item" role="presentation">
                <button class="nav-link fw-bold ${activa ? 'active' : ''}" id="tab-tutoria-${n}" data-bs-toggle="tab" data-bs-target="#panelTutoria${n}" type="button" role="tab">
                    ${NOMBRES_SESION[n - 1]} <span class="badge rounded-pill ms-1" id="estadoTabTutoria${n}"></span>
                </button>
            </li>`);

        // Problemáticas por categoría; la académica incluye la materia con dificultad
        const categorias = catalogoTutorias.problematicas.map((c, i) => `
            <div class="col-lg-6">
                <div class="border rounded p-2 h-100 ${c.privada ? 'categoria-privada' : ''}">
                    <div class="small fw-bold mb-1"><i class="bi ${escapeHTML(c.icono)} me-1"></i>${escapeHTML(c.nombre)}
                        ${c.privada ? '<span class="badge bg-light text-secondary border ms-1" title="Solo lo ven el tutor y la coordinación de tutorías"><i class="bi bi-lock-fill"></i> Confidencial</span>' : ''}</div>
                    ${casillasTutoria(n, 'situaciones', c.opciones, '-c' + i)}
                    ${i === 0 ? `<select class="form-select form-select-sm mt-2" id="materiaDificultadTutoria${n}" onchange="marcarCambios()" title="Materia con la que tiene dificultad"><option value="">Materia con dificultad (opcional)</option></select>` : ''}
                </div>
            </div>`).join('');

        contenido.insertAdjacentHTML('beforeend', `
            <div class="tab-pane fade ${activa ? 'show active' : ''}" id="panelTutoria${n}" role="tabpanel">

                <div class="paso-tutoria">
                    ${tituloPaso(1, 'La sesión')}
                    <div class="row g-2">
                        <div class="col-sm-4">
                            <label class="app-etiqueta" for="fechaTutoria${n}">Fecha de la cita o sesión</label>
                            <input type="date" class="form-control form-control-sm" id="fechaTutoria${n}" onchange="marcarCambios()">
                        </div>
                        <div class="col-sm-4">
                            <label class="app-etiqueta" for="asistenciaTutoria${n}">Asistencia</label>
                            <select class="form-select form-select-sm" id="asistenciaTutoria${n}" onchange="marcarCambios()">
                                <option value="pendiente">Sin citar todavía</option>
                                <option value="citado">Citado (aún no se realiza)</option>
                                <option value="asistio">Asistió</option>
                                <option value="falto">No asistió</option>
                                <option value="justificado">No asistió (justificado)</option>
                                <option value="reprogramada">Reprogramada</option>
                            </select>
                        </div>
                        <div class="col-sm-4">
                            <label class="app-etiqueta" for="modalidadTutoria${n}">Modalidad</label>
                            <select class="form-select form-select-sm" id="modalidadTutoria${n}" onchange="marcarCambios()">
                                <option value="">Sin indicar</option>
                                ${catalogoTutorias.modalidades.map(m => `<option value="${escapeHTML(m)}">${escapeHTML(m)}</option>`).join('')}
                            </select>
                        </div>
                    </div>
                    <div class="small text-muted mt-2"><i class="bi bi-info-circle me-1"></i>Solo «No asistió» cuenta como inasistencia. Mientras no se le cite, no se genera ninguna alerta.</div>
                    <div class="small text-danger mt-1 d-none" id="avisoFaltaTutoria${n}"><i class="bi bi-exclamation-triangle-fill me-1"></i>La inasistencia se suma a las alertas. Si el alumno tiene otras señales de riesgo académico, su semáforo pasa a rojo y lo verá coordinación. Si avisó o tuvo una razón válida, usa «No asistió (justificado)».</div>
                </div>

                <div class="paso-tutoria">
                    ${tituloPaso(2, 'Valoración del semáforo', '¿Coincide con lo que observaste?')}
                    <div class="small mb-2" id="semaforoSistemaTutoria${n}"></div>
                    <div class="btn-group flex-wrap" role="group" aria-label="Valoración del riesgo">
                        <input type="radio" class="btn-check" name="valoracion${n}" id="valoracion${n}-" value="" onchange="cambioValoracion(${n})">
                        <label class="btn btn-sm btn-outline-secondary" for="valoracion${n}-">Sin valorar</label>
                        <input type="radio" class="btn-check" name="valoracion${n}" id="valoracion${n}-confirma" value="confirma" onchange="cambioValoracion(${n})">
                        <label class="btn btn-sm btn-outline-primary" for="valoracion${n}-confirma"><i class="bi bi-check2-circle me-1"></i>Confirmo</label>
                        <input type="radio" class="btn-check" name="valoracion${n}" id="valoracion${n}-eleva" value="eleva" onchange="cambioValoracion(${n})">
                        <label class="btn btn-sm btn-outline-danger" for="valoracion${n}-eleva"><i class="bi bi-arrow-up-circle me-1"></i>El riesgo es mayor</label>
                        <input type="radio" class="btn-check" name="valoracion${n}" id="valoracion${n}-descarta" value="descarta" onchange="cambioValoracion(${n})">
                        <label class="btn btn-sm btn-outline-success" for="valoracion${n}-descarta"><i class="bi bi-x-circle me-1"></i>No hay riesgo</label>
                    </div>
                    <input type="text" class="form-control form-control-sm mt-2" id="motivoValoracionTutoria${n}" placeholder="Motivo (obligatorio si indicas que no hay riesgo)" oninput="validarMotivoValoracion(${n})" onchange="marcarCambios()">
                    <div class="invalid-feedback">Escribe el motivo: sin él, la valoración no se toma en cuenta.</div>
                </div>

                <div class="paso-tutoria">
                    ${tituloPaso(3, 'Lo que se trató')}
                    <div class="app-etiqueta mb-1"><i class="bi bi-chat-square-text me-1"></i>Temas tratados</div>
                    ${casillasTutoria(n, 'temas', catalogoTutorias.temas)}
                    <div class="app-etiqueta mt-3 mb-1"><i class="bi bi-exclamation-diamond me-1"></i>Problemáticas detectadas</div>
                    <div class="row g-2">${categorias}</div>
                </div>

                <div class="paso-tutoria mb-0">
                    ${tituloPaso(4, 'Canalización y acuerdos')}
                    <!-- Canalizaciones de sesiones anteriores (o de un caso que viene de otro periodo): ¿acudió? -->
                    <div id="seguimientoCanalizaciones${n}" class="d-none mb-3"></div>
                    <div class="app-etiqueta mb-1"><i class="bi bi-signpost-split me-1"></i>¿A dónde se orientó al estudiante?</div>
                    ${casillasTutoria(n, 'canalizaciones', catalogoTutorias.canalizaciones)}
                    <input type="text" class="form-control form-control-sm mt-2" id="otraCanalizacionTutoria${n}" placeholder="Otra área o departamento (opcional)" onchange="marcarCambios()">
                    <div class="row g-2 mt-2">
                        <div class="col-lg-6">
                            <label class="app-etiqueta" for="acuerdosTutoria${n}">Acuerdo principal</label>
                            <textarea class="form-control form-control-sm" id="acuerdosTutoria${n}" rows="2" placeholder="Ej. Acudirá a CENDHIU y llevará su comprobante." onchange="marcarCambios()"></textarea>
                        </div>
                        <div class="col-sm-6 col-lg-3">
                            <label class="app-etiqueta" for="fechaRevisionTutoria${n}">Revisar el</label>
                            <input type="date" class="form-control form-control-sm" id="fechaRevisionTutoria${n}" onchange="marcarCambios()">
                        </div>
                        <div class="col-sm-6 col-lg-3">
                            <label class="app-etiqueta" for="estadoCasoTutoria${n}">Estado del caso</label>
                            <select class="form-select form-select-sm" id="estadoCasoTutoria${n}" onchange="marcarCambios()" title="Abierto o En atención aparece en las alertas de coordinación">
                                <option value="">Sin caso</option>
                                ${catalogoTutorias.estadosCaso.map(e => `<option value="${escapeHTML(e)}">${escapeHTML(e)}</option>`).join('')}
                            </select>
                            <div class="small text-success mt-1 d-none" id="avisoCasoAbierto${n}"><i class="bi bi-info-circle me-1"></i>Se abrió el caso porque canalizaste al estudiante. Ciérralo cuando ya no necesite seguimiento.</div>
                        </div>
                        <div class="col-12">
                            <label class="app-etiqueta" for="tutoria${n}">Comentario (solo lo que no cubren las casillas)</label>
                            <textarea class="form-control form-control-sm" id="tutoria${n}" rows="2" placeholder="Notas adicionales de la sesión." onchange="marcarCambios()"></textarea>
                        </div>
                    </div>
                </div>
            </div>`);
    }

    // Al cambiar algo, se actualizan la marca de la pestaña y los avisos
    contenido.addEventListener('change', actualizarEstadoPestanasTutoria);

    // Canalizar al estudiante abre el caso (si estaba "Sin caso"), para que el seguimiento no se olvide; si se quita
    // esa canalización y la sesión se queda sin ninguna, el caso vuelve a "Sin caso" (solo si lo abrió el sistema).
    // También se actualiza el "¿acudió?" de las sesiones, que depende de las canalizaciones anteriores.
    contenido.addEventListener('change', e => {
        const panel = e.target.closest('[id^="panelTutoria"]');
        if (!panel) return;
        const n = parseInt(panel.id.replace('panelTutoria', ''));
        const estado = $('estadoCasoTutoria' + n);
        const esCasilla = e.target.getAttribute('data-grupo') === 'canalizaciones';
        const esOtra = e.target.id === 'otraCanalizacionTutoria' + n;

        if ((esCasilla && e.target.checked) || (esOtra && e.target.value.trim())) {
            if (!estado.value) {
                estado.value = 'Abierto';
                casoAbiertoAutomatico[n] = true;
                mostrarAvisoCaso(n, 'ok', 'Se abrió el caso porque canalizaste al estudiante. Ciérralo cuando ya no necesite seguimiento.');
                actualizarEstadoPestanasTutoria();
                marcarCambios();
            }
        } else if ((esCasilla || esOtra) && !tieneCanalizaciones(n)) {
            if (casoAbiertoAutomatico[n] && estado.value === 'Abierto') {
                estado.value = '';
                delete casoAbiertoAutomatico[n];
                mostrarAvisoCaso(n);
                actualizarEstadoPestanasTutoria();
                marcarCambios();
            } else if (estado.value === 'Abierto' || estado.value === 'En atención') {
                mostrarAvisoCaso(n, 'info', 'Esta sesión ya no tiene canalizaciones. Si el caso ya no requiere seguimiento, cambia su estado.');
            }
        }
        if (e.target === estado) { delete casoAbiertoAutomatico[n]; mostrarAvisoCaso(n); }

        // El seguimiento de esta sesión y de las siguientes depende de lo canalizado y de lo que ya concluyó
        if (esCasilla || esOtra) for (let k = n + 1; k <= SESIONES_POR_PERIODO; k++) pintarSeguimientoCanalizaciones(k);
        if (e.target.matches('select[data-area]') || e.target === estado)
            for (let k = n; k <= SESIONES_POR_PERIODO; k++) pintarSeguimientoCanalizaciones(k);
    });
    formularioTutoriasListo = true;
}

// Sesiones cuyo caso abrió el sistema al canalizar (para poder regresarlo a "Sin caso" si se quita la canalización)
let casoAbiertoAutomatico = {};

const tieneCanalizaciones = n => valoresMarcados(n, 'canalizaciones').length > 0 || !!$('otraCanalizacionTutoria' + n).value.trim();

// Aviso bajo "Estado del caso": tipo 'ok' (verde) o 'info' (gris); sin texto lo oculta
function mostrarAvisoCaso(n, tipo, texto) {
    const aviso = $('avisoCasoAbierto' + n);
    if (!aviso) return;
    aviso.className = `small mt-1 ${tipo === 'info' ? 'text-secondary' : 'text-success'} ${texto ? '' : 'd-none'}`;
    aviso.innerHTML = texto ? `<i class="bi bi-info-circle me-1"></i>${escapeHTML(texto)}` : '';
}

// Cierra el caso desde el aviso "Todas las canalizaciones concluyeron"
function cerrarCasoTutoria(n) {
    $('estadoCasoTutoria' + n).value = 'Cerrado';
    delete casoAbiertoAutomatico[n];
    mostrarAvisoCaso(n);
    actualizarEstadoPestanasTutoria();
    marcarCambios();
    for (let k = n; k <= SESIONES_POR_PERIODO; k++) pintarSeguimientoCanalizaciones(k);
}

const valoresMarcados = (n, grupo) =>
    [...document.querySelectorAll(`#panelTutoria${n} input[data-grupo="${grupo}"]:checked`)].map(c => c.getAttribute('data-valor'));

const valoracionElegida = n => { const r = document.querySelector(`input[name="valoracion${n}"]:checked`); return r ? r.value : ''; };

// Al valorar se guarda qué color marcaba el sistema en ese momento
function cambioValoracion(n) {
    const s = situacionActual();
    semaforoAlValorar[n] = valoracionElegida(n) ? ((s && s.semaforo && s.semaforo.sistema) || '') : '';
    validarMotivoValoracion(n);
    marcarCambios();
}

function validarMotivoValoracion(n) {
    const motivo = $('motivoValoracionTutoria' + n);
    if (motivo) motivo.classList.toggle('is-invalid', valoracionElegida(n) === 'descarta' && !motivo.value.trim());
}

// Lee la sesión n del formulario
function leerSesionTutoria(n, periodo) {
    const estado = $('estadoCasoTutoria' + n).value;
    const valoracion = valoracionElegida(n);
    return {
        Periodo: periodo,
        Sesion: n,
        Fecha: $('fechaTutoria' + n).value,
        Asistencia: $('asistenciaTutoria' + n).value,
        Modalidad: $('modalidadTutoria' + n).value,
        Temas: valoresMarcados(n, 'temas'),
        Situaciones: valoresMarcados(n, 'situaciones'),
        Canalizaciones: valoresMarcados(n, 'canalizaciones'),
        OtraCanalizacion: $('otraCanalizacionTutoria' + n).value.trim(),
        Acuerdos: $('acuerdosTutoria' + n).value,
        Comentarios: $('tutoria' + n).value,
        MateriaDificultad: $('materiaDificultadTutoria' + n).value,
        FechaRevision: $('fechaRevisionTutoria' + n).value,
        EstadoCaso: estado,
        RequiereSeguimiento: estado === 'Abierto' || estado === 'En atención',
        ValoracionRiesgo: valoracion,
        MotivoValoracion: $('motivoValoracionTutoria' + n).value.trim(),
        SemaforoSistema: valoracion ? (semaforoAlValorar[n] || '') : '',
        AcudioCanalizaciones: [...document.querySelectorAll(`#seguimientoCanalizaciones${n} select[data-area]`)]
            .filter(s => s.value).map(s => `${s.getAttribute('data-area')}=${s.value}`)
    };
}

// Una sesión sin nada capturado no se guarda
function sesionTieneInformacion(t) {
    return !!(t.Fecha || (t.Asistencia && t.Asistencia !== 'pendiente') || t.Modalidad || t.Temas.length || t.Situaciones.length
        || t.Canalizaciones.length || t.OtraCanalizacion || (t.Acuerdos || '').trim() || (t.Comentarios || '').trim() || t.RequiereSeguimiento
        || t.MateriaDificultad || t.FechaRevision || t.EstadoCaso || t.ValoracionRiesgo || (t.AcudioCanalizaciones || []).length);
}

// Pasa lo que hay en el formulario (periodo de trabajo actual) a tutoriasGlobal. Lo llama el guardado automático.
function guardarTutoriasEnMemoria() {
    if (!formularioTutoriasListo) return;   // el formulario aún no existe: se conservan las tutorías tal como se cargaron
    let selP = $('selectPeriodoGlobal');
    let periodoActual = selP ? selP.value : '';
    tutoriasGlobal = tutoriasGlobal.filter(t => t.Periodo !== periodoActual);

    for (let n = 1; n <= SESIONES_POR_PERIODO; n++) {
        const sesion = leerSesionTutoria(n, periodoActual);
        if (sesionTieneInformacion(sesion)) tutoriasGlobal.push(sesion);
    }
}

// Materias del alumno para "Materia con dificultad": las de su plan y las que tiene registradas
// (la pantalla de tutoría llena materiasDelAlumno al cargar al alumno)
let materiasDelAlumno = [];

function opcionesMateriaDificultad(select, valor) {
    const nombres = materiasDelAlumno.slice();
    if (valor && !nombres.includes(valor)) nombres.unshift(valor);
    select.innerHTML = '<option value="">Materia con dificultad (opcional)</option>' + nombres.map(m => `<option value="${escapeHTML(m)}">${escapeHTML(m)}</option>`).join('');
    select.value = valor || '';
}

// Muestra en el formulario las sesiones del periodo de trabajo actual
function cargarTutoriasALaVista() {
    let selectP = $('selectPeriodoGlobal');
    let periodoActual = selectP ? selectP.value : '';
    const etiquetaPeriodo = $('periodoTutorias'); if (etiquetaPeriodo) etiquetaPeriodo.innerText = periodoActual || '—';
    if (!formularioTutoriasListo) { pintarSenalesTutoria(); return; }

    let tuts = tutoriasGlobal.filter(t => t.Periodo === periodoActual);
    semaforoAlValorar = {};
    casoAbiertoAutomatico = {};
    for (let n = 1; n <= SESIONES_POR_PERIODO; n++) {
        const t = tuts.find(x => x.Sesion === n) || {};
        $('fechaTutoria' + n).value = t.Fecha || '';
        $('asistenciaTutoria' + n).value = t.Asistencia || 'pendiente';
        $('modalidadTutoria' + n).value = t.Modalidad || '';
        const elegidos = { temas: t.Temas, situaciones: t.Situaciones, canalizaciones: t.Canalizaciones };
        Object.keys(elegidos).forEach(grupo => {
            const marcados = new Set(elegidos[grupo] || []);
            document.querySelectorAll(`#panelTutoria${n} input[data-grupo="${grupo}"]`).forEach(c => c.checked = marcados.has(c.getAttribute('data-valor')));
        });
        $('otraCanalizacionTutoria' + n).value = t.OtraCanalizacion || '';
        $('acuerdosTutoria' + n).value = t.Acuerdos || '';
        $('tutoria' + n).value = t.Comentarios || '';
        opcionesMateriaDificultad($('materiaDificultadTutoria' + n), t.MateriaDificultad || '');
        $('fechaRevisionTutoria' + n).value = t.FechaRevision || '';
        $('estadoCasoTutoria' + n).value = t.EstadoCaso || (t.RequiereSeguimiento ? 'Abierto' : '');
        const radio = $(`valoracion${n}-${t.ValoracionRiesgo || ''}`) || $(`valoracion${n}-`);
        radio.checked = true;
        $('motivoValoracionTutoria' + n).value = t.MotivoValoracion || '';
        semaforoAlValorar[n] = t.SemaforoSistema || '';
        validarMotivoValoracion(n);
        mostrarAvisoCaso(n);
    }
    // El "¿acudió?" depende de las canalizaciones de las sesiones anteriores: se pinta con todas ya cargadas
    for (let n = 1; n <= SESIONES_POR_PERIODO; n++) {
        const t = tuts.find(x => x.Sesion === n) || {};
        pintarSeguimientoCanalizaciones(n, t.AcudioCanalizaciones || []);
    }
    pintarSenalesTutoria();
}

// Áreas a las que se canalizó ANTES de la sesión n: en sesiones anteriores de este periodo y, si el caso viene
// de un periodo anterior, las de ese caso (las da el servidor)
// Las que ya concluyeron (antes de esta sesión) no se vuelven a preguntar.
function canalizacionesAnteriores(n) {
    let areas = [];
    const agregar = a => { if (a && !areas.includes(a)) areas.push(a); };
    const s = situacionActual();
    const periodo = $('selectPeriodoGlobal') ? $('selectPeriodoGlobal').value : '';
    if (s && s.semaforo) (s.semaforo.canalizacionesCaso || [])
        .filter(c => c.periodo !== periodo && !(c.acudio === 'concluida' && c.concluidaEn !== periodo))
        .forEach(c => agregar(c.area));
    for (let k = 1; k < n; k++) {
        // En la sesión k: primero se quitan las que concluyeron y luego se agregan las que se canalizaron
        document.querySelectorAll(`#seguimientoCanalizaciones${k} select[data-area]`).forEach(sel => {
            if (sel.value === 'concluida') areas = areas.filter(a => a !== sel.getAttribute('data-area'));
        });
        valoresMarcados(k, 'canalizaciones').forEach(agregar);
        agregar($('otraCanalizacionTutoria' + k).value.trim());
    }
    return areas;
}

// "¿Acudió?" de cada canalización anterior en la sesión n. Conserva lo que ya estaba elegido en pantalla;
// 'guardados' (["Área=si", ...]) se usa al cargar las sesiones.
function pintarSeguimientoCanalizaciones(n, guardados) {
    const caja = $('seguimientoCanalizaciones' + n);
    if (!caja) return;
    const elegidos = {};
    (guardados || []).forEach(r => { const i = r.lastIndexOf('='); if (i > 0) elegidos[r.slice(0, i)] = r.slice(i + 1); });
    if (!guardados) caja.querySelectorAll('select[data-area]').forEach(s => { elegidos[s.getAttribute('data-area')] = s.value; });

    const areas = canalizacionesAnteriores(n);
    Object.keys(elegidos).forEach(a => { if (elegidos[a] && !areas.includes(a)) areas.push(a); });   // no perder lo guardado
    if (areas.length === 0) { caja.classList.add('d-none'); caja.innerHTML = ''; return; }

    caja.classList.remove('d-none');
    caja.innerHTML = `
        <div class="border rounded p-2 bg-white">
            <div class="app-etiqueta mb-1"><i class="bi bi-arrow-repeat me-1"></i>¿Acudió a la canalización anterior?</div>
            ${areas.map((a, i) => `
                <div class="d-flex flex-wrap align-items-center justify-content-between gap-2 py-1 ${i ? 'border-top' : ''}">
                    <span class="small">${escapeHTML(a)}</span>
                    <select class="form-select form-select-sm w-auto" data-area="${escapeHTML(a)}" onchange="marcarCambios()">
                        <option value="">Pendiente de confirmar</option>
                        <option value="si" ${elegidos[a] === 'si' ? 'selected' : ''}>Acudió, sigue en atención</option>
                        <option value="no" ${elegidos[a] === 'no' ? 'selected' : ''}>No acudió</option>
                        <option value="concluida" ${elegidos[a] === 'concluida' ? 'selected' : ''}>Concluyó</option>
                    </select>
                </div>`).join('')}
            <div class="small text-muted mt-1">Marca «Concluyó» cuando esa atención terminó: ya no se volverá a preguntar. El caso sigue abierto mientras quede alguna canalización activa.</div>
            ${todasConcluidas(n, areas, elegidos)
                ? `<div class="alert alert-success py-2 px-2 small mt-2 mb-0 d-flex flex-wrap align-items-center justify-content-between gap-2">
                       <span><i class="bi bi-check2-circle me-1"></i>Todas las canalizaciones concluyeron.</span>
                       <button type="button" class="btn btn-sm btn-success" onclick="cerrarCasoTutoria(${n})">Cerrar el caso</button>
                   </div>` : ''}
        </div>`;
}

// ¿Ya concluyeron todas las canalizaciones del caso en la sesión n (y no hay nuevas en ella)?
function todasConcluidas(n, areas, elegidos) {
    const estado = $('estadoCasoTutoria' + n) ? $('estadoCasoTutoria' + n).value : '';
    return areas.length > 0 && estado !== 'Cerrado' && !tieneCanalizaciones(n) && areas.every(a => elegidos[a] === 'concluida');
}

// Marca en cada pestaña si la sesión ya tiene información y el estado del caso; muestra los avisos de cada paso
function actualizarEstadoPestanasTutoria() {
    if (!formularioTutoriasListo) return;
    const s = situacionActual();
    const sistema = s && s.semaforo ? s.semaforo.sistema : null;

    for (let n = 1; n <= SESIONES_POR_PERIODO; n++) {
        const sesion = leerSesionTutoria(n, '');

        const aviso = $('avisoFaltaTutoria' + n);
        if (aviso) aviso.classList.toggle('d-none', sesion.Asistencia !== 'falto');

        // Paso 2: qué marca el sistema hoy y, si ya se valoró, qué marcaba en ese momento
        const info = $('semaforoSistemaTutoria' + n);
        if (info) {
            const alValorar = sesion.ValoracionRiesgo && semaforoAlValorar[n];
            info.innerHTML = sistema
                ? `El sistema calcula: ${pastillaSemaforo(sistema)}` + (alValorar && alValorar !== sistema ? ` <span class="text-muted">· al valorar marcaba ${pastillaSemaforo(alValorar)}</span>` : '')
                : '<span class="text-muted">Calculando el semáforo...</span>';
        }

        const marca = $('estadoTabTutoria' + n);
        if (!marca) continue;
        if (sesion.EstadoCaso === 'Abierto' || sesion.EstadoCaso === 'En atención') { marca.className = 'badge rounded-pill ms-1 bg-warning text-dark'; marca.innerText = 'Caso ' + sesion.EstadoCaso.toLowerCase(); }
        else if (sesion.EstadoCaso === 'Cerrado') { marca.className = 'badge rounded-pill ms-1 bg-secondary'; marca.innerText = 'Caso cerrado'; }
        else if (sesionTieneInformacion(sesion)) { marca.className = 'badge rounded-pill ms-1 bg-success'; marca.innerText = 'Registrada'; }
        else { marca.className = 'badge rounded-pill ms-1'; marca.innerText = ''; }
    }
}

// Recuadro superior: lo que el sistema detectó para esta sesión (semáforo, señales con su fuente,
// temas sugeridos para conversar y seguimiento del caso). Se repinta cada vez que llega la situación del servidor.
function pintarSenalesTutoria() {
    const caja = $('senalesTutoria');
    if (!caja) return;
    const s = situacionActual();
    const sem = s ? s.semaforo : null;
    if (!sem) { caja.innerHTML = '<div class="small text-muted">Calculando las señales del alumno...</div>'; actualizarEstadoPestanasTutoria(); return; }

    // Las fechas vienen como yyyy-MM-dd; si no tienen ese formato se muestran tal cual, escapadas
    const fecha = f => { if (!f) return ''; const p = /^(\d{4})-(\d{2})-(\d{2})$/.exec(f); return p ? `${p[3]}/${p[2]}/${p[1]}` : escapeHTML(f); };
    const valoracion = sem.valoracion
        ? `<div class="small mt-1">El tutor ${TEXTO_VALORACION[sem.valoracion]}${sem.motivoValoracion ? `: <em>${escapeHTML(sem.motivoValoracion)}</em>` : ''}.</div>`
        : '';
    const desactualizada = sem.valoracionDesactualizada
        ? '<div class="small text-danger mt-1"><i class="bi bi-exclamation-triangle-fill me-1"></i>Hay señales nuevas más graves desde la última valoración; conviene valorarlo de nuevo.</div>'
        : '';
    const senales = sem.senales.length === 0
        ? '<div class="small text-muted">Sin señales de riesgo en este periodo.</div>'
        : `<ul class="small mb-0 ps-3">${sem.senales.map(x => `<li class="${x.grave ? 'fw-bold' : x.informativa ? 'text-secondary' : ''}">${x.informativa ? '<i class="bi bi-info-circle me-1"></i>' : ''}${escapeHTML(x.texto)} <span class="text-muted fw-normal">· ${escapeHTML(x.fuente)}</span></li>`).join('')}</ul>`;
    const temas = sem.final === 'sin'
        ? '<div class="small text-muted">Alumno dado de baja.</div>'
        : `<ul class="small mb-0 ps-3">${sem.temasSugeridos.map(t => `<li>${escapeHTML(t)}</li>`).join('')}</ul>`;

    const seguimiento = [];
    if (sem.estadoCaso) seguimiento.push(`Caso: <strong>${escapeHTML(sem.estadoCaso)}</strong>${sem.casoDesde ? ` (desde ${escapeHTML(sem.casoDesde)})` : ''}`);
    (sem.canalizacionesCaso || []).forEach(c => seguimiento.push(
        `${escapeHTML(c.area)}: <strong class="${c.acudio === 'no' ? 'text-danger' : c.acudio === 'si' ? 'text-success' : c.acudio === 'concluida' ? 'text-secondary' : ''}">${{ si: 'acudió, sigue en atención', no: 'no acudió', concluida: 'concluyó' }[c.acudio] || 'pendiente de confirmar'}</strong>`));
    seguimiento.push(sem.diasSinContacto === null || sem.diasSinContacto === undefined
        ? 'Sin tutorías con asistencia registradas'
        : `Último contacto: <strong>${fecha(sem.ultimoContacto)}</strong> (hace ${sem.diasSinContacto} día${sem.diasSinContacto === 1 ? '' : 's'})`);
    const acuerdo = sem.acuerdoPendiente
        ? `<div class="small mt-1">Acuerdo pendiente: <em>${escapeHTML(sem.acuerdoPendiente)}</em>`
            + (sem.fechaRevision ? ` · revisar el ${fecha(sem.fechaRevision)}${sem.revisionVencida ? ' <span class="badge bg-danger">Vencida</span>' : ''}` : '') + '</div>'
        : '';

    // Columna lateral de la pantalla: una sección debajo de otra
    caja.innerHTML = `
        <div class="card app-card mb-3">
            <div class="card-body">
                <div class="d-flex flex-wrap align-items-center gap-2 mb-1">
                    <span class="app-etiqueta mb-0">Semáforo</span> ${pastillaSemaforo(sem.final)}
                    ${sem.final !== sem.sistema ? `<span class="small text-muted">(sistema: ${infoSemaforo(sem.sistema).texto})</span>` : ''}
                </div>
                ${valoracion}${desactualizada}
                <div class="small text-muted mt-2">${seguimiento.join('<br>')}</div>
                ${acuerdo}
            </div>
        </div>
        <div class="card app-card mb-3">
            <div class="card-body">
                <div class="app-etiqueta mb-1"><i class="bi bi-activity me-1"></i>Señales detectadas</div>${senales}
            </div>
        </div>
        <div class="card app-card mb-3">
            <div class="card-body">
                <div class="app-etiqueta mb-1"><i class="bi bi-chat-dots me-1"></i>Temas sugeridos para la sesión</div>${temas}
            </div>
        </div>`;
    actualizarEstadoPestanasTutoria();
}

// Convierte las tutorías que manda el servidor (nombres en minúscula) al formato que usa la pantalla
function tutoriaDesdeServidor(t) {
    return {
        Periodo: t.periodo, Sesion: t.sesion, Fecha: t.fecha || '', Asistencia: t.asistencia || 'pendiente', Comentarios: t.comentarios || '',
        Modalidad: t.modalidad || '', Temas: t.temas || [], Situaciones: t.situaciones || [], Canalizaciones: t.canalizaciones || [],
        OtraCanalizacion: t.otraCanalizacion || '', Acuerdos: t.acuerdos || '', RequiereSeguimiento: !!t.requiereSeguimiento,
        MateriaDificultad: t.materiaDificultad || '', FechaRevision: t.fechaRevision || '', EstadoCaso: t.estadoCaso || '',
        ValoracionRiesgo: t.valoracionRiesgo || '', MotivoValoracion: t.motivoValoracion || '', SemaforoSistema: t.semaforoSistema || '',
        AcudioCanalizaciones: t.acudioCanalizaciones || []
    };
}

// Ficha para llevar a la sesión: se abre en otra pestaña lista para imprimir o guardar en PDF
function abrirFichaTutoria() {
    const inputMat = $('inputMatricula');
    const matricula = inputMat ? inputMat.value.trim() : '';
    if (!matricula) { avisar("Selecciona un alumno primero.", { tipo: 'advertencia' }); return; }
    const selP = $('selectPeriodoGlobal');
    const periodo = selP ? selP.value : '';
    const url = `/Home/FichaTutoria?matricula=${encodeURIComponent(matricula)}&periodo=${encodeURIComponent(periodo)}`;
    // La pestaña se abre en el mismo clic (si se abre después, el navegador puede bloquearla como ventana emergente)
    // y se carga cuando termine el guardado en curso, para que la ficha incluya lo último que se capturó.
    const pestana = window.open('', '_blank');
    if (!pestana) { esperarGuardadoEnCurso().then(() => { window.location.href = url; }); return; }
    pestana.document.write('<p style="font-family:sans-serif;color:#555;padding:20px">Preparando la ficha...</p>');
    esperarGuardadoEnCurso().then(() => { pestana.location.href = url; });
}

function descargarWordTutorias() {
    let inputMat = $('inputMatricula');
    var matricula = inputMat ? inputMat.value.trim() : '';
    if (!matricula) { avisar("Selecciona un alumno primero.", { tipo: 'advertencia' }); return; }
    // Se espera a que se guarde lo último que se capturó para que salga en el Word
    esperarGuardadoEnCurso().then(() => { window.location.href = `/Home/DescargarWordTutorias?matricula=${encodeURIComponent(matricula)}`; });
}
