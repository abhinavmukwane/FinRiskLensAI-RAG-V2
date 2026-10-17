/**
 * Google Translate Utility — JAPAC Region Edition
 * FinRiskLens AI — Google AI Builder Cup 2026 (BFSI track)
 *
 * Full in-page DOM translation engine powered by Google Translate & Google Cloud Translation.
 * - Default language: English (en) — native, no translation needed.
 * - JAPAC languages: Japanese, Chinese (Simp/Trad), Korean, Hindi, Indonesian, Thai, Vietnamese, Malay, Filipino, Bengali, Tamil, Telugu, Marathi.
 * - Preserves numbers, technical terms, layout, and HTML structure.
 * - Full instant undo back to English with zero network calls.
 */

(function () {
    'use strict';

    // JAPAC Languages Configuration
    var JAPAC_LANGUAGES = [
        { code: 'en', name: 'English', native: 'English', isDefault: true },
        { code: 'hi', name: 'Hindi', native: 'हिन्दी' },
        { code: 'ja', name: 'Japanese', native: '日本語' },
        { code: 'zh-CN', name: 'Chinese (Simplified)', native: '简体中文' },
        { code: 'zh-TW', name: 'Chinese (Traditional)', native: '繁體中文' },
        { code: 'ko', name: 'Korean', native: '한국어' },
        { code: 'id', name: 'Indonesian', native: 'Bahasa Indonesia' },
        { code: 'th', name: 'Thai', native: 'ไทย' },
        { code: 'vi', name: 'Vietnamese', native: 'Tiếng Việt' },
        { code: 'ms', name: 'Malay', native: 'Bahasa Melayu' },
        { code: 'tl', name: 'Filipino', native: 'Tagalog' },
        { code: 'bn', name: 'Bengali', native: 'বাংলা' },
        { code: 'ta', name: 'Tamil', native: 'தமிழ்' },
        { code: 'te', name: 'Telugu', native: 'తెలుగు' },
        { code: 'mr', name: 'Marathi', native: 'मराठी' }
    ];

    // Translation Cache (sessionStorage + memory)
    var translationCache = {};
    try {
        sessionStorage.removeItem('frl_gt_cache'); // Purge legacy cache
        var cached = sessionStorage.getItem('frl_gt_cache_v2');
        if (cached) {
            var parsed = JSON.parse(cached);
            // Ensure no invalid entries where non-en translation equals source
            Object.keys(parsed).forEach(function (k) {
                var parts = k.split('::');
                if (parts.length === 2 && parts[0] !== 'en' && parsed[k] !== parts[1]) {
                    translationCache[k] = parsed[k];
                }
            });
        }
    } catch (e) {
        translationCache = {};
    }

    var saveCacheTimer = null;
    function saveCache() {
        if (saveCacheTimer) clearTimeout(saveCacheTimer);
        saveCacheTimer = setTimeout(function () {
            try {
                sessionStorage.setItem('frl_gt_cache_v2', JSON.stringify(translationCache));
            } catch (e) {}
        }, 500);
    }

    // Helper: current selected language
    function getCurrentLanguage() {
        return localStorage.getItem('preferredLanguage') || 'en';
    }

    // Toast feedback
    function showToast(message) {
        var existing = document.querySelector('.gt-toast');
        if (existing) existing.remove();

        var toast = document.createElement('div');
        toast.className = 'gt-toast notranslate';
        toast.setAttribute('translate', 'no');
        toast.innerHTML = '<i class="bi bi-translate" style="color: #F3E2B2;"></i> <span>' + message + '</span>';
        document.body.appendChild(toast);

        requestAnimationFrame(function () {
            toast.classList.add('visible');
            setTimeout(function () {
                toast.classList.remove('visible');
                setTimeout(function () { toast.remove(); }, 300);
            }, 2500);
        });
    }

    // Single text translation via Google Translate API with fallback to /api/translate
    async function translateString(text, targetLang) {
        if (!text || !text.trim() || targetLang === 'en') return text;
        var trimmed = text.trim();
        var cacheKey = targetLang + '::' + trimmed;
        if (translationCache[cacheKey]) return translationCache[cacheKey];

        // 1. Google Translate direct endpoint (dict-chrome-ex allows CORS and returns clean JSON)
        try {
            var url = 'https://translate.googleapis.com/translate_a/single?client=dict-chrome-ex&sl=en&tl=' +
                encodeURIComponent(targetLang) + '&dt=t&q=' + encodeURIComponent(trimmed);
            var res = await fetch(url);
            if (res.ok) {
                var json = await res.json();
                if (json && json[0] && Array.isArray(json[0])) {
                    var translated = json[0].map(function (segment) { return segment[0] || ''; }).join('');
                    if (translated && translated.trim()) {
                        translationCache[cacheKey] = translated;
                        saveCache();
                        return translated;
                    }
                }
            }
        } catch (e) {
            // Fallback to server endpoint
        }

        // 2. Server-side Google Cloud Translation API endpoint (/api/translate)
        try {
            var resp = await fetch('/api/translate', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ sourceLanguage: 'en', targetLanguage: targetLang, textData: [trimmed] })
            });
            if (resp.ok) {
                var data = await resp.json();
                if (data && data.length > 0 && data[0].target) {
                    var tr = data[0].target;
                    if (tr && tr !== trimmed) {
                        translationCache[cacheKey] = tr;
                        saveCache();
                        return tr;
                    }
                }
            }
        } catch (e) {}

        return text;
    }

    // High-performance batch translation: checks cache first, queries missing strings in chunks
    async function translateBatch(texts, targetLang) {
        if (!texts || texts.length === 0 || targetLang === 'en') return;

        // Collect unique texts not in cache
        var missing = [];
        texts.forEach(function (t) {
            var trimmed = (t || '').trim();
            if (trimmed && !translationCache[targetLang + '::' + trimmed]) {
                if (missing.indexOf(trimmed) === -1) {
                    missing.push(trimmed);
                }
            }
        });

        if (missing.length === 0) return;

        // Try /api/translate in chunks of 25 items
        var CHUNK_SIZE = 25;
        for (var i = 0; i < missing.length; i += CHUNK_SIZE) {
            var chunk = missing.slice(i, i + CHUNK_SIZE);
            try {
                var resp = await fetch('/api/translate', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ sourceLanguage: 'en', targetLanguage: targetLang, textData: chunk })
                });
                if (resp.ok) {
                    var data = await resp.json();
                    if (Array.isArray(data)) {
                        data.forEach(function (item) {
                            if (item.source && item.target && item.target !== item.source) {
                                translationCache[targetLang + '::' + item.source.trim()] = item.target;
                            }
                        });
                    }
                }
            } catch (e) {}

            // For any items still missing in this chunk, call direct Google Translate endpoint
            var stillMissing = chunk.filter(function (t) { return !translationCache[targetLang + '::' + t]; });
            if (stillMissing.length > 0) {
                await Promise.all(stillMissing.map(async function (t) {
                    try {
                        var url = 'https://translate.googleapis.com/translate_a/single?client=dict-chrome-ex&sl=en&tl=' +
                            encodeURIComponent(targetLang) + '&dt=t&q=' + encodeURIComponent(t);
                        var res = await fetch(url);
                        if (res.ok) {
                            var json = await res.json();
                            if (json && json[0] && Array.isArray(json[0])) {
                                var trans = json[0].map(function (segment) { return segment[0] || ''; }).join('');
                                if (trans && trans.trim()) {
                                    translationCache[targetLang + '::' + t] = trans;
                                }
                            }
                        }
                    } catch (err) {}
                }));
            }
        }

        saveCache();
    }

    // Check if an element or its ancestor should be skipped
    function isSkippable(node) {
        if (!node) return true;
        var el = node.nodeType === Node.TEXT_NODE ? node.parentElement : node;
        while (el && el !== document.body) {
            var tag = el.tagName;
            if (tag === 'SCRIPT' || tag === 'STYLE' || tag === 'NOSCRIPT' || tag === 'CODE' || tag === 'PRE' || tag === 'SVG') {
                return true;
            }
            if (el.classList && (
                el.classList.contains('notranslate') ||
                el.classList.contains('dont-translate') ||
                el.classList.contains('gt-wrapper') ||
                el.classList.contains('google-translate-container') ||
                el.classList.contains('bhashini-plugin-container') ||
                el.classList.contains('gt-dropdown-menu') ||
                el.classList.contains('gt-toast')
            )) {
                return true;
            }
            if (el.getAttribute && el.getAttribute('translate') === 'no') {
                return true;
            }
            el = el.parentElement;
        }
        return false;
    }

    // Collect all translatable text nodes
    function getTranslatableTextNodes(root) {
        var textNodes = [];
        var walker = document.createTreeWalker(
            root || document.body,
            NodeFilter.SHOW_TEXT,
            {
                acceptNode: function (node) {
                    if (isSkippable(node)) return NodeFilter.FILTER_REJECT;
                    var text = node.nodeValue ? node.nodeValue.trim() : '';
                    if (!text || text.length === 0) return NodeFilter.FILTER_SKIP;
                    // Skip purely numbers, math or punctuation
                    if (/^[\d\s.,:;!?()[\]{}<>=+*\/\\%#@$&~^`|'"–—•→←↓↑₹$€£]+$/.test(text)) {
                        return NodeFilter.FILTER_SKIP;
                    }
                    return NodeFilter.FILTER_ACCEPT;
                }
            }
        );
        while (walker.nextNode()) {
            textNodes.push(walker.currentNode);
        }
        return textNodes;
    }

    var isTranslating = false;

    // Apply translation to all DOM nodes
    async function translateDocument(targetLang) {
        if (isTranslating) return;
        isTranslating = true;

        var textNodes = getTranslatableTextNodes(document.body);
        var inputs = Array.from(document.querySelectorAll('input[placeholder], textarea[placeholder]')).filter(function (inp) { return !isSkippable(inp); });
        var titled = Array.from(document.querySelectorAll('[title]')).filter(function (el) { return !isSkippable(el); });

        // 1. If English (Default): Restore original English text immediately with zero network calls
        if (targetLang === 'en') {
            textNodes.forEach(function (node) {
                if (node._frlOriginal !== undefined) {
                    node.nodeValue = node._frlOriginal;
                }
            });

            inputs.forEach(function (inp) {
                if (inp._frlOriginalPlaceholder !== undefined) {
                    inp.setAttribute('placeholder', inp._frlOriginalPlaceholder);
                }
            });

            titled.forEach(function (el) {
                if (el._frlOriginalTitle !== undefined) {
                    el.setAttribute('title', el._frlOriginalTitle);
                }
            });

            isTranslating = false;
            updateButtonLabels('en');
            return;
        }

        // 2. Non-English JAPAC language:
        // Save original values first
        var allPhrases = [];

        textNodes.forEach(function (node) {
            if (node._frlOriginal === undefined) {
                node._frlOriginal = node.nodeValue;
            }
            var trimmed = node._frlOriginal ? node._frlOriginal.trim() : '';
            if (trimmed) allPhrases.push(trimmed);
        });

        inputs.forEach(function (inp) {
            if (inp._frlOriginalPlaceholder === undefined) {
                inp._frlOriginalPlaceholder = inp.getAttribute('placeholder') || '';
            }
            var pl = inp._frlOriginalPlaceholder.trim();
            if (pl) allPhrases.push(pl);
        });

        titled.forEach(function (el) {
            if (el._frlOriginalTitle === undefined) {
                el._frlOriginalTitle = el.getAttribute('title') || '';
            }
            var t = el._frlOriginalTitle.trim();
            if (t) allPhrases.push(t);
        });

        // Translate all phrases in parallel batches
        await translateBatch(allPhrases, targetLang);

        // Apply translations in DOM
        textNodes.forEach(function (node) {
            var original = node._frlOriginal;
            if (!original) return;
            var trimmed = original.trim();
            if (!trimmed) return;
            var cacheKey = targetLang + '::' + trimmed;
            var translated = translationCache[cacheKey];
            if (translated && translated !== trimmed) {
                var leading = original.match(/^\s*/)[0];
                var trailing = original.match(/\s*$/)[0];
                node.nodeValue = leading + translated + trailing;
            }
        });

        inputs.forEach(function (inp) {
            var pl = inp._frlOriginalPlaceholder ? inp._frlOriginalPlaceholder.trim() : '';
            if (!pl) return;
            var cacheKey = targetLang + '::' + pl;
            var translated = translationCache[cacheKey];
            if (translated) inp.setAttribute('placeholder', translated);
        });

        titled.forEach(function (el) {
            var t = el._frlOriginalTitle ? el._frlOriginalTitle.trim() : '';
            if (!t) return;
            var cacheKey = targetLang + '::' + t;
            var translated = translationCache[cacheKey];
            if (translated) el.setAttribute('title', translated);
        });

        isTranslating = false;
        updateButtonLabels(targetLang);
    }

    // Switch Language handler
    async function setLanguage(langCode) {
        var current = getCurrentLanguage();
        var selectedObj = JAPAC_LANGUAGES.find(function (l) { return l.code === langCode; }) || JAPAC_LANGUAGES[0];

        localStorage.setItem('preferredLanguage', langCode);

        if (langCode === 'en') {
            showToast('Restored to English (Default)');
            await translateDocument('en');
        } else {
            showToast('Translating to ' + selectedObj.name + ' (' + selectedObj.native + ')...');
            await translateDocument(langCode);
            showToast('Translated to ' + selectedObj.name + ' (' + selectedObj.native + ')');
        }

        updateDropdownActiveItem(langCode);
    }

    // Update button text badges
    function updateButtonLabels(langCode) {
        document.querySelectorAll('.gt-current-code').forEach(function (el) {
            el.textContent = langCode.toUpperCase();
        });
    }

    // Update active checkmarks in menu
    function updateDropdownActiveItem(langCode) {
        document.querySelectorAll('.gt-language-item').forEach(function (item) {
            var val = item.getAttribute('data-lang');
            if (val === langCode) {
                item.classList.add('active');
                item.setAttribute('aria-selected', 'true');
            } else {
                item.classList.remove('active');
                item.setAttribute('aria-selected', 'false');
            }
        });
    }

    // Build the language widget inside containers
    function renderWidget() {
        var containers = document.querySelectorAll('.google-translate-container, .bhashini-plugin-container');
        if (containers.length === 0) return;

        var currentLang = getCurrentLanguage();

        containers.forEach(function (container) {
            if (container.querySelector('.gt-dropdown-btn')) return;

            container.classList.add('notranslate');
            container.setAttribute('translate', 'no');

            var wrapper = document.createElement('div');
            wrapper.className = 'gt-wrapper notranslate';
            wrapper.setAttribute('translate', 'no');

            // Button — styled with exact height, colors, and border matching Sign In button
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'btn text-white px-3 py-2 rounded-3 font-heading d-flex align-items-center gap-2 gt-dropdown-btn notranslate';
            btn.setAttribute('aria-haspopup', 'listbox');
            btn.setAttribute('aria-expanded', 'false');
            btn.setAttribute('title', 'Change Language (Google Translate JAPAC)');
            btn.innerHTML =
                '<i class="bi bi-translate gt-icon" aria-hidden="true"></i>' +
                '<span class="gt-current-code">' + currentLang.toUpperCase() + '</span>' +
                '<i class="bi bi-chevron-down gt-caret" aria-hidden="true"></i>';

            // Menu panel
            var menu = document.createElement('div');
            menu.className = 'gt-dropdown-menu notranslate';
            menu.setAttribute('role', 'listbox');

            // Header
            var header = document.createElement('div');
            header.className = 'gt-menu-header';
            header.innerHTML = '<span>Translate Page</span><span class="gt-menu-header-badge">JAPAC</span>';
            menu.appendChild(header);

            // Search filter
            var searchBox = document.createElement('div');
            searchBox.className = 'gt-search-box';
            var searchInput = document.createElement('input');
            searchInput.type = 'text';
            searchInput.className = 'gt-search-input';
            searchInput.placeholder = 'Search language...';
            searchInput.setAttribute('aria-label', 'Search language');
            searchBox.appendChild(searchInput);
            menu.appendChild(searchBox);

            // Language list
            var list = document.createElement('ul');
            list.className = 'gt-language-list';

            JAPAC_LANGUAGES.forEach(function (lang) {
                var li = document.createElement('li');
                li.className = 'gt-language-item' + (lang.code === currentLang ? ' active' : '');
                li.setAttribute('role', 'option');
                li.setAttribute('data-lang', lang.code);
                li.setAttribute('tabindex', '0');
                li.setAttribute('aria-selected', lang.code === currentLang ? 'true' : 'false');
                li.innerHTML =
                    '<div class="gt-lang-name">' +
                    '  <span class="gt-lang-english">' + lang.name + (lang.isDefault ? ' (Default)' : '') + '</span>' +
                    '  <span class="gt-lang-native">' + lang.native + '</span>' +
                    '</div>' +
                    '<span class="gt-lang-check"><i class="bi bi-check2"></i></span>';

                li.addEventListener('click', function (e) {
                    e.stopPropagation();
                    container.classList.remove('gt-open');
                    btn.setAttribute('aria-expanded', 'false');
                    setLanguage(lang.code);
                });

                li.addEventListener('keydown', function (e) {
                    if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        container.classList.remove('gt-open');
                        btn.setAttribute('aria-expanded', 'false');
                        setLanguage(lang.code);
                    }
                });

                list.appendChild(li);
            });

            menu.appendChild(list);

            // Search filter handler
            searchInput.addEventListener('input', function () {
                var q = searchInput.value.toLowerCase().trim();
                list.querySelectorAll('.gt-language-item').forEach(function (item) {
                    var txt = item.textContent.toLowerCase();
                    item.style.display = txt.includes(q) ? 'flex' : 'none';
                });
            });

            // Prevent closing when typing in search input
            searchBox.addEventListener('click', function (e) {
                e.stopPropagation();
            });

            // Footer
            var footer = document.createElement('div');
            footer.className = 'gt-menu-footer';
            footer.innerHTML =
                '<svg viewBox="0 0 24 24" fill="#4285F4"><path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" fill="#4285F4"/><path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/><path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.06H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.94l2.85-2.22.81-.63z" fill="#FBBC05"/><path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.06l3.66 2.84c.87-2.6 3.3-4.52 6.16-4.52z" fill="#EA4335"/></svg>' +
                '<span>Google Cloud Translation</span>';
            menu.appendChild(footer);

            wrapper.appendChild(btn);
            wrapper.appendChild(menu);
            container.appendChild(wrapper);

            // Toggle dropdown
            btn.addEventListener('click', function (e) {
                e.stopPropagation();
                var isOpen = container.classList.toggle('gt-open');
                btn.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
                if (isOpen) {
                    searchInput.value = '';
                    list.querySelectorAll('.gt-language-item').forEach(function (i) { i.style.display = 'flex'; });
                    setTimeout(function () { searchInput.focus(); }, 100);
                }
            });
        });

        // Close on click outside
        document.addEventListener('click', function (e) {
            containers.forEach(function (c) {
                if (!c.contains(e.target)) {
                    c.classList.remove('gt-open');
                    var b = c.querySelector('.gt-dropdown-btn');
                    if (b) b.setAttribute('aria-expanded', 'false');
                }
            });
        });

        // Close on Escape
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                containers.forEach(function (c) {
                    c.classList.remove('gt-open');
                    var b = c.querySelector('.gt-dropdown-btn');
                    if (b) b.setAttribute('aria-expanded', 'false');
                });
            }
        });
    }

    // MutationObserver to translate dynamically added content when non-English is active
    var observer = new MutationObserver(function (mutations) {
        var currentLang = getCurrentLanguage();
        if (currentLang === 'en' || isTranslating) return;

        var hasNewNodes = false;
        mutations.forEach(function (m) {
            if (m.type === 'childList' && m.addedNodes.length > 0) {
                for (var i = 0; i < m.addedNodes.length; i++) {
                    var n = m.addedNodes[i];
                    if (!isSkippable(n)) {
                        hasNewNodes = true;
                        break;
                    }
                }
            }
        });

        if (hasNewNodes) {
            translateDocument(currentLang);
        }
    });

    observer.observe(document.body, {
        childList: true,
        subtree: true
    });

    // Initialize on DOM load
    async function init() {
        renderWidget();
        var preferred = getCurrentLanguage();
        if (preferred && preferred !== 'en') {
            await translateDocument(preferred);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
