var currentScript = document.currentScript;
// var TRANSLATION_PLUGIN_API_KEY = currentScript.getAttribute('secretKey');
// var posX = currentScript.getAttribute("data-pos-x") || 100;
// var posY = currentScript.getAttribute("data-pos-y") || 5;
var defaultTranslatedLanguage = currentScript.getAttribute(
  "default-translated-language"
);
var languageListAttribute = currentScript.getAttribute(
  "translation-language-list"
);

var initialPreferredLanguage = currentScript.getAttribute(
  "initial_preferred_language"
);

var isRedirection =
  currentScript.getAttribute("is-redirection") === "true" || false;

var isWcagNotification =
  currentScript.getAttribute("wcag-notification") === "true" || false;

var ignoreEmailTranslation =
  currentScript.getAttribute("ignore-email-translation") !== "false";

// Clear the target_lang session flag on actual navigation (not script re-execution)
window.addEventListener('beforeunload', function() {
  sessionStorage.removeItem('bhashini_from_target_lang');
});

/**
 * --------------------------------------------------------------------------
 * Domain Language Detection Configuration
 * 
 * This feature allows language-specific domains to automatically detect
 * which language they should display without hardcoding.
 * 
 * How it works:
 * 1. On page load, check if current domain is a language-specific domain
 * 2. If yes, backend returns the target language (e.g., "hi" for Hindi domain)
 * 3. Page auto-translates to that language
 * 
 * Example:
 * - Original: sandbox.digifootprint.gov.in (shows language selector)
 * - Hindi: सैंडबॉक्स.digifootprint.gov.in (auto-translates to Hindi)
 * 
 * Backend API: GET /redirection/language?domain=<current-domain>
 * Returns: { "targetLang": "hi", "originalUrl": "https://sandbox..." }
 * --------------------------------------------------------------------------
 */
async function getDomainLanguage() {
  try {
    const currentDomain = window.location.origin;
    const response = await fetch(
      `${TRANSLATION_PLUGIN_API_BASE_URL}/redirection/language?domain=${encodeURIComponent(currentDomain)}`
    );
    
    if (response.ok) {
      const data = await response.json();
      return data.targetLang;
    } else if (response.status === 404) {
      return null;
    }
  } catch (error) {
    console.error('[Bhashini] Error detecting domain language:', error);
    return null;
  }
}

/**
 * --------------------------------------------------------------------------
 * Language Ordering Configuration
 *
 * This section reads the `language_order` attribute from the <script> tag
 * used to include the plugin on the host website.
 *
 * Example usage in HTML or React:
 *   <script src="./translation_with_feedback_url.js" language_order="en,hi,ta"></script>
 *
 * The attribute `language_order` defines the preferred display order of languages
 * in the language dropdown menu. If not provided, languages will appear in their
 * default order.
 *
 * The value should be a comma-separated list of language codes:
 *   e.g., "en,hi,mr" → English, Hindi, Marathi shown at the top.
 *
 * Logic:
 *   - `orderLanguageArr` stores the ordered list of language codes.
 *   - The `getOrderedLanguages()` function (later in this file) reorders
 *     the global `supportedTargetLangArr` accordingly.
 *
 * Dependencies:
 *   - Uses `supportedTargetLangArr` to reorder UI display.
 *   - Applied before rendering dropdown in `fetchTranslationSupportedLanguages()`.
 * --------------------------------------------------------------------------
 */
// ------------------------------------------------------------------------------------------------------------------
var orderLanguageAttribute = currentScript.getAttribute("language_order");
var orderLanguageArr = [];
if (orderLanguageAttribute) {
  orderLanguageArr = orderLanguageAttribute
    .split(",")
    .map((lang) => lang.trim());
}
// ------------------------------------------------------------------------------------------------------------------

//var TRANSLATION_PLUGIN_API_BASE_URL = new URL(currentScript.getAttribute("src")).origin;
var languageIconColor =
  currentScript.getAttribute("language-icon-color") || "#1D0A69";

var TRANSLATION_PLUGIN_API_BASE_URL = "https://translation-plugin.bhashini.co.in"
var mixedCode = currentScript.getAttribute("mixed-code") || false;
var languageDetection =
  currentScript.getAttribute("language-detection") || false;
var pageSourceLanguage =
  currentScript.getAttribute("page-source-language") || "en";
var isReload = currentScript.getAttribute("isreload") !== "false";
var supportedTargetLangArr = [
  { code: "en", label: "English" },
  { code: "as", label: "Assamese (অসমীয়া)" },
  { code: "bn", label: "Bengali (বাংলা)" },
  { code: "brx", label: "Bodo (बड़ो)" },
  { code: "doi", label: "Dogri (डोगरी)" },
  { code: "gom", label: "Goan Konkani (गोवा कोंकणी)" },
  { code: "gu", label: "Gujarati (ગુજરાતી)" },
  { code: "hi", label: "Hindi (हिन्दी)" },
  { code: "kn", label: "Kannada (ಕನ್ನಡ)" },
  { code: "ks", label: "Kashmiri (कश्मीरी)" },
  { code: "mai", label: "Maithili (मैथिली)" },
  { code: "ml", label: "Malayalam (മലയാളം)" },
  { code: "mni", label: "Manipuri (মণিপুরী)" },
  { code: "mr", label: "Marathi (मराठी)" },
  { code: "ne", label: "Nepali (नेपाली)" },
  { code: "or", label: "Odia (ଓଡ଼ିଆ)" },
  { code: "pa", label: "Punjabi (ਪੰਜਾਬੀ)" },
  { code: "sa", label: "Sanskrit (संस्कृत)" },
  { code: "sat", label: "Santali (संताली)" },
  { code: "sd", label: "Sindhi (سنڌي)" },
  { code: "ta", label: "Tamil (தமிழ்)" },
  { code: "te", label: "Telugu (తెలుగు)" },
  { code: "ur", label: "Urdu (اردو)" },
];

/**
 * --------------------------------------------------------------------------
 * Function: getOrderedLanguages(languageArray)
 *
 * Description:
 * Reorders the provided array of language objects (`languageArray`) based on
 * a preferred language code sequence defined in the global variable `orderLanguageArr`.
 *
 * This function ensures that preferred languages appear first in the dropdown,
 * while maintaining the order of all remaining languages afterward.
 *
 * Input:
 * - languageArray: Array of language objects with shape { code: string, label: string }
 *   Example:
 *     [
 *       { code: "hi", label: "Hindi" },
 *       { code: "en", label: "English" },
 *       ...
 *     ]
 *
 * Global Dependency:
 * - orderLanguageArr (e.g., ['en', 'hi', 'ta']) which is populated from the
 *   <script order_language="en,hi,ta"> tag attribute earlier in the script.
 *
 * Logic:
 * - Step 1: Create a shallow copy of the input array (`remainingLanguages`).
 * - Step 2: Loop through each code in `orderLanguageArr`:
 *     → If found in `remainingLanguages`, move it to `orderedLanguages`.
 *     → Remove it from `remainingLanguages` to prevent duplication.
 * - Step 3: Append the rest of the `remainingLanguages` to `orderedLanguages`.
 * - Step 4: Return the reordered array.
 *
 * Usage:
 * - Applied to `supportedTargetLangArr` before rendering the UI dropdown.
 *
 * Returns:
 * - A reordered array of languages.
 * --------------------------------------------------------------------------
 */
function getOrderedLanguages(languageArray) {
  if (orderLanguageArr.length === 0) {
    return languageArray;
  }

  const orderedLanguages = [];
  const remainingLanguages = [...languageArray];

  // First, add languages in the specified order
  orderLanguageArr.forEach((code) => {
    const foundIndex = remainingLanguages.findIndex(
      (lang) => lang.code === code
    );
    if (foundIndex !== -1) {
      orderedLanguages.push(remainingLanguages[foundIndex]);
      remainingLanguages.splice(foundIndex, 1);
    }
  });

  // Then add remaining languages
  orderedLanguages.push(...remainingLanguages);

  return orderedLanguages;
}

supportedTargetLangArr = getOrderedLanguages(supportedTargetLangArr);
// ------------------------------------------------------------------------------------------------------------------

var CHUNK_SIZE = 25;

// Define translationCache object to store original text
var translationCache = {};

// Flag to track whether content has been translated initially
var isContentTranslated = false;

// Debug mode - set to false for production
var BHASHINI_DEBUG = false;

// ============================================================================
// INTERSECTION OBSERVER-BASED TRANSLATION OPTIMIZATION
// Uses native browser API for efficient viewport detection
// Translates elements IMMEDIATELY when they become visible (no batching)
// ============================================================================

// Set to track nodes that have already been translated (to avoid duplicates)
var translatedNodesSet = new WeakSet();

