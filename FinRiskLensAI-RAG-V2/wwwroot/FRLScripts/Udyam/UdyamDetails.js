// ============================================================================
// Udyam Details — Business Identity & Trust Analysis page behavior.
// All data is server-rendered by UdyamController into the Razor view; this
// script only handles presentation behavior (gauge animation, modal titles,
// refresh). The computed analysis is also available as JSON from
// GET /Udyam/GetUdyamAnalysis for reuse elsewhere.
// ============================================================================

(function () {
    'use strict';

    // Verification modal — swap the title to the launching button's data-title
    const verificationModal = document.getElementById('verificationModal');
    if (verificationModal) {
        verificationModal.addEventListener('show.bs.modal', function (event) {
            const button = event.relatedTarget;
            const title = button ? button.getAttribute('data-title') : null;
            if (title) {
                verificationModal.querySelector('.modal-title').textContent = title;
            }
        });
    }

    document.addEventListener('DOMContentLoaded', function () {

        // Animate the Business Identity Score gauge on load
        const circle = document.getElementById('gaugeProgress');
        if (circle) {
            const radius = circle.r.baseVal.value;
            const circumference = 2 * Math.PI * radius;
            const score = parseInt(document.getElementById('reliabilityScore').textContent, 10) || 0;
            const offset = circumference - (score / 100) * circumference;

            circle.style.strokeDasharray = circumference + ' ' + circumference;
            circle.style.strokeDashoffset = circumference;

            setTimeout(function () {
                circle.style.transition = 'stroke-dashoffset 1.2s ease-out';
                circle.style.strokeDashoffset = offset;
            }, 300);
        }

        // Refresh Data — re-render from the server (data comes from m_StaticResponces)
        const btnRefresh = document.getElementById('btnRefreshData');
        if (btnRefresh) {
            btnRefresh.addEventListener('click', function () {
                window.location.reload();
            });
        }
    });
})();
