// PlaneaUV · Periodos escolares: genera el selector de Periodo de Trabajo según la fecha.
// Se carga como script clásico (ver Views/Home/Index.cshtml): comparte el ámbito global con los demás archivos de esta carpeta.

// Periodo que corresponde a una fecha (por defecto, hoy)
function periodoDeFecha(fecha = new Date()) {
    const mes = fecha.getMonth(), anio = fecha.getFullYear();   // enero = 0
    if (mes >= 7) return `Agosto - Enero ${anio + 1}`;          // agosto a diciembre
    if (mes === 0) return `Agosto - Enero ${anio}`;             // enero
    return `Febrero - Julio ${anio}`;                           // febrero a julio
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
