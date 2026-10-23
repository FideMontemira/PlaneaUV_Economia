// PlaneaUV · Arranque de la página: crea las ventanas, llena el selector de periodos y conecta los eventos.
// Se carga como script clásico (ver Views/Home/Index.cshtml): comparte el ámbito global con los demás archivos de esta carpeta.

document.addEventListener('DOMContentLoaded', function () {
    try {
        // Debe ir primero: alertas, tutorías y periodo del alumno leen el Periodo de Trabajo
        llenarSelectorDePeriodos();
        cargarCatalogoTutorias();   // arma el formato guiado de tutoría con las opciones del servidor

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
                actualizarProgreso();      // muestra "Calculando..." mientras llega la respuesta
                cargarSituacionAlumno();   // el servidor recalcula periodo que cursa y rezago
                cargarAlertas();           // las alertas dependen del periodo que cursa cada alumno
            });
        }

    } catch (err) {
        console.error("Error iniciando script:", err);
    }
});