// Map to track elements being observed and their associated translation data
// Key: element, Value: array of nodeData objects (supports multiple text nodes per parent)
var observedElementsMap = new Map();

// IntersectionObserver instance for lazy translation
var translationObserver = null;

// Translation queue for buffering API calls
var translationQueue = [];
var translationDebounceTimer = null;
var TRANSLATION_DEBOUNCE_DELAY = 50; // ms to wait for more nodes

// Timer for debounced sessionStorage writes
var sessionStorageWriteTimer = null;
var SESSION_STORAGE_WRITE_DELAY = 500;

/**
 * Debug logger - only logs when BHASHINI_DEBUG is true
 */
function debugLog() {
  if (BHASHINI_DEBUG && console && console.log) {
    console.log.apply(console, ['[Bhashini]'].concat(Array.prototype.slice.call(arguments)));
  }
}

/**
 * Debounced write to sessionStorage to avoid frequent writes
 */
function debouncedSessionStorageWrite() {
  if (sessionStorageWriteTimer) {
    clearTimeout(sessionStorageWriteTimer);
  }
  sessionStorageWriteTimer = setTimeout(function() {
    try {
      sessionStorage.setItem("translationCache", JSON.stringify(translationCache));
    } catch (e) {
      // sessionStorage might be full or disabled
      if (BHASHINI_DEBUG) console.warn('[Bhashini] Failed to write to sessionStorage:', e);
    }
  }, SESSION_STORAGE_WRITE_DELAY);
}

/**
 * Initialize IntersectionObserver for lazy translation
 * Elements are observed and translated when they enter viewport + 20% buffer
 */
function initTranslationObserver() {
  if (translationObserver) {
    return; // Already initialized
  }
  
  // Create observer with 100% bottom margin (rootMargin)
  // This means elements are detected one viewport height before they enter viewport
  translationObserver = new IntersectionObserver(
    handleIntersection,
    {
      root: null, // Use viewport as root
      rootMargin: '0px 0px 100% 0px', // 100% buffer below viewport
      threshold: 0 // Trigger as soon as any part is visible
    }
  );
  
  //debugLog('IntersectionObserver initialized for lazy translation');
}

// Initialize observer early so MutationObserver can use it
initTranslationObserver();

/**
 * Handle intersection events - called when observed elements enter/exit viewport
 * IMMEDIATE MODE: Calls API right away for each visible element, no batching
 * @param {IntersectionObserverEntry[]} entries - Array of intersection entries
 */
function handleIntersection(entries) {
  var nodesToTranslate = [];
  
  entries.forEach(function(entry) {
    if (entry.isIntersecting) {
      var element = entry.target;
      
      // Get ALL translation data stored for this element (supports multiple text nodes)
      var translationDataArray = observedElementsMap.get(element);
      
      if (translationDataArray && translationDataArray.length > 0) {
        translationDataArray.forEach(function(translationData) {
          if (!translatedNodesSet.has(translationData.node)) {
            nodesToTranslate.push(translationData);
            // Mark as translated immediately to prevent re-queuing
            translatedNodesSet.add(translationData.node);
          }
        });
      }
      
      // Stop observing this element
      translationObserver.unobserve(element);
      
      // Clean up stored data
      observedElementsMap.delete(element);
    }
  });
  
  // Immediately translate all visible nodes (no batching, no waiting)
  if (nodesToTranslate.length > 0) {
    translateNodesImmediately(nodesToTranslate);
  }
}

/**
 * Queues nodes for translation and processes them in batches
 * Ensures we send requests of size CHUNK_SIZE whenever possible
 * @param {Array} nodes - Array of node data to translate
 */
function translateNodesImmediately(nodes) {
  if (!selectedTargetLanguageCode || selectedTargetLanguageCode === "en") {
    return;
  }
  
  // Add to queue
  translationQueue.push(...nodes);
  
  // Process full chunks immediately
  while (translationQueue.length >= CHUNK_SIZE) {
    var chunk = translationQueue.splice(0, CHUNK_SIZE);
    processTranslationBatch(chunk);
  }
  
  // Schedule remaining nodes
  if (translationQueue.length > 0) {
    if (translationDebounceTimer) {
      clearTimeout(translationDebounceTimer);
    }
    
    translationDebounceTimer = setTimeout(function() {
      if (translationQueue.length > 0) {
        var remaining = translationQueue.splice(0, translationQueue.length);
        processTranslationBatch(remaining);
      }
      translationDebounceTimer = null;
    }, TRANSLATION_DEBOUNCE_DELAY);
  }
}

/**
 * Process a batch of nodes for translation
 * Makes API call for the batch
 * @param {Array} nodes - Array of node data to translate
 */
async function processTranslationBatch(nodes) {
  
  //debugLog('Processing batch of', nodes.length, 'nodes');
  
  try {
    var textContentArray = nodes.map(function(item, index) {
      var id = "translation-" + Date.now() + "-" + Math.random().toString(36).substr(2, 9);
      translationCache[id] = item.content;
      
      if (item.node.parentNode && item.node.parentNode.setAttribute) {
        item.node.parentNode.setAttribute("data-translation-id", id);
      }
      
      return { text: item.content, id: id, node: item };
    });
    
    // Nodes are already batched/chunked by translateNodesImmediately logic mostly,
    // but just in case this function is called directly or with larger set
    var textChunks = chunkArray(textContentArray, CHUNK_SIZE);
    
    // Fire all chunk requests in parallel (no waiting between chunks)
    var promises = textChunks.map(async function(chunk) {
      var texts = chunk.map(function(item) { return item.text; });
      var translatedTexts = await translateTextChunks(texts, selectedTargetLanguageCode);
      
      chunk.forEach(function(item, index) {
        var translatedText = translatedTexts[index].target || texts[index];
        
        if (item.node.type === "text") {
          item.node.node.nodeValue = translatedText;
        }
        if (item.node.type === "value") {
          item.node.node.value = translatedText;
        }
        if (item.node.type === "placeholder") {
          item.node.node.placeholder = translatedText;
        }
        if (item.node.type === "title") {
          item.node.node.setAttribute("title", translatedText);
        }
      });
    });
    
    // Don't wait for completion - let all requests run in parallel
    Promise.all(promises).then(function() {
      debouncedSessionStorageWrite();
    }).catch(function(error) {
      console.error('[Bhashini] Error in parallel translation:', error);
    });
    
  } catch (error) {
    console.error('[Bhashini] Error translating nodes immediately:', error);
  }
}

/**
 * Observe a translatable node for visibility
 * Supports multiple text nodes per parent element
 * @param {Object} nodeData - Object with type, node, and content properties
 */
function observeNodeForTranslation(nodeData) {
  // Skip already translated nodes
  if (translatedNodesSet.has(nodeData.node)) {
    return;
  }
  
  // Get the element to observe (parent element for text nodes)
  var elementToObserve = nodeData.node;
  if (nodeData.node.nodeType === Node.TEXT_NODE) {
    elementToObserve = nodeData.node.parentElement;
  }
  
  if (!elementToObserve) {
    return;
  }
  
  // Check if we're already observing this element
  if (observedElementsMap.has(elementToObserve)) {
    // Add this nodeData to existing array (multiple text nodes in same parent)
    observedElementsMap.get(elementToObserve).push(nodeData);
  } else {
    // Start observing new element
    observedElementsMap.set(elementToObserve, [nodeData]);
    translationObserver.observe(elementToObserve);
  }
}

/**
 * Check if an element is currently visible in viewport + buffer
 * Used for initial page load to translate immediately visible content
 * @param {Element} element - The DOM element to check
 * @param {number} bufferPercent - Additional percentage of viewport height to include below
 * @returns {boolean} - True if element is in the extended viewport
 */
function isElementInViewport(element, bufferPercent) {
  bufferPercent = bufferPercent || 20;
  
  var checkElement = element;
  if (element.nodeType === Node.TEXT_NODE) {
    checkElement = element.parentElement;
  }

  if (!checkElement || !checkElement.getBoundingClientRect) {
    return false;
  }

  // <option> elements are hidden inside a collapsed <select> and return zero rect.
  // Use the parent <select>'s rect instead so options get translated with the select.
  // if (checkElement.tagName === "OPTION") {
  //   var selectEl = checkElement.closest("select");
  //   if (selectEl) {
  //     // Library-managed selects (Select2, Chosen, etc.) hide the native <select>
  //     // with aria-hidden="true". Translating their <option> text triggers the
  //     // library's own MutationObserver on the <select>, causing it to re-render
  //     // and reset the selection to the first option.
  //     if (selectEl.getAttribute("aria-hidden") === "true") {
  //       return false;
  //     }
  //     checkElement = selectEl;
  //   }
  // }
  
  var rect = checkElement.getBoundingClientRect();
  var viewportHeight = window.innerHeight || document.documentElement.clientHeight;
  var bufferHeight = viewportHeight * (bufferPercent / 100);
  
  return (
    rect.top < (viewportHeight + bufferHeight) &&
    rect.bottom > 0
  );
}

