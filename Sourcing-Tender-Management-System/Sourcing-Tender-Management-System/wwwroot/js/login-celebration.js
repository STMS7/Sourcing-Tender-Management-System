// A one-use visual preference, never an authentication token or access grant.
(() => {
    'use strict';
    const key = 'zt.login.celebration';
    const quiet = () => matchMedia('(prefers-reduced-motion: reduce)').matches ||
        document.body.classList.contains('auth-motion-paused');
    window.stmsQueueLoginCelebration = () => {
        try {
            if (quiet() || localStorage.getItem('zt.login.motion') === 'paused') {
                sessionStorage.removeItem(key);
                return;
            }
            sessionStorage.setItem(key, String(Date.now()));
        } catch { /* A blocked storage API must never prevent sign-in. */ }
    };
    if (document.body.classList.contains('zt-auth')) return;
    let timestamp;
    try { timestamp = Number(sessionStorage.getItem(key)); sessionStorage.removeItem(key); }
    catch { return; }
    if (!timestamp || Date.now() - timestamp > 10000 || Date.now() < timestamp || quiet()) return;
    try { if (localStorage.getItem('zt.login.motion') === 'paused') return; } catch { }
    const rain = document.createElement('div');
    rain.className = 'zt-signin-rain';
    rain.setAttribute('aria-hidden', 'true');
    const count = innerWidth < 768 ? 12 : 22;
    for (let i = 0; i < count; i++) {
        const olive = document.createElement('span');
        olive.className = 'zt-signin-olive';
        olive.style.setProperty('--olive-x', `${(i + .5) / count * 100}vw`);
        olive.style.setProperty('--olive-drift', `${Math.sin(i * 2.2) * 22}px`);
        olive.style.setProperty('--olive-rotate', `${i * 47 % 160 - 80}deg`);
        olive.style.setProperty('--olive-delay', `${i % 5 * 40}ms`);
        olive.style.setProperty('--olive-size', `${11 + i % 4 * 2}px`);
        olive.style.setProperty('--olive-color', i % 3 ? '#4E6B2F' : '#17402A');
        rain.append(olive);
    }
    document.body.append(rain);
    setTimeout(() => rain.remove(), 2200);
})();
