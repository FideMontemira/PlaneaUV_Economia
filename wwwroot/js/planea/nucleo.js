// PlaneaUV · Utilidades compartidas: escape de HTML, acceso a elementos, respuestas del servidor y avisos.
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