/**
 * Filter translatable content to only include visible elements
 * @param {Array} translatableContent - Array of translatable nodes
 * @param {number} bufferPercent - Buffer percentage below viewport
 * @returns {Array} - Filtered array of visible translatable nodes
 */
function filterVisibleNodes(translatableContent, bufferPercent) {
  bufferPercent = bufferPercent || 20;
  return translatableContent.filter(function(item) {
    if (translatedNodesSet.has(item.node)) {
      return false;
    }
    return isElementInViewport(item.node, bufferPercent);
  });
}

/**
 * Get all translatable nodes (without visibility filter)
 */
function getAllTextNodesToTranslate(rootNode) {
  var translatableContent = [];

  function isSkippableElement(node) {
    return (
      node.nodeType === Node.ELEMENT_NODE &&
      (node.classList.contains("dont-translate") ||
        node.classList.contains("bhashini-skip-translation") ||
        node.tagName === "SCRIPT" ||
        node.tagName === "STYLE" ||
        node.tagName === "NOSCRIPT")
    );
  }

  function isNodeOrAncestorsSkippable(node, maxLevels) {
    maxLevels = maxLevels || 5;
    var currentNode = node;
    var level = 0;

    while (currentNode && level < maxLevels) {
      if (isSkippableElement(currentNode)) {
        return true;
      }
      // Skip content inside library-managed selects (aria-hidden="true").
      // Translating <option> text inside these triggers Select2 / Chosen to
      // detect the change and reset the selection to the first option.
      if (currentNode.tagName === "SELECT" &&
          currentNode.getAttribute("aria-hidden") === "true") {
        return true;
      }
      currentNode = currentNode.parentElement;
      level++;
    }

    return false;
  }

  function traverseNode(node) {
    if (Array.isArray(node)) {
      node.forEach(function(item) {
        if (item && typeof item === "object" && item.node) {
          traverseNode(item.node);
        } else {
          traverseNode(item);
        }
      });
      return;
    }

    if (!node || !node.nodeType) {
      return;
    }

    if (isNodeOrAncestorsSkippable(node)) {
      return;
    }

    if (node.nodeType === Node.TEXT_NODE) {
      var text = node.textContent;
      var originalText =
        node.parentElement && node.parentElement.hasAttribute("bhashini-original-text")
          ? node.parentElement.getAttribute("bhashini-original-text")
          : text;
      var isNumeric = /^[\d.]+$/.test(text);
      if (text && !isIgnoredNode(node, originalText) && !isNumeric) {
        translatableContent.push({
          type: "text",
          node: node,
          content: originalText,
        });
      }
    } else if (node.nodeType === Node.ELEMENT_NODE) {
      if (node.hasAttribute("placeholder")) {
        var originalPlaceholder = node.hasAttribute("bhashini-original-placeholder")
          ? node.getAttribute("bhashini-original-placeholder")
          : node.getAttribute("placeholder");
        translatableContent.push({
          type: "placeholder",
          node: node,
          content: originalPlaceholder,
        });
      }
      if (node.hasAttribute("title")) {
        var originalTitle = node.hasAttribute("bhashini-original-title")
          ? node.getAttribute("bhashini-original-title")
          : node.getAttribute("title");
        translatableContent.push({
          type: "title",
          node: node,
          content: originalTitle,
        });
      }

      for (var i = 0; i < node.childNodes.length; i++) {
        traverseNode(node.childNodes[i]);
      }
    }
  }

  traverseNode(rootNode);
  return translatableContent;
}

function persistOriginalNodeContent(nodeData, translationId) {
  var originalContent = nodeData.content;

  if (nodeData.type === "text" && nodeData.node.parentElement) {
    if (!nodeData.node.parentElement.hasAttribute("bhashini-original-text")) {
      nodeData.node.parentElement.setAttribute(
        "bhashini-original-text",
        originalContent
      );
    }
    nodeData.node.parentElement.setAttribute("data-translation-id", translationId);
    return originalContent;
  }

  if (nodeData.type === "placeholder" && nodeData.node) {
    if (!nodeData.node.hasAttribute("bhashini-original-placeholder")) {
      nodeData.node.setAttribute("bhashini-original-placeholder", originalContent);
    }
    nodeData.node.setAttribute("data-translation-id", translationId);
    return nodeData.node.getAttribute("bhashini-original-placeholder");
  }

  if (nodeData.type === "title" && nodeData.node) {
    if (!nodeData.node.hasAttribute("bhashini-original-title")) {
      nodeData.node.setAttribute("bhashini-original-title", originalContent);
    }
    nodeData.node.setAttribute("data-translation-id", translationId);
    return nodeData.node.getAttribute("bhashini-original-title");
  }

  if (nodeData.node.parentNode && nodeData.node.parentNode.setAttribute) {
    nodeData.node.parentNode.setAttribute("data-translation-id", translationId);
  }

  return originalContent;
}

function resetTranslationStateForInPlaceUpdate() {
  translatedNodesSet = new WeakSet();
  observedElementsMap.clear();
  translationQueue = [];

  if (translationDebounceTimer) {
    clearTimeout(translationDebounceTimer);
    translationDebounceTimer = null;
  }

  if (sessionStorageWriteTimer) {
    clearTimeout(sessionStorageWriteTimer);
    sessionStorageWriteTimer = null;
  }

  if (translationObserver) {
    translationObserver.disconnect();
  }

  initTranslationObserver();
}

/**
 * Setup IntersectionObserver for non-visible nodes
 * Called after initial visible content is translated
 * @param {Array} allNodes - All translatable nodes
 */
function observeHiddenNodes(allNodes) {
  var hiddenNodesCount = 0;
  
  allNodes.forEach(function(nodeData) {
    // Skip already translated nodes
    if (translatedNodesSet.has(nodeData.node)) {
      return;
    }
    
    // Observe for lazy translation
    observeNodeForTranslation(nodeData);
    hiddenNodesCount++;
  });
  
  //debugLog('Observing', hiddenNodesCount, 'hidden nodes for lazy translation');
}

// Check for target_lang query param EARLY - before setting selectedTargetLanguageCode
// This prevents the MutationObserver from picking up the wrong language
var _earlyUrlParams = new URLSearchParams(window.location.search);
var _earlyTargetLang = _earlyUrlParams.get('target_lang');

// Selected target language for translation
// If target_lang param exists, use it directly (including 'en' which means no translation)
// This ensures MutationObserver has the correct language from the start
var selectedTargetLanguageCode;
if (_earlyTargetLang) {
  // Use the target_lang from URL - if 'en', set to null to prevent translation
  selectedTargetLanguageCode = _earlyTargetLang === 'en' ? null : _earlyTargetLang;
} else {
  // No target_lang in URL, use saved preference or initial preference
  selectedTargetLanguageCode = localStorage.getItem("preferredLanguage") || initialPreferredLanguage;
  // If saved preference is 'en', also set to null (no translation needed for English source)
  if (selectedTargetLanguageCode === 'en') {
    selectedTargetLanguageCode = null;
  }
}

// Retrieve translationCache from session storage if available
if (sessionStorage.getItem("translationCache")) {
  translationCache = JSON.parse(sessionStorage.getItem("translationCache"));
}

  // The plugin stylesheet is vendored at ~/css/bhashini-plugin.css and linked
  // from each layout's <head>. Fetching it here instead meant it arrived a
  // network round-trip AFTER the markup below was already in the DOM, so the
  // language list flashed as a bare bulleted list on every page load.
  //
  // To go back to the live vendor copy, uncomment these three lines and drop
  // the <link> from _Layout / _DashboardLayout / _CustOnboardingLayout.
  // var cssLink = document.createElement("link");
  // cssLink.rel = "stylesheet";
  // cssLink.href = `${TRANSLATION_PLUGIN_API_BASE_URL}/v3/website_translation_utility.css`;
  // document.head.appendChild(cssLink);


var getPoweredByText = (lang) => {
  switch (lang) {
    case "kn":
      return "ಮೂಲಕ ನಡೆಸಲ್ಪಡುತ್ತಿದೆ";
    case "te":
      return "ఆధారితం";
    default:
      return "Powered by";
  }
};

/**
 * Returns the localized aria-label for the Language Translator button
 * based on the selected language code.
 */
