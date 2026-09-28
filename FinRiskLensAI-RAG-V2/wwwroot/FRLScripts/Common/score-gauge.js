// ============================================================================
// Overall Financial Health Score gauge.
//
// A 0–1000 semicircular gauge: five coloured band segments, a needle, and the
// score itself filled into the middle in the colour of the band it landed in.
//
// The score used to sit as HTML text under the arc, which read as a caption
// rather than as the result. Drawing it into the centre means it scales with
// the canvas and always carries its own band colour — no CSS chasing the
// canvas geometry.
//
// Shared by the Financial Health Card (customer + bank Customer 360) and the
// printable Financial Report, which had identical copies of this before.
// ============================================================================

(function () {
    'use strict';

    // Band widths and colours match RiskScoringService.Band(): 350 / 500 / 650 / 800.
    // Semantic on purpose — red means risk in either theme, same rule the bank
    // dashboard's BAND_COLORS follow.
    var WIDTHS = [350, 150, 150, 150, 200];
    var COLORS = ['#c22f3e', '#e08a3c', '#e6c34c', '#7fae5e', '#2e7d4f'];
    var MAX = 1000;

    function bandColor(score) {
        var upper = 0;
        for (var i = 0; i < WIDTHS.length; i++) {
            upper += WIDTHS[i];
            if (score <= upper) return COLORS[i];
        }
        return COLORS[COLORS.length - 1];
    }

    // The score is always white — the fill moves to meet it, never the other way.
    var INK = '#ffffff';

    // WCAG minimum for large text (both the score and the caption are drawn
    // well above the 24px / 18.66px-bold threshold).
    var MIN_CONTRAST = 3;

    function luminance(hex) {
        var c = [1, 3, 5].map(function (i) { return parseInt(hex.substr(i, 2), 16) / 255; })
            .map(function (v) { return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); });
        return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];
    }

    function contrast(a, b) {
        var x = luminance(a), y = luminance(b);
        return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05);
    }

    function toHex(r, g, b) {
        return '#' + [r, g, b].map(function (v) {
            return Math.round(Math.max(0, Math.min(255, v))).toString(16).padStart(2, '0');
        }).join('');
    }

    /**
     * The centre fill: the band's own colour, stepped toward black only as far
     * as white legibility needs.
     *
     * At full strength white manages just 1.71:1 on the yellow Fair band and
     * 2.67:1 on AtRisk — a score nobody could read. Darkening the fill rather
     * than switching the number to black keeps every gauge looking the same
     * and still reads as the band it belongs to; the ring segment above stays
     * the undimmed colour, so the band is never misrepresented.
     */
    function centreFillFor(score) {
        var hex = bandColor(score);
        var rgb = [1, 3, 5].map(function (i) { return parseInt(hex.substr(i, 2), 16); });

        for (var f = 1; f > 0.05; f -= 0.01) {
            var candidate = toHex(rgb[0] * f, rgb[1] * f, rgb[2] * f);
            if (contrast(INK, candidate) >= MIN_CONTRAST) return candidate;
        }
        return '#333333';
    }

    /**
     * Draws the gauge onto a canvas and returns the Chart instance.
     *
     * @param {HTMLCanvasElement} canvas
     * @param {number} score      0–1000
     * @param {function} needleColor  called at draw time, so a theme switch
     *                                followed by chart.update() repaints it
     */
    window.frlScoreGauge = function (canvas, score, needleColor) {
        if (!canvas || typeof Chart === 'undefined') return null;

        var value = Math.max(0, Math.min(MAX, Number(score) || 0));

        // Filled half-disc in the band's colour, drawn under the needle.
        var centreFill = {
            id: 'frlGaugeCentre',
            beforeDatasetsDraw: function (chart) {
                var arc = chart.getDatasetMeta(0).data[0];
                if (!arc) return;
                var ctx = chart.ctx;
                var r = arc.innerRadius - 6;      // small gap so the ring stays readable
                if (r <= 0) return;

                ctx.save();
                ctx.beginPath();
                ctx.arc(arc.x, arc.y, r, Math.PI, Math.PI * 2);
                ctx.closePath();
                ctx.fillStyle = centreFillFor(value);
                ctx.fill();
                ctx.restore();
            }
        };

        // Needle: a triangle spanning the ring only. It starts at the inner
        // radius rather than the centre, so it never crosses the number.
        var needle = {
            id: 'frlGaugeNeedle',
            afterDatasetsDraw: function (chart) {
                var arc = chart.getDatasetMeta(0).data[0];
                if (!arc) return;
                var ctx = chart.ctx;
                var rIn = arc.innerRadius, rOut = arc.outerRadius;

                ctx.save();
                ctx.translate(arc.x, arc.y);
                ctx.rotate(Math.PI + (value / MAX) * Math.PI);
                ctx.beginPath();
                ctx.moveTo(rIn - 2, -6);
                ctx.lineTo(rOut - 2, 0);
                ctx.lineTo(rIn - 2, 6);
                ctx.closePath();
                ctx.fillStyle = typeof needleColor === 'function' ? needleColor() : (needleColor || '#4a5568');
                ctx.fill();
                ctx.restore();
            }
        };

        // Score + caption, white on the filled half-disc.
        var centreText = {
            id: 'frlGaugeText',
            afterDatasetsDraw: function (chart) {
                var arc = chart.getDatasetMeta(0).data[0];
                if (!arc) return;
                var ctx = chart.ctx;
                var r = arc.innerRadius;

                ctx.save();
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillStyle = INK;

                // A half-disc's visual centre sits about 0.42r above its flat edge.
                ctx.font = '800 ' + Math.round(r * 0.46) + "px 'Sora', 'Outfit', sans-serif";
                ctx.fillText(String(Math.round(value)), arc.x, arc.y - r * 0.50);

                ctx.font = '700 ' + Math.round(r * 0.18) + "px 'Inter', sans-serif";
                ctx.fillText('out of ' + MAX, arc.x, arc.y - r * 0.22);
                ctx.restore();
            }
        };

        return new Chart(canvas, {
            type: 'doughnut',
            data: {
                datasets: [{
                    data: WIDTHS,
                    backgroundColor: COLORS,
                    borderWidth: 2,
                    borderColor: '#fff'
                }]
            },
            options: {
                responsive: true,
                rotation: -90,
                circumference: 180,
                cutout: '62%',                 // roomier middle than the old 72%
                aspectRatio: 1.9,
                layout: { padding: { bottom: 4 } },
                plugins: { legend: { display: false }, tooltip: { enabled: false } },
                animation: { animateRotate: true }
            },
            plugins: [centreFill, needle, centreText]
        });
    };
})();
