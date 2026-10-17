// ============================================================================
// Corporate Affairs (MCA) — Corporate Intelligence Report page behavior.
// All data is server-rendered by McaController; this script handles the charge
// chart, the charge accordion, and the director DIN popup, whose data is fetched
// ON CLICK from /Mca/GetDinDetail (DINResponce in m_StaticResponces).
// ============================================================================

(function () {
    'use strict';

    // ── Tabs ─────────────────────────────────────────────────────────────
    // Switching itself lives in ~/FRLScripts/Common/panel-tabs.js, shared with
    // the ITR page. Only the chart rebuild below is MCA-specific.

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

        // --success-green and friends are declared on .udyam-dashboard, NOT on
        // :root — reading them off documentElement always returned '' and fell
        // through to the hardcoded fallbacks, so the bars never followed the
        // theme. Resolve against the page wrapper, where the tokens live.
        var scope = canvas.closest('.udyam-dashboard') || document.documentElement;

        // Read on every render, never once at load: a CSS variable captured
        // into a JS string is a snapshot, and flipping data-theme cannot reach
        // a colour already baked into a Chart.js dataset.
        function palette() {
            var css = getComputedStyle(scope);
            function token(name, fallback) { return (css.getPropertyValue(name) || fallback).trim(); }
            return {
                // Open charges take the theme accent so the chart follows the
                // theme switch; satisfied stays green, semantic in either palette.
                open: token('--brand-accent', '#F37021'),
                closed: token('--success-green', '#2e7d32'),
                grid: token('--card-border', '#f1f5f6'),
                ink: token('--brand-primary-dark', '#005647')
            };
        }

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

            var pal = palette();

            chart = new Chart(canvas, {
                type: 'bar',
                data: {
                    labels: rows.map(r => r.year),
                    datasets: [{
                        label: 'Charge amount',
                        data: amounts,
                        backgroundColor: rows.map(r => (r.open ? pal.open : pal.closed) + "cc"),
                        borderRadius: 4
                    }]
                },
                options: {
                    responsive: true, maintainAspectRatio: false,
                    plugins: {
                        legend: { display: false },
                        tooltip: {
                            // Explicit colours: the default tooltip inherits a
                            // near-transparent background here and the text was
                            // unreadable against the bars.
                            backgroundColor: pal.ink,
                            titleColor: '#fff',
                            bodyColor: '#fff',
                            borderColor: 'rgba(255,255,255,.25)',
                            borderWidth: 1,
                            padding: 10,
                            cornerRadius: 8,
                            displayColors: false,
                            titleFont: { size: 12, weight: '700' },
                            bodyFont: { size: 12 },
                            callbacks: {
                                title: c => rows[c[0].dataIndex].label,
                                label: function (c) {
                                    var r = rows[c.dataIndex];
                                    return [money(r.amount), r.year + ' · ' + r.status, r.asset];
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
                            grid: { color: pal.grid }
                        },
                        x: { ticks: { font: { size: 10 } }, grid: { display: false } }
                    }
                }
            });
        }

        document.querySelectorAll('.mca2-tab').forEach(function (tab) {
            if (tab.dataset.tab === 'tab-charges') {
                // Registered after panel-tabs.js, so by the time this runs that
                // handler has already made the panel visible.
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

        // Theme switch: the toggle flips data-theme, but the colours above are
        // already plain strings inside the chart. render() rebuilds from a
        // fresh palette(), which is why no page refresh is needed. Skipped
        // while the panel is hidden — render() no-ops there, and the tab
        // handler rebuilds on the next visit anyway.
        document.addEventListener('frl-theme-changed', function () { setTimeout(render, 0); });
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
            // The UAN is only used by the bank portal — a customer session always
            // resolves its own UAN server-side and ignores what we send.
            var modalEl = document.getElementById('dinModal');
            var uan = (modalEl && modalEl.dataset.uan) || '';
            var res = await fetch('/Mca/GetDinDetail?din=' + encodeURIComponent(din)
                + (uan ? '&uan=' + encodeURIComponent(uan) : ''));
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
    // Each card button raises a 'frl:board-screen' event carrying the director's
    // DIN and the list; the demo workflow below handles it. Demo mode only: no
    // screening provider is connected (see McaController.DemoScreening).
    document.querySelectorAll('.director-screen .screen-btn').forEach(function (btn) {
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

    // Demo screening workflow: one request per click, results kept per DIN + list
    // so one director's result can never show under another.
    var screenOverlay = document.getElementById('screenModalOverlay');
    var screenModal = document.getElementById('screenModal');
    var screenResults = {};      // key "din|list" → last result for that director and list
    var lastRequest = null;      // for Retry

    function setScreenView(view) {
        ['screenLoading', 'screenError', 'screenContent'].forEach(function (id) {
            var el = document.getElementById(id);
            if (el) el.style.display = id === view ? 'block' : 'none';
        });
    }

    function openScreen() {
        if (!screenModal) return;
        screenOverlay.classList.add('active');
        screenModal.classList.add('active');
    }

    function closeScreen() {
        if (!screenModal) return;
        screenOverlay.classList.remove('active');
        screenModal.classList.remove('active');
    }

    function renderScreen(r) {
        var title = document.getElementById('screenTitle');
        if (title) {
            title.innerHTML = '<i class="bi bi-shield-check me-2"></i>';
            title.appendChild(document.createTextNode(r.list + ' (Demo)'));
        }
        text('screenDirector', r.directorName);
        text('screenDin', r.din);
        text('screenType', r.list + ' · demo mode');
        text('screenAt', r.screenedAt);
        text('screenStatus', r.statusText);
        var status = document.getElementById('screenStatus');
        if (status) status.style.color = r.outcome === 'potential-match' ? 'var(--warning-orange)' : '';
        text('screenScope', r.scope);
        text('screenScoreNote', r.scoreNote);

        var box = document.getElementById('screenMatches');
        if (box) {
            box.innerHTML = '';
            if (!r.matches || r.matches.length === 0) {
                var none = document.createElement('p');
                none.className = 'small text-muted mb-0';
                none.textContent = 'None in the demo list.';
                box.appendChild(none);
            } else {
                r.matches.forEach(function (m) {
                    var item = document.createElement('div');
                    item.className = 'p-2 mb-2 border rounded small';
                    [['Matched name', m.matchedName], ['List', m.source],
                     ['Name overlap (demo engine)', m.overlapPercent + '%'],
                     ['Why', m.reason], ['Details', m.detail]].forEach(function (row) {
                        var line = document.createElement('div');
                        var label = document.createElement('span');
                        label.className = 'text-muted';
                        label.textContent = row[0] + ': ';
                        line.appendChild(label);
                        line.appendChild(document.createTextNode(row[1]));
                        item.appendChild(line);
                    });
                    box.appendChild(item);
                });
            }
        }

        var next = document.getElementById('screenNext');
        if (next) {
            next.innerHTML = '';
            (r.nextActions || []).forEach(function (a) {
                var li = document.createElement('li');
                li.textContent = a;
                next.appendChild(li);
            });
        }
        setScreenView('screenContent');
    }

    function noteOnCard(din) {
        var slot = document.querySelector('.screen-last[data-din="' + CSS.escape(din) + '"]');
        if (!slot) return;
        var parts = ['ofac', 'aml']
            .filter(function (l) { return screenResults[din + '|' + l]; })
            .map(function (l) {
                var r = screenResults[din + '|' + l];
                return (l === 'ofac' ? 'OFAC' : 'AML') + ' (demo): '
                    + (r.outcome === 'potential-match' ? 'potential match, needs review' : 'no match in demo list');
            });
        slot.textContent = parts.join(' · ');
    }

    async function runScreening(req) {
        lastRequest = req;
        openScreen();
        var title = document.getElementById('screenTitle');
        if (title) {
            title.innerHTML = '<i class="bi bi-shield-check me-2"></i>';
            title.appendChild(document.createTextNode((req.list === 'ofac' ? 'OFAC' : 'AML') + ' Screening (Demo)'));
        }
        setScreenView('screenLoading');

        var controller = typeof AbortController !== 'undefined' ? new AbortController() : null;
        var timer = controller ? setTimeout(function () { controller.abort(); }, 15000) : null;
        try {
            var uan = (modal && modal.dataset.uan) || '';
            var res = await fetch('/Mca/ScreenDirector?din=' + encodeURIComponent(req.din)
                + '&list=' + encodeURIComponent(req.list)
                + (uan ? '&uan=' + encodeURIComponent(uan) : ''),
                controller ? { signal: controller.signal } : undefined);
            var json = await res.json();
            if (!json || !json.status || !json.data) throw new Error((json && json.error) || 'The screening request failed.');

            // Only keep the result if it is for the director and list that were asked for
            if (json.data.din !== req.din) throw new Error('The response did not match the selected director.');
            screenResults[req.din + '|' + req.list] = json.data;
            renderScreen(json.data);
            noteOnCard(req.din);
        } catch (e) {
            text('screenErrorText', e && e.name === 'AbortError'
                ? 'The screening request timed out. Try again.'
                : (e && e.message) || 'The screening request failed. Try again.');
            setScreenView('screenError');
        } finally {
            if (timer) clearTimeout(timer);
        }
    }

    document.addEventListener('frl:board-screen', function (e) {
        if (e.detail && e.detail.din && e.detail.list) runScreening(e.detail);
    });
    var retry = document.getElementById('screenRetry');
    if (retry) retry.addEventListener('click', function () { if (lastRequest) runScreening(lastRequest); });
    if (screenOverlay) screenOverlay.addEventListener('click', closeScreen);
    var screenClose = document.getElementById('screenModalClose');
    if (screenClose) screenClose.addEventListener('click', closeScreen);

    if (overlay) overlay.addEventListener('click', closeModal);
    var closeBtn = document.getElementById('dinModalClose');
    if (closeBtn) closeBtn.addEventListener('click', closeModal);
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') { closeModal(); closeScreen(); }
    });

    // ── Refresh — re-render from the server ──────────────────────────────
    var refresh = document.getElementById('btnRefreshData');
    if (refresh) refresh.addEventListener('click', function () { window.location.reload(); });
})();