var getLanguageTranslatorLabel = (lang) => {
  switch (lang) {
    case "as":
      return "ভাষা অনুবাদক";
    case "bn":
      return "ভাষা অনুবাদক";
    case "brx":
      return "रोखा सोलायनाय";
    case "doi":
      return "भाशा अनुवादक";
    case "gom":
      return "भाशा अणकारी";
    case "gu":
      return "ભાષા અનુવાદક";
    case "hi":
      return "भाषा अनुवादक";
    case "kn":
      return "ಭಾಷಾ ಅನುವಾದಕ";
    case "ks":
      return "زَبان ترجمہ کار";
    case "mai":
      return "भाषा अनुवादक";
    case "ml":
      return "ഭാഷാ വിവർത്തകൻ";
    case "mni":
      return "ꯂꯣꯟ ꯍꯟꯗꯣꯛꯄ";
    case "mr":
      return "भाषा अनुवादक";
    case "ne":
      return "भाषा अनुवादक";
    case "or":
      return "ଭାଷା ଅନୁବାଦକ";
    case "pa":
      return "ਭਾਸ਼ਾ ਅਨੁਵਾਦਕ";
    case "sa":
      return "भाषा अनुवादकः";
    case "sat":
      return "ᱯᱟᱹᱨᱥᱤ ᱛᱚᱨᱡᱚᱢᱟ";
    case "sd":
      return "ٻولي ترجمو ڪندڙ";
    case "ta":
      return "மொழி மொழிபெயர்ப்பாளர்";
    case "te":
      return "భాషా అనువాదకుడు";
    case "ur":
      return "زبان مترجم";
    default:
      return "Language Translator";
  }
};

/**
 * Updates the aria-label of the Language Translator button
 * based on the selected language.
 */
var updateTranslatorButtonLabel = (lang) => {
  var button = document.querySelector(".bhashini-dropdown-btn");
  if (button) {
    button.setAttribute("aria-label", getLanguageTranslatorLabel(lang));
  }
};

function toggleDropdown() {
  var dropdown = document.getElementById("bhashiniLanguageDropdown");
  var button = document.querySelector(".bhashini-dropdown-btn");
  var isExpanded = dropdown.style.display === "block";
  dropdown.style.display = isExpanded ? "none" : "block";
  button.setAttribute("aria-expanded", isExpanded ? "false" : "true");

  // Get measurements after showing the dropdown
  var dropdownHeight = dropdown.clientHeight;
  var dropdownWidth = dropdown.clientWidth;
  var windowHeight = window.innerHeight;
  var windowWidth = window.innerWidth;
  var dropdownRect = dropdown.getBoundingClientRect();

  // Handle vertical positioning
  var spaceBelow = windowHeight - dropdownRect.top;
  var spaceAbove = dropdownRect.top;

  if (spaceBelow < dropdownHeight && spaceAbove > spaceBelow) {
    dropdown.style.bottom = "100%";
    dropdown.style.top = "auto";
  } else {
    dropdown.style.top = "100%";
    dropdown.style.bottom = "auto";
  }

  // Handle horizontal positioning - check both left and right space
  var spaceRight = windowWidth - dropdownRect.left;
  var spaceLeft = dropdownRect.right;

  if (spaceRight < dropdownWidth && spaceLeft > spaceRight) {
    // Not enough space on right, and left has more space
    dropdown.style.right = "0";
    dropdown.style.left = "auto";
  } else {
    // Enough space on right, or right has more space than left
    dropdown.style.left = "0";
    dropdown.style.right = "auto";
  }
}

// Fetch supported translation languages
function fetchTranslationSupportedLanguages() {
  // false check commenting to test
  // if (window.__bhashiniLanguagesRendered) return;
  // window.__bhashiniLanguagesRendered = true;

  var targetLangSelectElement = document.getElementById(
    "bhashiniLanguageDropdown"
  );
  var brandingDiv = document.createElement("div");
  brandingDiv.setAttribute("class", "bhashini-branding");
  var poweredBy = document.createElement("span");
  poweredBy.textContent = getPoweredByText(selectedTargetLanguageCode);
  var bhashiniLogoLink = document.createElement("a");
  bhashiniLogoLink.href = "https://bhashini.gov.in";
  bhashiniLogoLink.target = "_blank";
  bhashiniLogoLink.rel = "noopener noreferrer";
  bhashiniLogoLink.setAttribute("aria-label", "Visit Bhashini website");
  var bhashiniLogo = document.createElement("img");
  bhashiniLogo.src = `${TRANSLATION_PLUGIN_API_BASE_URL}/v3/bhashini-logo.png`;
  bhashiniLogo.alt = "Bhashini Logo";
  bhashiniLogoLink.appendChild(bhashiniLogo);

  brandingDiv.appendChild(poweredBy);
  brandingDiv.appendChild(bhashiniLogoLink);

  /**
   * --------------------------------------------------------------------------
   * Language Display Filter and Ordering Logic
   *
   * Description:
   * Determines the final list of languages (`languagesToShow`) to be displayed
   * in the translation dropdown. This process respects both the `order_language`
   * and `translation-language-list` attributes set in the <script> tag.
   *
   * Workflow:
   * 1. By default, `languagesToShow` is initialized with the full list:
   *      → `supportedTargetLangArr` (which may already be reordered).
   *
   * 2. If `translation-language-list` attribute is provided in the script tag:
   *      → The list is filtered to include only the specified languages.
   *      → e.g., <script translation-language-list="en,hi,ta">
   *
   * 3. For each language in the filtered or full list:
   *      → Create a clickable div element with accessibility roles and data attributes.
   *      → Set the first item as `selected` (default language).
   *      → Append all language options to the dropdown container.
   *
   * 4. Accessibility Support:
   *      → Adds keyboard support for language selection via `Enter` key.
   *
   * Example:
   * <script
   *   src="translation_with_feedback_url.js"
   *   order_language="en,hi,ta"
   *   translation-language-list="en,hi,ta,bn,ml"
   * ></script>
   *
   * This will:
   *   - Filter dropdown to only English, Hindi, Tamil, Bengali, Malayalam.
   *   - Reorder those so English, Hindi, Tamil appear first.
   *
   * Dependencies:
   *   - `supportedTargetLangArr` (may already be reordered).
   *   - `languageListAttribute` (optional, filters final list).
   * --------------------------------------------------------------------------
   */
  let languagesToShow = supportedTargetLangArr;

  // Step 1: Filter languages if `translation-language-list` attribute is present
  if (languageListAttribute) {
    const languageList = languageListAttribute
      .split(",")
      .map((lang) => lang.trim());
    languagesToShow = supportedTargetLangArr.filter((lang) =>
      languageList.includes(lang.code)
    );
  }

  // Step 2: Render dropdown options (once, cleanly)
  targetLangSelectElement.innerHTML = ""; // clear before rendering
  var storedLanguage = localStorage.getItem("preferredLanguage");
  languagesToShow.forEach((element, index) => {
    const option_element = document.createElement("li");
    option_element.setAttribute("class", "dont-translate language-option");
    option_element.setAttribute("data-value", element.code);
    option_element.setAttribute("tabindex", "0");
    option_element.setAttribute("role", "option");

    // Parse label to separate English name and native script
    // Format: "Hindi (हिन्दी)" or just "English"
    const labelMatch = element.label.match(/^([^(]+)(?:\(([^)]+)\))?$/);
    if (labelMatch) {
      const englishName = labelMatch[1].trim();
      const nativeScript = labelMatch[2] ? labelMatch[2].trim() : null;
      
      // Add English name as text
      option_element.appendChild(document.createTextNode(englishName + " "));
      
      // Add native script in span with lang attribute if available
      if (nativeScript) {
        const nativeSpan = document.createElement("span");
        nativeSpan.setAttribute("lang", element.code);
        nativeSpan.textContent = "(" + nativeScript + ")";
        option_element.appendChild(nativeSpan);
      }
    } else {
      // Fallback: use label as-is
      option_element.textContent = element.label;
    }

    // Set aria-selected based on stored preference in localStorage
    if (storedLanguage && element.code === storedLanguage) {
      option_element.setAttribute("aria-selected", "true");
    } else {
      option_element.setAttribute("aria-selected", "false");
    }

    targetLangSelectElement.appendChild(option_element);
  });

  // console.log("targetLangSelectElement: ", targetLangSelectElement);

  // Step 3: Accessibility – keyboard support
  targetLangSelectElement.addEventListener("keydown", function (event) {
    const languageOption = event.target.closest(".language-option");
    if (languageOption && event.key === "Enter") {
      event.preventDefault();
      selectLanguage(languageOption.textContent);
    }
  });

  // ------------------------------------------------------------------------------------------------------------------
  targetLangSelectElement.appendChild(brandingDiv);

  // Add single event listener to parent container using event delegation
  targetLangSelectElement.addEventListener("click", function (event) {
    var languageOption = event.target.closest(".language-option");
    if (languageOption) {
      selectLanguage(languageOption.textContent);
    }
  });

  // Update the Language Translator button aria-label for the initial language
  updateTranslatorButtonLabel(selectedTargetLanguageCode);
}

