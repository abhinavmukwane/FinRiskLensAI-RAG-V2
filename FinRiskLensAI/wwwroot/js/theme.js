// FinRiskLens AI theme switcher.
// Loaded synchronously in <head> so the data-theme attribute is set before
// first paint (no flash of the wrong theme). Theme 1 = IDBI (default),
// Theme 2 = classic maroon. Persisted in localStorage under "frl-theme".
(function () {
    var theme = 'theme1';
    try { theme = localStorage.getItem('frl-theme') || 'theme1'; } catch (e) { /* storage blocked */ }
    if (theme !== 'theme1' && theme !== 'theme2') theme = 'theme1';
    document.documentElement.setAttribute('data-theme', theme);

    window.frlSetTheme = function (next) {
        if (next !== 'theme1' && next !== 'theme2') return;
        document.documentElement.setAttribute('data-theme', next);
        try { localStorage.setItem('frl-theme', next); } catch (e) { /* storage blocked */ }
        // Anything that baked a --brand-* CSS variable into a JS value at load
        // time (e.g. Chart.js canvases) can't react to the attribute flip on
        // its own — let it know the theme actually changed.
        document.dispatchEvent(new CustomEvent('frl-theme-changed', { detail: { theme: next } }));
    };

    // Header toggle button(s) — flip between the two themes.
    document.addEventListener('DOMContentLoaded', function () {
        var toggles = document.querySelectorAll('[data-frl-theme-toggle]');
        for (var i = 0; i < toggles.length; i++) {
            toggles[i].addEventListener('click', function () {
                var current = document.documentElement.getAttribute('data-theme');
                window.frlSetTheme(current === 'theme2' ? 'theme1' : 'theme2');
            });
        }
    });
})();
