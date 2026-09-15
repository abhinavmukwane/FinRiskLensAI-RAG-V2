// Loan-case push from the bank portal's Customer 360.
//
// Flow: [Push to LOS / ULI / ONDC] → modal (pick channel, see what will be sent,
// open the exact payload in a new tab) → Confirm → POST → receipt opens in a
// new tab and the header chips refresh.
//
// Deliberately dependency-free: _BankAdminLayout loads Bootstrap CSS but no
// Bootstrap JS and no jQuery. The .detail-modal classes come from Dashboard.css.
(function () {
    'use strict';

    var root = document.getElementById('lcRoot');
    if (!root) return;

    var uan = root.dataset.uan;
    var token = root.dataset.token;
    var currentScore = parseFloat(root.dataset.score || '0');
    var isScored = root.dataset.scored === 'true';

    var overlay = document.getElementById('lcOverlay');
    var dialog = document.getElementById('lcModal');
    var channelBtns = Array.prototype.slice.call(dialog.querySelectorAll('.lc-channel'));
    var status = document.getElementById('lcStatus');
    var confirmBtn = document.getElementById('lcConfirm');
    var previewLink = document.getElementById('lcPreview');
    var liveNote = document.getElementById('lcLiveNote');
    var repushNote = document.getElementById('lcRepushNote');

    var live = [];         // channels with a real endpoint
    var pushes = {};       // channel → latest summary
    var selected = null;

    function el(id) { return document.getElementById(id); }

    // ── modal open / close ─────────────────────────────────────────────
    function open() {
        overlay.classList.add('active');
        dialog.classList.add('active');
        document.body.style.overflow = 'hidden';
    }
    function close() {
        overlay.classList.remove('active');
        dialog.classList.remove('active');
        document.body.style.overflow = '';
        status.textContent = '';
        status.className = 'lc-status';
    }
    el('lcClose').addEventListener('click', close);
    overlay.addEventListener('click', close);
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') close(); });

    // ── channel selection ──────────────────────────────────────────────
    function select(channel) {
        selected = channel;
        channelBtns.forEach(function (b) { b.classList.toggle('active', b.dataset.channel === channel); });

        var isLive = live.indexOf(channel) >= 0;
        liveNote.innerHTML = isLive
            ? '<i class="bi bi-broadcast"></i> Live endpoint configured — this will be <strong>sent</strong> to ' + channel + '.'
            : '<i class="bi bi-info-circle"></i> No ' + channel + ' endpoint configured — the push will be recorded as <strong>Simulated</strong> with the exact payload that would be sent.';

        var prev = pushes[channel];
        if (prev) {
            var moved = Math.abs(currentScore - prev.scoreAtPush) >= 1;
            repushNote.style.display = '';
            repushNote.innerHTML = moved
                ? '<i class="bi bi-arrow-repeat"></i> Already raised as <strong>' + prev.caseReference + '</strong> on ' + prev.pushedAt
                  + ' at score <strong>' + prev.scoreAtPush + '</strong>. The score is now <strong>' + Math.round(currentScore) + '</strong> — confirming raises a new case at the current score.'
                : '<i class="bi bi-check2-circle"></i> Already raised as <strong>' + prev.caseReference + '</strong> on ' + prev.pushedAt
                  + ' at the same score. Confirming records a second push.';
            confirmBtn.innerHTML = '<i class="bi bi-send"></i> Re-push to ' + channel;
        } else {
            repushNote.style.display = 'none';
            confirmBtn.innerHTML = '<i class="bi bi-send"></i> Confirm push to ' + channel;
        }

        previewLink.href = '/loan-case/preview?uan=' + encodeURIComponent(uan) + '&channel=' + channel;
        confirmBtn.disabled = !isScored;
    }
    channelBtns.forEach(function (b) {
        b.addEventListener('click', function () { select(b.dataset.channel); });
    });

    // ── header chips ───────────────────────────────────────────────────
    function renderChips() {
        var host = el('lcChips');
        var channels = ['LOS', 'ULI', 'ONDC'];
        host.innerHTML = channels.map(function (ch) {
            var p = pushes[ch];
            if (!p) return '';
            var stale = Math.abs(currentScore - p.scoreAtPush) >= 1;
            var cls = p.state === 'Failed' ? 'failed' : (stale ? 'stale' : (p.state === 'Sent' ? 'sent' : 'simulated'));
            var icon = p.state === 'Failed' ? 'bi-x-octagon' : (stale ? 'bi-exclamation-diamond' : 'bi-check2-circle');

            // Keep chips short — three of them sit side by side. The channel is the
            // headline; everything else lives in the tooltip and on the receipt.
            var label = ch + (p.state === 'Simulated' ? '<span class="lc-chip-sub">sim</span>' : '')
                           + (stale ? '<span class="lc-chip-sub">re-score</span>' : '');

            var tip = (p.state === 'Failed' ? 'Failed' : (p.state === 'Simulated' ? 'Recorded (simulated)' : 'Sent'))
                    + ' · ' + p.caseReference + ' · ' + p.pushedAt
                    + ' · score ' + p.scoreAtPush + (stale ? ' (now ' + Math.round(currentScore) + ')' : '')
                    + (p.pushedBy ? ' · ' + p.pushedBy : '');

            return '<a class="lc-chip ' + cls + '" href="/loan-case/receipt/' + p.id + '" target="_blank" rel="noopener"'
                 + ' title="' + tip + '"><i class="bi ' + icon + '"></i> ' + label + '</a>';
        }).join('');
    }

    function refresh() {
        return fetch('/loan-case/status?uan=' + encodeURIComponent(uan))
            .then(function (r) { return r.json(); })
            .then(function (j) {
                live = j.live || [];
                pushes = {};
                (j.pushes || []).forEach(function (p) { pushes[p.channel] = p; });
                renderChips();
            })
            .catch(function () { /* chips are informational; the page stays usable */ });
    }

    // ── open button ────────────────────────────────────────────────────
    el('lcOpen').addEventListener('click', function () {
        refresh().then(function () {
            select(selected || 'LOS');
            open();
        });
    });

    // ── confirm ────────────────────────────────────────────────────────
    confirmBtn.addEventListener('click', function () {
        if (!selected) return;
        confirmBtn.disabled = true;
        status.className = 'lc-status';
        status.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Raising case in ' + selected + '…';

        var body = new URLSearchParams();
        body.set('uan', uan);
        body.set('channel', selected);
        body.set('__RequestVerificationToken', token);

        fetch('/loan-case/push', { method: 'POST', body: body, headers: { 'X-Requested-With': 'fetch' } })
            .then(function (r) { return r.json(); })
            .then(function (j) {
                if (j.status) {
                    status.className = 'lc-status ok';
                    status.innerHTML = '<i class="bi bi-check-circle-fill"></i> ' + j.message
                        + (j.receiptUrl ? ' <a href="' + j.receiptUrl + '" target="_blank" rel="noopener">Open receipt</a>' : '');
                    if (j.receiptUrl) window.open(j.receiptUrl, '_blank', 'noopener');
                    refresh().then(function () { select(selected); });
                } else {
                    status.className = 'lc-status err';
                    status.innerHTML = '<i class="bi bi-x-circle-fill"></i> ' + (j.message || 'The push failed.');
                    confirmBtn.disabled = false;
                }
            })
            .catch(function () {
                status.className = 'lc-status err';
                status.innerHTML = '<i class="bi bi-x-circle-fill"></i> Could not reach the server. Please try again.';
                confirmBtn.disabled = false;
            });
    });

    // Initial chips from the server-rendered data (no fetch on first paint).
    try {
        var seed = JSON.parse(root.dataset.pushes || '[]');
        seed.forEach(function (p) { pushes[p.channel] = p; });
        live = JSON.parse(root.dataset.live || '[]');
        renderChips();
    } catch (e) { /* fall back to fetch on open */ }
})();