// Function to split an array into chunks of a specific size
function chunkArray(array, size) {
  var chunkedArray = [];
  for (var i = 0; i < array.length; i += size) {
    chunkedArray.push(array.slice(i, i + size));
  }
  return chunkedArray;
}

// Function to get all input and textArea element with placeholders

// Function to translate text chunks using custom API
async function translateTextChunks(chunks, target_lang) {
  if (pageSourceLanguage && pageSourceLanguage === target_lang) {
    // If the target language is the same as the page source language, return the original chunks
    return chunks.map((chunk) => ({ source: chunk, target: chunk }));
  }

  var payload = {
    // sourceLanguage: pageSourceLanguage,
    targetLanguage: target_lang,
    textData: chunks,
  };

  if (mixedCode === "true") {
    payload.mixed_code = true;
  }
  if (languageDetection === "true") {
    payload.languageDetection = true;
  } else {
    payload.sourceLanguage = pageSourceLanguage || "en";
  }

  try {
    var response = await fetch("https://api.finrisklensai.com/api/translate",
      {
        method: "POST",
        headers: {
          // 'auth-token': TRANSLATION_PLUGIN_API_KEY,
          "Content-Type": "application/json",
          "X-Utility-Version": "v3", // Identify this as v3 utility
        },
        body: JSON.stringify(payload),
      }
    );
    var data = await response.json();
    return data;
  } catch (error) {
    console.error("Error translating text:", error);
    return [];
  }
}

// function to get redirection url
// Sends full URL to backend, which extracts domain and preserves path/query/fragment

async function getRedirectionUrl(targetLang) {
  try {
    const res = await fetch(
      `${TRANSLATION_PLUGIN_API_BASE_URL}/redirection?url=${encodeURIComponent(
        window.location.href
      )}&target_lang=${targetLang}`
    );
    const data = await res.json();
    if (data && data.replacementUrl) {
      // Backend returns full URL with domain replaced but path/query preserved
      return data.replacementUrl;
    } else {
      return null;
    }
  } catch (error) {
    console.error("Error fetching redirection URL:", error);
    return null;
  }
}

// Function to recursively traverse DOM tree and get text nodes while skipping elements with "dont-translate" class
// function getTextNodesToTranslate(rootNode) {
//   var translatableContent = [];

//   function isSkippableElement(node) {
//     return (
//       node.nodeType === Node.ELEMENT_NODE &&
//       (node.classList.contains("dont-translate") ||
//         node.classList.contains("bhashini-skip-translation") ||
//         node.tagName === "SCRIPT" ||
//         node.tagName === "STYLE" ||
//         node.tagName === "NOSCRIPT")
//     );
//   }

//   function traverseNode(node) {
//     // Skip the entire subtree if this is a skippable element
//     if (isSkippableElement(node)) {
//       return;
//     }

//     // Process this node
//     if (node.nodeType === Node.TEXT_NODE) {
//       var text = node.textContent;
//       var isNumeric = /^[\d.]+$/.test(text);
//       if (text && !isIgnoredNode(node) && !isNumeric) {
//         translatableContent.push({
//           type: "text",
//           node: node,
//           content: text,
//         });
//       }
//     } else if (node.nodeType === Node.ELEMENT_NODE) {
//       if (node.hasAttribute("placeholder")) {
//         translatableContent.push({
//           type: "placeholder",
//           node: node,
//           content: node.getAttribute("placeholder"),
//         });
//       }
//       if (node.hasAttribute("title")) {
//         translatableContent.push({
//           type: "title",
//           node: node,
//           content: node.getAttribute("title"),
//         });
//       }

//       // Process all child nodes
//       for (let i = 0; i < node.childNodes.length; i++) {
//         traverseNode(node.childNodes[i]);
//       }
//     }
//   }

//   traverseNode(rootNode);
//   return translatableContent;
// }

// Consolidated function - use getAllTextNodesToTranslate for consistency
// This wrapper maintains backward compatibility with existing code
function getTextNodesToTranslate(rootNode) {
  return getAllTextNodesToTranslate(rootNode);
}

function isIgnoredNode(node, originalText) {

  var emailRegex = /\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,7}\b/ ;
  var isValidGovtEmail = (email) => {
    var normalizedEmail = email.replace(/\[dot]/g, ".").replace(/\[at]/g, "@");
    return emailRegex.test(normalizedEmail);
  };
  var nonEnglishRegex = /^[^A-Za-z0-9]+$/;
  var onlyNewLinesOrWhiteSpaceRegex = /^[\n\s\r\t]*$/;
  
  // Use original text for validation if provided, otherwise use current text
  var textToCheck = originalText || node.textContent;
  
  return (
    (node.parentNode &&
      (node.parentNode.tagName === "STYLE" ||
        node.parentNode.tagName === "SCRIPT" ||
        node.parentNode.tagName === "NOSCRIPT" ||
        node.parentNode.classList.contains("dont-translate") ||
        node.parentNode.classList.contains("bhashini-skip-translation") ||
      (ignoreEmailTranslation &&   emailRegex.test(textToCheck)) ||
     (ignoreEmailTranslation &&   isValidGovtEmail(textToCheck) )||
        (languageDetection !== "true" &&
          pageSourceLanguage === "en" &&
          nonEnglishRegex.test(textToCheck)))) ||
    onlyNewLinesOrWhiteSpaceRegex.test(node.textContent)
  );
}

function selectLanguage(language) {
  // Trim whitespace from language to handle extra spaces in textContent
  language = language.trim();
  // document.querySelector(".bhashini-dropdown-btn-text").textContent = language;
  document.getElementById("bhashiniLanguageDropdown").classList.remove("show");
  
  // Close dropdown and update aria-expanded
  var button = document.querySelector(".bhashini-dropdown-btn");
  if (button) {
    button.setAttribute("aria-expanded", "false");
  }
  
  var selectedLang = supportedTargetLangArr.find(
    (lang) => lang.label === language
  );
  if (selectedLang) {
    // Update aria-selected on all language options
    var languageOptions = document.querySelectorAll(".language-option");
    languageOptions.forEach(function(option) {
      if (option.getAttribute("data-value") === selectedLang.code) {
        option.setAttribute("aria-selected", "true");
      } else {
        option.setAttribute("aria-selected", "false");
      }
    });
    
    onDropdownChange({ target: { value: selectedLang.code } });
  } else {
    console.log('[Bhashini] Language not found in supportedTargetLangArr!');
  }
}

window.onclick = function (event) {
  if (!event.target.matches(".bhashini-dropdown-btn")) {
    var dropdowns = document.getElementsByClassName(
      "bhashini-dropdown-content"
    );
    var button = document.querySelector(".bhashini-dropdown-btn");
    for (var i = 0; i < dropdowns.length; i++) {
      var openDropdown = dropdowns[i];
      if (openDropdown.classList.contains("show")) {
        openDropdown.classList.remove("show");
      }
      if (openDropdown.style.display === "block") {
        openDropdown.style.display = "none";
        if (button) button.setAttribute("aria-expanded", "false");
      }
    }
  }
};

var pluginContainer = document.querySelector(".bhashini-plugin-container");

