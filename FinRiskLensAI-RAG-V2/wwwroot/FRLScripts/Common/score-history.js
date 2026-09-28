// Score History modal — draws the append-only series from t_MsmeScoreHistory.
//
// Requires: Bootstrap 5 (modal) + Chart.js, both already loaded by
// _FinancialHealthCardBody before this file. Deliberately jQuery-free: this
// partial also renders under _BankAdminLayout, which does not load jQuery.
//
// The UAN comes from the button's data-uan. A customer session ignores it
// server-side and resolves its own UAN; the bank portal needs it because there
// is no customer session there.
(function () {
    'use strict';

    var chart = null;

    function el(id) { return document.getElementById(id); }

    function show(which) {
        ['shLoading', 'shEmpty', 'shContent', 'shError'].forEach(function (id) {
            var n = el(id);
            if (n) n.style.display = (id === which) ? '' : 'none';
        });
    }

    // Band colours match the .band-chip palette used on the card itself.
    function bandColour(band) {
        switch ((band || '').toLowerCase()) {
            case 'excellent': return '#1c7c54';
            case 'good': return '#4c9f70';
            case 'fair': return '#d9a441';
            case 'atrisk': return '#d97941';
            default: return '#c0392b';
        }
    }

    function render(points) {
        // Reveal the pane BEFORE constructing the chart: Chart.js measures the
        // canvas at construction time, and a display:none canvas measures 0x0,
        // which renders the axes but no line.
        show('shContent');

        var last = points[points.length - 1];
        var first = points[0];
        var delta = Math.round(last.score - first.score);

        el('shLatest').textContent = Math.round(last.score);
        el('shBand').innerHTML = '<span style="color:' + bandColour(last.band) + ';font-weight:600;">'
            + last.band + '</span>';
        el('shCount').textContent = points.length;

        var arrow = delta > 0 ? '▲' : (delta < 0 ? '▼' : '■');
        var colour = delta > 0 ? '#1c7c54' : (delta < 0 ? '#c0392b' : '#6b7280');
        el('shDelta').innerHTML = '<span style="color:' + colour + ';">' + arrow + ' '
            + (delta > 0 ? '+' : '') + delta + '</span>';
        el('shSpan').textContent = first.label + ' → ' + last.label;

        var bandChanged = first.band !== last.band;
        el('shFootnote').textContent = bandChanged
            ? 'Band moved from ' + first.band + ' to ' + last.band + ' over ' + points.length + ' recorded runs.'
            : 'Band held at ' + last.band + ' across ' + points.length + ' recorded runs.';

        var ctx = el('shChart').getContext('2d');
        if (chart) chart.destroy();

        chart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: points.map(function (p) { return p.label; }),
                datasets: [
                    {
                        label: 'Overall score',
                        data: points.map(function (p) { return p.score; }),
                        borderColor: '#1c7c54',
                        backgroundColor: 'rgba(28,124,84,0.10)',
                        borderWidth: 2.5,
                        fill: true,
                        tension: 0.3,
                        pointRadius: 4,
                        pointBackgroundColor: points.map(function (p) { return bandColour(p.band); }),
                        pointBorderColor: '#fff',
                        pointBorderWidth: 1.5
                    },
                    {
                        label: 'Cash Flow Health (of 200)',
                        data: points.map(function (p) { return p.cashflow; }),
                        borderColor: '#4c9f70',
                        borderWidth: 1.5,
                        borderDash: [5, 4],
                        fill: false,
                        tension: 0.3,
                        pointRadius: 2,
                        yAxisID: 'y1'
                    },
                    {
                        label: 'Compliance (of 150)',
                        data: points.map(function (p) { return p.compliance; }),
                        borderColor: '#d9a441',
                        borderWidth: 1.5,
                        borderDash: [5, 4],
                        fill: false,
                        tension: 0.3,
                        pointRadius: 2,
                        yAxisID: 'y1'
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { position: 'bottom', labels: { boxWidth: 12, font: { size: 11 } } },
                    tooltip: {
                        callbacks: {
                            afterBody: function (items) {
                                var p = points[items[0].dataIndex];
                                var lines = ['Band: ' + p.band];
                                if (p.anomalous) lines.push('⚠ Cross-source anomaly flagged');
                                return lines;
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        min: 0, max: 1000,
                        title: { display: true, text: 'Overall score', font: { size: 11 } },
                        ticks: { stepSize: 200 }
                    },
                    y1: {
                        position: 'right',
                        min: 0, max: 250,
                        grid: { drawOnChartArea: false },
                        title: { display: true, text: 'Dimension points', font: { size: 11 } }
                    }
                }
            }
        });

    }

    // ── open / close ────────────────────────────────────────────────────
    // Hand-rolled instead of bootstrap.Modal: _BankAdminLayout loads Bootstrap
    // CSS but no Bootstrap JS, and this partial renders there too. The
    // .detail-modal / .detail-modal-overlay classes come from Dashboard.css,
    // which both layouts load.
    var TRANSITION_MS = 380;   // .detail-modal transition is 0.35s

    function openModal(onShown) {
        var overlay = el('shOverlay'), dialog = el('scoreHistoryModal');
        overlay.classList.add('active');
        dialog.classList.add('active');
        document.body.style.overflow = 'hidden';
        // Chart.js measures the canvas when it is built; building it before the
        // dialog has finished scaling in produces a chart drawn into the wrong box.
        setTimeout(onShown, TRANSITION_MS);
    }

    function closeModal() {
        el('shOverlay').classList.remove('active');
        el('scoreHistoryModal').classList.remove('active');
        document.body.style.overflow = '';
    }

    function init() {
        var btn = el('scoreHistoryBtn');
        var dialog = el('scoreHistoryModal');
        if (!btn || !dialog) return;

        el('shClose').addEventListener('click', closeModal);
        el('shOverlay').addEventListener('click', closeModal);
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') closeModal();
        });

        btn.addEventListener('click', function () {
            show('shLoading');

            var pending = null, opened = false;

            openModal(function () {
                opened = true;
                if (pending) { render(pending); pending = null; }
            });

            var uan = btn.dataset.uan || '';
            fetch('/score-history?take=50' + (uan ? '&uan=' + encodeURIComponent(uan) : ''))
                .then(function (r) { return r.json(); })
                .then(function (json) {
                    if (!json || !json.status) {
                        el('shError').textContent = (json && json.message) || 'Could not load the score history.';
                        show('shError');
                        return;
                    }
                    // One point is not a trend — say so rather than drawing a dot.
                    if (!json.points || json.points.length < 2) { show('shEmpty'); return; }

                    if (opened) render(json.points);
                    else pending = json.points;
                })
                .catch(function () {
                    el('shError').textContent = 'Could not load the score history. Please try again.';
                    show('shError');
                });
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
