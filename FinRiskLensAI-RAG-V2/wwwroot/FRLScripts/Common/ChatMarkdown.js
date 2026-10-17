// ============================================================================
// Safe Markdown → DOM for the FinRiskLens AI chat panels (Chatbot.js, ProductGuide.js).
//
// Supports headings, paragraphs, **bold**, *italic*, `code`, lists (one nested level),
// tables, block quotes, rules and fenced code shown as plain text. Every piece of text
// goes in with createTextNode, so HTML or script in a model reply is shown as text and
// can never run. Links are rendered as their text only.
// ============================================================================

window.FrlChatMarkdown = (function () {
    'use strict';

    function el(tag, cls, text) {
        var n = document.createElement(tag);
        if (cls) n.className = cls;
        if (text !== undefined && text !== null) n.textContent = text;
        return n;
    }

    // ── safe Markdown → DOM ─────────────────────────────────────────────
    // Inline: **bold**, *italic*, `code`, [text](url) shown as text only. Underscores are
    // left alone, so identifiers such as total_tax or __xss keep their underscores.
    function renderInline(parent, text) {
        var re = /(\*\*([^*]+)\*\*|`([^`]+)`|\*([^*\s][^*]*)\*|\[([^\]]+)\]\([^)]*\))/g;
        var last = 0, m;
        while ((m = re.exec(text)) !== null) {
            if (m.index > last) parent.appendChild(document.createTextNode(text.slice(last, m.index)));
            if (m[2]) {
                var b = document.createElement('strong');
                renderInline(b, m[2]);
                parent.appendChild(b);
            } else if (m[3]) {
                parent.appendChild(el('code', null, m[3]));
            } else if (m[4]) {
                var i = document.createElement('em');
                renderInline(i, m[4]);
                parent.appendChild(i);
            } else if (m[5]) {
                parent.appendChild(document.createTextNode(m[5]));
            }
            last = re.lastIndex;
        }
        if (last < text.length) parent.appendChild(document.createTextNode(text.slice(last)));
    }

    function splitRow(line) {
        var t = line.trim();
        if (t.charAt(0) === '|') t = t.slice(1);
        if (t.charAt(t.length - 1) === '|') t = t.slice(0, -1);
        return t.split('|').map(function (c) { return c.trim(); });
    }

    function isTableSeparator(line) {
        return /^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$/.test(line);
    }

    function renderMarkdown(md) {
        var frag = document.createElement('div');
        frag.className = 'frl-md';
        var lines = String(md || '').replace(/\r\n?/g, '\n').split('\n');
        var i = 0;

        function para(buf) {
            if (!buf.length) return;
            var p = el('p');
            renderInline(p, buf.join(' '));
            frag.appendChild(p);
            buf.length = 0;
        }

        var buf = [];
        while (i < lines.length) {
            var line = lines[i];
            var trimmed = line.trim();

            if (!trimmed) { para(buf); i++; continue; }

            // Fenced code: shown as plain preformatted text
            if (/^```/.test(trimmed)) {
                para(buf);
                var code = [];
                i++;
                while (i < lines.length && !/^```/.test(lines[i].trim())) { code.push(lines[i]); i++; }
                i++;
                frag.appendChild(el('pre', 'frl-pre', code.join('\n')));
                continue;
            }

            var h = /^(#{1,6})\s+(.*)$/.exec(trimmed);
            if (h) {
                para(buf);
                var level = Math.min(6, Math.max(3, h[1].length + 1));   // # → h3 inside a chat bubble
                var heading = el('h' + level, 'frl-h');
                renderInline(heading, h[2].replace(/\s*#+\s*$/, ''));
                frag.appendChild(heading);
                i++;
                continue;
            }

            if (/^(-{3,}|\*{3,}|_{3,})$/.test(trimmed)) { para(buf); frag.appendChild(el('hr', 'frl-hr')); i++; continue; }

            // Table: header row, separator row, body rows
            if (trimmed.indexOf('|') !== -1 && i + 1 < lines.length && isTableSeparator(lines[i + 1])) {
                para(buf);
                var head = splitRow(line);
                i += 2;
                var wrap = el('div', 'frl-table-wrap');
                var table = el('table', 'frl-table');
                var thead = el('thead'), tr = el('tr');
                head.forEach(function (c) { var th = el('th'); renderInline(th, c); tr.appendChild(th); });
                thead.appendChild(tr);
                table.appendChild(thead);
                var tbody = el('tbody');
                while (i < lines.length && lines[i].trim() && lines[i].indexOf('|') !== -1) {
                    var row = el('tr');
                    var cells = splitRow(lines[i]);
                    for (var c = 0; c < head.length; c++) {
                        var td = el('td');
                        renderInline(td, cells[c] || '');
                        row.appendChild(td);
                    }
                    tbody.appendChild(row);
                    i++;
                }
                table.appendChild(tbody);
                wrap.appendChild(table);
                frag.appendChild(wrap);
                continue;
            }

            if (/^>\s?/.test(trimmed)) {
                para(buf);
                var quote = el('blockquote', 'frl-quote');
                var qb = [];
                while (i < lines.length && /^>\s?/.test(lines[i].trim())) { qb.push(lines[i].trim().replace(/^>\s?/, '')); i++; }
                renderInline(quote, qb.join(' '));
                frag.appendChild(quote);
                continue;
            }

            // Lists (one nested level, by indentation)
            var li = /^(\s*)([-*+]|\d+[.)])\s+(.*)$/.exec(line);
            if (li) {
                para(buf);
                var ordered = /\d/.test(li[2]);
                var list = el(ordered ? 'ol' : 'ul', 'frl-mdlist');
                var baseIndent = li[1].length;
                var lastItem = null;
                while (i < lines.length) {
                    var m = /^(\s*)([-*+]|\d+[.)])\s+(.*)$/.exec(lines[i]);
                    if (!m) {
                        // a wrapped continuation line belongs to the previous item
                        if (lastItem && lines[i].trim() && /^\s{2,}/.test(lines[i])) {
                            lastItem.appendChild(document.createTextNode(' '));
                            renderInline(lastItem, lines[i].trim());
                            i++;
                            continue;
                        }
                        break;
                    }
                    if (m[1].length > baseIndent + 1 && lastItem) {
                        var sub = lastItem.querySelector(':scope > ul, :scope > ol');
                        if (!sub) { sub = el(/\d/.test(m[2]) ? 'ol' : 'ul', 'frl-mdlist'); lastItem.appendChild(sub); }
                        var subItem = el('li');
                        renderInline(subItem, m[3]);
                        sub.appendChild(subItem);
                    } else {
                        lastItem = el('li');
                        renderInline(lastItem, m[3]);
                        list.appendChild(lastItem);
                    }
                    i++;
                }
                frag.appendChild(list);
                continue;
            }

            buf.push(trimmed);
            i++;
        }
        para(buf);
        return frag;
    }

    return { render: renderMarkdown, el: el };
})();