// Create translation popup elements
var wrapperButton = document.createElement("div");
wrapperButton.setAttribute(
  "class",
  "dont-translate bhashini-skip-translation bhashini-dropdown"
);
wrapperButton.setAttribute("id", "bhashini-translation");
wrapperButton.setAttribute("title", "Translate this page!");
// wrapperButton.innerHTML = `<select class="translate-plugin-dropdown" id="translate-plugin-target-language-list"></select><img src=${TRANSLATION_PLUGIN_API_BASE_URL}/bhashini_logo.png alt="toggle translation popup">`;
wrapperButton.innerHTML = `
        <button role="combobox" aria-label="Language Translator" aria-expanded="false" aria-haspopup="listbox" aria-controls="bhashiniLanguageDropdown" class="bhashini-dropdown-btn">
          <div class="bhashini-dropdown-btn-icon">
          <svg width="32" height="32" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg" aria-hidden="true" role="img"><path d="M14.125 3.735V12H12.91V3.735H11.815V2.67H15.685V3.735H14.125ZM8.47 2.52C9.25 2.52 9.845 2.715 10.255 3.105C10.675 3.495 10.885 3.985 10.885 4.575C10.885 5.005 10.77 5.395 10.54 5.745C10.32 6.085 9.99 6.355 9.55 6.555C9.11 6.755 8.56 6.865 7.9 6.885L7.825 5.835C8.505 5.815 8.985 5.695 9.265 5.475C9.555 5.255 9.7 4.96 9.7 4.59C9.7 4.23 9.58 3.97 9.34 3.81C9.11 3.65 8.84 3.57 8.53 3.57C8.16 3.57 7.825 3.62 7.525 3.72C7.225 3.82 6.905 3.955 6.565 4.125L6.19 3.09C6.45 2.95 6.77 2.82 7.15 2.7C7.54 2.58 7.98 2.52 8.47 2.52ZM11.05 8.73C11.05 9.19 10.945 9.575 10.735 9.885C10.525 10.195 10.24 10.425 9.88 10.575C9.53 10.725 9.13 10.8 8.68 10.8C8.11 10.8 7.58 10.66 7.09 10.38C6.61 10.1 6.15 9.655 5.71 9.045C5.28 8.435 4.855 7.64 4.435 6.66L5.5 6.27C5.79 6.98 6.09 7.595 6.4 8.115C6.72 8.625 7.06 9.02 7.42 9.3C7.78 9.57 8.165 9.705 8.575 9.705C8.955 9.705 9.265 9.62 9.505 9.45C9.745 9.27 9.865 8.985 9.865 8.595C9.865 8.115 9.7 7.7 9.37 7.35C9.04 7 8.64 6.68 8.17 6.39L9.055 6.345L9.7 6.21C9.84 6.33 9.995 6.475 10.165 6.645C10.335 6.815 10.47 6.985 10.57 7.155L10.645 7.44C10.775 7.63 10.875 7.83 10.945 8.04C11.015 8.25 11.05 8.48 11.05 8.73ZM11.29 6.75C11.77 6.75 12.185 6.715 12.535 6.645C12.885 6.565 13.295 6.44 13.765 6.27V7.35C13.335 7.54 12.945 7.665 12.595 7.725C12.255 7.785 11.88 7.815 11.47 7.815C11.32 7.815 11.145 7.805 10.945 7.785C10.745 7.755 10.555 7.725 10.375 7.695C10.205 7.655 10.08 7.62 10 7.59L9.295 6.75L9.385 6.525C9.675 6.595 9.98 6.65 10.3 6.69C10.62 6.73 10.95 6.75 11.29 6.75Z" fill=${languageIconColor}></path><path d="M19.63 22L18.426 18.906H14.464L13.274 22H12L15.906 11.962H17.04L20.932 22H19.63ZM18.048 17.786L16.928 14.762C16.9 14.6873 16.8533 14.552 16.788 14.356C16.7227 14.16 16.6573 13.9593 16.592 13.754C16.536 13.5393 16.4893 13.376 16.452 13.264C16.3773 13.5533 16.298 13.838 16.214 14.118C16.1393 14.3887 16.074 14.6033 16.018 14.762L14.884 17.786H18.048Z" fill="${languageIconColor}"></path></svg>
          </div>
        </button>
        <ul class="bhashini-dropdown-content" id="bhashiniLanguageDropdown" role="listbox" aria-label="Select language">
        </ul>
    `;
pluginContainer.appendChild(wrapperButton);

wrapperButton.addEventListener("click", (e) => {
  e.stopPropagation();
  e.preventDefault();
  toggleDropdown();
});

// Fetch supported translation languages
fetchTranslationSupportedLanguages();

// Function to translate dynamically added elements
// OPTIMIZED: Uses IntersectionObserver for hidden elements, translates visible immediately
async function translateElementText(element, target_lang) {
  var promises = [];
  var allTextNodes = getTextNodesToTranslate(element);
  
  // Separate visible and hidden nodes
  var visibleNodes = [];
  var hiddenNodes = [];
  
  allTextNodes.forEach(function(item) {
    // Skip already translated nodes
    if (translatedNodesSet.has(item.node)) {
      return;
    }
    
    if (isElementInViewport(item.node, 20)) {
      visibleNodes.push(item);
    } else {
      hiddenNodes.push(item);
    }
  });
  
  // Translate visible nodes immediately
  if (visibleNodes.length > 0) {
    var textContentArray = visibleNodes.map((node, index) => {
      var id = `translation-${Date.now()}-${index}`;
      var originalText = persistOriginalNodeContent(node, id);
      translationCache[id] = originalText;
      translatedNodesSet.add(node.node);
      return { text: originalText, id, node };
    });
    var textChunks = chunkArray(textContentArray, CHUNK_SIZE);

    // Create an array to hold promises for each chunk translation
    var textNodePromises = textChunks.map(async (chunk) => {
      var texts = chunk.map(({ text }) => text);
      var translatedTexts = await translateTextChunks(texts, target_lang);
      chunk.forEach(({ node }, index) => {
        var translatedText = translatedTexts[index].target || texts[index];

        if (node.type === "text") {
          node.node.nodeValue = translatedText;
        }
        if (node.type === "value") {
          node.node.value = translatedText;
        }
        if (node.type === "placeholder") {
          node.node.placeholder = translatedText;
        }
        if (node.type === "title") {
          node.node.setAttribute("title", translatedText);
        }
      });
    });
    promises.push(textNodePromises);

    await Promise.all(promises);
    
    // Debounced write to session storage
    debouncedSessionStorageWrite();
  }
  
  // Observe hidden nodes for lazy translation via IntersectionObserver
  if (hiddenNodes.length > 0 && translationObserver) {
    hiddenNodes.forEach(function(nodeData) {
      observeNodeForTranslation(nodeData);
    });
    //debugLog('Added', hiddenNodes.length, 'dynamic hidden nodes to observer');
  }
}
var nodesToTranslate = []; // Array to store nodes and their associated language codes
var debounceTimer = null;
var DEBOUNCE_DELAY = 250;

function translateElementTextNodes(node, targetLangCode) {
  nodesToTranslate.push({ node, targetLangCode });

  // If we've reached 25 nodes, translate immediately.
  if (nodesToTranslate.length >= 25) {
    if (debounceTimer) {
      clearTimeout(debounceTimer);
      debounceTimer = null;
    }
    translateElementText([...nodesToTranslate], targetLangCode);
    nodesToTranslate = [];
    return; // exit early to avoid setting a new timer below
  }

  // If no timer is currently active, set one for the first node.
  if (!debounceTimer) {
    debounceTimer = setTimeout(() => {
      translateElementText([...nodesToTranslate], targetLangCode);
      nodesToTranslate = [];
      debounceTimer = null;
    }, DEBOUNCE_DELAY);
  }
}

/**
 * Returns true when a node is inside a custom dropdown library's "selected display"
 * container (role="combobox") or any element marked bhashini-skip-mutation.
 *
 * Why we skip these:
 *   Searchable dropdown libraries (Select2, Tom Select, Choices.js, …) manage a
 *   role="combobox" area that shows the currently selected value.  When the plugin
 *   translates that text the library detects the DOM change, can't match the
 *   translated string to its internal English option-label cache, and resets the
 *   selection to the first option.
 *
 *   The dropdown *list* items (role="listbox" panel) are normally rendered outside
 *   the combobox container, so they are still translated via childList mutations.
 *
 * Clients can also add `bhashini-skip-mutation` to any container they want to
 * exclude from MutationObserver-triggered translation.
 */
function isInsideDropdownDisplay(node) {
  var el = (node.nodeType === Node.TEXT_NODE) ? node.parentElement : node;
  while (el && el !== document.body) {
    if (el.classList && el.classList.contains("bhashini-skip-mutation")) {
      return true;
    }
    // Select2 names its listbox panel "select2-{selectId}-results".
    // If the corresponding <select> has bhashini-skip-translation, skip
    // the <li> items inside this panel too — otherwise translating them
    // causes Select2 to lose its internal state and reset to first option.
    if (el.getAttribute && el.getAttribute("role") === "listbox" && el.id) {
      var m = el.id.match(/^select2-(.+)-results$/);
      if (m) {
        var linkedSelect = document.getElementById(m[1]);
        if (linkedSelect && linkedSelect.classList &&
            linkedSelect.classList.contains("bhashini-skip-translation")) {
          return true;
        }
      }
    }
    el = el.parentElement;
  }
  return false;
}

