// ============================================================================
// Bank Statement Deep Analysis — page behaviour.
//
// Everything numeric is already computed server-side by
// AaStatementAnalysisService; this file only animates, charts and requests the
// on-demand AI narrative. No analysis logic lives here. The transaction ledger
// moved to aa-transactions.js when it got its own page.
// ============================================================================

(function () {
    'use strict';

    const root = document.getElementById('aaAnalysis');
    if (!root) return;

    // ── Score ring ───────────────────────────────────────────────────────
    (function scoreRing() {
        const ring = document.getElementById('aaScoreRing');
        if (!ring) return;
        const r = ring.r.baseVal.value;
        const circumference = 2 * Math.PI * r;
        const score = Math.max(0, Math.min(100, parseInt(ring.dataset.score, 10) || 0));

        ring.style.strokeDasharray = circumference + ' ' + circumference;
        ring.style.strokeDashoffset = circumference;
        setTimeout(function () {
            ring.style.strokeDashoffset = circumference - (score / 100) * circumference;
        }, 150);
    })();

    // ── Charts ───────────────────────────────────────────────────────────
    (function charts() {
        const el = document.getElementById('aaMonthlyData');
        if (!el || typeof Chart === 'undefined') return;

        let data = [];
        try { data = JSON.parse(el.textContent) || []; } catch (e) { return; }
        if (!data.length) return;

        // Re-read on every call: a CSS variable captured into a JS string is a
        // snapshot, so flipping data-theme can never reach a colour already
        // baked into a Chart.js dataset. See the frl-theme-changed hook below.
        const palette = () => {
            const css = getComputedStyle(document.documentElement);
            return {
                primary: (css.getPropertyValue('--brand-primary') || '#117A8B').trim(),
                accent: (css.getPropertyValue('--brand-accent') || '#f37021').trim()
            };
        };
        let { primary, accent } = palette();
        const labels = data.map(d => d.label);

        const money = v => {
            const a = Math.abs(v);
            if (a >= 1e7) return '₹' + (v / 1e7).toFixed(2) + ' Cr';
            if (a >= 1e5) return '₹' + (v / 1e5).toFixed(2) + ' L';
            return '₹' + Math.round(v).toLocaleString('en-IN');
        };

        const axis = {
            y: { ticks: { callback: money, font: { size: 10 } }, grid: { color: '#f1f5f6' } },
            x: { ticks: { font: { size: 10 } }, grid: { display: false } }
        };

        let monthlyChart = null, balanceChart = null;

        const monthly = document.getElementById('aaMonthlyChart');
        if (monthly) {
            monthlyChart = new Chart(monthly, {
                data: {
                    labels,
                    datasets: [
                        { type: 'bar', label: 'Credits', data: data.map(d => d.credits), backgroundColor: primary + 'cc', borderRadius: 4 },
                        { type: 'bar', label: 'Debits', data: data.map(d => d.debits), backgroundColor: accent + 'cc', borderRadius: 4 },
                        { type: 'line', label: 'Net Flow', data: data.map(d => d.net), borderColor: '#1d7a4c', backgroundColor: '#1d7a4c', tension: .3, borderWidth: 2, pointRadius: 3 }
                    ]
                },
                options: {
                    responsive: true, maintainAspectRatio: false,
                    interaction: { mode: 'index', intersect: false },
                    plugins: {
                        legend: { labels: { boxWidth: 12, font: { size: 11 } } },
                        tooltip: { callbacks: { label: c => c.dataset.label + ': ' + money(c.parsed.y) } }
                    },
                    scales: axis
                }
            });
        }

        const balanceEl = document.getElementById('aaBalanceChart');
        const balances = data.filter(d => d.balance !== null && d.balance !== undefined);
        if (balanceEl && balances.length) {
            balanceChart = new Chart(balanceEl, {
                type: 'line',
                data: {
                    labels: balances.map(d => d.label),
                    datasets: [{
                        label: 'Closing Balance', data: balances.map(d => d.balance),
                        borderColor: primary, backgroundColor: primary + '22',
                        fill: true, tension: .3, borderWidth: 2, pointRadius: 3
                    }]
                },
                options: {
                    responsive: true, maintainAspectRatio: false,
                    plugins: {
                        legend: { display: false },
                        tooltip: { callbacks: { label: c => money(c.parsed.y) } }
                    },
                    scales: axis
                }
            });
        }

        // Theme switch — recolour in place. Chart.js datasets hold plain colour
        // strings, so the data-theme flip alone never reaches an already-drawn
        // canvas (this is why the page used to need a refresh). Net Flow keeps
        // its green: it is a semantic up/down line, not a brand accent.
        document.addEventListener('frl-theme-changed', function () {
            ({ primary, accent } = palette());

            if (monthlyChart) {
                monthlyChart.data.datasets[0].backgroundColor = primary + 'cc';
                monthlyChart.data.datasets[1].backgroundColor = accent + 'cc';
                monthlyChart.update();
            }
            if (balanceChart) {
                const ds = balanceChart.data.datasets[0];
                ds.borderColor = primary;
                ds.backgroundColor = primary + '22';
                balanceChart.update();
            }
        });
    })();

    // ── Month drill-down → the transaction history page ───────────────────
    (function monthDrillDown() {
        root.querySelectorAll('.aa-month-row').forEach(function (tr) {
            tr.addEventListener('click', function () {
                window.location.href = '/AccountAggregator/Transactions?month='
                    + encodeURIComponent(tr.dataset.month || '');
            });
        });
    })();

    // ── Section 18: on-demand AI assessment ──────────────────────────────
    (function aiInsights() {
        const btn = document.getElementById('aaAiBtn');
        if (!btn) return;

        const statusEl = document.getElementById('aaAiStatus');
        const resultEl = document.getElementById('aaAiResult');
        const hintEl = document.getElementById('aaAiHint');

        function el(tag, cls, text) {
            const n = document.createElement(tag);
            if (cls) n.className = cls;
            if (text) n.textContent = text;   // textContent — never innerHTML for model output
            return n;
        }

        function block(title, body) {
            if (!body) return null;
            const wrap = el('div', 'aa-ai-block');
            wrap.appendChild(el('h6', null, title));
            wrap.appendChild(el('p', null, body));
            return wrap;
        }

        function list(title, items, cls) {
            if (!items || !items.length) return null;
            const wrap = el('div', 'aa-ai-block');
            wrap.appendChild(el('h6', null, title));
            const ul = el('ul');
            ul.style.paddingLeft = '18px';
            ul.style.margin = '0';
            items.forEach(function (i) {
                const li = el('li', null, i);
                li.style.fontSize = '12.5px';
                li.style.lineHeight = '1.7';
                li.style.color = cls === 'good' ? '#1d7a4c' : cls === 'bad' ? '#b3261e' : '#3d4a4f';
                ul.appendChild(li);
            });
            wrap.appendChild(ul);
            return wrap;
        }

        function render(d) {
            resultEl.textContent = '';

            if (d.riskLevel) {
                const head = el('div', 'mb-3');
                const badge = el('span', 'aa-grade grade-' + String(d.riskLevel).toLowerCase(),
                    'AI Risk Level: ' + d.riskLevel);
                head.appendChild(badge);
                resultEl.appendChild(head);
            }

            [
                block('Overall Assessment', d.overallAssessment),
                list('Key Strengths', d.keyStrengths, 'good'),
                list('Key Concerns', d.keyConcerns, 'bad'),
                block('Cash Flow', d.cashFlowAssessment),
                block('Inflows', d.inflowAssessment),
                block('Outflows', d.outflowAssessment),
                block('Pass-through', d.passThroughAssessment),
                block('Obligations', d.obligationAssessment),
                block('Balance', d.balanceAssessment),
                block('Banking Discipline', d.bankingDisciplineAssessment),
                block('Credit Analyst Summary', d.creditAnalystSummary),
                list('Recommended Verification', d.recommendedVerification)
            ].forEach(function (n) { if (n) resultEl.appendChild(n); });

            resultEl.style.display = 'block';
        }

        btn.addEventListener('click', async function () {
            btn.disabled = true;
            btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Analysing…';
            statusEl.style.display = 'block';
            statusEl.textContent = 'Sending computed metrics to FinRiskLens AI…';
            resultEl.style.display = 'none';

            try {
                const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
                const body = new FormData();
                if (tokenEl) body.append('__RequestVerificationToken', tokenEl.value);

                const res = await fetch('/AccountAggregator/AiInsights', { method: 'POST', body: body });
                const json = await res.json();

                if (json && json.status && json.data) {
                    statusEl.style.display = 'none';
                    if (hintEl) hintEl.style.display = 'none';
                    render(json.data);
                } else {
                    statusEl.textContent = (json && json.message)
                        || 'The AI assessment could not be generated. The analysis above remains valid.';
                }
            } catch (e) {
                statusEl.textContent = 'Could not reach the AI service. The deterministic analysis above remains valid.';
            }

            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-magic me-1"></i>Generate AI Assessment';
        });
    })();
})();
