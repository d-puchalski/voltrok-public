// Deck.gl interop for Blazor
// v1.0.1

const MIN_MAP_ZOOM = 2;
const MAX_MAP_ZOOM = 19;
const DEFAULT_MAP_ZOOM = 4;

function clampZoom(zoom) {
    const numeric = Number(zoom);
    if (!Number.isFinite(numeric)) {
        return MIN_MAP_ZOOM;
    }

    return Math.min(MAX_MAP_ZOOM, Math.max(MIN_MAP_ZOOM, numeric));
}

function isValidCoordinate(value, min, max) {
    const numeric = Number(value);
    return Number.isFinite(numeric) && numeric >= min && numeric <= max;
}

function shouldPersistMapViewState(viewState) {
    if (!viewState) {
        return false;
    }

    const longitude = Number(viewState.longitude);
    const latitude = Number(viewState.latitude);
    const zoom = Number(viewState.zoom);

    if (!isValidCoordinate(longitude, -180, 180) || !isValidCoordinate(latitude, -90, 90) || !Number.isFinite(zoom)) {
        return false;
    }

    // Prevent accidental overwrite with the Gulf of Guinea fallback state that can
    // appear during transient map initialization before the real player focus lands.
    if (Math.abs(longitude) < 0.000001 && Math.abs(latitude) < 0.000001) {
        return false;
    }

    return true;
}

function persistMapViewState(viewState) {
    if (!shouldPersistMapViewState(viewState)) {
        return;
    }

    localStorage.setItem('map_view_state', JSON.stringify({
        longitude: Number(viewState.longitude),
        latitude: Number(viewState.latitude),
        zoom: clampZoom(viewState.zoom)
    }));
}

function getMapState(map) {
    if (!map._state) {
        map._state = {
            countryStyles: [],
            countriesData: null,
            pointsData: null,
            zoom: 2,
            viewState: null,
            visibility: {
                points: true,
                countries: true,
                flags: false,
                transports: true,
                sieges: true
            },
            baseLayer: null,
            colorMode: 'countries',
            ownWarCountryIds: [],
            enemyWarCountryIds: [],
            countryTooltipData: [],
            countryTooltipByCountryId: {},
            playerTooltipData: [],
            playerTooltipByPlayerId: {},
            localization: {
                locale: 'en',
                labels: {}
            },
            dotnetRefs: {},
            hoveredLayerId: null,
            hoveredTransportId: null,
            keyboardHandlerAttached: false,
            debugMode: false
        };
    }
    return map._state;
}

function updateDebugZoomOverlay(map) {
    if (!map) return;

    const state = getMapState(map);
    const containerId = map.props?.container;
    const container = containerId ? document.getElementById(containerId) : null;
    if (!container) return;

    let overlay = container.querySelector('[data-map-debug-zoom="true"]');
    if (!state.debugMode) {
        if (overlay) {
            overlay.remove();
        }
        return;
    }

    if (!overlay) {
        if (getComputedStyle(container).position === 'static') {
            container.style.position = 'relative';
        }

        overlay = document.createElement('div');
        overlay.setAttribute('data-map-debug-zoom', 'true');
        overlay.style.position = 'absolute';
        overlay.style.left = '12px';
        overlay.style.bottom = '12px';
        overlay.style.zIndex = '30';
        overlay.style.pointerEvents = 'none';
        overlay.style.padding = '6px 10px';
        overlay.style.borderRadius = '8px';
        overlay.style.border = '1px solid var(--medieval-map-border)';
        overlay.style.background = 'color-mix(in srgb, var(--medieval-panel-strong) 88%, transparent)';
        overlay.style.color = 'var(--medieval-map-text)';
        overlay.style.fontFamily = 'Cinzel, serif';
        overlay.style.fontSize = '12px';
        overlay.style.lineHeight = '1';
        overlay.style.letterSpacing = '0.04em';
        overlay.style.boxShadow = '0 6px 18px var(--medieval-shadow)';
        container.appendChild(overlay);
    }

    overlay.textContent = `DEBUG ZOOM ${Number(state.zoom || 0).toFixed(2)}`;
}

function normalizeLocale(locale) {
    const normalized = String(locale || 'en').trim().toLowerCase();
    if (!normalized) {
        return 'en';
    }

    const [primary] = normalized.split('-');
    return primary || 'en';
}

function applyLocalization(state, locale, localization) {
    const labels = localization?.labels && typeof localization.labels === 'object'
        ? localization.labels
        : (localization && typeof localization === 'object' ? localization : {});

    state.localization = {
        locale: normalizeLocale(locale || localization?.locale),
        labels: labels || {}
    };
}

function translateLabel(state, key, fallback = '') {
    const value = state?.localization?.labels?.[key];
    if (typeof value === 'string' && value.length > 0 && value !== key) {
        return value;
    }

    return fallback;
}

function translateCodeLabel(state, prefixes, rawCode, fallback = '-') {
    const normalizedCode = String(rawCode || '').trim().toLowerCase();
    if (!normalizedCode) {
        return fallback;
    }

    for (const prefix of prefixes) {
        const translated = translateLabel(state, `${prefix}${normalizedCode}`, '');
        if (translated) {
            return translated;
        }
    }

    return rawCode || fallback;
}

function translateCountryName(state, countryCode, fallback = '') {
    const normalizedCode = getCountryCode(countryCode);
    const translated = translateLabel(state, `countries.name.${normalizedCode}`, '');
    return translated || fallback || normalizedCode;
}

function translateWarScope(state, scope) {
    const normalizedScope = String(scope || '').trim().toLowerCase();
    if (!normalizedScope) {
        return translateLabel(state, 'layout.map.tooltip.warScope.default', 'War');
    }

    return translateLabel(state, `layout.map.tooltip.warScope.${normalizedScope}`, scope) || scope;
}

function translateTransportStatus(state, movementType, status) {
    const normalizedStatus = String(status || '').trim();
    if (!normalizedStatus) {
        return translateLabel(state, 'tabs.transports.status.unknown', 'Unknown');
    }

    const suffix = normalizedStatus.replace(/[_\s]+(.)?/g, (_, ch) => ch ? ch.toUpperCase() : '');
    const normalizedMovementType = String(movementType || '').toLowerCase();
    const keys = normalizedMovementType === 'military'
        ? [`tabs.military.transportStatus.${suffix}`, `tabs.transports.status.${suffix}`]
        : [`tabs.transports.status.${suffix}`, `tabs.military.transportStatus.${suffix}`];

    for (const key of keys) {
        const translated = translateLabel(state, key, '');
        if (translated) {
            return translated;
        }
    }

    return normalizedStatus;
}

function attachKeyboardPan(map) {
    const state = getMapState(map);
    if (state.keyboardHandlerAttached) {
        return;
    }

    state.keyboardKeys = new Set();
    state.keyboardAnimating = false;
    state.keyboardLastFrame = 0;

    const shouldIgnoreEvent = (event) => {
        const tag = event.target?.tagName;
        return tag === 'INPUT' || tag === 'TEXTAREA' || event.target?.isContentEditable;
    };

    const startAnimation = () => {
        if (state.keyboardAnimating) {
            return;
        }

        state.keyboardAnimating = true;
        state.keyboardLastFrame = performance.now();

        const animate = (timestamp) => {
            if (!state.keyboardAnimating) {
                return;
            }

            const current = state.viewState ?? map.props.viewState;
            if (!current) {
                state.keyboardLastFrame = timestamp;
                requestAnimationFrame(animate);
                return;
            }

            state.keyboardLastFrame = timestamp;
            const speed = 0.07;

            let longitude = current.longitude;
            let latitude = current.latitude;

            if (state.keyboardKeys.has('w')) latitude += speed;
            if (state.keyboardKeys.has('s')) latitude -= speed;
            if (state.keyboardKeys.has('a')) longitude -= speed;
            if (state.keyboardKeys.has('d')) longitude += speed;

            const nextViewState = {
                ...current,
                longitude,
                latitude
            };

            state.viewState = nextViewState;
            map.setProps({ viewState: nextViewState });

            if (state.keyboardKeys.size === 0) {
                state.keyboardAnimating = false;
                return;
            }

            requestAnimationFrame(animate);
        };

        requestAnimationFrame(animate);
    };

    window.addEventListener('keydown', (event) => {
        if (shouldIgnoreEvent(event)) {
            return;
        }

        const key = event.key?.toLowerCase();
        if (!['w', 'a', 's', 'd'].includes(key)) {
            return;
        }

        state.keyboardKeys.add(key);
        startAnimation();
        event.preventDefault();
    });

    window.addEventListener('keyup', (event) => {
        const key = event.key?.toLowerCase();
        if (!['w', 'a', 's', 'd'].includes(key)) {
            return;
        }

        state.keyboardKeys.delete(key);
    });

    state.keyboardHandlerAttached = true;
}

const layerIdMap = {
    "points-layer": "points",
    "countries-layer": "countries",
    "country-flags": "flags",
    "transports-layer": "transports",
    "sieges-layer": "sieges"
};

function normalizeFeature(feature) {
    if (!feature) return null;
    const props = feature.properties || {};
    const featureName =
        props.name ||
        props.NAME ||
        props.name_en ||
        props.admin ||
        props.geonunit ||
        props.label ||
        "";
    const rawIsoCode2 =
        props.iso_code_2 ||
        props.iso_3166_2 ||
        props.ISO_3166_2 ||
        props.iso_a2 ||
        props.ISO_A2 ||
        props.HASC_1 ||
        "";
    const resolvedIsoCode2 = resolveCountryCode(rawIsoCode2, featureName) || rawIsoCode2;
    const id = String(
        feature.id ||
        props.id ||
        props.iso_3166_2 ||
        props.ISO_3166_2 ||
        props.iso_code_2 ||
        props.iso_a2 ||
        props.ISO_A2 ||
        ""
    );

    return {
        id: id,
        properties: {
            area: props.area || props.AREA || 0,
            adm_code_1: props.adm_code_1 || props.ADM1_PCODE || props.iso_3166_2 || props.ISO_3166_2 || "",
            iso_code_2: resolvedIsoCode2,
            name: featureName,
            playerId: props.playerId || props.PlayersId || null,
            code: props.code || props.Code || null
        }
    };
}

function hashString(value) {
    const text = String(value || "");
    let hash = 0;
    for (let i = 0; i < text.length; i++) {
        hash = ((hash << 5) - hash) + text.charCodeAt(i);
        hash |= 0;
    }
    return Math.abs(hash);
}

function clampChannel(value) {
    return Math.max(0, Math.min(255, Math.round(value)));
}

function blendColors(base, tint, amount) {
    const ratio = Math.max(0, Math.min(1, amount));
    return [
        clampChannel(base[0] * (1 - ratio) + tint[0] * ratio),
        clampChannel(base[1] * (1 - ratio) + tint[1] * ratio),
        clampChannel(base[2] * (1 - ratio) + tint[2] * ratio),
        base[3] ?? 255
    ];
}

function atlasPalette(key, alpha = 255) {
    const hash = hashString(key);
    const family = hash % 6;
    const palettes = [
        [124, 194, 215],
        [99, 175, 203],
        [147, 205, 220],
        [89, 162, 194],
        [176, 220, 230],
        [111, 188, 210]
    ];
    const base = palettes[family];
    const drift = ((hash >> 3) % 18) - 9;
    return [
        clampChannel(base[0] + drift),
        clampChannel(base[1] + drift),
        clampChannel(base[2] + drift),
        alpha
    ];
}