// Create a new MutationObserver
var observer = new MutationObserver((mutations) => {
  // Skip translation if no target language set or if target matches page source language.
  if (!selectedTargetLanguageCode || selectedTargetLanguageCode === pageSourceLanguage) {
    return;
  }

  var nodesToProcess = new Set();

  mutations.forEach((mutation) => {
    if (mutation.type === "childList") {
      mutation.addedNodes.forEach((node) => {
        // Skip nodes inside combobox display areas — translating them causes
        // searchable dropdown libraries to reset the selection (see isInsideDropdownDisplay).
        if (isInsideDropdownDisplay(node)) return;
        if (node.nodeType === Node.ELEMENT_NODE) {
          nodesToProcess.add(node);
        } else if (node.nodeType === Node.TEXT_NODE && node.parentElement) {
          // Frameworks may insert text nodes directly without wrapping element mutations.
          if (!isInsideDropdownDisplay(node.parentElement)) {
            nodesToProcess.add(node.parentElement);
          }
        }
      });
      return;
    }

    if (mutation.type === "characterData") {
      // React/Vue often update text by mutating existing text nodes.
      if (mutation.target && mutation.target.nodeType === Node.TEXT_NODE && mutation.target.parentElement) {
        // Skip combobox display areas (library-managed selected-value text).
        if (!isInsideDropdownDisplay(mutation.target)) {
          nodesToProcess.add(mutation.target.parentElement);
        }
      }
      return;
    }

    if (mutation.type === "attributes" && mutation.target && mutation.target.nodeType === Node.ELEMENT_NODE) {
      nodesToProcess.add(mutation.target);
    }
  });

  nodesToProcess.forEach(function(node) {
    translateElementTextNodes(node, selectedTargetLanguageCode);
  });
});

// Start observing the document body for changes
observer.observe(document.body, {
  childList: true,
  subtree: true,
  characterData: true,
  attributes: true,
  attributeFilter: ["placeholder", "title", "aria-label", "value"]
});

// check if isSelectedLangEnglish is present in sessionStorage
var isSelectedLang = sessionStorage.getItem("selectedLang");
if (isSelectedLang) {
  sessionStorage.removeItem("selectedLang");
  defaultTranslatedLanguage = null;
}

/**
 * --------------------------------------------------------------------------
 * Auto-Translation Initialization
 * 
 * Priority order for determining which language to use:
 * 1. URL query parameter target_lang (fallback from language-specific domain)
 * 2. Domain-specific language (from backend API) - ONLY if is-redirection="true"
 * 3. default-translated-language attribute (from script tag)
 * 4. initial_preferred_language attribute (from script tag)
 * 5. User's saved preference (localStorage)
 * 
 * This allows language-specific domains to work with the same codebase.
 * --------------------------------------------------------------------------
 */
(async function initializeTranslation() {
  var languageToUse = null;
  var isFromTargetLangParam = sessionStorage.getItem('bhashini_from_target_lang') === 'true';

  
  // Priority 1: target_lang query param (explicit user selection from another domain)
  const urlParams = new URLSearchParams(window.location.search);
  const targetLangParam = urlParams.get('target_lang');
  if (targetLangParam) {
    languageToUse = targetLangParam;
    isFromTargetLangParam = true;
    sessionStorage.setItem('bhashini_from_target_lang', 'true');

    // Save the preference (including 'en') so subsequent page loads remember the choice
    localStorage.setItem("preferredLanguage", targetLangParam);

    urlParams.delete('target_lang');
    const newUrl = window.location.pathname + (urlParams.toString() ? '?' + urlParams.toString() : '') + window.location.hash;
    window.history.replaceState({}, '', newUrl);
  } else if (isFromTargetLangParam) {
    // We already processed target_lang, use the saved preference
    languageToUse = localStorage.getItem("preferredLanguage") || "en";
  }

  // Priority 2: Domain-configured language (only when isRedirection)
  if (!languageToUse && isRedirection) {
    try {
      const domainLanguage = await getDomainLanguage();
      if (domainLanguage && domainLanguage !== "en") {
        languageToUse = domainLanguage;
        localStorage.setItem("preferredLanguage", domainLanguage);
      }
    } catch (err) {
      console.error('[Bhashini] Error getting domain language:', err);
    }
  } else {
    console.log('[Bhashini] Priority 2 SKIPPED');
  }

  // Priority 3: default-translated-language attribute
  // SKIP redirect if we just processed a target_lang param (prevents bounce back)
  
  if (!languageToUse && defaultTranslatedLanguage && defaultTranslatedLanguage !== "en") {
    languageToUse = defaultTranslatedLanguage;
    if (isRedirection && !isFromTargetLangParam) {
      try {
        const redirUrl = await getRedirectionUrl(languageToUse);
        if (redirUrl) {
          const redirHref = new URL(redirUrl).href;
          const currentHref = window.location.href;
          const redirOrigin = new URL(redirUrl).origin;
          const currentOrigin = window.location.origin;

          // Only redirect if origin is different AND URL differs
          if (redirOrigin !== currentOrigin && redirHref !== currentHref) {
            localStorage.setItem("preferredLanguage", languageToUse);
            sessionStorage.setItem('bhashini_redirected', '1');
            window.location.href = redirHref;
            return;
          } else {
            console.log('[Bhashini] Skipping redirect — same origin or identical URL.');
          }
        } else {
          console.log('[Bhashini] No mapped URL returned for defaultTranslatedLanguage; falling back to in-place translation.');
        }
      } catch (err) {
        console.error('[Bhashini] Error while fetching redirection URL:', err);
      }
    } else {
      // Save preference even when redirect is skipped so next page load doesn't re-redirect
      localStorage.setItem("preferredLanguage", languageToUse);
      console.log('[Bhashini] Priority 3 redirect SKIPPED - isFromTargetLangParam:', isFromTargetLangParam);
    }
  } else {
    console.log('[Bhashini] Priority 3 SKIPPED - languageToUse already set or no defaultTranslatedLanguage');
  }

  // Priority 4: initial_preferred_language + isRedirection
  // Redirect if: no saved preference, OR saved preference matches initialPreferredLanguage.
  // If user chose a DIFFERENT language (e.g. they selected English explicitly), respect that choice.
  const savedPreference = localStorage.getItem("preferredLanguage");
  // Allow redirect if: no saved preference, OR saved preference matches initialPreferredLanguage
  // (i.e. user hasn't chosen a DIFFERENT language — respect site's redirect intent)
  const shouldRedirectForInitialPreference = !savedPreference || savedPreference === initialPreferredLanguage;
  if (!languageToUse && initialPreferredLanguage && initialPreferredLanguage !== "en" && isRedirection && shouldRedirectForInitialPreference) {
    languageToUse = initialPreferredLanguage;
    if (isRedirection && !isFromTargetLangParam) {
      try {
        const redirUrl = await getRedirectionUrl(languageToUse);
        if (redirUrl) {
          const redirHref = new URL(redirUrl).href;
          const currentHref = window.location.href;
          const redirOrigin = new URL(redirUrl).origin;
          const currentOrigin = window.location.origin;

          if (redirOrigin !== currentOrigin && redirHref !== currentHref) {
            localStorage.setItem("preferredLanguage", languageToUse);
            sessionStorage.setItem('bhashini_redirected', '1');
            window.location.href = redirHref;
            return;
          } else {
            console.log('[Bhashini] Priority 4 - Skipping redirect — same origin or identical URL.');
          }
        } else {
          console.log('[Bhashini] Priority 4 - No mapped URL returned; falling back to in-place translation.');
        }
      } catch (err) {
        console.error('[Bhashini] Priority 4 - Error while fetching redirection URL:', err);
      }
    } else {
      // Save preference even when redirect is skipped so next page load doesn't re-redirect
      localStorage.setItem("preferredLanguage", languageToUse);
      console.log('[Bhashini] Priority 4 redirect SKIPPED - isFromTargetLangParam:', isFromTargetLangParam);
    }
  } else {
    console.log('[Bhashini] Priority 4 SKIPPED - languageToUse:', languageToUse, 'savedPreference:', savedPreference);
  }

  // Priority 5: saved preference or initial preference (no redirect)
  if (!languageToUse) {
    languageToUse = localStorage.getItem("preferredLanguage") || initialPreferredLanguage;
    if (languageToUse) {
      console.log('[Bhashini] Priority 5 - Using saved/initial preference:', languageToUse);
    }
  }

  // If language determined and no redirect executed, do in-place translation
  // Translation is needed when:
  // 1. languageToUse is set AND
  // 2. Either languageToUse is not English, OR pageSourceLanguage is not English (translate TO English)
  if (languageToUse && (languageToUse !== "en" || pageSourceLanguage !== "en")) {
    selectedTargetLanguageCode = languageToUse;
    isContentTranslated = true;
    translateAllTextNodes(languageToUse);
    scheduleInitialTranslationRescan(languageToUse);
  } else {
    console.log('[Bhashini] No translation needed - English source with English preference or no preference');
  }
})();

// var languageToUse =
//   defaultTranslatedLanguage && defaultTranslatedLanguage !== "en"
//     ? defaultTranslatedLanguage
//     : localStorage.getItem("preferredLanguage");
// if (languageToUse) {
//   selectedTargetLanguageCode = languageToUse;
//   // document.getElementById("translate-plugin-target-language-list").value =
//   //   languageToUse;
//   isContentTranslated = true;
//   translateAllTextNodes(languageToUse);
// }

