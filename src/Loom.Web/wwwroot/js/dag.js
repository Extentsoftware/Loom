// DAG dashboard interop. Loaded as an ES module from the
// /projects/{id}/dag page; uses cytoscape's built-in `breadthfirst`
// layout (no external layout plugin) to render the project's node tree
// with phase-coloured nodes and click-to-navigate.
//
// Public surface: window.LoomDag.render(elementId, jsonUrl).

import cytoscape from 'https://cdn.jsdelivr.net/npm/cytoscape@3.30.2/+esm';

const phaseColours = {
    Discovery: '#7aa2f7',
    Enrich:    '#bb9af7',
    Build:     '#9ece6a',
    Test:      '#e0af68',
    Done:      '#73daca',
    Archived:  '#565f89'
};

const statusGlyph = {
    paused:    ' ⏸',
    active:    ' ▶',
    failed:    ' ⚠',
    completed: '',
    none:      '',
    other:     ''
};

window.LoomDag = {
    async render(containerId, jsonUrl) {
        const el = document.getElementById(containerId);
        if (!el) return;

        let payload;
        try {
            const resp = await fetch(jsonUrl, { credentials: 'same-origin' });
            if (!resp.ok) {
                el.textContent = `Failed to load DAG (${resp.status}).`;
                return;
            }
            payload = await resp.json();
        } catch (err) {
            el.textContent = `Failed to load DAG: ${err.message}`;
            return;
        }

        const elements = [
            ...payload.nodes.map(n => ({
                data: {
                    id: n.id,
                    label: `${n.title}${statusGlyph[n.runStatus] || ''}`,
                    phase: n.phase,
                    type: n.type,
                    runStatus: n.runStatus
                }
            })),
            ...payload.edges.map(e => ({
                data: { id: `${e.source}->${e.target}`, source: e.source, target: e.target }
            }))
        ];

        const cy = cytoscape({
            container: el,
            elements,
            style: [
                {
                    selector: 'node',
                    style: {
                        'background-color': ele => phaseColours[ele.data('phase')] || '#9aa5ce',
                        'label': 'data(label)',
                        'color': '#c0caf5',
                        'font-size': 12,
                        'text-valign': 'bottom',
                        'text-margin-y': 6,
                        'text-wrap': 'wrap',
                        'text-max-width': 160,
                        'width': 32,
                        'height': 32,
                        'border-color': '#1a1b26',
                        'border-width': 2
                    }
                },
                {
                    selector: 'node[runStatus = "failed"]',
                    style: { 'border-color': '#f7768e', 'border-width': 3 }
                },
                {
                    selector: 'node[runStatus = "paused"]',
                    style: { 'border-color': '#e0af68', 'border-width': 3 }
                },
                {
                    selector: 'edge',
                    style: {
                        'curve-style': 'bezier',
                        'line-color': '#3b4261',
                        'target-arrow-shape': 'triangle',
                        'target-arrow-color': '#3b4261',
                        'width': 1.5
                    }
                }
            ],
            layout: {
                // Built-in layout — top-down tree from roots (no parent edge).
                // Good fit for FeatureNode trees; no extra layout plugin needed.
                name: 'breadthfirst',
                directed: true,
                spacingFactor: 1.4,
                padding: 20,
                roots: payload.nodes
                    .filter(n => !n.parentId)
                    .map(n => n.id)
            }
        });

        cy.on('tap', 'node', evt => {
            const id = evt.target.data('id');
            window.location.href = `/n/${id}`;
        });
    }
};