const playerBadgeIconCache = new Map();
const playerAvatarIconCache = new Map();
const pendingAvatarLoads = new Set();
const UNIFIED_AVATAR_FRAME_WIDTH = 2.0;
const UNIFIED_AVATAR_RING_LAYER_WIDTH = 2;
const PLAYER_ICON_SIZE_METERS = 1000;
const PLAYER_ICON_MIN_PIXELS = 48;
const PLAYER_ICON_MAX_PIXELS = 256;
const PLAYER_RING_RADIUS_METERS = Math.round(PLAYER_ICON_SIZE_METERS * 0.5);
const PLAYER_RING_MIN_PIXELS = Math.round(PLAYER_ICON_MIN_PIXELS * 0.5);
const PLAYER_RING_MAX_PIXELS = Math.round(PLAYER_ICON_MAX_PIXELS * 0.5);
const ACTIVE_SIEGE_ICON_SIZE_METERS = 2800;
const ACTIVE_SIEGE_ICON_MIN_PIXELS = 20;
const ACTIVE_SIEGE_ICON_MAX_PIXELS = 42;
const ACTIVE_SIEGE_RING_RADIUS_METERS = 1680;
const ACTIVE_SIEGE_RING_MIN_PIXELS = 12;
const ACTIVE_SIEGE_RING_MAX_PIXELS = 24;

function toAbsoluteAvatarUrl(url) {
    const defaultAvatarPath = "/assets/icons/kings/1.png";
    const raw = String(url ?? "").trim();
    if (!raw) {
        return `${window.location.origin}${defaultAvatarPath}`;
    }

    const legacyHeraldicMatch = raw.match(/(?:^|\/)assets\/icons\/heraldic\/(\d+)\.(?:png|jpe?g|webp)$/i);
    if (legacyHeraldicMatch) {
        return `${window.location.origin}/assets/icons/kings/${legacyHeraldicMatch[1]}.png`;
    }

    const kingsAvatarMatch = raw.match(/(?:^|\/)assets\/icons\/kings\/(\d+)\.(?:png|jpe?g|webp)$/i);
    if (kingsAvatarMatch) {
        return `${window.location.origin}/assets/icons/kings/${kingsAvatarMatch[1]}.png`;
    }

    if (raw.startsWith("data:") || /^https?:\/\//i.test(raw)) {
        return raw;
    }

    if (raw.startsWith("/")) {
        return `${window.location.origin}${raw}`;
    }

    try {
        return new URL(raw, window.location.origin).href;
    } catch {
        return `${window.location.origin}${defaultAvatarPath}`;
    }
}

function getStylizedPlayerBadgeIcon() {
    const cacheKey = "__badge__";
    if (playerBadgeIconCache.has(cacheKey)) {
        return playerBadgeIconCache.get(cacheKey);
    }

    const svg = `
<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">
  <defs>
    <linearGradient id="frameGlow" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#6df7ff"/>
      <stop offset="55%" stop-color="#32b6ff"/>
      <stop offset="100%" stop-color="#7e6bff"/>
    </linearGradient>
    <linearGradient id="panel" x1="0%" y1="0%" x2="0%" y2="100%">
      <stop offset="0%" stop-color="#11243b"/>
      <stop offset="100%" stop-color="#0a1324"/>
    </linearGradient>
    <filter id="softGlow" x="-50%" y="-50%" width="200%" height="200%">
      <feGaussianBlur stdDeviation="7" result="blur"/>
      <feBlend mode="screen" in="SourceGraphic" in2="blur"/>
    </filter>
  </defs>

  <rect x="64" y="44" width="384" height="424" rx="36" fill="#020610" opacity="0.92"/>
  <rect x="70" y="50" width="372" height="412" rx="32" fill="url(#panel)" stroke="#263f60" stroke-width="4"/>
  <rect x="82" y="64" width="348" height="384" rx="22" fill="none" stroke="url(#frameGlow)" stroke-width="10" filter="url(#softGlow)"/>
  <rect x="100" y="84" width="312" height="344" rx="16" fill="none" stroke="#9de9ff" stroke-opacity="0.55" stroke-width="3"/>

  <path d="M96 104 L148 104 L130 122 L96 122 Z" fill="#6df7ff" opacity="0.85"/>
  <path d="M416 104 L364 104 L382 122 L416 122 Z" fill="#6df7ff" opacity="0.85"/>
  <path d="M96 408 L136 408 L120 392 L96 392 Z" fill="#7e6bff" opacity="0.8"/>
  <path d="M416 408 L376 408 L392 392 L416 392 Z" fill="#7e6bff" opacity="0.8"/>
</svg>`;

    const badgeDataUrl = `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`;
    const icon = {
        url: badgeDataUrl,
        width: 512,
        height: 512,
        anchorX: 256,
        anchorY: 256,
        mask: false
    };

    playerBadgeIconCache.set(cacheKey, icon);
    return icon;
}

function getFallbackAvatarIcon() {
    const cacheKey = "__avatar_fallback__";
    if (playerAvatarIconCache.has(cacheKey)) {
        return playerAvatarIconCache.get(cacheKey);
    }

    const svg = `
<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">
  <defs>
    <linearGradient id="bg" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#1f3b58"/>
      <stop offset="100%" stop-color="#0d1724"/>
    </linearGradient>
  </defs>
  <circle cx="256" cy="256" r="214" fill="url(#bg)"/>
  <circle cx="256" cy="208" r="74" fill="#9fb3c8"/>
  <path d="M136 376c0-70 54-118 120-118s120 48 120 118" fill="#9fb3c8"/>
  <circle cx="256" cy="256" r="214" fill="none" stroke="rgba(148,163,184,0.8)" stroke-width="10"/>
</svg>`;

    const icon = {
        url: `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`,
        width: 512,
        height: 512,
        anchorX: 256,
        anchorY: 256,
        mask: false
    };

    playerAvatarIconCache.set(cacheKey, icon);
    return icon;
}

function scheduleMapRerender(map) {
    const state = getMapState(map);
    if (state.avatarRerenderScheduled) {
        return;
    }

    state.avatarRerenderScheduled = true;
    requestAnimationFrame(() => {
        state.avatarRerenderScheduled = false;
        renderLayers(map);
    });
}

function getLeadershipTier(playerStats) {
    const badges = Array.isArray(playerStats?.badges) ? playerStats.badges : [];
    const hasCountryPresidentBadge = badges.some(b => String(b?.code || "").toLowerCase() === "country_president");
    const hasRegionPresidentBadge = badges.some(b => String(b?.code || "").toLowerCase() === "region_president");

    if (playerStats?.isCountryPresident || hasCountryPresidentBadge) {
        return "country";
    }
    if (playerStats?.isCountryCouncilMember) {
        return "council";
    }
    if (playerStats?.isRegionPresident || hasRegionPresidentBadge) {
        return "region";
    }
    return "default";
}

function getAvatarRingColorByTier(tier) {
    if (tier === "country") {
        return [245, 197, 66, 246];
    }
    if (tier === "council") {
        return [192, 198, 210, 246];
    }
    if (tier === "region") {
        return [205, 214, 224, 244];
    }
    return [90, 136, 255, 244];
}

function drawAvatarFrame(context, center, avatarRadius, tier) {
    const baseOuterRadius = avatarRadius + 8;

    context.save();
    context.beginPath();
    context.arc(center, center, baseOuterRadius, 0, Math.PI * 2);
    context.lineWidth = UNIFIED_AVATAR_FRAME_WIDTH;
    context.strokeStyle = "rgba(90,136,255,0.98)";
    context.stroke();
    context.restore();

    context.beginPath();
    context.arc(center, center, avatarRadius - 3, 0, Math.PI * 2);
    context.lineWidth = UNIFIED_AVATAR_FRAME_WIDTH;
    context.strokeStyle = "rgba(228,236,255,0.92)";
    context.stroke();

    if (tier === "region" || tier === "country") {
        const isCountry = tier === "country";
        const accentOuterRadius = avatarRadius + 18;

        context.save();
        context.beginPath();
        context.arc(center, center, accentOuterRadius, 0, Math.PI * 2);
        context.lineWidth = UNIFIED_AVATAR_FRAME_WIDTH;
        context.strokeStyle = isCountry
            ? "rgba(245,197,66,0.98)"
            : "rgba(204,214,224,0.98)";
        context.stroke();
        context.restore();
    }
}

function loadCircularAvatarTexture(avatarUrl, map, leadershipTier = "default") {
    const resolvedAvatar = toAbsoluteAvatarUrl(avatarUrl);
    const cacheKey = `avatar::${resolvedAvatar}::${leadershipTier}`;
    if (playerAvatarIconCache.has(cacheKey) || pendingAvatarLoads.has(cacheKey)) {
        return;
    }

    pendingAvatarLoads.add(cacheKey);

    const img = new Image();
    const isExternal = /^https?:\/\//i.test(resolvedAvatar) && !resolvedAvatar.startsWith(window.location.origin);
    if (isExternal) {
        img.crossOrigin = "anonymous";
    }
    img.onload = () => {
        try {
            const size = 512;
            const canvas = document.createElement("canvas");
            canvas.width = size;
            canvas.height = size;
            const context = canvas.getContext("2d");
            if (!context) {
                throw new Error("Canvas context unavailable");
            }

            context.clearRect(0, 0, size, size);
            const center = size / 2;
            const avatarRadius = 214;

            // Cover-fit with centered crop.
            const sourceWidth = img.naturalWidth || img.width || size;
            const sourceHeight = img.naturalHeight || img.height || size;
            const targetAspect = 1;
            const sourceAspect = sourceWidth / sourceHeight;

            let cropWidth = sourceWidth;
            let cropHeight = sourceHeight;
            let cropX = 0;
            let cropY = 0;

            if (sourceAspect > targetAspect) {
                cropWidth = sourceHeight * targetAspect;
                cropX = (sourceWidth - cropWidth) / 2;
            } else {
                cropHeight = sourceWidth / targetAspect;
                cropY = (sourceHeight - cropHeight) / 2;
            }

            context.save();
            context.beginPath();
            context.arc(center, center, avatarRadius, 0, Math.PI * 2);
            context.closePath();
            context.clip();
            context.drawImage(
                img,
                cropX, cropY, cropWidth, cropHeight,
                center - avatarRadius, center - avatarRadius, avatarRadius * 2, avatarRadius * 2
            );
            context.restore();

            drawAvatarFrame(context, center, avatarRadius, leadershipTier);

            playerAvatarIconCache.set(cacheKey, {
                url: canvas.toDataURL("image/png"),
                width: size,
                height: size,
                anchorX: size / 2,
                anchorY: size / 2,
                mask: false
            });
        } catch {
            playerAvatarIconCache.set(cacheKey, {
                url: resolvedAvatar,
                width: 512,
                height: 512,
                anchorX: 256,
                anchorY: 256,
                mask: false
            });
        } finally {
            pendingAvatarLoads.delete(cacheKey);
            scheduleMapRerender(map);
        }
    };

    img.onerror = () => {
        playerAvatarIconCache.set(cacheKey, {
            ...getFallbackAvatarIcon()
        });
        pendingAvatarLoads.delete(cacheKey);
        scheduleMapRerender(map);
    };

    img.src = resolvedAvatar;
}

function getPlayerAvatarIcon(avatarUrl, map, leadershipTier = "default") {
    const resolvedAvatar = toAbsoluteAvatarUrl(avatarUrl);

    return {
        url: resolvedAvatar,
        width: 64,
        height: 64,
        anchorX: 32,
        anchorY: 32,
        mask: false
    };
}

