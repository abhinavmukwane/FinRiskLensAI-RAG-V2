// ============================================================================
// FinRiskLens AI Product Guide (Views/Home/_ProductGuide.cshtml) — the public
// home page assistant. Talks to ProductGuideController: GET /ProductGuide/State
// (welcome, starter topics, this visit's conversation), POST /ProductGuide/Ask
// ({ topic } for a starter, { question } otherwise), POST /ProductGuide/Clear.
//
// Everything is built as DOM nodes with textContent; answers go through the
// shared safe Markdown renderer (ChatMarkdown.js), so nothing a model returns
// can run as HTML or script. Nothing typed here is kept in browser storage.
// ============================================================================

(function () {
    'use strict';

    var root = document.getElementById('frlGuide');
    if (!root || !window.FrlChatMarkdown) return;

    var renderMarkdown = window.FrlChatMarkdown.render;
    var el = window.FrlChatMarkdown.el;

    var launcher = document.getElementById('frlGuideLauncher');
    var panel = document.getElementById('frlGuidePanel');
    var closeBtn = document.getElementById('frlGuideClose');
    var clearBtn = document.getElementById('frlGuideClear');
    var expandBtn = document.getElementById('frlGuideExpand');
    var messages = document.getElementById('frlGuideMessages');
    var form = document.getElementById('frlGuideForm');
    var input = document.getElementById('frlGuideText');
    var sendBtn = document.getElementById('frlGuideSend');
    var statusEl = document.getElementById('frlGuideStatus');

    var EXPANDED_KEY = 'frl-guide-expanded';   // layout preference only
    var SEEN_KEY = 'frl-guide-seen';           // hides the launcher hint after the first open
    var MAX_CHARS = 500;
    var loaded = false;
    var busy = false;
    var lastRequest = null;
    var starters = [];
    var startRoute = '/Onboarding/CustOnboarding';
    var welcomeText = '';

    // ── helpers ─────────────────────────────────────────────────────────
    function icon(name) {
        var i = el('i', 'bi ' + name);
        i.setAttribute('aria-hidden', 'true');
        return i;
    }

    function nearBottom() {
        return messages.scrollHeight - messages.scrollTop - messages.clientHeight < 80;
    }

    function scrollToBottom() { messages.scrollTop = messages.scrollHeight; }

    function setStatus(state, text) {
        statusEl.className = 'frl-chat-status is-' + state;
        statusEl.querySelector('.frl-status-text').textContent = text;
    }

    function setBusy(b) {
        busy = b;
        sendBtn.disabled = b;
        input.setAttribute('aria-busy', b ? 'true' : 'false');
        messages.querySelectorAll('.frl-guide-chip, .frl-guide-starter').forEach(function (c) { c.disabled = b; });
    }

    function botRow() {
        var row = el('div', 'frl-row frl-row-bot');
        var avatar = el('div', 'frl-row-avatar');
        avatar.setAttribute('aria-hidden', 'true');
        avatar.appendChild(icon('bi-stars'));
        row.appendChild(avatar);
        var msg = el('div', 'frl-msg frl-msg-bot');
        row.appendChild(msg);
        return { row: row, msg: msg };
    }

    // Only the latest answer keeps its follow-up chips
    function clearOldChips() {
        messages.querySelectorAll('.frl-guide-next').forEach(function (n) { n.remove(); });
    }

    // ── content blocks ──────────────────────────────────────────────────
    function starterGrid() {
        var grid = el('div', 'frl-guide-starters');
        starters.forEach(function (s) {
            var b = el('button', 'frl-guide-starter');
            b.type = 'button';
            b.appendChild(icon(s.icon || 'bi-chat'));
            b.appendChild(el('span', null, s.label));
            b.addEventListener('click', function () { send({ topic: s.id, label: s.label }); });
            grid.appendChild(b);
        });
        return grid;
    }

    function featureCards(cards) {
        var grid = el('div', 'frl-guide-cards');
        cards.forEach(function (c) {
            var card = el('div', 'frl-guide-card');
            var head = el('div', 'frl-guide-card-head');
            head.appendChild(icon(c.icon || 'bi-dot'));
            head.appendChild(el('span', 'frl-guide-card-title', c.title));
            card.appendChild(head);
            card.appendChild(el('p', 'frl-guide-card-text', c.text));
            if (c.tag) card.appendChild(el('span', 'frl-guide-tag', c.tag));
            grid.appendChild(card);
        });
        return grid;
    }

    function analysisCards(cards) {
        var list = el('div', 'frl-guide-analysis');
        cards.forEach(function (c, n) {
            var d = el('details', 'frl-guide-acc');
            if (n === 0) d.open = true;
            var s = el('summary');
            s.appendChild(icon(c.icon || 'bi-dot'));
            s.appendChild(el('span', null, c.title));
            d.appendChild(s);
            var dl = el('dl');
            [['What is examined', c.examines], ['Why it matters', c.whyItMatters],
             ['What you can understand', c.insight], ['Useful extra context', c.context]]
                .forEach(function (r) {
                    if (!r[1]) return;
                    dl.appendChild(el('dt', null, r[0]));
                    dl.appendChild(el('dd', null, r[1]));
                });
            d.appendChild(dl);
            list.appendChild(d);
        });
        return list;
    }

    function startCta() {
        var box = el('div', 'frl-guide-cta');
        box.appendChild(el('span', null, 'Ready to understand your business financial health?'));
        var a = el('a', 'frl-guide-cta-btn');
        a.href = startRoute;
        a.appendChild(document.createTextNode('Start MSME Assessment '));
        a.appendChild(icon('bi-arrow-right'));
        box.appendChild(a);
        return box;
    }

    function nextChips(items) {
        var wrap = el('div', 'frl-guide-next');
        if (items && items.length) {
            wrap.appendChild(el('div', 'frl-sugg-title', 'Ask next'));
            items.slice(0, 3).forEach(function (q) {
                var b = el('button', 'frl-guide-chip', q);
                b.type = 'button';
                b.addEventListener('click', function () { send({ question: q }); });
                wrap.appendChild(b);
            });
        }
        var topics = el('button', 'frl-guide-chip frl-guide-chip-ghost');
        topics.type = 'button';
        topics.appendChild(icon('bi-grid-1x2'));
        topics.appendChild(document.createTextNode(' Explore topics'));
        topics.addEventListener('click', function () { showTopics(); });
        wrap.appendChild(topics);
        return wrap;
    }

    // ── rendering ───────────────────────────────────────────────────────
    function addUser(text) {
        var row = el('div', 'frl-row frl-row-user');
        var bubble = el('div', 'frl-msg frl-msg-user', text);
        row.appendChild(bubble);
        messages.appendChild(row);
        scrollToBottom();
        return bubble;
    }

    function addBot(reply, opts) {
        opts = opts || {};
        var parts = botRow();
        if (reply.source === 'fallback') parts.msg.classList.add('frl-guide-fallback');
        parts.msg.appendChild(renderMarkdown(reply.answer));
        if (reply.cards && reply.cards.length)
            parts.msg.appendChild(reply.cardStyle === 'analysis' ? analysisCards(reply.cards) : featureCards(reply.cards));
        if (reply.showStartCta) parts.msg.appendChild(startCta());
        if (opts.chips !== false) parts.msg.appendChild(nextChips(reply.followUps));
        messages.appendChild(parts.row);
        // Bring the start of a new answer into view, but only if the reader was following along
        if (opts.follow) messages.scrollTop = Math.max(0, parts.row.offsetTop - 8);
    }

    function welcome() {
        var parts = botRow();
        parts.msg.classList.add('frl-guide-welcome');
        parts.msg.appendChild(el('p', 'frl-guide-welcome-text', welcomeText));
        parts.msg.appendChild(starterGrid());
        messages.appendChild(parts.row);
    }

    function showTopics() {
        if (busy) return;
        clearOldChips();
        var parts = botRow();
        parts.msg.appendChild(el('p', null, 'What would you like to explore?'));
        parts.msg.appendChild(starterGrid());
        messages.appendChild(parts.row);
        messages.scrollTop = Math.max(0, parts.row.offsetTop - 8);
    }

    function addError(text, canRetry) {
        var parts = botRow();
        parts.msg.classList.add('frl-msg-error');
        parts.msg.appendChild(el('p', null, text));
        if (canRetry && lastRequest) {
            var b = el('button', 'frl-retry', 'Try Again');
            b.type = 'button';
            b.addEventListener('click', function () { parts.row.remove(); send(lastRequest, true); });
            parts.msg.appendChild(b);
        }
        messages.appendChild(parts.row);
        scrollToBottom();
    }

    // ── server calls ────────────────────────────────────────────────────
    async function loadState() {
        setStatus('busy', 'Loading…');
        try {
            var res = await fetch('/ProductGuide/State', { headers: { 'Accept': 'application/json' } });
            var json = await res.json();
            if (!json.status) throw new Error();
            loaded = true;
            starters = json.starters || [];
            startRoute = json.startRoute || startRoute;
            welcomeText = json.welcome || '';
            messages.textContent = '';
            welcome();
            var history = json.history || [];
            var lastBot = -1;
            history.forEach(function (t, n) { if (t.role !== 'user') lastBot = n; });
            history.forEach(function (t, n) {
                if (t.role === 'user') { addUser(t.text); return; }
                var reply = null;
                try { reply = t.reply ? JSON.parse(t.reply) : null; } catch (e) { reply = null; }
                addBot(reply || { answer: t.text }, { chips: n === lastBot });
            });
            scrollToBottom();
            setStatus('online', 'Online');
        } catch (e) {
            setStatus('error', 'Unavailable');
            messages.textContent = '';
            addError('The guide couldn\'t load. Try again in a moment.', false);
            var retry = el('button', 'frl-retry', 'Try Again');
            retry.type = 'button';
            retry.addEventListener('click', loadState);
            messages.lastChild.querySelector('.frl-msg').appendChild(retry);
        }
    }

    // request: { topic, label } for a starter, { question } otherwise
    async function send(request, isRetry) {
        if (busy) return;
        var text = String(request.label || request.question || '').trim();
        if (!text) return;
        if (text.length > MAX_CHARS) { addError('Keep the question under 500 characters.', false); return; }

        lastRequest = request;
        clearOldChips();
        var bubble = isRetry ? null : addUser(text);
        input.value = '';
        autoSize();
        setBusy(true);
        setStatus('busy', 'Thinking…');

        var typing = botRow();
        typing.msg.classList.add('frl-typing');
        typing.msg.setAttribute('aria-label', 'The guide is writing an answer');
        typing.msg.appendChild(el('span'));
        typing.msg.appendChild(el('span'));
        typing.msg.appendChild(el('span'));
        messages.appendChild(typing.row);
        scrollToBottom();

        var controller = new AbortController();
        var timer = setTimeout(function () { controller.abort(); }, 60000);
        try {
            var res = await fetch('/ProductGuide/Ask', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
                body: JSON.stringify(request.topic ? { topic: request.topic } : { question: request.question }),
                signal: controller.signal
            });
            var json = await res.json();
            var following = nearBottom();
            typing.row.remove();
            if (!json.status || !json.reply) {
                setStatus('error', 'Error');
                addError(json.error || 'The guide couldn\'t answer. Try again.', json.retryable !== false);
            } else {
                // A question with personal details was not sent on or kept: don't keep showing it either
                if (json.reply.source === 'private' && bubble) {
                    bubble.textContent = 'Message hidden: it contained personal details.';
                    bubble.classList.add('frl-guide-hidden-msg');
                }
                setStatus('online', 'Online');
                addBot(json.reply, { follow: following });
            }
        } catch (e) {
            typing.row.remove();
            setStatus('error', 'Error');
            addError(e && e.name === 'AbortError'
                ? 'The guide took too long to answer. Try again.'
                : 'Couldn\'t reach the guide. Check your connection and try again.', true);
        } finally {
            clearTimeout(timer);
            setBusy(false);
            input.focus();
        }
    }

    async function clearConversation() {
        if (busy) return;
        try { await fetch('/ProductGuide/Clear', { method: 'POST' }); } catch (e) { /* the view resets either way */ }
        lastRequest = null;
        await loadState();
    }

    // ── open / close / expand ───────────────────────────────────────────
    function setExpanded(on) {
        root.classList.toggle('expanded', on);
        expandBtn.setAttribute('aria-pressed', on ? 'true' : 'false');
        expandBtn.setAttribute('aria-label', on ? 'Restore the guide to a side panel' : 'Expand the guide');
        expandBtn.title = on ? 'Restore to side panel' : 'Expand';
        expandBtn.querySelector('i').className = on ? 'bi bi-fullscreen-exit' : 'bi bi-arrows-fullscreen';
        try { localStorage.setItem(EXPANDED_KEY, on ? '1' : '0'); } catch (e) { /* optional */ }
    }

    function open() {
        panel.hidden = false;
        root.classList.add('open');
        root.classList.add('seen');
        launcher.setAttribute('aria-expanded', 'true');
        try { sessionStorage.setItem(SEEN_KEY, '1'); } catch (e) { /* optional */ }
        if (!loaded) loadState();
        setTimeout(function () { input.focus(); }, 50);
    }

    function close() {
        root.classList.remove('open');
        launcher.setAttribute('aria-expanded', 'false');
        setTimeout(function () { if (!root.classList.contains('open')) panel.hidden = true; }, 250);
        launcher.focus();
    }

    function autoSize() {
        input.style.height = 'auto';
        input.style.height = Math.min(input.scrollHeight, 140) + 'px';
    }

    try { setExpanded(localStorage.getItem(EXPANDED_KEY) === '1'); } catch (e) { setExpanded(false); }
    try { if (sessionStorage.getItem(SEEN_KEY) === '1') root.classList.add('seen'); } catch (e) { /* optional */ }

    launcher.addEventListener('click', open);
    closeBtn.addEventListener('click', close);
    expandBtn.addEventListener('click', function () { setExpanded(!root.classList.contains('expanded')); });
    clearBtn.addEventListener('click', clearConversation);
    form.addEventListener('submit', function (e) { e.preventDefault(); send({ question: input.value }); });
    input.addEventListener('input', autoSize);
    input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); send({ question: input.value }); }
    });
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape' || !root.classList.contains('open')) return;
        if (root.classList.contains('expanded')) setExpanded(false); else close();
    });
})();
