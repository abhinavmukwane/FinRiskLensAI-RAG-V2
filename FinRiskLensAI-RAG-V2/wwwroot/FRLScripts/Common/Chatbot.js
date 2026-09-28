// ============================================================================
// FinRiskLens AI — universal chatbot (Views/Shared/_Chatbot.cshtml).
//
// Any page can hand the bot its screen data by setting, before this script:
//     window.frlChatContext = { pageKey, pageName, context };
// The context object travels to the server ONCE — with the first question —
// where ChatbotController caches it in session for the rest of the chat.
// ============================================================================

(function () {
    'use strict';

    const pageInfo = window.frlChatContext || {
        pageKey: 'General',
        pageName: 'FinRiskLens AI dashboard',
        context: null
    };

    const root = document.getElementById('frlChatbot');
    if (!root) return;

    const launcher = document.getElementById('frlChatLauncher');
    const panel = document.getElementById('frlChatPanel');
    const closeBtn = document.getElementById('frlChatClose');
    const messages = document.getElementById('frlChatMessages');
    const input = document.getElementById('frlChatText');
    const sendBtn = document.getElementById('frlChatSend');

    let history = [];        // rolling {role, content} turns for the server
    let contextSent = false; // screen data goes with the FIRST question only
    let greeted = false;
    let busy = false;

    // ── UI helpers ──────────────────────────────────────────────────────

    function addMessage(kind, text) {
        const bubble = document.createElement('div');
        bubble.className = 'frl-msg ' + (kind === 'user' ? 'frl-msg-user' : 'frl-msg-bot');
        bubble.textContent = text;
        messages.appendChild(bubble);
        messages.scrollTop = messages.scrollHeight;
        return bubble;
    }

    function showTyping() {
        const wrap = document.createElement('div');
        wrap.className = 'frl-msg frl-msg-bot frl-typing';
        wrap.id = 'frlTyping';
        wrap.innerHTML = '<span></span><span></span><span></span>';
        messages.appendChild(wrap);
        messages.scrollTop = messages.scrollHeight;
    }

    function hideTyping() {
        const t = document.getElementById('frlTyping');
        if (t) t.remove();
    }

    // ── Open / close ────────────────────────────────────────────────────

    function openChat() {
        root.classList.add('open');
        if (!greeted) {
            greeted = true;
            const greeting = 'Hi! I’m your FinRiskLens AI assistant. I can explain anything on the ' +
                pageInfo.pageName + ' — scores, statuses, what the numbers mean. What would you like to know?';
            addMessage('bot', greeting);
            history.push({ role: 'assistant', content: greeting });
        }
        setTimeout(function () { input.focus(); }, 260);
    }

    function closeChat() {
        root.classList.remove('open');
    }

    // ── Ask the server ──────────────────────────────────────────────────

    async function send() {
        const text = (input.value || '').trim();
        if (!text || busy) return;

        busy = true;
        sendBtn.disabled = true;
        input.value = '';
        addMessage('user', text);

        // History snapshot BEFORE this question; the server appends it itself.
        const payload = {
            pageKey: pageInfo.pageKey,
            question: text,
            history: history.slice(-10),
            pageContext: (!contextSent && pageInfo.context)
                ? JSON.stringify(pageInfo.context)
                : null
        };
        history.push({ role: 'user', content: text });

        showTyping();
        try {
            const res = await fetch('/Chatbot/Ask', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            const data = await res.json();

            hideTyping();
            const answer = (data && data.answer) || 'Sorry — something went wrong. Please try again.';
            addMessage('bot', answer);
            history.push({ role: 'assistant', content: answer });

            if (data && data.status) contextSent = true; // context is cached server-side now
        } catch (e) {
            hideTyping();
            addMessage('bot', 'Sorry — I couldn’t reach the assistant. Please check your connection and try again.');
        }

        busy = false;
        sendBtn.disabled = false;
        input.focus();
    }

    // ── Wire up ─────────────────────────────────────────────────────────

    launcher.addEventListener('click', openChat);
    closeBtn.addEventListener('click', closeChat);
    sendBtn.addEventListener('click', send);
    input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') { e.preventDefault(); send(); }
    });
})();