function getCountryCode(iso) {
    return String(iso || "").split("-")[0].toUpperCase();
}

function darkenColor(color, amount = 0.18) {
    const ratio = Math.max(0, Math.min(1, amount));
    return [
        clampChannel((color[0] ?? 0) * (1 - ratio)),
        clampChannel((color[1] ?? 0) * (1 - ratio)),
        clampChannel((color[2] ?? 0) * (1 - ratio)),
        color[3] ?? 255
    ];
}

function withAlpha(color, alpha) {
    return [color[0] ?? 0, color[1] ?? 0, color[2] ?? 0, clampChannel(alpha)];
}

function createStrategicGridLines(step = 20) {
    const lines = [];

    for (let lng = -180; lng <= 180; lng += step) {
        lines.push({
            id: `lng-${lng}`,
            source: [lng, -85],
            target: [lng, 85],
            strength: lng % 60 === 0 ? "major" : "minor"
        });
    }

    for (let lat = -80; lat <= 80; lat += step) {
        lines.push({
            id: `lat-${lat}`,
            source: [-180, lat],
            target: [180, lat],
            strength: lat % 40 === 0 ? "major" : "minor"
        });
    }

    return lines;
}

function isValidCountryCode(value) {
    return /^[A-Z]{2}$/.test(String(value || "").trim().toUpperCase());
}

function resolveCountryCode(iso, countryName = "") {
    const directCode = getCountryCode(iso);
    if (isValidCountryCode(directCode)) {
        return directCode;
    }

    const byName = normalizeCountryName(countryName);
    return isValidCountryCode(byName) ? byName : "";
}

function normalizeCountryName(value) {
    const normalized = String(value || "")
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, " ")
        .trim();

    return normalized;
}

function getCountryDisplayName(countryCode, state) {
    if (!countryCode) {
        return "";
    }

    const translatedCountryName = translateCountryName(state, countryCode, '');
    if (translatedCountryName) {
        return translatedCountryName;
    }

    try {
        const displayNames = new Intl.DisplayNames([state?.localization?.locale || "en"], { type: "region" });
        return displayNames.of(countryCode) || countryCode;
    } catch {
        return countryCode;
    }
}

