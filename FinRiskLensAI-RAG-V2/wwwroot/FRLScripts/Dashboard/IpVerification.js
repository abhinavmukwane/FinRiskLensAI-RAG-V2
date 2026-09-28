// ============================================================================
// Source IP Security Audit — dashboard "Secure Connection IP" popup.
// The badge IP is server-rendered from session (captured at login). The IP
// risk data is fetched ONLY when the popup opens (DashboardController
// .GetIpVerificationDetail → the single IP risk service), then bound into the
// existing modal, and a Leaflet map is rendered.
// ============================================================================

(function () {
    'use strict';

    const GAUGE_CIRCUMFERENCE = 2 * Math.PI * 70; // r = 70 in the SVG (≈ 440)

    let data = null;   // last fetched IP verification data
    let loaded = false;
    let mapInited = false;

    function text(id, val) {
        const el = document.getElementById(id);
        if (el) el.textContent = (val === null || val === undefined || val === '') ? '—' : val;
    }

    function setCheck(id, isClean, cleanText, flaggedText) {
        const el = document.getElementById(id);
        if (!el) return;
        el.classList.remove('checked', 'flagged');
        el.classList.add(isClean ? 'checked' : 'flagged');
        const status = el.querySelector('.check-status-lbl');
        if (status) status.textContent = isClean ? cleanText : flaggedText;
    }

    function render(d) {
        const empty = document.getElementById('ipAuditEmpty');
        const content = document.getElementById('ipAuditContent');

        if (!d || !d.hasData) {
            if (empty) empty.style.display = 'block';
            if (content) content.style.display = 'none';
            return;
        }
        if (empty) empty.style.display = 'none';
        if (content) content.style.display = 'block';

        const secure = !!d.isSecure;
        const score = Math.max(0, Math.min(100, d.fraudScore || 0));
        const color = score < 25 ? '#2e7d32' : (score < 60 ? '#d97706' : '#b3261e');

        // Trust alert banner
        const alert = document.getElementById('ipTrustAlert');
        const icon = document.getElementById('ipTrustIcon');
        const badge = document.getElementById('ipTrustBadge');
        if (alert) alert.className = 'trust-alert-banner ' + (secure ? 'secure' : 'insecure');
        if (icon) icon.className = 'bi alert-icon-ring ' + (secure ? 'bi-shield-fill-check' : 'bi-shield-fill-exclamation');
        text('ipTrustTitle', secure ? 'IP Address Verified & Trusted' : 'IP Address Needs Review');
        text('ipTrustDesc', 'Your connection IP (' + d.ip + ') was screened. Fraud index is ' + score +
            '%, indicating a ' + (secure ? 'highly secure' : 'potentially risky') + ' login origin.');
        if (badge) badge.textContent = secure ? 'SECURE' : 'REVIEW';

        // Small trust pill under the gauge
        const pill = document.getElementById('ipTrustPill');
        if (pill) {
            pill.className = 'badge px-3 py-2 rounded-pill ' +
                (secure ? 'bg-success-subtle text-success' : 'bg-danger-subtle text-danger');
            pill.innerHTML = secure
                ? '<i class="bi bi-shield-check me-1"></i> Trust Verified'
                : '<i class="bi bi-shield-exclamation me-1"></i> Review Advised';
        }

        // Fraud gauge — let the CSS scan play, then settle at the real score.
        const gauge = document.getElementById('ipGauge');
        text('ipGaugeVal', score + '%');
        if (gauge) {
            const finalOffset = GAUGE_CIRCUMFERENCE - (score / 100) * GAUGE_CIRCUMFERENCE;
            const settle = function () {
                gauge.style.animation = 'none';               // release the keyframes' forwards fill
                gauge.style.stroke = color;
                gauge.style.strokeDasharray = GAUGE_CIRCUMFERENCE;
                gauge.style.transition = 'stroke-dashoffset .6s ease, stroke .3s ease';
                gauge.style.strokeDashoffset = finalOffset;
            };
            // Restart the scan each time the modal is rendered, then settle on end.
            gauge.style.animation = 'none';
            void gauge.getBoundingClientRect();
            gauge.style.animation = '';
            gauge.addEventListener('animationend', settle, { once: true });
            // Fallback in case animationend doesn't fire (e.g. reduced motion).
            setTimeout(settle, 2400);
        }

        // Connection checks
        setCheck('ipChkVpn', !d.vpn, 'Clean / No VPN', 'VPN Detected');
        setCheck('ipChkProxy', !d.proxy, 'Clean / No Proxy', 'Proxy Detected');
        setCheck('ipChkTor', !d.tor, 'Clean / No TOR', 'Tor Exit Node');
        setCheck('ipChkBot', !d.bot, 'Clean / Human', 'Bot Traffic');

        // Network specs
        text('ipIsp', d.isp);
        text('ipOrg', d.organization);
        text('ipAsn', d.asn);
        text('ipProto', d.connectionType);

        // Geo details
        text('ipCountry', d.countryCode ? (d.countryName + ' / ' + d.countryCode) : d.countryName);
        text('ipRegion', d.region);
        text('ipCity', d.city);
        text('ipCoords', d.coordinates || '—');
        text('ipVerifiedOn', new Date().toLocaleString());

        const maps = document.getElementById('ipMapsLink');
        if (maps) {
            const q = d.coordinates ? d.coordinates.replace(/\s/g, '')
                : encodeURIComponent([d.city, d.region, d.countryName].filter(Boolean).join(', '));
            maps.href = 'https://www.google.com/maps/search/?api=1&query=' + q;
        }
    }

    // ── Leaflet map (rendered when the modal is first shown) ─────────────
    function initMap() {
        if (mapInited || typeof L === 'undefined' || !data || !data.hasData) return;
        const el = document.getElementById('popupMap');
        if (!el) return;

        const lat = parseFloat(data.latitude);
        const lng = parseFloat(data.longitude);
        if (isNaN(lat) || isNaN(lng)) return;

        mapInited = true;
        const map = L.map('popupMap', { center: [lat, lng], zoom: 12, zoomControl: true });
        L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
            attribution: '&copy; OpenStreetMap &copy; CARTO',
            subdomains: 'abcd',
            maxZoom: 20
        }).addTo(map);

        const pin = L.divIcon({
            className: 'popup-marker',
            html: '<div style="background:#8b1538;width:14px;height:14px;border-radius:50%;border:3px solid #fff;box-shadow:0 0 10px rgba(139,21,56,.6);"></div>',
            iconSize: [14, 14],
            iconAnchor: [7, 7]
        });
        L.marker([lat, lng], { icon: pin }).addTo(map)
            .bindPopup('<b>Secure Audit Location</b><br>' + data.ip + ' (' + data.city + ', ' + data.countryCode + ')')
            .openPopup();

        // Tiles need a size recalc once the modal is fully visible.
        setTimeout(function () { map.invalidateSize(); }, 200);
    }

    // Fetch the IP data once (static method until the production key is live).
    async function load() {
        if (loaded) return;
        loaded = true;
        try {
            const res = await fetch('/Dashboard/GetIpVerificationDetail');
            const json = await res.json();
            data = json && json.data;

            // Show the IP (from the static method) in the badge.
            const badgeEl = document.getElementById('ipBadgeValue');
            if (badgeEl && data && data.ip) badgeEl.textContent = data.ip;
        } catch (e) {
            data = null;
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        // Populate the badge from the static IP data on page load.
        load();

        const modal = document.getElementById('ipAuditModal');
        if (!modal) return;

        modal.addEventListener('shown.bs.modal', async function () {
            await load();
            render(data);
            initMap();
        });
    });
})();
