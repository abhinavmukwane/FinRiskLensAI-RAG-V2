// ============================================================================
// FinRiskLens AI Assistant (Views/Shared/_Chatbot.cshtml).
//
// Talks to ChatbotController: GET /Chatbot/State (conversation + suggestions),
// POST /Chatbot/Ask, POST /Chatbot/Clear. The server rebuilds the business's
// whole assessment for every question, so this script sends only the question
// and the current page's name. Everything from the server is rendered with
// textContent: model output never becomes HTML.
// ============================================================================

(function () {
    'use strict';

    var page = window.frlChatContext || {};
    var pageName = page.pageName || page.pageKey || document.title || '';

    var root = document.getElementById('frlChatbot');
    if (!root) return;

    var launcher = document.getElementById('frlChatLauncher');
    var panel = document.getElementById('frlChatPanel');
    var closeBtn = document.getElementById('frlChatClose');
    var clearBtn = document.getElementById('frlChatClear');
    var messages = document.getElementById('frlChatMessages');
    var suggestionsBox = document.getElementById('frlChatSuggestions');
    var form = document.getElementById('frlChatForm');
    var input = document.getElementById('frlChatText');
    var sendBtn = document.getElementById('frlChatSend');
    var statusEl = document.getElementById('frlChatStatus');

    var loaded = false;
    var busy = false;
    var lastQuestion = null;
    var openingSuggestions = [];

    // ── small DOM helpers ───────────────────────────────────────────────
    function el(tag, cls, text) {
        var n = document.createElement(tag);
        if (cls) n.className = cls;
        if (text !== undefined && text !== null) n.textContent = text;
        return n;
    }

    function scrollDown() { messages.scrollTop = messages.scrollHeight; }

    function setStatus(state, text) {
        statusEl.className = 'frl-chat-status is-' + state;
        statusEl.querySelector('.frl-status-text').textContent = text;
    }

    function setBusy(b) {
        busy = b;
        sendBtn.disabled = b;
        input.setAttribute('aria-busy', b ? 'true' : 'false');
    }

    // ── rendering ───────────────────────────────────────────────────────
    function addUser(text) {
        var m = el('div', 'frl-msg frl-msg-user', text);
        messages.appendChild(m);
        scrollDown();
    }

    function paragraphs(parent, text) {
        String(text || '').split(/\n+/).filter(Boolean).forEach(function (p) {
            parent.appendChild(el('p', 'frl-p', p));
        });
    }

    function list(parent, title, items, cls) {
        if (!items || !items.length) return;
        var box = el('div', 'frl-block ' + (cls || ''));
        box.appendChild(el('div', 'frl-block-title', title));
        var ul = el('ul', 'frl-list');
        items.forEach(function (i) { ul.appendChild(el('li', null, i)); });
        box.appendChild(ul);
        parent.appendChild(box);
    }

    function finding(f) {
        var card = el('div', 'frl-finding');
        card.appendChild(el('div', 'frl-finding-title', f.title));
        if (f.sources && f.sources.length) {
            var chips = el('div', 'frl-chips');
            f.sources.forEach(function (s) { chips.appendChild(el('span', 'frl-chip', s)); });
            card.appendChild(chips);
        }
        [['Observation', f.observation], ['Figures', f.figures], ['Calculation', f.calculation],
         ['Why it matters', f.whyItMatters], ['Caveats', f.caveats]].forEach(function (row) {
            if (!row[1]) return;
            var line = el('div', 'frl-finding-row');
            line.appendChild(el('span', 'frl-finding-label', row[0]));
            line.appendChild(el('span', null, row[1]));
            card.appendChild(line);
        });
        if (f.nextStep) {
            var next = el('div', 'frl-finding-next');
            next.appendChild(el('i', 'bi bi-arrow-right-circle-fill'));
            next.appendChild(el('span', null, f.nextStep));
            card.appendChild(next);
        }
        return card;
    }

    function addBot(reply) {
        var m = el('div', 'frl-msg frl-msg-bot');
        if (reply.usesSampleData) {
            var note = el('div', 'frl-sample-note');
            note.appendChild(el('i', 'bi bi-info-circle'));
            note.appendChild(el('span', null, 'Part of this answer uses sample data, not live records.'));
            m.appendChild(note);
        }
        paragraphs(m, reply.answer);
        (reply.findings || []).forEach(function (f) { m.appendChild(finding(f)); });
        list(m, 'Recommended next steps', reply.nextActions, 'frl-block-next');
        list(m, 'Limitations', reply.limitations, 'frl-block-limits');
        messages.appendChild(m);
        scrollDown();
        renderSuggestions(reply.followUps && reply.followUps.length ? reply.followUps : openingSuggestions,
            reply.followUps && reply.followUps.length ? 'Ask next' : 'Suggested questions');
    }

    function addError(text, canRetry) {
        var m = el('div', 'frl-msg frl-msg-bot frl-msg-error');
        m.appendChild(el('p', 'frl-p', text));
        if (canRetry && lastQuestion) {
            var b = el('button', 'frl-retry', 'Retry');
            b.type = 'button';
            b.addEventListener('click', function () { m.remove(); send(lastQuestion, true); });
            m.appendChild(b);
        }
        messages.appendChild(m);
        scrollDown();
    }

    function renderSuggestions(items, title) {
        suggestionsBox.innerHTML = '';
        if (!items || !items.length) return;
        suggestionsBox.appendChild(el('div', 'frl-sugg-title', title || 'Suggested questions'));
        items.slice(0, 5).forEach(function (q) {
            var b = el('button', 'frl-sugg', q);
            b.type = 'button';
            b.addEventListener('click', function () { send(q); });
            suggestionsBox.appendChild(b);
        });
    }

    function welcome(available, missing) {
        var m = el('div', 'frl-msg frl-msg-bot');
        m.appendChild(el('p', 'frl-p', 'Hello! I can explain and compare this business\'s assessment across all its sources.'));
        if (available && available.length) list(m, 'Available', available);
        if (missing && missing.length) list(m, 'Missing or not applicable', missing, 'frl-block-limits');
        messages.appendChild(m);
    }

    // ── server calls ────────────────────────────────────────────────────
    async function loadState() {
        setStatus('busy', 'Loading…');
        try {
            var res = await fetch('/Chatbot/State', { headers: { 'Accept': 'application/json' } });
            var json = await res.json();
            if (!json.status) throw new Error(json.error || 'Could not load the assistant.');
            loaded = true;
            openingSuggestions = json.suggestions || [];
            messages.innerHTML = '';
            welcome(json.available, json.missing);
            (json.history || []).forEach(function (t) {
                if (t.role === 'user') { addUser(t.text); return; }
                var reply = null;
                try { reply = t.reply ? JSON.parse(t.reply) : null; } catch (e) { reply = null; }
                addBot(reply || { answer: t.text });
            });
            renderSuggestions(openingSuggestions, 'Suggested questions');
            setStatus('online', 'Online');
        } catch (e) {
            setStatus('error', 'Unavailable');
            addError((e && e.message) || 'Could not load the assistant.', false);
        }
    }

    async function send(question, isRetry) {
        question = String(question || '').trim();
        if (!question || busy) return;
        if (question.length > 1000) { addError('Keep the question under 1,000 characters.', false); return; }

        lastQuestion = question;
        if (!isRetry) addUser(question);
        input.value = '';
        autoSize();
        suggestionsBox.innerHTML = '';
        setBusy(true);
        setStatus('busy', 'Analysing the assessment…');

        var typing = el('div', 'frl-msg frl-msg-bot frl-typing');
        typing.setAttribute('aria-label', 'The assistant is working');
        typing.innerHTML = '<span></span><span></span><span></span>';
        messages.appendChild(typing);
        scrollDown();

        var controller = new AbortController();
        var timer = setTimeout(function () { controller.abort(); }, 75000);
        try {
            var res = await fetch('/Chatbot/Ask', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
                body: JSON.stringify({ question: question, pageKey: pageName }),
                signal: controller.signal
            });
            var json = await res.json();
            typing.remove();
            if (!json.status || !json.reply) {
                setStatus('error', 'Error');
                addError(json.error || 'The assistant couldn\'t answer. Try again.', json.retryable !== false);
            } else {
                setStatus('online', 'Online');
                addBot(json.reply);
            }
        } catch (e) {
            typing.remove();
            setStatus('error', 'Error');
            addError(e && e.name === 'AbortError'
                ? 'The assistant took too long to answer. Try again.'
                : 'Couldn\'t reach the assistant. Check your connection and try again.', true);
        } finally {
            clearTimeout(timer);
            setBusy(false);
            input.focus();
        }
    }

    async function clearConversation() {
        if (busy) return;
        try { await fetch('/Chatbot/Clear', { method: 'POST' }); } catch (e) { /* the view resets either way */ }
        loaded = false;
        lastQuestion = null;
        await loadState();
    }

    // ── open / close ────────────────────────────────────────────────────
    function open() {
        panel.hidden = false;
        root.classList.add('open');
        launcher.setAttribute('aria-expanded', 'true');
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

    launcher.addEventListener('click', open);
    closeBtn.addEventListener('click', close);
    clearBtn.addEventListener('click', clearConversation);
    form.addEventListener('submit', function (e) { e.preventDefault(); send(input.value); });
    input.addEventListener('input', autoSize);
    input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); send(input.value); }
    });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && root.classList.contains('open')) close();
    });
})();
