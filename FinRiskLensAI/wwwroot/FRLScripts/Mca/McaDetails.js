// ============================================================================
// Corporate Affairs (MCA) — Corporate Intelligence Report page behavior.
// All data is server-rendered by McaController; this script handles tabs,
// the charge accordion, and the director DIN popup, whose data is fetched
// ON CLICK from /Mca/GetDinDetail (DINResponce in m_StaticResponces).
// ============================================================================

(function () {
    'use strict';

    // ── Tabs ─────────────────────────────────────────────────────────────
    document.querySelectorAll('.mca2-tab').forEach(function (tab) {
        tab.addEventListener('click', function () {
            document.querySelectorAll('.mca2-tab').forEach(t => t.classList.remove('active'));
            document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
            tab.classList.add('active');
            var panel = document.getElementById(tab.dataset.tab);
            if (panel) panel.classList.add('active');
        });
    });

    // ── Charge chart ─────────────────────────────────────────────────────
    // Same Chart.js setup as the Deep Analysis charts (aa-analysis.js): a
    // responsive canvas inside a fixed-height .aa-chart-wrap.
    //
    // Rendered when the Charges tab is shown, and re-rendered on each return
    // to it. Chart.js freezes bar geometry at the canvas size it sees when the
    // chart is constructed, and a canvas still reports a default 300x150 while
    // its panel is display:none — building it hidden yields hairline bars
    // stacked behind the y-axis. Rebuilding while visible is cheap here (one
    // bar per registered charge) and avoids depending on resize timing.
    (function chargeChart() {
        var payload = document.getElementById('mcaChargeData');
        var canvas = document.getElementById('mcaChargeChart');
        if (!payload || !canvas || typeof Chart === 'undefined') return;

        var rows = [];
        try { rows = JSON.parse(payload.textContent) || []; } catch (e) { return; }
        if (!rows.length) return;

        var css = getComputedStyle(document.documentElement);
        var openColor = (css.getPropertyValue('--warning-orange') || '#b26a00').trim();
        var closedColor = (css.getPropertyValue('--success-green') || '#2e7d32').trim();

        var money = function (v) {
            var a = Math.abs(v);
            if (a >= 1e7) return '₹' + (v / 1e7).toFixed(2) + ' Cr';
            if (a >= 1e5) return '₹' + (v / 1e5).toFixed(2) + ' L';
            return '₹' + Math.round(v).toLocaleString('en-IN');
        };

        // A logarithmic bar axis needs an explicit floor: without one the bar
        // base resolves to 0, log(0) is -Infinity and nothing is drawn. Taking
        // a decade below the smallest charge also keeps that bar visible
        // instead of flattening it onto the axis.
        var amounts = rows.map(r => r.amount);
        var axisMin = Math.pow(10, Math.floor(Math.log10(Math.min.apply(null, amounts))) - 1);
        var axisMax = Math.pow(10, Math.ceil(Math.log10(Math.max.apply(null, amounts))));

        var chart = null;
        function render() {
            if (chart) { chart.destroy(); chart = null; }
            // offsetParent is null while any ancestor is display:none.
            if (!canvas.offsetParent) return;

            chart = new Chart(canvas, {
                type: 'bar',
                data: {
                    labels: rows.map(r => r.year),
                    datasets: [{
                        label: 'Charge amount',
                        data: amounts,
                        backgroundColor: rows.map(r => (r.open ? openColor : closedColor) + 'cc'),
                        borderRadius: 4
                    }]
                },
                options: {
                    responsive: true, maintainAspectRatio: false,
                    plugins: {
                        legend: { display: false },
                        tooltip: {
                            callbacks: {
                                title: c => rows[c[0].dataIndex].label,
                                label: function (c) {
                                    var r = rows[c.dataIndex];
                                    return [r.year + ' · ' + r.status, r.asset];
                                }
                            }
                        }
                    },
                    scales: {
                        y: {
                            type: 'logarithmic',
                            min: axisMin,
                            max: axisMax,
                            // One gridline per decade; the default log ticks
                            // crowd the axis with intermediate values.
                            afterBuildTicks: function (scale) {
                                var ticks = [];
                                for (var d = Math.log10(axisMin); d <= Math.log10(axisMax) + 0.001; d++) {
                                    ticks.push({ value: Math.pow(10, d) });
                                }
                                scale.ticks = ticks;
                            },
                            ticks: { callback: money, font: { size: 10 } },
                            grid: { color: '#f1f5f6' }
                        },
                        x: { ticks: { font: { size: 10 } }, grid: { display: false } }
                    }
                }
            });
        }

        document.querySelectorAll('.mca2-tab').forEach(function (tab) {
            if (tab.dataset.tab === 'tab-charges') {
                // After the tab handler above has made the panel visible.
                tab.addEventListener('click', function () { setTimeout(render, 0); });
            }
        });

        // If Charges is already the open tab, wait for load: a chart built
        // before layout settles keeps the bar widths it computed then, and
        // resize() alone does not recover them — only a rebuild does.
        var panel = document.getElementById('tab-charges');
        if (panel && panel.classList.contains('active')) {
            if (document.readyState === 'complete') { setTimeout(render, 0); }
            else { window.addEventListener('load', function () { setTimeout(render, 0); }); }
        }
    })();

    // ── Charge accordion (one open at a time) ────────────────────────────
    document.querySelectorAll('.mca2-accordion-header').forEach(function (header) {
        header.addEventListener('click', function () {
            var body = header.nextElementSibling;
            var isOpen = body && body.classList.contains('open');
            document.querySelectorAll('.mca2-accordion-body').forEach(b => b.classList.remove('open'));
            document.querySelectorAll('.mca2-accordion-header').forEach(h => h.classList.remove('open'));
            if (!isOpen && body) {
                body.classList.add('open');
                header.classList.add('open');
            }
        });
    });

    // ── Director DIN popup ───────────────────────────────────────────────
    var overlay = document.getElementById('dinModalOverlay');
    var modal = document.getElementById('dinModal');
    var cache = {}; // din -> profile, so each DIN is fetched once

    function text(id, val) {
        var el = document.getElementById(id);
        if (el) el.textContent = (val === null || val === undefined || val === '') ? '—' : val;
    }

    function openModal() {
        overlay.classList.add('active');
        modal.classList.add('active');
        document.body.style.overflow = 'hidden';
    }

    function closeModal() {
        overlay.classList.remove('active');
        modal.classList.remove('active');
        document.body.style.overflow = '';
    }

    function renderProfile(d, fallbackName) {
        var name = (d && d.fullName && d.fullName !== '-') ? d.fullName : (fallbackName || '—');
        text('dinName', name);
        text('dinFullName', name);

        var avatar = document.getElementById('dinAvatar');
        if (avatar) avatar.textContent = (d && d.initials && d.initials !== '?') ? d.initials
            : name.split(' ').filter(Boolean).map(w => w[0]).slice(0, 2).join('').toUpperCase() || '?';

        var din = d ? d.din : '—';
        text('dinNumber', 'DIN: ' + din);
        text('dinDinValue', din);
        text('dinFatherName', d && d.fatherName);
        text('dinDob', d && d.dob);
        text('dinNationality', d && d.nationality);
        text('dinEmail', d && d.email);
        text('dinPresentAddress', d && d.presentAddress);
        text('dinPermanentAddress', d && d.permanentAddress);
        text('dinAppointedOn', d && d.appointedOn);
        text('dinDirectorSince', d && d.directorSince);
        text('dinCompanies', d && d.companies);

        var pan = document.getElementById('dinPan');
        if (pan) {
            pan.textContent = (d && d.pan) || 'Not Available';
            pan.style.color = (d && d.pan && d.pan !== 'Not Available') ? '' : 'var(--danger-red)';
        }

        // Verified pill only when the profile came from a real DIN response.
        var pill = document.getElementById('dinVerifiedPill');
        if (pill) {
            pill.innerHTML = (d && d.dinVerified)
                ? '<i class="bi bi-patch-check-fill me-1"></i>DIN Verified'
                : '<i class="bi bi-clock-history me-1"></i>From MCA Record';
        }
    }

    async function showDirector(din, name) {
        openModal();

        var loading = document.getElementById('dinLoading');
        var content = document.getElementById('dinContent');

        if (cache[din]) {
            renderProfile(cache[din], name);
            return;
        }

        renderProfile(null, name);
        if (loading) loading.style.display = 'block';
        if (content) content.style.display = 'none';

        try {
            // DIN data is fetched only when the user clicks the director.
            var res = await fetch('/Mca/GetDinDetail?din=' + encodeURIComponent(din));
            var json = await res.json();
            cache[din] = (json && json.data) || null;
            renderProfile(cache[din], name);
        } catch (e) {
            renderProfile(null, name);
        }

        if (loading) loading.style.display = 'none';
        if (content) content.style.display = 'block';
    }

    document.querySelectorAll('.director-strip-card').forEach(function (card) {
        card.addEventListener('click', function (e) {
            // The screening buttons live inside the card; a click on one of
            // them must not also open the DIN profile.
            if (e.target.closest('.screen-btn')) return;
            showDirector(card.dataset.din, card.dataset.name);
        });
    });

    // ── Board screening (OFAC / AML) ─────────────────────────────────────
    // UI only for now: no screening service is wired up. Each button raises
    // a 'frl:board-screen' event carrying the director's DIN and the list to
    // check, which is the single place to hook a real provider in.
    document.querySelectorAll('.screen-btn').forEach(function (btn) {
        btn.addEventListener('click', function (e) {
            e.stopPropagation();
            document.dispatchEvent(new CustomEvent('frl:board-screen', {
                detail: {
                    list: btn.dataset.screen,
                    din: btn.dataset.din,
                    name: btn.dataset.name
                }
            }));
        });
    });

    if (overlay) overlay.addEventListener('click', closeModal);
    var closeBtn = document.getElementById('dinModalClose');
    if (closeBtn) closeBtn.addEventListener('click', closeModal);
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') closeModal();
    });

    // ── Refresh — re-render from the server ──────────────────────────────
    var refresh = document.getElementById('btnRefreshData');
    if (refresh) refresh.addEventListener('click', function () { window.location.reload(); });
})();
