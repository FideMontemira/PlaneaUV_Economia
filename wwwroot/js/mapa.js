document.addEventListener('DOMContentLoaded', function () {
    // 1. Definición de Nodos (Todos los periodos)
    var nodes = new vis.DataSet([
        // --- 1er Periodo ---
        { id: 101, label: "Lengua I\n(4 Créditos)", level: 1, group: "general", creditos: 4 },
        { id: 102, label: "Literacidad Digital\n(4 Créditos)", level: 1, group: "general", creditos: 4 },
        { id: 103, label: "Lectura y escritura de textos\n(4 Créditos)", level: 1, group: "general", creditos: 4 },
        { id: 104, label: "Introducción a la Economía\n(6 Créditos)", level: 1, group: "basica", creditos: 6 },
        { id: 105, label: "Optativa I\n(6 Créditos)", level: 1, group: "optativa", creditos: 6 },

        // --- 2do Periodo ---
        { id: 201, label: "Lengua II\n(4 Créditos)", level: 2, group: "general", creditos: 4 },
        { id: 202, label: "Pensamiento Crítico\n(4 Créditos)", level: 2, group: "general", creditos: 4 },
        { id: 203, label: "Cálculo I\n(9 Créditos)", level: 2, group: "basica", creditos: 9 },
        { id: 204, label: "Álgebra Lineal\n(9 Créditos)", level: 2, group: "basica", creditos: 9 },
        { id: 205, label: "Historia del Pensam. Económico\n(8 Créditos)", level: 2, group: "basica", creditos: 8 },

        // --- 3er Periodo ---
        { id: 301, label: "Microeconomía I\n(10 Créditos)", level: 3, group: "disciplinar", creditos: 10 },
        { id: 302, label: "Cálculo II\n(9 Créditos)", level: 3, group: "disciplinar", creditos: 9 },
        { id: 303, label: "Probabilidad\n(8 Créditos)", level: 3, group: "basica", creditos: 8 },
        { id: 304, label: "Historia Económica General\n(8 Créditos)", level: 3, group: "basica", creditos: 8 },
        { id: 305, label: "Acreditación de la lengua\n(8 Créditos)", level: 3, group: "general", creditos: 8 },

        // --- 4to Periodo ---
        { id: 401, label: "Microeconomía II\n(10 Créditos)", level: 4, group: "disciplinar", creditos: 10 },
        { id: 402, label: "Macroeconomía I\n(10 Créditos)", level: 4, group: "disciplinar", creditos: 10 },
        { id: 403, label: "Estadística\n(8 Créditos)", level: 4, group: "disciplinar", creditos: 8 },
        { id: 404, label: "Economía Mexicana I\n(8 Créditos)", level: 4, group: "disciplinar", creditos: 8 },
        { id: 405, label: "Técnicas de Investigación\n(8 Créditos)", level: 4, group: "basica", creditos: 8 },

        // --- 5to Periodo ---
        { id: 501, label: "Organización Industrial\n(8 Créditos)", level: 5, group: "disciplinar", creditos: 8 },
        { id: 502, label: "Microeconomía III\n(9 Créditos)", level: 5, group: "disciplinar", creditos: 9 },
        { id: 503, label: "Macroeconomía II\n(10 Créditos)", level: 5, group: "disciplinar", creditos: 10 },
        { id: 504, label: "Econometría I\n(8 Créditos)", level: 5, group: "disciplinar", creditos: 8 },
        { id: 505, label: "Modelos Dinámicos\n(8 Créditos)", level: 5, group: "disciplinar", creditos: 8 },
        { id: 506, label: "Economía Mexicana II\n(8 Créditos)", level: 5, group: "disciplinar", creditos: 8 },
        { id: 507, label: "Finanzas Públicas\n(9 Créditos)", level: 5, group: "disciplinar", creditos: 9 },
        { id: 508, label: "Optativa II\n(6 Créditos)", level: 5, group: "optativa", creditos: 6 },

        // --- 6to Periodo ---
        { id: 601, label: "Economía Internacional I\n(9 Créditos)", level: 6, group: "disciplinar", creditos: 9 },
        { id: 602, label: "Macroeconomía III\n(10 Créditos)", level: 6, group: "disciplinar", creditos: 10 },
        { id: 603, label: "Análisis de Series de Tiempo\n(8 Créditos)", level: 6, group: "disciplinar", creditos: 8 },
        { id: 604, label: "Econometría II\n(8 Créditos)", level: 6, group: "disciplinar", creditos: 8 },
        { id: 605, label: "Seminario de Investigación\n(7 Créditos)", level: 6, group: "disciplinar", creditos: 7 },
        { id: 606, label: "Planeación, program. y presup.\n(8 Créditos)", level: 6, group: "disciplinar", creditos: 8 },
        { id: 607, label: "Optativa III\n(6 Créditos)", level: 6, group: "optativa", creditos: 6 },

        // --- 7mo Periodo ---
        { id: 701, label: "Economía Internacional II\n(9 Créditos)", level: 7, group: "disciplinar", creditos: 9 },
        { id: 702, label: "Crecimiento Económico\n(9 Créditos)", level: 7, group: "disciplinar", creditos: 9 },
        { id: 703, label: "Política Económica\n(8 Créditos)", level: 7, group: "disciplinar", creditos: 8 },
        { id: 704, label: "Servicio Social\n(12 Créditos)", level: 7, group: "terminal", creditos: 12 },
        { id: 705, label: "Optativa IV\n(6 Créditos)", level: 7, group: "optativa", creditos: 6 },

        // --- 8vo Periodo ---
        { id: 801, label: "Práctica profesional\n(12 Créditos)", level: 8, group: "terminal", creditos: 12 },
        { id: 802, label: "Desarrollo Económico\n(9 Créditos)", level: 8, group: "disciplinar", creditos: 9 },
        { id: 803, label: "Formulación y eval. de proyectos\n(6 Créditos)", level: 8, group: "disciplinar", creditos: 6 },
        { id: 804, label: "Experiencia recepcional\n(12 Créditos)", level: 8, group: "terminal", creditos: 12 }
    ]);

    // 2. Definición de Aristas (Conexiones)
    var edges = new vis.DataSet([
        { from: 101, to: 201, arrows: "to" },
        { from: 203, to: 302, arrows: "to" },
        { from: 301, to: 401, arrows: "to" },
        { from: 303, to: 403, arrows: "to" },
        { from: 401, to: 501, arrows: "to" },
        { from: 401, to: 502, arrows: "to" },
        { from: 402, to: 503, arrows: "to" },
        { from: 503, to: 602, arrows: "to" },
        { from: 403, to: 504, arrows: "to" },
        { from: 504, to: 603, arrows: "to" },
        { from: 504, to: 604, arrows: "to" },
        { from: 404, to: 506, arrows: "to" },
        { from: 601, to: 701, arrows: "to" },
        { from: 702, to: 802, arrows: "to" }
    ]);

    var container = document.getElementById("curriculum-network");
    var data = { nodes: nodes, edges: edges };

    var options = {
        layout: {
            hierarchical: {
                direction: "LR",
                levelSeparation: 250,
                nodeSpacing: 100
            }
        },
        physics: false,
        groups: {
            basica: { color: { background: "#fff3cd", border: "#ffc107" }, font: { color: "black" } },
            optativa: { color: { background: "#cce5ff", border: "#007bff" }, font: { color: "black" } },
            disciplinar: { color: { background: "#d4edda", border: "#28a745" }, font: { color: "black" } },
            general: { color: { background: "#ffffff", border: "#6c757d" }, font: { color: "black" } },
            terminal: { color: { background: "#f8d7da", border: "#dc3545" }, font: { color: "black" } }
        },
        nodes: {
            shape: "box",
            margin: 10,
            widthConstraint: { maximum: 150 }
        },
        edges: {
            color: { color: '#555555' },
            smooth: { type: 'cubicBezier', forceDirection: 'horizontal' }
        }
    };

    var network = new vis.Network(container, data, options);

    // 3. Evento On-Click para abrir el modal
    network.on("click", function (params) {
        if (params.nodes.length > 0) {
            var nodeId = params.nodes[0];
            var clickedNode = nodes.get(nodeId);

            document.getElementById('modalMateriaNombre').innerText = clickedNode.label.split('\n')[0];
            document.getElementById('modalMateriaCreditos').innerText = clickedNode.creditos;

            var myModal = new bootstrap.Modal(document.getElementById('modalAvance'));
            myModal.show();
            evaluarLogicaInscripcion();
        }
    });

    // 4. Lógica dinámica del Modal
    var selectInscripcion = document.getElementById('selectInscripcion');
    var selectAprobacion = document.getElementById('selectAprobacion');

    if (selectInscripcion && selectAprobacion) {
        selectInscripcion.addEventListener('change', evaluarLogicaInscripcion);
        selectAprobacion.addEventListener('change', evaluarLogicaInscripcion);
    }

    function evaluarLogicaInscripcion() {
        if (!selectInscripcion || !selectAprobacion) return;

        var inscripcion = selectInscripcion.value;
        var aprobacion = selectAprobacion.value;

        var divTitulo = document.getElementById('divTitulo');
        var checkTitulo = document.getElementById('checkTitulo');
        var alertaSegunda = document.getElementById('alertaSegunda');
        var alertaBaja = document.getElementById('alertaBaja');

        if (inscripcion === "primera") {
            divTitulo.classList.remove('d-none');
            alertaSegunda.classList.add('d-none');
        } else {
            divTitulo.classList.add('d-none');
            checkTitulo.checked = false;
            alertaSegunda.classList.remove('d-none');
        }

        if (inscripcion === "segunda" && aprobacion === "no") {
            alertaBaja.classList.remove('d-none');
        } else {
            alertaBaja.classList.add('d-none');
        }
    }
});

function guardarAvance() {
    alert("Lógica para guardar en progreso... Posteriormente se exportará a Excel.");
    var modalEl = document.getElementById('modalAvance');
    var modal = bootstrap.Modal.getInstance(modalEl);
    if (modal) {
        modal.hide();
    }
}