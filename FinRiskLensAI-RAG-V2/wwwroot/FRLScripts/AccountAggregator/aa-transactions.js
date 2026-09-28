// ============================================================================
// Transaction History — tabs, search and the month drill-down.
//
// Moved out of aa-analysis.js when the ledger got its own page. Filtering is
// client-side over rows the server already rendered and classified; nothing
// here decides what a transaction is.
// ============================================================================

(function () {
    'use strict';

    const root = document.getElementById('aaTransactions');
    if (!root) return;

    const table = document.getElementById('aaTxnTable');
    if (!table) return;

    const rows = Array.from(table.querySelectorAll('tbody tr'));
    const counter = document.getElementById('aaRowCount');
    const search = document.getElementById('aaSearch');
    const tabs = Array.from(root.querySelectorAll('.aa-tab'));
    const clearMonth = document.getElementById('aaClearMonth');
    const monthLabel = document.getElementById('aaMonthLabel');

    let group = 'all';

    // Arriving from a month row on the deep-analysis page: /Transactions?month=2025-07
    const requested = new URLSearchParams(window.location.search).get('month');
    let month = /^\d{4}-\d{2}$/.test(requested || '') ? requested : null;

    function apply() {
        const q = (search && search.value || '').trim().toLowerCase();
        let shown = 0;

        rows.forEach(function (tr) {
            const inGroup = group === 'all' || (tr.dataset.groups || '').split(' ').indexOf(group) !== -1;
            const inMonth = !month || tr.dataset.month === month;
            const matches = !q || tr.textContent.toLowerCase().indexOf(q) !== -1;
            const visible = inGroup && inMonth && matches;
            tr.style.display = visible ? '' : 'none';
            if (visible) shown++;
        });

        if (counter) counter.textContent = shown + ' of ' + rows.length + ' shown';
        if (monthLabel) monthLabel.textContent = month || '';
        if (clearMonth) clearMonth.style.display = month ? '' : 'none';
    }

    tabs.forEach(function (tab) {
        tab.addEventListener('click', function () {
            tabs.forEach(t => t.classList.remove('active'));
            tab.classList.add('active');
            group = tab.dataset.group;
            apply();
        });
    });

    if (search) search.addEventListener('input', apply);

    if (clearMonth) {
        clearMonth.addEventListener('click', function () {
            month = null;
            apply();
        });
    }

    apply();
})();
