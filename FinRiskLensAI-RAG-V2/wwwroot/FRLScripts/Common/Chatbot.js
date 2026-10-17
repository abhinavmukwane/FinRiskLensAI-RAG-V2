// ============================================================================
// FinRiskLens AI Assistant (Views/Shared/_Chatbot.cshtml).
//
// Talks to ChatbotController: GET /Chatbot/State (conversation + suggestions),
// POST /Chatbot/Ask, POST /Chatbot/Clear. The server rebuilds the business's
// whole assessment for every question, so this script sends only the question
// and the current page's name.
//
// Answers arrive as Markdown. renderMarkdown() (ChatMarkdown.js) builds DOM nodes and puts every
// piece of text in with createTextNode, so HTML or script in a model reply is
// shown as text and can never run.
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
    var expandBtn = document.getElementById('frlChatExpand');
    var messages = document.getElementById('frlChatMessages');
    var suggestionsBox = document.getElementById('frlChatSuggestions');
    var form = document.getElementById('frlChatForm');
    var input = document.getElementById('frlChatText');
    var sendBtn = document.getElementById('frlChatSend');
    var statusEl = document.getElementById('frlChatStatus');

    var EXPANDED_KEY = 'frl-chat-expanded';
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
    }

    // Safe Markdown → DOM lives in ChatMarkdown.js (shared with the home page Product Guide)
    var renderMarkdown = window.FrlChatMarkdown.render;

    // ── rendering ───────────────────────────────────────────────────────
    function addUser(text) {
        var row = el('div', 'frl-row frl-row-user');
        row.appendChild(el('div', 'frl-msg frl-msg-user', text));
        messages.appendChild(row);
        scrollToBottom();
    }

    function botRow() {
        var row = el('div', 'frl-row frl-row-bot');
        var avatar = el('div', 'frl-row-avatar');
        avatar.setAttribute('aria-hidden', 'true');
        avatar.appendChild(el('i', 'bi bi-robot'));
        row.appendChild(avatar);
        var msg = el('div', 'frl-msg frl-msg-bot');
        row.appendChild(msg);
        return { row: row, msg: msg };
    }

    // Older structured replies (findings / next actions) still render, as Markdown
    function legacyMarkdown(reply) {
        var out = [];
        (reply.findings || []).forEach(function (f) {
            out.push('#### ' + f.title);
            if (f.sources && f.sources.length) out.push('*' + f.sources.join(' · ') + '*');
            [['Observation', f.observation], ['Figures', f.figures], ['Calculation', f.calculation],
             ['Why it matters', f.whyItMatters], ['Caveats', f.caveats], ['Next step', f.nextStep]]
                .forEach(function (r) { if (r[1]) out.push('- **' + r[0] + ':** ' + r[1]); });
            out.push('');
        });
        if (reply.nextActions && reply.nextActions.length) {
            out.push('### Recommended Next Steps');
            reply.nextActions.forEach(function (a, n) { out.push((n + 1) + '. ' + a); });
            out.push('');
        }
        if (reply.limitations && reply.limitations.length) {
            out.push('### Evidence and Limitations');
            reply.limitations.forEach(function (a) { out.push('- ' + a); });
        }
        return out.join('\n');
    }

    function addBot(reply, followView) {
        var parts = botRow();
        parts.msg.appendChild(renderMarkdown(reply.answer));
        var extra = legacyMarkdown(reply);
        if (extra.trim()) parts.msg.appendChild(renderMarkdown(extra));
        messages.appendChild(parts.row);

        // Bring the start of a new answer into view, but only if the reader was
        // already following the conversation; never yank them away from an earlier message.
        if (followView) messages.scrollTop = Math.max(0, parts.row.offsetTop - 8);

        var follow = reply.followUps && reply.followUps.length;
        renderSuggestions(follow ? reply.followUps : openingSuggestions, follow ? 'Ask next' : 'Suggested questions');
    }

    function addError(text, canRetry) {
        var parts = botRow();
        parts.msg.classList.add('frl-msg-error');
        parts.msg.appendChild(el('p', null, text));
        if (canRetry && lastQuestion) {
            var b = el('button', 'frl-retry', 'Retry');
            b.type = 'button';
            b.addEventListener('click', function () { parts.row.remove(); send(lastQuestion, true); });
            parts.msg.appendChild(b);
        }
        messages.appendChild(parts.row);
        scrollToBottom();
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
        var md = ['Hello! I can explain and compare this business\'s assessment across all its sources.'];
        if (available && available.length) { md.push('', '**Available**'); available.forEach(function (a) { md.push('- ' + a); }); }
        if (missing && missing.length) { md.push('', '**Missing or not applicable**'); missing.forEach(function (a) { md.push('- ' + a); }); }
        var parts = botRow();
        parts.msg.appendChild(renderMarkdown(md.join('\n')));
        messages.appendChild(parts.row);
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
                addBot(reply || { answer: t.text }, false);
            });
            renderSuggestions(openingSuggestions, 'Suggested questions');
            scrollToBottom();
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

        var typing = botRow();
        typing.msg.classList.add('frl-typing');
        typing.msg.setAttribute('aria-label', 'The assistant is working');
        typing.msg.innerHTML = '<span></span><span></span><span></span>';
        messages.appendChild(typing.row);
        scrollToBottom();

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
            var following = nearBottom();
            typing.row.remove();
            if (!json.status || !json.reply) {
                setStatus('error', 'Error');
                addError(json.error || 'The assistant couldn\'t answer. Try again.', json.retryable !== false);
            } else {
                setStatus('online', 'Online');
                addBot(json.reply, following);
            }
        } catch (e) {
            typing.row.remove();
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

    // ── open / close / expand ───────────────────────────────────────────
    function setExpanded(on) {
        root.classList.toggle('expanded', on);
        expandBtn.setAttribute('aria-pressed', on ? 'true' : 'false');
        expandBtn.setAttribute('aria-label', on ? 'Restore the assistant to a side panel' : 'Expand the assistant');
        expandBtn.title = on ? 'Restore to side panel' : 'Expand';
        expandBtn.querySelector('i').className = on ? 'bi bi-fullscreen-exit' : 'bi bi-arrows-fullscreen';
        try { localStorage.setItem(EXPANDED_KEY, on ? '1' : '0'); } catch (e) { /* optional */ }
    }

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
        input.style.height = Math.min(input.scrollHeight, 160) + 'px';
    }

    try { setExpanded(localStorage.getItem(EXPANDED_KEY) === '1'); } catch (e) { setExpanded(false); }

    launcher.addEventListener('click', open);
    closeBtn.addEventListener('click', close);
    expandBtn.addEventListener('click', function () { setExpanded(!root.classList.contains('expanded')); });
    clearBtn.addEventListener('click', clearConversation);
    form.addEventListener('submit', function (e) { e.preventDefault(); send(input.value); });
    input.addEventListener('input', autoSize);
    input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); send(input.value); }
    });
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape' || !root.classList.contains('open')) return;
        if (root.classList.contains('expanded')) setExpanded(false); else close();
    });
})();
