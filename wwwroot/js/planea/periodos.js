// PlaneaUV · Periodos escolares: genera el selector de Periodo de Trabajo según la fecha.
// Se carga como script clásico (ver Views/Home/Index.cshtml): comparte el ámbito global con los demás archivos de esta carpeta.

// Periodo que corresponde a una fecha (por defecto, hoy)
function periodoDeFecha(fecha = new Date()) {
    const mes = fecha.getMonth(), anio = fecha.getFullYear();   // enero = 0
    if (mes >= 7) return `Agosto - Enero ${anio + 1}`;          // agosto a diciembre
    if (mes === 0) return `Agosto - Enero ${anio}`;             // enero
    return `Febrero - Julio ${anio}`;                           // febrero a julio
}

// Número para ordenar periodos en el tiempo: "Agosto - Enero 2025" va antes que "Febrero - Julio 2025".
// Devuelve null si el texto no es un periodo.
function ordenPeriodo(periodo) {
    const m = /^(Agosto - Enero|Febrero - Julio)\s+(\d{4})$/.exec((periodo || '').trim());
    if (!m) return null;
    return parseInt(m[2]) * 2 + (m[1] === 'Febrero - Julio' ? 1 : 0);
}

// Ordena los intentos de una materia del más antiguo al más reciente (los que no tienen periodo válido, al final)
function ordenarIntentos(intentos) {
    const clave = x => { const o = ordenPeriodo(x.Periodo); return o === null ? Infinity : o; };
    return intentos.slice().sort((a, b) => clave(a) - clave(b));
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