function escapeHtml(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function formatNumber(value, maximumFractionDigits = 0, state) {
    const num = Number(value);
    if (!Number.isFinite(num)) {
        return "0";
    }
    return new Intl.NumberFormat(state?.localization?.locale || "en", { maximumFractionDigits }).format(num);
}

function formatDateTime(value, state) {
    if (!value) {
        return "-";
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return "-";
    }

    return new Intl.DateTimeFormat(state?.localization?.locale || "en", {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit"
    }).format(date);
}

function buildRegionTooltipHtml(regionStats, state) {
    const regionName = regionStats?.regionName || '-';
    const countryName = regionStats?.countryName || '-';
    const presidentName = regionStats?.presidentName || '-';
    const playerCount = regionStats?.playerCount ?? 0;
    const area = regionStats?.area ?? 0;
    const totalMoney = regionStats?.totalMoney ?? 0;
    const totalScore = regionStats?.totalScore ?? 0;
    const totalUnits = regionStats?.totalUnits ?? 0;
    const attackPower = regionStats?.totalAttackPower ?? 0;
    const defensePower = regionStats?.totalDefensePower ?? 0;
    const wars = Array.isArray(regionStats?.activeWars) ? regionStats.activeWars : [];

    const warsHtml = wars.length === 0
        ? `<div style="opacity:.8;">${escapeHtml(translateLabel(state, 'tabs.common.noActiveWars', 'No active wars'))}</div>`
        : wars.map(w => {
            const role = w.isAttacker
                ? translateLabel(state, 'layout.map.tooltip.warRole.attacker', 'Attacker')
                : translateLabel(state, 'layout.map.tooltip.warRole.defender', 'Defender');
            return `<div style="margin-top:4px;"><strong>${escapeHtml(role)}</strong>: ${escapeHtml(w.opponentName || '-')} <span style="opacity:.75;">(${escapeHtml(formatDateTime(w.startedAtUtc, state))})</span></div>`;
        }).join('');

    return `<div style="min-width:360px;max-width:460px;background:rgba(5,10,24,.96);color:#e2e8f0;border:1px solid rgba(148,163,184,.45);border-radius:12px;padding:14px 16px;box-shadow:0 18px 40px rgba(2,6,23,.45);">
        <div style="display:flex;align-items:center;gap:10px;margin-bottom:8px;">
            <div style="width:26px;height:26px;border-radius:999px;background:rgba(56,189,248,.22);display:flex;align-items:center;justify-content:center;color:#8ed8ff;font-weight:800;">i</div>
            <div style="font-size:18px;font-weight:800;line-height:1.1;">${escapeHtml(regionName)}</div>
        </div>
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:6px 14px;font-size:12px;margin-top:10px;">
            <div>${escapeHtml(translateLabel(state, 'modals.player.detail.country', 'Country'))}: <strong>${escapeHtml(countryName)}</strong></div>
            <div>${escapeHtml(translateLabel(state, 'tabs.country.president', 'President'))}: <strong>${escapeHtml(presidentName)}</strong></div>
            <div>${escapeHtml(translateLabel(state, 'tabs.region.players', 'Players'))}: <strong>${escapeHtml(formatNumber(playerCount, 0, state))}</strong></div>
            <div>${escapeHtml(translateLabel(state, 'tabs.region.area', 'Area'))}: <strong>${escapeHtml(formatNumber(area, 2, state))}</strong></div>
            <div>${escapeHtml(translateLabel(state, 'modals.player.detail.score', 'Score'))}: <strong>${escapeHtml(formatNumber(totalScore, 2, state))}</strong></div>
            <div>${escapeHtml(translateLabel(state, 'layout.map.tooltip.treasury', 'Treasury'))}: <strong>${escapeHtml(formatNumber(totalMoney, 2, state))}</strong></div>
            <div>${escapeHtml(translateLabel(state, 'tabs.military.units', 'Units'))}: <strong>${escapeHtml(formatNumber(totalUnits, 0, state))}</strong></div>
            <div>${escapeHtml(translateLabel(state, 'tabs.common.attack', 'Attack'))}/${escapeHtml(translateLabel(state, 'tabs.common.defend', 'Defense'))}: <strong>${escapeHtml(formatNumber(attackPower, 0, state))} / ${escapeHtml(formatNumber(defensePower, 0, state))}</strong></div>
        </div>
        <div style="margin-top:12px;padding-top:10px;border-top:1px solid rgba(148,163,184,.25);">
            <div style="font-size:11px;letter-spacing:.06em;text-transform:uppercase;opacity:.85;">${escapeHtml(translateLabel(state, 'layout.map.tooltip.activeWars', 'Active wars'))}</div>
            ${warsHtml}
        </div>
    </div>`;
}

function normalizeGuid(value) {
    return String(value || "").trim().toLowerCase();
}

function normalizeFlagCode(rawFlag) {
    const raw = String(rawFlag || "").trim();
    if (!raw || raw.includes("/") || raw.includes(":")) {
        return "";
    }

    const letters = raw.replace(/[^a-z]/gi, "").slice(0, 2).toLowerCase();
    return letters.length === 2 ? letters : "";
}

function buildFlagUrl(rawFlag, size = 40) {
    const code = normalizeFlagCode(rawFlag);
    return code ? `https://flagcdn.com/w${size}/${encodeURIComponent(code)}.png` : "";
}

function buildRoundFlagUrl(rawFlag, size = 64) {
    const code = normalizeFlagCode(rawFlag);
    return code ? `https://hatscripts.github.io/circle-flags/flags/${encodeURIComponent(code)}.svg` : "";
}

function buildRegionStyleByIso(regionStyles) {
    const map = new Map();
    for (const style of regionStyles || []) {
        const iso = String(style?.isoCode2 || style?.IsoCode2 || "").toLowerCase();
        if (!iso || map.has(iso)) {
            continue;
        }
        map.set(iso, style);
    }
    return map;
}

function buildCountryStyleById(countryStyles) {
    const map = new Map();
    for (const style of countryStyles || []) {
        const countryId = normalizeGuid(style?.countriesId || style?.CountriesId);
        if (!countryId) {
            continue;
        }
        map.set(countryId, style);
    }
    return map;
}

function buildCountryStyleByName(countryStyles) {
    const map = new Map();
    for (const style of countryStyles || []) {
        const nameKey = normalizeCountryName(style?.name || style?.Name);
        if (!nameKey || map.has(nameKey)) {
            continue;
        }
        map.set(nameKey, style);
    }
    return map;
}

function buildCountryMetadataById(countryStyles, state) {
    const countryStyleById = buildCountryStyleById(countryStyles);
    const metadataById = new Map();

    for (const countryStyle of countryStyles || []) {
        const countryId = normalizeGuid(countryStyle?.countriesId || countryStyle?.CountriesId);
        if (!countryId) {
            continue;
        }

        const isoCode = String(countryStyle?.isoCode2 || countryStyle?.IsoCode2 || "");
        const fallbackCode = getCountryCode(isoCode);
        const explicitFlag = normalizeFlagCode(countryStyle?.flag || countryStyle?.Flag);

        metadataById.set(countryId, {
            countryId,
            label: String(countryStyle?.name || countryStyle?.Name || ""),
            representativeArea: 0,
            fallbackCode,
            flagCode: explicitFlag || fallbackCode.toLowerCase(),
            countryPresidentId: normalizeGuid(countryStyle?.presidentPlayersId || countryStyle?.PresidentPlayersId),
            presidentFlagCode: ""
        });
    }

    for (const [countryId, meta] of metadataById.entries()) {
        if (!meta.label) {
            const countryStyle = countryStyleById.get(countryId);
            meta.label = String(countryStyle?.name || countryStyle?.Name || "");
        }
        if (meta.presidentFlagCode) {
            meta.flagCode = meta.presidentFlagCode;
        }
        if (!meta.label) {
            meta.label = getCountryDisplayName(meta.fallbackCode, state);
        } else {
            meta.label = translateCountryName(state, meta.fallbackCode, meta.label);
        }
        metadataById.set(countryId, meta);
    }

    return metadataById;
}

function buildCountryLabels(features, colorMode, countryStyles, state) {
    const countries = new Map();
    const countryStyleByIso = buildRegionStyleByIso(countryStyles);
    const countryStyleByName = buildCountryStyleByName(countryStyles);
    const metadataById = buildCountryMetadataById(countryStyles, state);

    for (const feature of features || []) {
        const normalized = normalizeFeature(feature);
        const iso = String(normalized?.properties?.iso_code_2 || "").toLowerCase();
        const featureName = String(normalized?.properties?.name || "");
        const featureNameKey = normalizeCountryName(featureName);
        const displayCode = resolveCountryCode(iso, featureName);
        const hasValidDisplayCode = isValidCountryCode(displayCode);
        if (!iso) {
            continue;
        }

        const directCountryStyle = countryStyleByIso.get(iso) || countryStyleByName.get(featureNameKey);
        const countryId = normalizeGuid(
            directCountryStyle?.countriesId
            || directCountryStyle?.CountriesId
        );
        const countryKey = countryId
            || (hasValidDisplayCode ? `iso:${displayCode.toLowerCase()}` : `name:${featureNameKey}`);

        const bounds = getFeatureBounds(feature);
        if (!bounds) {
            continue;
        }
        const primaryCentroid = getCentroids(feature, 1)[0] || [
            (bounds.minX + bounds.maxX) / 2,
            (bounds.minY + bounds.maxY) / 2
        ];

        const explicitArea = Number(normalized?.properties?.area || 0);
        const boundsArea = Math.max(1, (bounds.maxX - bounds.minX) * (bounds.maxY - bounds.minY));
        const area = explicitArea > 0 ? explicitArea : boundsArea * 1000;
        const existing = countries.get(countryKey);
        if (!existing) {
            const metadata = countryId ? metadataById.get(countryId) : null;
            countries.set(countryKey, {
                countryId,
                countryName: featureName,
                code: hasValidDisplayCode ? displayCode : "",
                label: metadata?.label || (hasValidDisplayCode
                    ? translateCountryName(state, displayCode, featureName || getCountryDisplayName(displayCode, state))
                    : featureName),
                bounds,
                position: primaryCentroid,
                area,
                flag: metadata?.flagCode || (hasValidDisplayCode ? displayCode.toLowerCase() : "")
            });
            continue;
        }

        if (area > existing.area) {
            existing.position = primaryCentroid;
        }
        existing.bounds = mergeBounds(existing.bounds, bounds);
        existing.area += area;
    }

    return Array.from(countries.values())
        .filter(country => country.area > 1500 || isValidCountryCode(country.code))
        .map(country => ({
            ...country,
            position: country.position || [
                (country.bounds.minX + country.bounds.maxX) / 2,
                (country.bounds.minY + country.bounds.maxY) / 2
            ]
        }))
        .sort((a, b) => b.area - a.area)
        .slice(0, colorMode === "countries" ? 260 : 220);
}

function buildRegionLabels(features) {
    const maxLabels = 260;
    return (features || [])
        .map(feature => {
            const normalized = normalizeFeature(feature);
            const centroid = getCentroids(feature, 1)[0];
            const area = Number(normalized?.properties?.area || 0);
            const name = normalized?.properties?.name || normalized?.properties?.adm_code_1 || "";
            const isoCode2 = normalized?.properties?.iso_code_2 || "";

            if (!centroid || !name || area < 1200) {
                return null;
            }

            const label = isoCode2 ? `${String(name).toUpperCase()} (${String(isoCode2).toUpperCase()})` : String(name).toUpperCase();

            return {
                label,
                position: centroid,
                area
            };
        })
        .filter(Boolean)
        .sort((a, b) => b.area - a.area)
        .slice(0, maxLabels);
}

function renderLayers(map) {
    const state = getMapState(map);
    const layers = [];
    const strategicGrid = createStrategicGridLines();

    if (state.baseLayer) {
        let url = "";
        switch (state.baseLayer) {
            case 'carto-dark':
                url = 'https://basemaps.cartocdn.com/dark_all/{z}/{x}/{y}.png';
                break;
            case 'esri-imagery':
                url = 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}';
                break;
        }

        if (url) {
            layers.push(new deck.TileLayer({
                id: 'base-tile-layer',
                data: url,
                minZoom: 0,
                maxZoom: 19,
                tileSize: 256,
                renderSubLayers: props => {
                    const {
                        bbox: { west, south, east, north }
                    } = props.tile;

                    return new deck.BitmapLayer(props, {
                        data: null,
                        image: props.data,
                        bounds: [west, south, east, north],
                        opacity: state.baseLayer === 'esri-imagery' ? 0.52 : 0.74,
                        desaturate: state.baseLayer === 'esri-imagery' ? 0.35 : 0.5
                    });
                }
            }));
        }
    }

    layers.push(new deck.LineLayer({
        id: 'strategic-grid-layer',
        data: strategicGrid,
        pickable: false,
        getSourcePosition: d => d.source,
        getTargetPosition: d => d.target,
        getColor: d => d.strength === "major"
            ? (state.baseLayer ? [112, 176, 230, 36] : [112, 176, 230, 54])
            : (state.baseLayer ? [90, 128, 168, 18] : [90, 128, 168, 34]),
        getWidth: d => d.strength === "major" ? 1.2 : 0.7,
        widthUnits: 'pixels'
    }));

    if (state.countriesData) {
        const countryStyleById = buildCountryStyleById(state.countryStyles || []);
        const countryStyleByIso = buildRegionStyleByIso(state.countryStyles || []);
        const countryStyleByName = buildCountryStyleByName(state.countryStyles || []);
        const findCountryStyleByCountryId = (countryId) => countryStyleById.get(normalizeGuid(countryId)) || null;

        const getStyleColor = (normalizedFeature) => {
            const iso = String(normalizedFeature?.properties?.iso_code_2 || "");
            const countryCode = (iso || "").split("-")[0].toLowerCase();
            const countryNameKey = normalizeCountryName(normalizedFeature?.properties?.name || "");
            const style = countryStyleByIso.get(countryCode) || countryStyleByName.get(countryNameKey);
            const countryId = normalizeGuid(style?.countriesId || style?.CountriesId || style?.countryId || style?.CountryId);
            const colorStr = style?.colorHex || style?.ColorHex || '#CCCCCC';
            const ownWarCountryIds = state.ownWarCountryIds || [];
            const enemyWarCountryIds = state.enemyWarCountryIds || [];

            const isOwnWarCountry = countryId && ownWarCountryIds.includes(countryId);
            const isEnemyWarCountry = countryId && enemyWarCountryIds.includes(countryId);
            if (isOwnWarCountry) {
                return state.baseLayer ? [44, 140, 255, 150] : [48, 162, 255, 238];
            }
            if (isEnemyWarCountry) {
                return state.baseLayer ? [210, 92, 92, 132] : [224, 88, 88, 222];
            }

            const baseColor = parseHexColor(colorStr);
            const atlasKey = countryId || countryCode;
            const atlasColor = atlasPalette(atlasKey, state.baseLayer ? 102 : 255);
            const strategicTone = state.baseLayer ? [49, 92, 131, 255] : [72, 128, 162, 255];
            const blended = blendColors(blendColors(baseColor, atlasColor, 0.68), strategicTone, 0.18);
            blended[3] = state.baseLayer ? 118 : 236;
            return blended;
        };

        const getShadowFillColor = (normalizedFeature) => {
            const base = getStyleColor(normalizedFeature);
            const shadow = darkenColor(base, state.baseLayer ? 0.54 : 0.34);
            shadow[3] = state.baseLayer ? 54 : 66;
            return shadow;
        };

        const getOutlineColor = (normalizedFeature) => {
            const iso = String(normalizedFeature?.properties?.iso_code_2 || "");
            const countryCode = (iso || "").split("-")[0].toLowerCase();
            const countryNameKey = normalizeCountryName(normalizedFeature?.properties?.name || "");
            const style = countryStyleByIso.get(countryCode) || countryStyleByName.get(countryNameKey);
            const countryId = normalizeGuid(style?.countriesId || style?.CountriesId || style?.countryId || style?.CountryId);
            const ownWarCountryIds = state.ownWarCountryIds || [];
            const enemyWarCountryIds = state.enemyWarCountryIds || [];

            if (countryId && ownWarCountryIds.includes(countryId)) {
                return state.baseLayer ? [100, 200, 255, 190] : [120, 216, 255, 255];
            }

            if (countryId && enemyWarCountryIds.includes(countryId)) {
                return state.baseLayer ? [255, 130, 130, 182] : [255, 148, 148, 255];
            }

            return state.baseLayer ? [131, 171, 198, 148] : [157, 201, 223, 208];
        };

        const getWarFillColor = (normalizedFeature) => {
            const iso = String(normalizedFeature?.properties?.iso_code_2 || "");
            const countryCode = (iso || "").split("-")[0].toLowerCase();
            const countryNameKey = normalizeCountryName(normalizedFeature?.properties?.name || "");
            const style = countryStyleByIso.get(countryCode) || countryStyleByName.get(countryNameKey);
            const countryId = normalizeGuid(style?.countriesId || style?.CountriesId || style?.countryId || style?.CountryId);
            const ownWarCountryIds = state.ownWarCountryIds || [];
            const enemyWarCountryIds = state.enemyWarCountryIds || [];

            if (countryId && ownWarCountryIds.includes(countryId)) {
                return [82, 192, 255, 62];
            }

            if (countryId && enemyWarCountryIds.includes(countryId)) {
                return [255, 88, 88, 76];
            }

            return [0, 0, 0, 0];
        };

        layers.push(new deck.GeoJsonLayer({
            id: 'countries-shadow-layer',
            data: state.countriesData,
            filled: true,
            stroked: false,
            pickable: false,
            getFillColor: f => getShadowFillColor(normalizeFeature(f))
        }));

        // Countries Layer (Fill)
        layers.push(new deck.GeoJsonLayer({
            id: 'countries-layer',
            data: state.countriesData,
            filled: true,
            getFillColor: f => {
                const norm = normalizeFeature(f);
                return getStyleColor(norm);
            },
            getLineColor: f => getOutlineColor(normalizeFeature(f)),
            getLineWidth: state.zoom <= 3.5 ? 0.75 : state.zoom <= 5.5 ? 0.95 : 1.2,
            lineWidthUnits: 'pixels',
            pickable: true,
            onClick: info => {
                if (state.dotnetRefs.countries && info.object && info.coordinate) {
                    const normalized = normalizeFeature(info.object);
                    state.dotnetRefs.countries.invokeMethodAsync("HandleCountryClick", normalized, {
                        lng: info.coordinate[0],
                        lat: info.coordinate[1]
                    });
                    return true;
                }
                return false;
            },
            updateTriggers: {
                getFillColor: [state.countryStyles, state.colorMode, state.visibility.countries, state.baseLayer, state.ownWarCountryIds, state.enemyWarCountryIds],
                getLineColor: [state.visibility.countries, state.baseLayer, state.zoom, state.ownWarCountryIds, state.enemyWarCountryIds]
            }
        }));

        layers.push(new deck.GeoJsonLayer({
            id: 'countries-outline-soft',
            data: state.countriesData,
            filled: false,
            stroked: true,
            getLineColor: state.baseLayer ? [201, 234, 255, 74] : [210, 242, 255, 112],
            getLineWidth: state.zoom <= 4.5 ? 1.15 : 1.55,
            lineWidthUnits: 'pixels',
            pickable: false
        }));

        if ((state.ownWarCountryIds?.length || 0) > 0 || (state.enemyWarCountryIds?.length || 0) > 0) {
            layers.push(new deck.GeoJsonLayer({
                id: 'countries-war-fill',
                data: state.countriesData,
                filled: true,
                stroked: false,
                pickable: false,
                getFillColor: f => getWarFillColor(normalizeFeature(f)),
                updateTriggers: {
                    getFillColor: [state.ownWarCountryIds, state.enemyWarCountryIds]
                }
            }));

            layers.push(new deck.GeoJsonLayer({
                id: 'countries-war-outline',
                data: state.countriesData,
                filled: false,
                stroked: true,
                pickable: false,
                getLineColor: f => {
                    const color = getOutlineColor(normalizeFeature(f));
                    return withAlpha(color, state.baseLayer ? 220 : 255);
                },
                getLineWidth: state.zoom <= 4.5 ? 2.9 : 3.6,
                lineWidthUnits: 'pixels',
                updateTriggers: {
                    getLineColor: [state.ownWarCountryIds, state.enemyWarCountryIds, state.baseLayer],
                    getLineWidth: [state.zoom]
                }
            }));
        }

        const countryLabels = buildCountryLabels(
            state.countriesData.features,
            state.colorMode,
            state.countryStyles || [],
            state
        );
        layers.push(new deck.IconLayer({
            id: 'country-flags',
            data: countryLabels.filter(d => buildRoundFlagUrl(d.flag || 'un', 64)),
            getIcon: d => ({
                url: buildRoundFlagUrl(d.flag || 'un', 64),
                width: 64,
                height: 64,
                anchorY: 32,
                anchorX: 32
            }),
            getPosition: d => d.position,
            getSize: d => Math.max(20, Math.min(34, 14 + Math.log10((d.area || 1) + 1) * 4)),
            sizeUnits: 'pixels',
            sizeMinPixels: 18,
            sizeMaxPixels: 22,
            pickable: true,
            onClick: info => {
                if (state.dotnetRefs.countries && info.object && info.coordinate) {
                    state.dotnetRefs.countries.invokeMethodAsync("HandleCountryClick", {
                        id: String(info.object.countryId || info.object.code || ''),
                        properties: {
                            area: info.object.area || 0,
                            adm_code_1: String(info.object.code || ''),
                            iso_code_2: String(info.object.code || ''),
                            name: String(info.object.countryName || info.object.label || '')
                        }
                    }, {
                        lng: info.coordinate[0],
                        lat: info.coordinate[1]
                    });
                    return true;
                }

                return false;
            }
        }));

        if (state.zoom >= 5.5) {
            layers.push(new deck.TextLayer({
                id: 'country-labels-shadow',
                data: countryLabels,
                getPosition: d => d.position,
                getText: d => String(d.label).toUpperCase(),
                getColor: () => state.baseLayer ? [5, 11, 18, 190] : [7, 14, 23, 178],
                getSize: d => Math.max(12, Math.min(24, 12 + Math.log10((d.area || 1) + 1) * 3.5)),
                sizeUnits: 'pixels',
                sizeMinPixels: 10,
                sizeMaxPixels: 26,
                getTextAnchor: 'middle',
                getAlignmentBaseline: 'center',
                fontFamily: 'Cinzel, serif',
                characterSet: 'auto',
                billboard: true,
                background: false,
                pickable: false,
                getPixelOffset: [0, 1]
            }));

            layers.push(new deck.TextLayer({
                id: 'country-labels',
                data: countryLabels,
                getPosition: d => d.position,
                getText: d => String(d.label).toUpperCase(),
                getColor: () => state.baseLayer ? [218, 238, 251, 190] : [187, 221, 242, 188],
                getSize: d => Math.max(12, Math.min(24, 12 + Math.log10((d.area || 1) + 1) * 3.5)),
                sizeUnits: 'pixels',
                sizeMinPixels: 10,
                sizeMaxPixels: 26,
                getTextAnchor: 'middle',
                getAlignmentBaseline: 'center',
                fontFamily: 'Cinzel, serif',
                characterSet: 'auto',
                billboard: true,
                background: false,
                pickable: false
            }));
        }

    }

    if (state.visibility.points && state.pointsData && state.zoom > 3.7) {
        const playerPoints = state.pointsData.features.filter(f => {
            const code = String(f?.properties?.code || "player").toLowerCase();
            return code === "player";
        });
        const highlightedPoints = playerPoints.filter(f => {
            const playerId = String(f?.properties?.id || '').toLowerCase();
            const playerStats = playerId ? state.playerTooltipByPlayerId[playerId] : null;
            return getLeadershipTier(playerStats) !== "default";
        });

        // Individual points (player avatars / non-player structures)
        layers.push(new deck.IconLayer({
            id: 'points-layer',
            data: state.pointsData.features,
            getIcon: f => {
                const playerAvatar = f.properties?.playerAvatar;
                const code = f.properties?.code || 'player';
                if (playerAvatar || String(code).toLowerCase() === 'player') {
                    const playerId = String(f?.properties?.id || '').toLowerCase();
                    const playerStats = playerId ? state.playerTooltipByPlayerId[playerId] : null;
                    const leadershipTier = getLeadershipTier(playerStats);
                    return getPlayerAvatarIcon(playerAvatar, map, leadershipTier);
                }
                return {
                    url: `/assets/icons/buildings/${code.toLowerCase()}.png`,
                    width: 128,
                    height: 128,
                    anchorY: 128,
                    mask: false
                };
            },
            getPosition: f => f.geometry.coordinates,
            getSize: PLAYER_ICON_SIZE_METERS,
            sizeUnits: 'meters',
            sizeMinPixels: PLAYER_ICON_MIN_PIXELS,
            sizeMaxPixels: PLAYER_ICON_MAX_PIXELS,
            pickable: true,
            onClick: (info) => {
                if (state.dotnetRefs.points && info.object) {
                    const featureCode = String(info.object?.properties?.code || '').toLowerCase();
                    const playerId = String(info.object?.properties?.id || '').toLowerCase();
                    const playerStats = playerId ? state.playerTooltipByPlayerId[playerId] : null;
                    const isMine = Boolean(info.object?.properties?.isMine);
                    if (featureCode === 'player' && playerStats?.premiumShieldEnabled === true && !isMine) {
                        return true;
                    }

                    const normalized = normalizeFeature(info.object);
                    state.dotnetRefs.points.invokeMethodAsync("HandlePointClick", normalized);
                    return true; // Stop propagation
                }
            }
        }));

        if (playerPoints.length > 0) {
            layers.push(new deck.ScatterplotLayer({
                id: 'points-avatar-ring-layer',
                data: playerPoints,
                getPosition: f => f.geometry.coordinates,
                getRadius: PLAYER_RING_RADIUS_METERS,
                radiusUnits: 'meters',
                radiusMinPixels: PLAYER_RING_MIN_PIXELS,
                radiusMaxPixels: PLAYER_RING_MAX_PIXELS,
                filled: false,
                stroked: true,
                getLineColor: f => {
                    const playerId = String(f?.properties?.id || '').toLowerCase();
                    const playerStats = playerId ? state.playerTooltipByPlayerId[playerId] : null;
                    if (playerStats?.premiumShieldEnabled === true) {
                        return [46, 204, 113, 255];
                    }
                    const leadershipTier = getLeadershipTier(playerStats);
                    return getAvatarRingColorByTier(leadershipTier);
                },
                lineWidthUnits: 'pixels',
                getLineWidth: UNIFIED_AVATAR_RING_LAYER_WIDTH,
                pickable: false
            }));
        }

        if (highlightedPoints.length > 0) {
            layers.push(new deck.ScatterplotLayer({
                id: 'points-highlight-ring-layer',
                data: highlightedPoints,
                getPosition: f => f.geometry.coordinates,
                getRadius: PLAYER_RING_RADIUS_METERS + 40,
                radiusUnits: 'meters',
                radiusMinPixels: PLAYER_RING_MIN_PIXELS + 3,
                radiusMaxPixels: PLAYER_RING_MAX_PIXELS + 3,
                filled: false,
                stroked: true,
                getLineColor: f => {
                    const playerId = String(f?.properties?.id || '').toLowerCase();
                    const playerStats = playerId ? state.playerTooltipByPlayerId[playerId] : null;
                    const leadershipTier = getLeadershipTier(playerStats);
                    return leadershipTier === "country"
                        ? [247, 197, 58, 255]
                        : leadershipTier === "council"
                            ? [192, 198, 210, 255]
                            : [218, 226, 236, 255];
                },
                lineWidthUnits: 'pixels',
                getLineWidth: UNIFIED_AVATAR_RING_LAYER_WIDTH,
                pickable: false
            }));
        }
    }

    if (state.visibility.transports && state.transportsData) {
        const getValue = (item, camel, pascal) => {
            if (item == null) return null;
            if (item[camel] !== undefined && item[camel] !== null) return item[camel];
            if (item[pascal] !== undefined && item[pascal] !== null) return item[pascal];
            return null;
        };

        const toNumber = (value, fallback = 0) => {
            const number = Number(value);
            return Number.isFinite(number) ? number : fallback;
        };

        const withAlpha = (color, alpha) => [color[0], color[1], color[2], alpha];

        const getTransportTheme = (item) => {
            const movementType = (getValue(item, 'movementType', 'MovementType') || '').toString().toLowerCase();
            const missionType = (getValue(item, 'missionType', 'MissionType') || '').toString().toLowerCase();

            if (movementType === 'military') {
                if (missionType === 'aid') {
                    return {
                        iconUrl: 'https://api.iconify.design/solar:shield-check-bold.svg?color=%232ecc71',
                        color: [16, 185, 129, 255],
                        accent: [110, 231, 183, 255],
                        label: translateLabel(state, 'layout.map.tooltip.mapMarker.aid', 'AID')
                    };
                }

                if (missionType === 'siege') {
                    return {
                        iconUrl: getValue(item, 'strongestUnitIconUrl', 'StrongestUnitIconUrl') || 'https://api.iconify.design/mdi:castle.svg?color=%23e74c3c',
                        color: [220, 38, 38, 255],
                        accent: [251, 146, 60, 255],
                        label: translateLabel(state, 'layout.map.tooltip.mapMarker.siege', 'SIEGE')
                    };
                }

                return {
                    iconUrl: getValue(item, 'strongestUnitIconUrl', 'StrongestUnitIconUrl') || 'https://api.iconify.design/solar:shield-cross-bold.svg?color=%23e74c3c',
                    color: [220, 38, 38, 255],
                    accent: [248, 113, 113, 255],
                    label: translateLabel(state, 'layout.map.tooltip.mapMarker.attack', 'ATTACK')
                };
            }

            return {
                iconUrl: '/assets/icons/general/truck.png',
                color: [0, 102, 255, 255],
                accent: [56, 189, 248, 255],
                label: translateLabel(state, 'layout.map.tooltip.mapMarker.trade', 'TRADE')
            };
        };

        const visualizedTransports = state.transportsData
            .map((item, index) => {
                const fromX = toNumber(getValue(item, 'fromX', 'FromX'), NaN);
                const fromY = toNumber(getValue(item, 'fromY', 'FromY'), NaN);
                const toX = toNumber(getValue(item, 'toX', 'ToX'), NaN);
                const toY = toNumber(getValue(item, 'toY', 'ToY'), NaN);
                const currentX = toNumber(getValue(item, 'currentX', 'CurrentX'), NaN);
                const currentY = toNumber(getValue(item, 'currentY', 'CurrentY'), NaN);

                if (![fromX, fromY, toX, toY, currentX, currentY].every(Number.isFinite)) {
                    return null;
                }

                const theme = getTransportTheme(item);
                const routeDx = toX - fromX;
                const routeDy = toY - fromY;
                const routeLength = Math.hypot(routeDx, routeDy);
                const id = getValue(item, 'transportId', 'TransportId')
                    || getValue(item, 'movementId', 'MovementId')
                    || getValue(item, 'id', 'Id')
                    || `${theme.label}-${index}`;

                return {
                    ...item,
                    id,
                    source: [fromX, fromY],
                    target: [toX, toY],
                    current: [currentX, currentY],
                    theme,
                    routeLength
                };
            })
            .filter(item => item !== null);

        if (state.hoveredTransportId) {
            const hoveredTransport = visualizedTransports.find(item => item.id === state.hoveredTransportId);
            if (hoveredTransport) {
                layers.push(new deck.LineLayer({
                    id: 'transports-routes-glow',
                    data: [hoveredTransport],
                    getSourcePosition: d => d.source,
                    getTargetPosition: d => d.target,
                    getColor: d => withAlpha(d.theme.accent, 90),
                    getWidth: 2,
                    widthUnits: 'pixels',
                    widthMinPixels: 2,
                    widthMaxPixels: 2,
                    pickable: false
                }));

                layers.push(new deck.LineLayer({
                    id: 'transports-routes-core',
                    data: [hoveredTransport],
                    getSourcePosition: d => d.source,
                    getTargetPosition: d => d.target,
                    getColor: d => withAlpha(d.theme.color, 230),
                    getWidth: 2.8,
                    widthUnits: 'pixels',
                    widthMinPixels: 2,
                    widthMaxPixels: 8,
                    pickable: false
                }));

                layers.push(new deck.LineLayer({
                    id: 'transports-hover-line',
                    data: [hoveredTransport],
                    getSourcePosition: d => d.source,
                    getTargetPosition: d => d.target,
                    getColor: d => withAlpha(d.theme.accent, 255),
                    getWidth: 2,
                    widthUnits: 'pixels',
                    widthMinPixels: 3,
                    widthMaxPixels: 2,
                    pickable: false
                }));

                const routeEndArrows = [
                    {
                        id: `${hoveredTransport.id}-start`,
                        position: hoveredTransport.source,
                        theme: hoveredTransport.theme,
                        label: 'A'
                    },
                    {
                        id: `${hoveredTransport.id}-end`,
                        position: hoveredTransport.target,
                        theme: hoveredTransport.theme,
                        label: 'B'
                    }
                ];

                //layers.push(new deck.ScatterplotLayer({
                //    id: 'transports-route-end-dots-glow',
                //    data: routeEndArrows,
                //    getPosition: d => d.position,
                //    getRadius: 6200,
                //    radiusUnits: 'meters',
                //    radiusMinPixels: 16,
                //    radiusMaxPixels: 32,
                //    getFillColor: d => withAlpha(d.theme.color, 95),
                //    stroked: false,
                //    filled: true,
                //    pickable: false
                //}));

                layers.push(new deck.ScatterplotLayer({
                    id: 'transports-route-end-dots',
                    data: routeEndArrows,
                    getPosition: d => d.position,
                    getRadius: 1800,
                    radiusUnits: 'meters',
                    radiusMinPixels: 5,
                    radiusMaxPixels: 11,
                    getFillColor: d => withAlpha(d.theme.color, 255),
                    stroked: true,
                    getLineColor: d => withAlpha(d.theme.accent, 245),
                    lineWidthUnits: 'pixels',
                    getLineWidth: 2,
                    filled: true,
                    pickable: false
                }));

                //layers.push(new deck.TextLayer({
                //    id: 'transports-route-end-labels',
                //    data: routeEndArrows,
                //    getPosition: d => d.position,
                //    getText: d => d.label,
                //    getSize: 18,
                //    sizeUnits: 'pixels',
                //    sizeMinPixels: 14,
                //    sizeMaxPixels: 24,
                //    getColor: () => [255, 255, 255, 255],
                //    getTextAnchor: 'middle',
                //    getAlignmentBaseline: 'center',
                //    getPixelOffset: [0, -18],
                //    pickable: false
                //}));
            }
        }

        //layers.push(new deck.ScatterplotLayer({
        //    id: 'transports-glow-outer',
        //    data: visualizedTransports,
        //    getPosition: d => d.current,
        //    getRadius: 3500,
        //    radiusUnits: 'meters',
        //    radiusMinPixels: 8,
        //    getFillColor: d => withAlpha(d.theme.accent, 55),
        //    stroked: false,
        //    filled: true,
        //    pickable: true
        //}));

        //layers.push(new deck.ScatterplotLayer({
        //    id: 'transports-glow-inner',
        //    data: visualizedTransports,
        //    getPosition: d => d.current,
        //    getRadius: 700,
        //    radiusUnits: 'meters',
        //    radiusMinPixels: 2,
        //    radiusMaxPixels: 10,
        //    getFillColor: d => withAlpha(d.theme.accent, 210),
        //    stroked: false,
        //    filled: true,
        //    pickable: true
        //}));

        //layers.push(new deck.IconLayer({
        //    id: 'transports-vehicles-shadow',
        //    data: visualizedTransports,
        //    getIcon: d => ({
        //        url: d.theme.iconUrl,
        //        width: 64,
        //        height: 64,
        //        anchorY: 32,
        //        anchorX: 32,
        //        mask: true
        //    }),
        //    getPosition: d => d.current,
        //    getSize: 1175,
        //    sizeUnits: 'meters',
        //    sizeMinPixels: 8,
        //    sizeMaxPixels: 23,
        //    getColor: [0, 0, 0, 160],
        //    pickable: false
        //}));

        const aggressiveMilitaryTransports = visualizedTransports.filter(d =>
            String(d?.movementType || '').toLowerCase() === 'military'
            && ['attack', 'siege'].includes(String(d?.missionType || '').toLowerCase())
        );

        layers.push(new deck.PathLayer({
            id: 'transports-routes-ambient',
            data: visualizedTransports,
            getPath: d => [d.source, d.target],
            getColor: d => withAlpha(d.theme.color, String(d?.movementType || '').toLowerCase() === 'military' ? 78 : 42),
            getWidth: d => String(d?.movementType || '').toLowerCase() === 'military'
                ? 1.85
                : 0.8,
            widthUnits: 'pixels',
            rounded: true,
            pickable: false
        }));

        if (aggressiveMilitaryTransports.length > 0) {
            layers.push(new deck.PathLayer({
                id: 'transports-routes-war-highlight',
                data: aggressiveMilitaryTransports,
                getPath: d => [d.source, d.target],
                getColor: d => withAlpha(d.theme.accent, 184),
                getWidth: 3.2,
                widthUnits: 'pixels',
                rounded: true,
                pickable: false
            }));
        }

        layers.push(new deck.ScatterplotLayer({
            id: 'transports-vehicles-rings',
            data: aggressiveMilitaryTransports,
            getPosition: d => d.current,
            getRadius: 1120,
            radiusUnits: 'meters',
            radiusMinPixels: 7,
            radiusMaxPixels: 16,
            getFillColor: [0, 0, 0, 0],
            stroked: true,
            getLineColor: [255, 98, 72, 220],
            lineWidthUnits: 'pixels',
            getLineWidth: 2.2,
            filled: true,
            pickable: false
        }));

        layers.push(new deck.IconLayer({
            id: 'transports-vehicles',
            data: visualizedTransports,
            getIcon: d => ({
                url: d.theme.iconUrl,
                width: 64,
                height: 64,
                anchorY: 32,
                anchorX: 32
            }),
            getPosition: d => d.current,
            getSize: d => String(d?.movementType || '').toLowerCase() === 'military'
                ? 1120
                : 980,
            sizeUnits: 'meters',
            sizeMinPixels: 12,
            sizeMaxPixels: 24,
            pickable: true
        }));
    }

    if (state.visibility.sieges && state.siegesData) {
        const getValue = (item, camel, pascal) => {
            if (item == null) return null;
            if (item[camel] !== undefined && item[camel] !== null) return item[camel];
            if (item[pascal] !== undefined && item[pascal] !== null) return item[pascal];
            return null;
        };

        layers.push(new deck.ScatterplotLayer({
            id: 'sieges-layer',
            data: state.siegesData,
            getPosition: d => [parseFloat(getValue(d, 'x', 'X')), parseFloat(getValue(d, 'y', 'Y'))],
            getRadius: ACTIVE_SIEGE_RING_RADIUS_METERS + 680,
            radiusUnits: 'meters',
            radiusMinPixels: ACTIVE_SIEGE_RING_MIN_PIXELS,
            radiusMaxPixels: ACTIVE_SIEGE_RING_MAX_PIXELS + 6,
            getFillColor: [231, 76, 60, 62],
            getLineColor: [255, 112, 71, 234],
            lineWidthUnits: 'pixels',
            getLineWidth: 2.1,
            stroked: true,
            filled: true,
            pickable: false
        }));

        layers.push(new deck.ScatterplotLayer({
            id: 'sieges-layer-outer',
            data: state.siegesData,
            getPosition: d => [parseFloat(getValue(d, 'x', 'X')), parseFloat(getValue(d, 'y', 'Y'))],
            getRadius: ACTIVE_SIEGE_RING_RADIUS_METERS + 3300,
            radiusUnits: 'meters',
            radiusMinPixels: ACTIVE_SIEGE_RING_MIN_PIXELS + 6,
            radiusMaxPixels: ACTIVE_SIEGE_RING_MAX_PIXELS + 10,
            getFillColor: [0, 0, 0, 0],
            getLineColor: [255, 147, 95, 98],
            lineWidthUnits: 'pixels',
            getLineWidth: 1.1,
            stroked: true,
            filled: false,
            pickable: false
        }));

        layers.push(new deck.IconLayer({
            id: 'sieges-icon-layer',
            data: state.siegesData,
            getIcon: d => ({
                url: getValue(d, 'strongestUnitIconUrl', 'StrongestUnitIconUrl') || 'https://api.iconify.design/mdi:castle.svg?color=%23e74c3c',
                width: 64,
                height: 64,
                anchorY: 32,
                anchorX: 32
            }),
            getPosition: d => [parseFloat(getValue(d, 'x', 'X')), parseFloat(getValue(d, 'y', 'Y'))],
            getSize: ACTIVE_SIEGE_ICON_SIZE_METERS,
            sizeUnits: 'meters',
            sizeMinPixels: ACTIVE_SIEGE_ICON_MIN_PIXELS,
            sizeMaxPixels: ACTIVE_SIEGE_ICON_MAX_PIXELS,
            pickable: false
        }));
    }

    map.setProps({ layers });
    updateDebugZoomOverlay(map);
}

