// ============================================================================
// Panel tabs — the .mca2-tabs / .tab-panel switcher.
//
// Lifted out of McaDetails.js when the ITR page needed the same behaviour.
// Purely presentational: shows one panel at a time, no data handling.
//
// Scoped per tab strip, NOT per document. The bank portal's Customer 360
// renders the MCA and ITR bodies into the same page (every pane is
// server-rendered and merely hidden), so a document-wide querySelectorAll
// would let a click on an ITR tab clear MCA's active tab and vice versa.
// Each strip only ever touches the panels its own buttons name.
// ============================================================================

(function () {
    'use strict';

    // _McaDetailsBody emits its own <script src> for this file, so on a page that
    // renders that partial *and* links the file itself — the bank portal's
    // Customer 360 — it would load twice and double-register every listener.
    // Harmless (the handler is idempotent) but wasteful, so bind once.
    if (window.__frlPanelTabsBound) return;
    window.__frlPanelTabsBound = true;

    document.querySelectorAll('.mca2-tabs').forEach(function (strip) {
        var tabs = Array.prototype.slice.call(strip.querySelectorAll('.mca2-tab'));
        if (!tabs.length) return;

        // The panels this strip owns, resolved from its own buttons.
        var panels = tabs
            .map(function (t) { return document.getElementById(t.dataset.tab); })
            .filter(Boolean);

        tabs.forEach(function (tab) {
            tab.addEventListener('click', function () {
                tabs.forEach(function (t) { t.classList.remove('active'); });
                panels.forEach(function (p) { p.classList.remove('active'); });

                tab.classList.add('active');
                var panel = document.getElementById(tab.dataset.tab);
                if (panel) panel.classList.add('active');
            });
        });
    });
})();
