// ============================================================================
// GST Analysis page — loaded only by Views/Dashboard/GSTAnalysis.cshtml.
// Renders the BI dashboard (gauges + ApexCharts) with static demo data, and
// parses the stored GSTR-2B/3B responses from window.frlGstAnalysis into
// window.FRLGst so the charts can switch to live figures in the next phase.
// Requires: ApexCharts only (loaded by the view). Deliberately jQuery-free —
// this partial also renders under _BankAdminLayout, which does not load jQuery.
// ============================================================================

(function () {
    'use strict';

    /** Stored payloads are JSON strings; tolerate empty/invalid content. */
    function tryParse(json) {
        if (!json || typeof json !== 'string') return null;
        try { return JSON.parse(json); } catch (e) { return null; }
    }

    // ── Raw data hand-off (live figures build on this next phase) ────────
    var raw = window.frlGstAnalysis || {};
    window.FRLGst = {
        hasData: !!raw.hasData,
        uan: raw.uan,
        gstin: raw.gstin,
        filingPeriod: raw.filingPeriod,
        createdDate: raw.createdDate,
        gstr2B: tryParse(raw.gstr2BResponseData),
        gstr3B: tryParse(raw.gstr3BResponseData)
    };

    // ── SVG circular gauges ──────────────────────────────────────────────
    function animateCircleGauge(id, value, max) {
        var circle = document.getElementById(id);
        if (!circle) return;

        var radius = circle.getAttribute('r');
        var circumference = 2 * Math.PI * radius; // r=58 → ≈364.4
        circle.style.strokeDasharray = circumference;

        var offset = circumference - (value / max) * circumference;
        setTimeout(function () {
            circle.style.strokeDashoffset = offset;
            circle.style.transition = 'stroke-dashoffset 1.5s cubic-bezier(0.4, 0, 0.2, 1)';
        }, 300);
    }

    // ── Theme-aware palette ─────────────────────────────────────────────
    // Reads the same --brand-* tokens as ~/css/theme.css instead of baking
    // in Theme 2's hexes (#8b1538 etc. below were literally Theme 2's
    // --brand-primary/--brand-secondary — that's why nothing moved on
    // Theme 1 or after a live theme switch).
    function getBrandPalette() {
        var cs = getComputedStyle(document.documentElement);
        function v(name, fallback) { return cs.getPropertyValue(name).trim() || fallback; }
        return {
            primary: v('--brand-primary', '#8b1538'),
            secondary: v('--brand-secondary', '#897174'),
            secondaryContainer: v('--brand-secondary-container', '#d08c9f'),
            tint2: v('--brand-tint-bg-2', '#ead9df'),
            accent: v('--brand-accent', '#b26a00')
        };
    }

    // ── ApexCharts ───────────────────────────────────────────────────────
    var charts = {};

    function renderChart(key, selector, options) {
        var el = document.querySelector(selector);
        if (!el || typeof ApexCharts === 'undefined') return;
        charts[key] = new ApexCharts(el, options);
        charts[key].render();
    }

    function renderCharts() {
        var p = getBrandPalette();

        // 1. Revenue & Purchase Trend
        renderChart('revenue', '#revenueChart', {
            series: [{
                name: 'Monthly Revenue (GSTR-3B)',
                data: [84, 90, 105, 112, 122, 130] // in Lakhs
            }, {
                name: 'Monthly Purchases (GSTR-2B)',
                data: [62, 65, 78, 80, 85, 92]
            }],
            chart: { type: 'bar', height: 280, toolbar: { show: false }, fontFamily: 'Inter, sans-serif' },
            plotOptions: { bar: { horizontal: false, columnWidth: '55%', borderRadius: 4, endingShape: 'rounded' } },
            dataLabels: { enabled: false },
            stroke: { show: true, width: 2, colors: ['transparent'] },
            colors: [p.primary, p.secondary],
            xaxis: {
                categories: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
                axisBorder: { show: false },
                axisTicks: { show: false }
            },
            yaxis: { title: { text: 'Amount (in ₹ Lakhs)', style: { color: '#64748b', fontWeight: 500 } } },
            fill: { opacity: 1 },
            tooltip: { y: { formatter: function (val) { return '₹ ' + val + ' Lakhs'; } } },
            legend: { position: 'top', horizontalAlign: 'right', fontWeight: 500 },
            grid: { borderColor: '#f1f5f9' }
        });

        // 2. Vendor Concentration Donut
        renderChart('vendor', '#vendorChart', {
            series: [28, 18, 13, 41],
            chart: { type: 'donut', height: 180, fontFamily: 'Inter, sans-serif' },
            labels: ['Microsoft Corp', 'Redington India', 'Persistent Systems', 'Other Vendors'],
            colors: [p.primary, p.secondary, p.secondaryContainer, p.tint2],
            legend: { show: false },
            dataLabels: { enabled: false },
            plotOptions: {
                pie: {
                    donut: {
                        size: '75%',
                        labels: {
                            show: true,
                            total: {
                                show: true,
                                label: 'Top Vendor',
                                formatter: function () { return '28%'; },
                                style: { fontSize: '13px', fontWeight: 600, color: '#475569' }
                            }
                        }
                    }
                }
            },
            tooltip: { y: { formatter: function (value) { return value + '%'; } } }
        });

        // 3. Tax Liability vs Payment
        renderChart('taxPayment', '#taxPaymentChart', {
            series: [{
                name: 'GST Tax Liability',
                data: [4.2, 4.5, 5.2, 5.6, 6.1, 6.5]
            }, {
                name: 'GST Paid Cash & ITC',
                data: [4.2, 4.5, 5.2, 5.6, 6.1, 6.5]
            }],
            chart: { type: 'bar', height: 250, toolbar: { show: false }, fontFamily: 'Inter, sans-serif' },
            plotOptions: { bar: { horizontal: false, columnWidth: '45%', borderRadius: 3 } },
            dataLabels: { enabled: false },
            colors: [p.accent, p.primary],
            xaxis: {
                categories: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
                axisBorder: { show: false },
                axisTicks: { show: false }
            },
            yaxis: { title: { text: 'Amount (in ₹ Lakhs)', style: { color: '#64748b' } } },
            legend: { position: 'top', horizontalAlign: 'right' },
            grid: { borderColor: '#f1f5f9' }
        });

        // 4. Stability Radar
        renderChart('stabilityRadar', '#stabilityRadarChart', {
            series: [{ name: 'Stability Score', data: [88, 98, 92, 87, 91, 97, 99] }],
            chart: { height: 220, type: 'radar', toolbar: { show: false }, fontFamily: 'Inter, sans-serif' },
            colors: [p.primary],
            xaxis: { categories: ['Revenue', 'Compliance', 'Cash Flow', 'Growth', 'Vendor Diversity', 'ITC', 'Tax Payment'] },
            fill: { opacity: 0.1, colors: [p.primary] },
            markers: { size: 4, colors: [p.primary], strokeColor: '#fff', strokeWidth: 2 },
            yaxis: { show: false, max: 100 }
        });

        // 5. 12-Month Forecast
        renderChart('forecast', '#forecastChart', {
            series: [{
                name: 'Projected Revenue (in Lakhs)',
                data: [132, 135, 138, 142, 145, 148, 152, 155, 160, 164, 168, 172]
            }, {
                name: 'Upper Bound (95% CI)',
                data: [135, 140, 144, 148, 152, 156, 161, 165, 171, 175, 180, 185]
            }],
            chart: { height: 250, type: 'line', toolbar: { show: false }, fontFamily: 'Inter, sans-serif' },
            stroke: { width: [3, 2], curve: 'smooth', dashArray: [0, 5] },
            colors: [p.primary, p.secondaryContainer],
            xaxis: {
                categories: ['Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec', 'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
                axisBorder: { show: false },
                axisTicks: { show: false }
            },
            yaxis: { title: { text: 'Projected Revenue (₹ Lakhs)' } },
            grid: { borderColor: '#f1f5f9' },
            legend: { position: 'top', horizontalAlign: 'right' }
        });
    }

    // ── Re-color in place on theme switch (see frlSetTheme in ~/js/theme.js) ─
    function applyChartTheme() {
        var p = getBrandPalette();
        if (charts.revenue) charts.revenue.updateOptions({ colors: [p.primary, p.secondary] });
        if (charts.vendor) charts.vendor.updateOptions({ colors: [p.primary, p.secondary, p.secondaryContainer, p.tint2] });
        if (charts.taxPayment) charts.taxPayment.updateOptions({ colors: [p.accent, p.primary] });
        if (charts.stabilityRadar) charts.stabilityRadar.updateOptions({ colors: [p.primary], fill: { opacity: 0.1, colors: [p.primary] }, markers: { colors: [p.primary] } });
        if (charts.forecast) charts.forecast.updateOptions({ colors: [p.primary, p.secondaryContainer] });
    }
    document.addEventListener('frl-theme-changed', applyChartTheme);

    // ── Download / export micro-interactions ─────────────────────────────
    function wireReportButtons() {
        document.querySelectorAll('.gst-analysis .btn-report').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var originalHtml = btn.innerHTML;

                btn.disabled = true;
                btn.innerHTML = '<i class="bi bi-hourglass-split"></i> Processing...';

                setTimeout(function () {
                    btn.innerHTML = '<i class="bi bi-check-circle-fill text-success"></i> Done!';
                    setTimeout(function () {
                        btn.disabled = false;
                        btn.innerHTML = originalHtml;
                    }, 2000);
                }, 1200);
            });
        });
    }

    function init() {
        animateCircleGauge('gstHealthGauge', 94, 100);
        animateCircleGauge('itcGauge', 97, 100);
        renderCharts();
        wireReportButtons();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