function parseHexColor(hex) {
    if (!hex) return [204, 204, 204, 255];
    let r = 0, g = 0, b = 0;
    if (hex.length === 4) {
        r = parseInt(hex[1] + hex[1], 16);
        g = parseInt(hex[2] + hex[2], 16);
        b = parseInt(hex[3] + hex[3], 16);
    } else if (hex.length === 7) {
        r = parseInt(hex.substring(1, 3), 16);
        g = parseInt(hex.substring(3, 5), 16);
        b = parseInt(hex.substring(5, 7), 16);
    }
    return [r, g, b, 255];
}

function getCentroid(feature) {
    try {
        const centroid = getCentroids(feature, 1)[0];
        return centroid || [0, 0];
    } catch (e) {
        console.warn("Centroid calculation failed", e);
        return [0, 0];
    }
}

function getPolygonBounds(coords) {
    if (!coords || !coords.length) return null;

    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const p of coords) {
        if (!Array.isArray(p) || p.length < 2) {
            continue;
        }
        if (p[0] < minX) minX = p[0];
        if (p[0] > maxX) maxX = p[0];
        if (p[1] < minY) minY = p[1];
        if (p[1] > maxY) maxY = p[1];
    }

    if (minX === Infinity) {
        return null;
    }

    return { minX, minY, maxX, maxY };
}

