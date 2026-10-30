// SITRA-UV · Utilidades compartidas: escape de HTML, acceso a elementos, respuestas del servidor y avisos.
// Se carga como script clásico (ver Views/Home/Index.cshtml): comparte el ámbito global con los demás archivos de esta carpeta.

function escapeHTML(str) {
    if (str === null || str === undefined) return '';
    return String(str).replace(/[&<>'"]/g, tag => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;'
    }[tag]));
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

// Lee la respuesta JSON del servidor; si es un error (p. ej. 409: Excel abierto) lanza el mensaje del servidor
function leerRespuesta(res) {
    return res.json().catch(() => ({})).then(data => {
        if (!res.ok) {
            const error = new Error(data.message || `Error del servidor (${res.status}).`);
            error.status = res.status;   // 400 = datos no válidos (no tiene caso reintentar); 409 = Excel abierto
            throw error;
        }
        return data;
    });
}

// Formato de matrícula aceptado (el servidor valida lo mismo en Services/ValidacionAlumno.cs)
const FORMATO_MATRICULA = /^[A-Za-z0-9-]{3,20}$/;

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

// ===================== DIÁLOGOS DEL SISTEMA =====================
// Reemplazan a alert() y confirm() del navegador con ventanas propias de SITRA-UV.
//   avisar(mensaje, { tipo, titulo })                       → Promise que se resuelve al cerrar
//   confirmar(mensaje, { titulo, textoAceptar, peligro })   → Promise<boolean>
// tipo: 'info' (predeterminado), 'exito', 'advertencia' o 'error'.
// El diálogo se dibuja dentro de la ventana de Bootstrap que esté abierta (si hay una), para quedar
// encima de ella y que Bootstrap no le quite el foco del teclado.
const TIPOS_DIALOGO = {
    info:        { icono: 'bi-info-circle-fill',          clase: 'dialogo-info',        titulo: 'Aviso' },
    exito:       { icono: 'bi-check-circle-fill',         clase: 'dialogo-exito',       titulo: 'Listo' },
    advertencia: { icono: 'bi-exclamation-triangle-fill', clase: 'dialogo-advertencia', titulo: 'Atención' },
    error:       { icono: 'bi-x-octagon-fill',            clase: 'dialogo-error',       titulo: 'No se pudo completar' },
    pregunta:    { icono: 'bi-question-circle-fill',      clase: 'dialogo-info',        titulo: 'Confirmar' }
};

function mostrarDialogo({ mensaje, tipo = 'info', titulo, botones, enfoque }) {
    return new Promise(resolver => {
        const t = TIPOS_DIALOGO[tipo] || TIPOS_DIALOGO.info;
        const capa = document.createElement('div');
        capa.className = 'dialogo-sistema';
        capa.setAttribute('role', 'alertdialog');
        capa.setAttribute('aria-modal', 'true');
        capa.innerHTML = `
            <div class="dialogo-caja ${t.clase}">
                <div class="dialogo-encabezado"><i class="bi ${t.icono}"></i><span>${escapeHTML(titulo || t.titulo)}</span></div>
                <div class="dialogo-mensaje">${escapeHTML(String(mensaje ?? ''))}</div>
                <div class="dialogo-botones">${botones.map((b, i) =>
                    `<button type="button" class="btn btn-sm ${b.clase}" data-i="${i}">${escapeHTML(b.texto)}</button>`).join('')}</div>
            </div>`;

        const anterior = document.activeElement;
        const contenedor = [...document.querySelectorAll('.modal.show')].pop() || document.body;
        contenedor.appendChild(capa);

        const cerrar = valor => {
            document.removeEventListener('keydown', teclado, true);
            capa.remove();
            if (anterior && document.contains(anterior)) anterior.focus();
            resolver(valor);
        };
        // Enter = botón principal; Escape = cancelar (o cerrar si solo hay uno). Se atrapa antes que Bootstrap
        // para que Escape no cierre también la ventana de abajo.
        const teclado = e => {
            if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); cerrar(botones[0].valor); }
            else if (e.key === 'Enter') {
                // Enter activa el botón que tiene el foco (por defecto, el principal)
                e.preventDefault(); e.stopPropagation();
                const enfocado = capa.contains(document.activeElement) ? document.activeElement.dataset.i : undefined;
                cerrar(botones[enfocado !== undefined ? +enfocado : botones.length - 1].valor);
            }
            else if (e.key === 'Tab') {
                // El foco no sale del diálogo
                const lista = [...capa.querySelectorAll('button')];
                const i = lista.indexOf(document.activeElement);
                e.preventDefault();
                lista[(i + (e.shiftKey ? lista.length - 1 : 1)) % lista.length].focus();
            }
        };
        document.addEventListener('keydown', teclado, true);
        capa.querySelectorAll('button').forEach(b => b.addEventListener('click', () => cerrar(botones[+b.dataset.i].valor)));
        // Clic fuera de la caja: igual que cancelar
        capa.addEventListener('mousedown', e => { if (e.target === capa) cerrar(botones[0].valor); });

        // Foco inicial: el botón principal, salvo que se pida otro (en las acciones que borran, Cancelar)
        const lista = capa.querySelectorAll('button');
        lista[enfoque !== undefined ? enfoque : lista.length - 1].focus();
    });
}

function avisar(mensaje, opciones = {}) {
    return mostrarDialogo({ mensaje, tipo: opciones.tipo, titulo: opciones.titulo,
        botones: [{ texto: 'Aceptar', clase: 'btn-primary', valor: undefined }] });
}

function confirmar(mensaje, opciones = {}) {
    return mostrarDialogo({ mensaje, tipo: opciones.peligro ? 'advertencia' : 'pregunta', titulo: opciones.titulo,
        enfoque: opciones.peligro ? 0 : undefined,
        botones: [
            { texto: opciones.textoCancelar || 'Cancelar', clase: 'btn-outline-secondary', valor: false },
            { texto: opciones.textoAceptar || 'Aceptar', clase: opciones.peligro ? 'btn-danger' : 'btn-primary', valor: true }
        ] });
}