// Function to handle dropdown change
async function onDropdownChange(event) {
  var selectedValue = event.target.value;

  // Resolve human-readable label for notifications (WCAG 3.2.2)
  var selectedLangObj = supportedTargetLangArr.find(function(l) { return l.code === selectedValue; });
  var selectedLangLabel = selectedLangObj ? selectedLangObj.label : selectedValue;

  isContentTranslated = true;
  sessionStorage.setItem("selectedLang", selectedValue);
  if (!isRedirection) {
    localStorage.setItem("preferredLanguage", selectedValue);
  }

  // Notify user before any context change (WCAG 3.2.2), then navigate after a brief delay
  function notifyAndNavigate(message, navigate) {
    if (isWcagNotification) {
      showToast(message);
      setTimeout(navigate, 1500);
    } else {
      navigate();
    }
  }

  // Handle English selection
  if (selectedValue === "en") {
    // If page source language is NOT English, we need to translate TO English
    if (pageSourceLanguage !== "en") {
      localStorage.setItem("preferredLanguage", selectedValue);
    notifyAndNavigate('Translating this page to English.', function() {
  if (isReload) {
    window.location.reload();
  } else {
    resetTranslationStateForInPlaceUpdate();
    selectedTargetLanguageCode = "en";
    translateAllTextNodes("en");
    scheduleInitialTranslationRescan("en");
  }
});
      return;
    }

    // Page source is English - save English preference and reload
    localStorage.setItem("preferredLanguage", "en");
    sessionStorage.removeItem('bhashini_from_target_lang');

    if (isRedirection) {
      // Use the redirection API - it will return original domain
      var redirectionUrl = await getRedirectionUrl(selectedValue);
      if (redirectionUrl) {
        // Ensure target_lang=en is in the URL (backend may not include it)
        var url = new URL(redirectionUrl);
        if (!url.searchParams.has('target_lang')) {
          url.searchParams.set('target_lang', 'en');
        }
        notifyAndNavigate('Redirecting to the original version of this page.', function() {
          window.location.href = url.href;
        });
        return;
      }
    }
    // No redirection or already on original - just reload
  notifyAndNavigate('Reverting to the original language of this page.', function() {
  if (isReload) {
    window.location.reload();
  } else {
    resetTranslationStateForInPlaceUpdate();
    selectedTargetLanguageCode = pageSourceLanguage;
    translateAllTextNodes(pageSourceLanguage);
    scheduleInitialTranslationRescan(pageSourceLanguage);
  }
});
    return;
  }

  // Perform translation for the selected language
  if (isRedirection) {
    var redirectionUrl = await getRedirectionUrl(selectedValue);
    if (redirectionUrl) {
      notifyAndNavigate('Redirecting to the ' + selectedLangLabel + ' version of this page.', function() {
        window.location.href = redirectionUrl;
      });
    } else {
      // No redirection URL returned (404 or error) - translate in place
      localStorage.setItem("preferredLanguage", selectedValue);
      if (isReload) {
        notifyAndNavigate('Translating this page to ' + selectedLangLabel + '.', function() {
          window.location.reload();
        });
      } else {
        resetTranslationStateForInPlaceUpdate();
        selectedTargetLanguageCode = selectedValue;
        translateAllTextNodes(selectedValue);
        scheduleInitialTranslationRescan(selectedValue);
      }
    }
  } else {
    if (isReload) {
      notifyAndNavigate('Translating this page to ' + selectedLangLabel + '.', function() {
        window.location.reload();
      });
    } else {
      resetTranslationStateForInPlaceUpdate();
      selectedTargetLanguageCode = selectedValue;
      translateAllTextNodes(selectedValue);
      scheduleInitialTranslationRescan(selectedValue);
    }
  }
}

// Function to show a toast messages
function showToast(message) {
  var toast = document.createElement("div");
  toast.className = "bhashini-toast";
  toast.textContent = message;
  toast.setAttribute("role", "alert");
  toast.setAttribute("aria-live", "assertive");
  toast.setAttribute("aria-label", message);
  document.body.appendChild(toast);
  setTimeout(() => {
    toast.classList.add("visible");
    toast.classList.add("bhashini-skip-translation");
  }, 100);
  setTimeout(() => {
    toast.classList.remove("visible");
    setTimeout(() => {
      document.body.removeChild(toast);
    }, 300);
  }, 3000);
}

// Function to restore translations from session storage
// function restoreTranslations() {
//   var textNodes = getTextNodesToTranslate(document.body);
//   textNodes.forEach((node) => {
//     var id = node.parentNode.getAttribute("data-translation-id");
//     if (id && translationCache[id]) {
//       node.nodeValue = translationCache[id];
//     }
//   });
//   fetchTranslationSupportedLanguages();
// }

// Function to translate all text nodes in the document
async function translateAllTextNodes(target_lang) {
  var promises = [];
  
  // Get all text nodes from the document
  var allTextNodes = getAllTextNodesToTranslate(document.body);
  
  // Filter to only visible nodes (viewport + 20% buffer below)
  var textNodes = filterVisibleNodes(allTextNodes, 20);
  
  //debugLog(' Total nodes:', allTextNodes.length, '| Visible nodes to translate:', textNodes.length);
  
  if (textNodes.length > 0) {
    var textContentArray = textNodes.map((node, index) => {
      var id = `translation-${Date.now()}-${index}`;
      var originalText = persistOriginalNodeContent(node, id);
      translationCache[id] = originalText;
      translatedNodesSet.add(node.node);
      return { text: originalText, id, node };
    });
    var textChunks = chunkArray(textContentArray, CHUNK_SIZE);

    // Create an array to hold promises for each chunk translation
    var textNodePromises = textChunks.map(async (chunk) => {
      var texts = chunk.map(({ text }) => text);
      // if (target_lang === "en") {
      //         return;
      // }
      var translatedTexts = await translateTextChunks(texts, target_lang);
      chunk.forEach(({ node }, index) => {
        var translatedText = translatedTexts[index].target || texts[index];

        if (node.type === "text") {
          node.node.nodeValue = translatedText;
        }
        if (node.type === "value") {
          node.node.value = translatedText;
        }
        if (node.type === "placeholder") {
          node.node.placeholder = translatedText;
        }
        if (node.type === "title") {
          node.node.setAttribute("title", translatedText);
        }
      });
    });
    promises.push(textNodePromises);

    // Wait for all translations to complete
    await Promise.all(promises);

    // Update the Language Translator button aria-label for the selected language
    updateTranslatorButtonLabel(target_lang);
  }

  // Always observe remaining nodes, even if the first visible scan found nothing.
  observeHiddenNodes(allTextNodes);
}

function scheduleInitialTranslationRescan(target_lang) {
  function rescan() {
    if (!target_lang || target_lang === pageSourceLanguage) {
      return;
    }
    translateAllTextNodes(target_lang);
  }

  // Catch content that renders just after the initial scan during SPA mount.
  if (window.requestAnimationFrame) {
    window.requestAnimationFrame(function() {
      window.requestAnimationFrame(rescan);
    });
  }

  setTimeout(rescan, 800);
}

// Store translationCache in session storage
sessionStorage.setItem("translationCache", JSON.stringify(translationCache));

// Function to adjust widget position based on device width
var adjustWidgetPosition = () => {
  var wrapperButton = document.getElementById("bhashini-translation");
  if (window.innerWidth <= 768) {
    // Position for mobile devices
    wrapperButton.style.left = `calc(100vw - ${
      wrapperButton.offsetWidth + 10
    }px)`;
    wrapperButton.style.bottom = `10px`;
  } else if (window.innerWidth <= 1024) {
    // Position for tabvar devices
    wrapperButton.style.left = `calc(100vw - ${
      wrapperButton.offsetWidth + 20
    }px)`;
    wrapperButton.style.bottom = `20px`;
  }
};

// CSS for toast message and dropdown list
var toastStyles = `
    .bhashini-toast {
        position: fixed;
        left: 50%;
        bottom: 20px;
        transform: translateX(-50%);
        background-color: rgba(0, 0, 0, 0.7);
        color: white;
        padding: 10px 20px;
        border-radius: 5px;
        opacity: 0;
        transition: opacity 0.3s ease, bottom 0.3s ease;
        z-index: 10000;
    }
    .bhashini-toast.visible {
        opacity: 1;
        bottom: 40px;
    }
  
    #bhashiniLanguageDropdown .language-option {
        list-style: none;
    }
`;

var styleSheet = document.createElement("style");
styleSheet.innerText = toastStyles;
document.head.appendChild(styleSheet);