function getPolygonAreaEstimate(coords) {
    const bounds = getPolygonBounds(coords);
    if (!bounds) {
        return 0;
    }
    return Math.max(1, (bounds.maxX - bounds.minX) * (bounds.maxY - bounds.minY));
}

function getCentroids(feature, maxCount = 1) {
    try {
        if (feature.geometry.type === 'Point') {
            return [feature.geometry.coordinates];
        }

        if (feature.geometry.type === 'MultiPolygon') {
            return feature.geometry.coordinates
                .map(polygon => ({
                    centroid: getPolygonCentroid(polygon[0]),
                    area: getPolygonAreaEstimate(polygon[0])
                }))
                .filter(item => item.centroid)
                .sort((a, b) => b.area - a.area)
                .slice(0, maxCount)
                .map(item => item.centroid)
                .filter(c => c);
        }

        if (feature.geometry.type === 'Polygon') {
            const centroid = getPolygonCentroid(feature.geometry.coordinates[0]);
            return centroid ? [centroid] : [];
        }
    } catch (e) {
        console.warn("Centroid calculation failed", e);
    }

    return [];
}

function getFeatureBounds(feature) {
    try {
        const bounds = {
            minX: Infinity,
            minY: Infinity,
            maxX: -Infinity,
            maxY: -Infinity
        };

        const visit = (coords) => {
            if (!Array.isArray(coords)) {
                return;
            }

            if (coords.length >= 2 && typeof coords[0] === 'number' && typeof coords[1] === 'number') {
                bounds.minX = Math.min(bounds.minX, coords[0]);
                bounds.maxX = Math.max(bounds.maxX, coords[0]);
                bounds.minY = Math.min(bounds.minY, coords[1]);
                bounds.maxY = Math.max(bounds.maxY, coords[1]);
                return;
            }

            coords.forEach(visit);
        };

        visit(feature?.geometry?.coordinates);

        if (bounds.minX === Infinity || bounds.minY === Infinity) {
            return null;
        }

        return bounds;
    } catch (e) {
        console.warn("Feature bounds calculation failed", e);
        return null;
    }
}

