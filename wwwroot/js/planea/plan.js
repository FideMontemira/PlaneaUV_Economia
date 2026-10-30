// SITRA-UV · Plan de estudios: carga la plantilla del plan, dibuja el mapa y administra los planes.
// Se carga como script clásico (ver Views/Home/Index.cshtml): comparte el ámbito global con los demás archivos de esta carpeta.

let modalGestorPlanesInstance = null;

let planActualAlumno = "";

let materiasPlanMaestro = [];

let planDibujadoActual = "";

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

    // Si la plantilla declara un total mayor que lo que suman sus materias, faltan créditos por capturar
    const deMaterias = parseInt(res.creditosMaterias) || 0;
    mostrarAvisoCreditosPlan(creditosTotalesPlan > deMaterias && deMaterias > 0
        ? `<i class="bi bi-exclamation-triangle-fill me-1"></i><strong>El plan no cuadra:</strong> declara ${creditosTotalesPlan} créditos, pero sus materias suman ${deMaterias} (faltan ${creditosTotalesPlan - deMaterias}). `
          + `Revisa en la plantilla los créditos de las AFEL y de las optativas. Mientras tanto, el avance se mide contra ${creditosTotalesPlan}.`
        : '');

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

function abrirGestorPlanes() {
    if (modalGestorPlanesInstance) modalGestorPlanesInstance.show();
}

function subirPlanEstudio() {
    const input = document.getElementById("inputFilePlan");
    if (!input || input.files.length === 0) {
        avisar("Selecciona un archivo Excel (.xlsx) primero.", { tipo: 'advertencia' });
        return;
    }

    const archivo = input.files[0];
    if (!archivo.name.endsWith('.xlsx')) {
        avisar("El archivo debe tener formato .xlsx.", { tipo: 'advertencia' });
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
                avisar(data.message, { tipo: 'exito', titulo: 'Plan de estudios guardado' });
                input.value = ""; // Limpiamos el input
                if (modalGestorPlanesInstance) modalGestorPlanesInstance.hide();
            } else {
                avisar(data.message, { tipo: 'error', titulo: 'No se pudo guardar el plan' });
            }
        })
        .catch(e => {
            console.error(e);
            btn.innerHTML = textoOriginal;
            btn.disabled = false;
            avisar("No hubo comunicación con el servidor al subir el plan de estudios.", { tipo: 'error' });
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
function mostrarAvisoCreditosPlan(html) {
    const aviso = $('avisoCreditosPlan');
    if (!aviso) return;
    aviso.innerHTML = html;
    aviso.classList.toggle('d-none', !html);
}

function mostrarSelectorPlan(mensaje) {
    planDibujadoActual = '';
    mostrarAvisoCreditosPlan('');
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
    if (!plan || !matricula) { avisar("Selecciona un plan de estudios.", { tipo: 'advertencia' }); return; }

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
                avisar("No se pudo dibujar el mapa. Verifica los datos del plan.", { tipo: 'error' });
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

    // 1er, 2do... 10mo; del 11 en adelante "11°", "12°"... (planes de más de 10 semestres)
    const ordinal = n => n > 10 ? n + '°' : n + (n === 1 || n === 3 ? 'er' : n === 2 ? 'do' : n === 7 || n === 10 ? 'mo' : n === 8 ? 'vo' : n === 9 ? 'no' : 'to');
    const clasesArea = { general: 'bg-general', 'básica': 'bg-basica', basica: 'bg-basica', disciplinar: 'bg-disciplinar', terminal: 'bg-terminal' };
    const tipoDe = m => ((m.tipo || m.Tipo) || '').toLowerCase();

    // Las AFEL no pertenecen a un periodo: van SIEMPRE juntas en su propia columna al final,
    // sin importar el número de semestre que traigan en la plantilla (así un plan de 10, 12 o más
    // semestres no las mezcla con un periodo aunque el coordinador les ponga "9").
    const COLUMNA_AFEL = 'AFEL';
    let semestresAgrupados = {};
    materiasPlanMaestro.forEach(m => {
        // Aceptamos tanto 'semestre' (ASP.NET) como 'Semestre'
        let sem = tipoDe(m) === 'afel' ? COLUMNA_AFEL : (m.semestre || m.Semestre);
        if (!semestresAgrupados[sem]) semestresAgrupados[sem] = [];
        semestresAgrupados[sem].push(m);
    });

    // Misma estructura visual que el mapa anterior (mapa-grid / periodo-columna / periodo-header)
    const grid = document.createElement('div');
    grid.className = 'mapa-grid w-100';

    const ordenColumnas = Object.keys(semestresAgrupados).filter(k => k !== COLUMNA_AFEL).map(Number).sort((a, b) => a - b);
    if (semestresAgrupados[COLUMNA_AFEL]) ordenColumnas.push(COLUMNA_AFEL);

    ordenColumnas.forEach(numSemestre => {
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
            // Requisito de créditos (columna G): servicio social, experiencia recepcional, prácticas...
            const porcentajeMat = parseInt(mat.porcentajeCreditos || mat.PorcentajeCreditos) || 0;
            if (porcentajeMat > 0) {
                card.setAttribute('data-creditos-minimos', porcentajeMat);
                prerreqHtml += `<span class="prerequisito-text requisito-creditos-text">Req: ${porcentajeMat} % de créditos</span>`;
            }
            // Periodos que dura (columna H): p. ej. servicio social se cursa hasta en 2 periodos
            const duracionMat = parseInt(mat.periodosDuracion || mat.PeriodosDuracion) || 1;
            if (duracionMat > 1) card.setAttribute('data-periodos-duracion', duracionMat);

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
