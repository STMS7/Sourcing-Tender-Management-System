/* MapLibre GL JS 5.24.0 + OpenFreeMap/Mapterhorn. No API key or paid account. */
(() => {
    'use strict';
    const base = new URL('../', document.currentScript.src);
    const asset = path => new URL(path, base).href;
    document.querySelectorAll('.zfm').forEach(root => {
    const frame = root.querySelector('.zfm-frame');
    if (!frame || frame.dataset.initialized) return;
    frame.dataset.initialized = 'true';
    const canvas = frame.querySelector('.zfm-canvas');
    const fallback = frame.querySelector('.zfm-fallback');
    const status = root.querySelector('[role="status"]');
    const controls = frame.querySelector('.zfm-controls');
    const replay = frame.querySelector('.zfm-replay');
    const explore = frame.querySelector('.zfm-explore');
    const motion = matchMedia('(prefers-reduced-motion: reduce)');
    const city = [35.85, 32.5556]; // Representative city center, not company premises.
    const initial = { center: [35.9, 32.75], zoom: 5.8, pitch: 0, bearing: 0 };
    const destination = { center: city, zoom: 14, pitch: 55, bearing: -15 };
    let map, pin, started = false, ready = false, visible = false, played = false;
    let flying = false, active = false, markerTimer, timeout, terrainFailed = false;

    function revealPin() { if (pin) pin.style.visibility = 'visible'; }
    function alignToTerrain() {
        if (!map || !map.getTerrain()) return;
        const height = map.queryTerrainElevation(map.getCenter());
        if (Number.isFinite(height) && Math.abs(map.getCenterElevation() - height) > 1)
            map.jumpTo({ elevation: height });
    }
    function settle() {
        clearTimeout(markerTimer);
        flying = false;
        alignToTerrain();
        replay.textContent = 'Replay approach';
        replay.setAttribute('aria-label', 'Replay the aerial approach to Irbid');
        revealPin();
        status.textContent = terrainFailed ? 'Irbid · Street map' : 'Irbid · 3D terrain';
    }
    function flight() {
        if (!ready || motion.matches) return;
        map.stop();
        map.jumpTo(initial);
        pin.style.visibility = 'hidden';
        flying = true;
        status.textContent = 'Approaching Irbid…';
        replay.textContent = 'Skip approach';
        replay.setAttribute('aria-label', 'Skip the aerial approach');
        markerTimer = setTimeout(revealPin, 3400);
        map.flyTo({ ...destination, duration: 5200, essential: false });
    }
    function maybePlay() {
        if (!ready || !visible || played) return;
        played = true;
        if (motion.matches) { map.jumpTo(destination); settle(); }
        else flight();
    }
    function interaction(enabled) {
        active = enabled;
        ['dragPan', 'dragRotate', 'doubleClickZoom', 'keyboard', 'touchZoomRotate'].forEach(name => {
            if (map[name]) map[name][enabled ? 'enable' : 'disable']();
        });
        // Wheel zoom remains disabled so the footer never captures page scrolling.
        map.scrollZoom.disable();
        map.getCanvas().tabIndex = enabled ? 0 : -1;
        explore.textContent = enabled ? 'Done exploring' : 'Explore map';
        explore.setAttribute('aria-pressed', String(enabled));
    }
    function fallbackMap() {
        clearTimeout(timeout);
        clearTimeout(markerTimer);
        if (map) { map.remove(); map = null; }
        ready = false;
        controls.hidden = true;
        fallback.hidden = false;
        frame.dataset.state = 'fallback';
        status.textContent = 'Irbid · Street map';
    }
    function library() {
        if (window.maplibregl) return Promise.resolve();
        if (!window.ztMapLibrary) window.ztMapLibrary = new Promise((resolve, reject) => {
            const css = document.createElement('link');
            css.rel = 'stylesheet'; css.href = asset('lib/maplibre/maplibre-gl.css');
            document.head.append(css);
            const js = document.createElement('script');
            js.src = asset('lib/maplibre/maplibre-gl.js');
            js.onload = resolve; js.onerror = reject;
            document.head.append(js);
        });
        return window.ztMapLibrary;
    }
    async function load() {
        if (started) return;
        started = true;
        frame.dataset.state = 'loading';
        timeout = setTimeout(fallbackMap, 25000);
        try {
            await library();
            if (frame.dataset.state === 'fallback') return;
            map = new maplibregl.Map({
                container: canvas, style: asset('maps/irbid.json'), ...initial,
                maxPitch: 65, maxZoom: 16, minZoom: 4, attributionControl: true,
                interactive: true, scrollZoom: false, dragPan: false, dragRotate: false,
                doubleClickZoom: false, keyboard: false, touchZoomRotate: false,
                canvasContextAttributes: { preserveDrawingBuffer: true }
            });
            map.on('error', event => {
                if (event.sourceId === 'zaytouna-terrain') {
                    terrainFailed = true;
                    if (map && map.isStyleLoaded()) map.setTerrain(null);
                }
                // Keep the real 2D fallback until map load succeeds; late tile errors
                // must not destroy a map the visitor is already exploring.
            });
            map.once('load', () => {
                clearTimeout(timeout);
                ready = true;
                frame.dataset.state = 'ready';
                fallback.hidden = true;
                controls.hidden = false;
                pin = document.createElement('div');
                pin.className = 'zfm-pin';
                pin.innerHTML = '<span>Irbid, Jordan</span><svg viewBox="0 0 32 40" aria-hidden="true"><path d="M16 39C12 32 2 22 2 15a14 14 0 1 1 28 0c0 7-10 17-14 24Z" fill="#17402A" stroke="#E9C46A" stroke-width="2"/><circle cx="16" cy="15" r="5" fill="#FBF3DF"/></svg>';
                pin.style.visibility = 'hidden';
                new maplibregl.Marker({ element: pin, anchor: 'bottom', offset: [0, 0] }).setLngLat(city).addTo(map);
                interaction(false);
                replay.hidden = motion.matches;
                map.on('moveend', () => { if (flying) settle(); });
                map.on('idle', () => { if (!flying) alignToTerrain(); });
                map.on('movestart', e => { if (e.originalEvent && flying) { map.stop(); settle(); } });
                map.getCanvas().addEventListener('webglcontextlost', fallbackMap, { once: true });
                maybePlay();
            });
        } catch (_) { fallbackMap(); }
    }
    replay.addEventListener('click', () => {
        if (!map || !ready) return;
        if (flying) { map.stop(); map.jumpTo(destination); settle(); }
        else { interaction(false); flight(); }
    });
    explore.addEventListener('click', () => {
        if (!map || !ready) return;
        if (flying) { map.stop(); settle(); }
        interaction(!active);
    });
    canvas.addEventListener('keydown', e => {
        if (e.key === 'Escape' && map) { map.stop(); settle(); interaction(false); explore.focus(); }
    });
    motion.addEventListener('change', () => {
        if (!ready) return;
        replay.hidden = motion.matches;
        if (motion.matches) { map.stop(); map.jumpTo(destination); settle(); }
    });
    document.addEventListener('visibilitychange', () => {
        if (document.hidden && ready && flying) { map.stop(); map.jumpTo(destination); settle(); }
    });
    if ('IntersectionObserver' in window) {
        const preload = new IntersectionObserver(entries => {
            if (entries.some(e => e.isIntersecting)) { preload.disconnect(); load(); }
        }, { rootMargin: '250px' });
        preload.observe(frame);
        new IntersectionObserver(entries => {
            visible = entries[0].isIntersecting;
            if (visible) maybePlay();
            else if (ready && flying) { map.stop(); map.jumpTo(destination); settle(); }
        }, { threshold: 0.35 }).observe(frame);
    } else { visible = true; load(); }
    });
})();