function mergeBounds(left, right) {
    return {
        minX: Math.min(left.minX, right.minX),
        minY: Math.min(left.minY, right.minY),
        maxX: Math.max(left.maxX, right.maxX),
        maxY: Math.max(left.maxY, right.maxY)
    };
}

function getPolygonCentroid(coords) {
    const bounds = getPolygonBounds(coords);
    if (!bounds) return null;
    return [(bounds.minX + bounds.maxX) / 2, (bounds.minY + bounds.maxY) / 2];
}

export async function initializeMap(containerId, centerLng, centerLat, zoom, countryStyles, colorMode, locale, localization, debugMode = false) {
    if (!window.deck) {
        console.error('Deck.gl not loaded');
        return null;
    }

    const initialZoom = clampZoom(zoom);

    const deckInstance = new deck.DeckGL({
        container: containerId,
        initialViewState: {
            longitude: centerLng,
            latitude: centerLat,
            zoom: initialZoom,
            pitch: 0,
            bearing: 0
        },
        controller: {
            minZoom: MIN_MAP_ZOOM,
            maxZoom: MAX_MAP_ZOOM,
            dragRotate: false,
            touchRotate: false,
            keyboard: false,
            doubleClickZoom: true
        },
        onHover: info => {
            const state = getMapState(deckInstance);
            state.hoveredLayerId = info.layer ? info.layer.id : null;

            const hoveredTransportLayer = info.layer && (
                info.layer.id === 'transports-vehicles'
                || info.layer.id === 'transports-vehicles-shadow'
                || info.layer.id === 'transports-glow-inner'
                || info.layer.id === 'transports-glow-outer'
            );
            const hoveredTransportId = hoveredTransportLayer && info.object ? (info.object.id ?? null) : null;
            if (state.hoveredTransportId !== hoveredTransportId) {
                state.hoveredTransportId = hoveredTransportId;
                renderLayers(deckInstance);
            }
        },
        onViewStateChange: ({ viewState }) => {
            const state = getMapState(deckInstance);
            const oldZoom = state.zoom;
            const clampedViewState = {
                ...viewState,
                zoom: clampZoom(viewState.zoom),
                pitch: 0,
                bearing: 0
            };
            state.zoom = clampedViewState.zoom;
            state.viewState = clampedViewState;
            persistMapViewState(clampedViewState);

            const crossedThreshold = (oldZoom <= 5 && state.zoom > 5) || (oldZoom > 5 && state.zoom <= 5);
            if (crossedThreshold || Math.floor(oldZoom) !== Math.floor(state.zoom)) {
                renderLayers(deckInstance);
            }
            deckInstance.setProps({ viewState: clampedViewState });
        },
        getCursor: ({ isHovering }) => {
            const state = getMapState(deckInstance);
            if (!isHovering || !state.hoveredLayerId) return deckInstance._customCursor || 'default';

            // Show pointer only when hovering player avatars.
            const pointerLayers = ['points-layer', 'points-badge-layer', 'region-info-icons', 'country-flags'];
            if (pointerLayers.includes(state.hoveredLayerId)) {
                return deckInstance._customCursor || 'pointer';
            }

            return deckInstance._customCursor || 'default';
        },
        getTooltip: ({ object, layer }) => {
            if (!object || !layer) {
                return null;
            }

            const state = getMapState(deckInstance);

            if (layer.id === 'transports-vehicles') {
                return {
                    html: buildTransportTooltipHtml(object, state),
                    style: {
                        backgroundColor: 'transparent',
                        boxShadow: 'none',
                        padding: '0'
                    }
                };
            }

            if (layer.id === 'points-layer' || layer.id === 'points-badge-layer') {
                const featureCode = String(object?.properties?.code || '').toLowerCase();
                if (featureCode !== 'player') {
                    return null;
                }
                const playerId = String(object?.properties?.id || '').toLowerCase();
                const stats = playerId ? state.playerTooltipByPlayerId[playerId] : null;
                const playerName = stats?.playerName || '-';
                const isNpc = Boolean(stats?.isNpc);
                const countryName = stats?.countryName || '-';
                const money = stats?.money ?? 0;
                const oil = stats?.oil ?? 0;
                const uranium = stats?.uranium ?? 0;
                const chips = stats?.chips ?? 0;
                const avatarUrl = toAbsoluteAvatarUrl(stats?.playerAvatar || object?.properties?.playerAvatar);
                const countryFlagUrl = buildFlagUrl(stats?.countryFlag || stats?.countryIsoCode2 || object?.properties?.countryFlag || object?.properties?.iso_code_2 || '', 40);
                const countryFlagHtml = countryFlagUrl
                    ? `<img src="${escapeHtml(countryFlagUrl)}" alt="flag" style="width:20px;height:15px;border-radius:3px;object-fit:cover;box-shadow:0 0 0 1px rgba(148,163,184,.45);" />`
                    : '';
                const resourceItem = (icon, labelKey, fallbackLabel, value) => `<div style="display:flex;align-items:center;gap:8px;background:rgba(15,23,42,.72);border:1px solid rgba(148,163,184,.18);border-radius:10px;padding:8px 10px;">
                        <img src="${icon}" alt="${escapeHtml(fallbackLabel)}" style="width:18px;height:18px;object-fit:contain;flex:0 0 auto;" />
                        <div style="display:flex;flex-direction:column;gap:1px;min-width:0;">
                            <span style="font-size:10px;line-height:1;text-transform:uppercase;letter-spacing:.05em;opacity:.72;">${escapeHtml(translateLabel(state, labelKey, fallbackLabel))}</span>
                            <strong style="font-size:12px;line-height:1.15;">${escapeHtml(formatNumber(value, 2, state))}</strong>
                        </div>
                    </div>`;

                const html = `<div style="min-width:300px;max-width:360px;background:rgba(5,10,24,.96);color:#e2e8f0;border:1px solid rgba(148,163,184,.45);border-radius:10px;padding:10px 12px;box-shadow:0 14px 26px rgba(2,6,23,.42);">
                    <div style="display:flex;align-items:center;gap:10px;margin-bottom:8px;">
                        <img src="${escapeHtml(avatarUrl)}" alt="avatar" style="width:34px;height:34px;border-radius:999px;object-fit:cover;box-shadow:0 0 0 1px rgba(148,163,184,.5);" />
                        <div style="min-width:0;display:flex;flex-direction:column;gap:4px;">
                            <div style="display:flex;align-items:center;gap:6px;flex-wrap:wrap;">
                                ${countryFlagHtml}
                                <div style="font-size:15px;font-weight:800;line-height:1.1;">${escapeHtml(playerName)}</div>
                                ${isNpc ? `<span style="font-size:10px;background:rgba(56,189,248,.22);padding:2px 6px;border-radius:999px;">${escapeHtml(translateLabel(state, 'modals.player.npcBadge', 'NPC'))}</span>` : ''}
                            </div>
                            <div style="font-size:11px;opacity:.82;">${escapeHtml(countryName)}</div>
                        </div>
                    </div>
                    <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-top:8px;">
                        ${resourceItem('/assets/icons/resources/oil.png', 'layout.map.tooltip.oil', 'Oil', oil)}
                        ${resourceItem('/assets/icons/resources/uranium.png', 'layout.map.tooltip.uranium', 'Uranium', uranium)}
                        ${resourceItem('/assets/icons/resources/chips.png', 'layout.map.tooltip.chips', 'Chips', chips)}
                        ${resourceItem('/assets/icons/resources/money.png', 'layout.map.tooltip.cash', 'Cash', money)}
                    </div>
                </div>`;

                return {
                    html,
                    style: {
                        backgroundColor: 'transparent',
                        boxShadow: 'none',
                        padding: '0'
                    }
                };
            }

            if (layer.id === 'region-info-icons') {
                const state = getMapState(deckInstance);
                const regionId = String(object?.regionId || '').toLowerCase();
                const regionStats = regionId ? state.regionTooltipByRegionId[regionId] : null;
                if (!regionStats) {
                    return null;
                }

                return {
                    html: buildRegionTooltipHtml(regionStats, state),
                    style: {
                        backgroundColor: 'transparent',
                        boxShadow: 'none',
                        padding: '0'
                    }
                };
            }

            if (layer.id !== 'country-flags') {
                return null;
            }

            const countryId = String(object.countryId || '').toLowerCase();
            const countryNameKey = normalizeCountryName(object.countryName || object.label || '');
            const stats = (countryId ? state.countryTooltipByCountryId[countryId] : null)
                || (countryNameKey ? state.countryTooltipByCountryName?.[countryNameKey] : null)
                || null;
            const displayCountryName = translateCountryName(state, object.code, stats?.countryName || object.countryName || object.label || getCountryDisplayName(object.code, state));
            const presidentName = stats?.presidentName || '-';
            const regionCount = stats?.regionCount ?? 0;
            const playerCount = stats?.playerCount ?? 0;
            const totalMoney = stats?.totalMoney ?? 0;

            const wars = Array.isArray(stats?.activeWars) ? stats.activeWars : [];
            const warsHtml = wars.length === 0
                ? `<div style="opacity:.8;">${escapeHtml(translateLabel(state, 'tabs.common.noActiveWars', 'No active wars'))}</div>`
                : wars.map(w => {
                    const opponentFlagUrl = buildFlagUrl(w.opponentFlag || 'un', 40);
                    return `<div style="margin-top:4px;display:flex;align-items:center;gap:8px;">
                        <img src="${escapeHtml(opponentFlagUrl)}" alt="flag" style="width:20px;height:15px;border-radius:3px;object-fit:cover;box-shadow:0 0 0 1px rgba(148,163,184,.45);" />
                        <span>${escapeHtml(w.opponentName || '-')}</span>
                    </div>`;
                }).join('');


            const html = `<div style="min-width:420px;max-width:520px;background:rgba(5,10,24,.96);color:#e2e8f0;border:1px solid rgba(148,163,184,.45);border-radius:12px;padding:14px 16px;box-shadow:0 18px 40px rgba(2,6,23,.45);">
                <div style="display:flex;align-items:center;gap:10px;margin-bottom:8px;">
                    <img src="${escapeHtml(buildFlagUrl(object.flag || 'un', 40))}" alt="flag" style="width:34px;height:26px;border-radius:4px;box-shadow:0 0 0 1px rgba(148,163,184,.5);" />
                    <div style="font-size:18px;font-weight:800;line-height:1.1;">${escapeHtml(displayCountryName)}</div>
                </div>
                <div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:6px 14px;font-size:12px;margin-top:10px;">
                    <div>${escapeHtml(translateLabel(state, 'tabs.country.president', 'President'))}: <strong>${escapeHtml(presidentName)}</strong></div>
                    <div>${escapeHtml(translateLabel(state, 'tabs.region.players', 'Players'))}: <strong>${escapeHtml(formatNumber(playerCount, 0, state))}</strong></div>
                    <div>${escapeHtml(translateLabel(state, 'layout.map.tooltip.treasury', 'Treasury'))}: <strong>${escapeHtml(formatNumber(totalMoney, 2, state))}</strong></div>
                </div>
                <div style="margin-top:12px;padding-top:10px;border-top:1px solid rgba(148,163,184,.25);">
                    <div style="font-size:11px;letter-spacing:.06em;text-transform:uppercase;opacity:.85;">${escapeHtml(translateLabel(state, 'layout.map.tooltip.activeWars', 'Active wars'))}</div>
                    ${warsHtml}
                </div>
            </div>`;

            return {
                html,
                style: {
                    backgroundColor: 'transparent',
                    boxShadow: 'none',
                    padding: '0'
                }
            };
        }
    });

    const state = getMapState(deckInstance);
    applyLocalization(state, locale, localization);
    state.countryStyles = countryStyles || [];
    state.colorMode = colorMode || 'countries';
    state.debugMode = Boolean(debugMode);
    state.zoom = initialZoom;
    state.viewState = {
        longitude: centerLng,
        latitude: centerLat,
        zoom: initialZoom,
        pitch: 0,
        bearing: 0
    };

    attachKeyboardPan(deckInstance);
    updateDebugZoomOverlay(deckInstance);

    try {
        const response = await fetch("/assets/maps/ne_10m_admin_0_countries.json");
        state.countriesData = await response.json();
        renderLayers(deckInstance);
    } catch (e) {
        console.error("Error loading map GeoJSON:", e);
    }

    return deckInstance;
}

