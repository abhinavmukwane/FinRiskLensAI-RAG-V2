// ============================================================================
// Panel tabs — the .mca2-tabs / .tab-panel switcher.
//
// Lifted out of McaDetails.js when the ITR page needed the same behaviour.
// Purely presentational: shows one panel at a time, no data handling.
// ============================================================================

(function () {
    'use strict';

    var tabs = document.querySelectorAll('.mca2-tab');
    if (!tabs.length) return;

    tabs.forEach(function (tab) {
        tab.addEventListener('click', function () {
            document.querySelectorAll('.mca2-tab').forEach(t => t.classList.remove('active'));
            document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
            tab.classList.add('active');
            var panel = document.getElementById(tab.dataset.tab);
            if (panel) panel.classList.add('active');
        });
    });
})();