export function cleanup(map) {
    if (!map) return;
    if (typeof map.finalize === 'function') {
        map.finalize();
    }
}

export function setCountryColorMode(map, colorMode) {
    if (!map) return;
    const state = getMapState(map);
    state.colorMode = colorMode;
    localStorage.setItem('map_color_mode', colorMode);
    renderLayers(map);
}


export function setWarCountries(map, ownWarCountryIds, enemyWarCountryIds) {
    if (!map) return;
    const state = getMapState(map);
    state.ownWarCountryIds = (ownWarCountryIds || []).map(id => String(id || '').toLowerCase());
    state.enemyWarCountryIds = (enemyWarCountryIds || []).map(id => String(id || '').toLowerCase());
    renderLayers(map);
}

export function setCountryStyles(map, countryStyles) {
    if (!map) return;
    const state = getMapState(map);
    state.countryStyles = Array.isArray(countryStyles) ? countryStyles : [];
    renderLayers(map);
}

export function setCountryTooltipData(map, countryTooltipData) {
    if (!map) return;
    const state = getMapState(map);
    state.countryTooltipData = Array.isArray(countryTooltipData) ? countryTooltipData : [];
    state.countryTooltipByCountryId = {};
    state.countryTooltipByCountryName = {};

    for (const item of state.countryTooltipData) {
        const key = String(item?.countryId || '').toLowerCase();
        if (key) {
            state.countryTooltipByCountryId[key] = item;
        }
        const nameKey = normalizeCountryName(item?.countryName || '');
        if (nameKey) {
            state.countryTooltipByCountryName[nameKey] = item;
        }
    }

    renderLayers(map);
}

export function setPlayerTooltipData(map, playerTooltipData) {
    if (!map) return;
    const state = getMapState(map);
    state.playerTooltipData = Array.isArray(playerTooltipData) ? playerTooltipData : [];
    state.playerTooltipByPlayerId = {};

    for (const item of state.playerTooltipData) {
        const key = String(item?.playerId || '').toLowerCase();
        if (!key) continue;
        state.playerTooltipByPlayerId[key] = item;
    }

    renderLayers(map);
}

export function setMapLocalization(map, locale, localization) {
    if (!map) return;
    const state = getMapState(map);
    applyLocalization(state, locale, localization);
    renderLayers(map);
}

export function addPointsLayer(map, pointsData) {
    if (!map) return;
    const state = getMapState(map);
    state.pointsData = pointsData;
    renderLayers(map);
}

export function addConnectionsLayer(map, connectionsData) {
    if (!map) return;
    const state = getMapState(map);
    state.connectionsData = connectionsData;
    renderLayers(map);
}

export function addTransportsLayer(map, transportData) {
    if (!map) return;
    const state = getMapState(map);
    state.transportsData = transportData;
    renderLayers(map);
}

export function addSiegesLayer(map, siegeData) {
    if (!map) return;
    const state = getMapState(map);
    state.siegesData = siegeData;
    renderLayers(map);
}

export function addBaseLayer(map, type) {
    if (!map) return;
    const state = getMapState(map);
    state.baseLayer = type;
    localStorage.setItem('map_base_layer', type);
    renderLayers(map);
}

export function removeBaseLayer(map) {
    if (!map) return;
    const state = getMapState(map);
    state.baseLayer = 'none';
    localStorage.setItem('map_base_layer', 'none');
    renderLayers(map);
}

export function jumpTo(map, coords) {
    if (!map || !coords) return;
    const state = getMapState(map);
    const current = state.viewState ?? map.props?.viewState ?? {
        longitude: coords.lng,
        latitude: coords.lat,
        zoom: clampZoom(state.zoom || DEFAULT_MAP_ZOOM),
        pitch: 0,
        bearing: 0
    };

    const nextViewState = {
        ...current,
        longitude: coords.lng,
        latitude: coords.lat,
        zoom: clampZoom(coords.zoom ?? current.zoom)
    };

    state.viewState = nextViewState;
    map.setProps({ viewState: nextViewState });
    persistMapViewState(nextViewState);
}

function formatDurationCompact(totalSeconds) {
    const safeSeconds = Math.max(0, Math.round(Number(totalSeconds) || 0));
    const hours = Math.floor(safeSeconds / 3600);
    const minutes = Math.floor((safeSeconds % 3600) / 60);
    const seconds = safeSeconds % 60;

    if (hours > 0) {
        return `${hours}h ${String(minutes).padStart(2, '0')}m`;
    }
    if (minutes > 0) {
        return `${minutes}m ${String(seconds).padStart(2, '0')}s`;
    }
    return `${seconds}s`;
}

function formatUtcDateTime(value, state) {
    if (!value) {
        return '-';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return '-';
    }

    return date.toLocaleString(state?.localization?.locale || 'en', {
        day: '2-digit',
        month: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function formatMissionLabel(movementType, missionType, state) {
    const normalizedMovementType = String(movementType || '').toLowerCase();
    const normalizedMissionType = String(missionType || '').toLowerCase();

    if (normalizedMovementType !== 'military') {
        return translateLabel(state, 'layout.map.tooltip.mission.tradeTransport', 'Trade transport');
    }

    switch (normalizedMissionType) {
        case 'attack':
            return translateLabel(state, 'tabs.common.attack', 'Attack');
        case 'siege':
            return translateLabel(state, 'layout.map.tooltip.mission.siege', 'Siege');
        case 'aid':
            return translateLabel(state, 'layout.map.tooltip.mission.aid', 'Aid');
        case 'return':
            return translateLabel(state, 'layout.map.tooltip.mission.return', 'Return');
        default:
            return normalizedMissionType
                ? normalizedMissionType.toUpperCase()
                : translateLabel(state, 'layout.map.tooltip.mission.militaryTransport', 'Military transport');
    }
}

function buildTransportTooltipHtml(object, state) {
    const movementType = String(object?.movementType || '').toLowerCase();
    const missionLabel = formatMissionLabel(movementType, object?.missionType, state);
    const status = escapeHtml(translateTransportStatus(state, movementType, object?.status || 'in_progress'));
    const progressPercent = Math.min(100, Math.max(0, Number(object?.progressPercent) || 0));


    return `<div style="min-width:320px;max-width:390px;background:rgba(5,10,24,.96);color:#e2e8f0;border:1px solid rgba(148,163,184,.45);border-radius:10px;padding:12px 14px;box-shadow:0 14px 26px rgba(2,6,23,.42);">
        <div style="display:flex;align-items:flex-start;justify-content:space-between;gap:10px;margin-bottom:8px;">
            <div>
                <div style="font-size:15px;font-weight:800;line-height:1.1;">${escapeHtml(missionLabel)}</div>
                <div style="margin-top:4px;font-size:11px;opacity:.78;">${escapeHtml(translateLabel(state, 'tabs.common.status', 'Status'))}: <strong>${status}</strong></div>
            </div>
            <div style="padding:3px 8px;border-radius:999px;background:rgba(59,130,246,.16);font-size:11px;font-weight:700;">
                ${escapeHtml(formatNumber(progressPercent, 1, state))}%
            </div>
        </div>
        <div style="margin-top:10px;">
            <div style="height:8px;background:rgba(148,163,184,.18);border-radius:999px;overflow:hidden;">
                <div style="height:100%;width:${progressPercent.toFixed(1)}%;background:linear-gradient(90deg,#3b82f6,#22c55e);"></div>
            </div>
        </div>
    </div>`;
}

export function updateAnimatedTransports(map, transportData) {
    if (!map) return;
    const state = getMapState(map);
    state.transportsData = transportData;
    renderLayers(map);
}

export function updateAnimatedSieges(map, siegeData) {
    if (!map) return;
    const state = getMapState(map);
    state.siegesData = siegeData;
    renderLayers(map);
}

export function toggleLayerVisibility(map, layerId, visible) {
    if (!map) return;
    const internalId = layerIdMap[layerId];
    if (!internalId) return;

    const state = getMapState(map);
    state.visibility[internalId] = visible;
    renderLayers(map);
}

export function onPointClick(map, dotnetRef) {
    if (!map) return;
    getMapState(map).dotnetRefs.points = dotnetRef;
}

export function onCountryClick(map, dotnetRef) {
    if (!map) return;
    getMapState(map).dotnetRefs.countries = dotnetRef;
}

export function onMapClick(map, dotnetRef) {
    if (!map) return;
    // Deck.gl's onClick on the Deck instance
    map.setProps({
        onClick: (info) => {
            if (info.coordinate) {
                // If we clicked an object that already has a specific handler, don't trigger global map click logic for building
                // But we handle this in C# via IsBuildingMode check.

                let isoCode2 = null;
                let admCode1 = null;
                let name = null;

                if (info.object) {
                    // Check if it's a castle (has feature property) or a building
                    const feature = info.object.feature || info.object;
                    const normalized = normalizeFeature(feature);
                    isoCode2 = normalized.properties.iso_code_2;
                    admCode1 = normalized.properties.adm_code_1;
                    name = normalized.properties.name;
                }

                dotnetRef.invokeMethodAsync("HandleMapClick", {
                    lng: info.coordinate[0],
                    lat: info.coordinate[1]
                }, isoCode2, admCode1, name);
            }
        }
    });
}

export function setMapCursor(map, cursorStyle) {
    if (!map) return;
    const state = getMapState(map);
    state.cursorStyle = cursorStyle;
    map.setProps({
        _customCursor: cursorStyle
    });
}
