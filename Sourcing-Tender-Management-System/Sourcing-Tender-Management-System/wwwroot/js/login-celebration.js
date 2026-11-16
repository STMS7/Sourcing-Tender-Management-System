// Pre-rendered macro film, Login only, after real server authentication.
(() => {
    'use strict';
    try { sessionStorage.removeItem('zt.login.celebration'); } catch { }
    const body = document.body, reduced = matchMedia('(prefers-reduced-motion: reduce)');
    const quiet = () => reduced.matches || body.classList.contains('auth-motion-paused');
    const movie = new URL('../login/cinema/olive-to-oil-v4.mp4',document.currentScript.src).href;
    const poster = new URL('../login/cinema/olive-still-v2.webp',document.currentScript.src).href;
    const preload = document.createElement('video');
    preload.preload = 'auto'; preload.muted = true; preload.playsInline = true;
    if (body.classList.contains('zt-auth') && !quiet()) { preload.src = movie; preload.load(); }
    let running = null; // one film per sign-in: a second call joins the first instead of starting another
    window.stmsPlayLoginCelebration = () => {
        if (running) return running;
        if (!body.classList.contains('zt-auth') || !document.querySelector('.auth-form') || quiet()) return Promise.resolve();
        return running = new Promise(resolve => {
            // On desktop the film plays inside the still render's own frame, so the picture simply
            // comes alive behind the words. Where that frame is hidden (mobile) it gets its own.
            const frame = document.querySelector('.auth-render');
            const slot = frame?.getBoundingClientRect();
            const desktop = !!slot && slot.width > 0 && slot.height > 0;
            const width = Math.min(innerWidth-40,440,(innerHeight-100)*1.5), height = width / 1.5;
            const left = (innerWidth-width)/2, top = Math.max(24,Math.min(innerHeight*.18,innerHeight-height-24));
            const overlay = document.createElement('div');
            overlay.className = 'zt-oil-success' + (desktop ? ' is-inline' : ' is-mobile');
            overlay.setAttribute('aria-hidden','true');
            overlay.dataset.phase = 'olive'; overlay.dataset.source = 'rendered-film';
            const video = document.createElement('video');
            video.className = 'zt-oil-film'; video.muted = true; video.playsInline = true; video.loop = false;
            video.preload = 'auto'; video.src = movie; video.poster = poster;
            video.setAttribute('playsinline',''); video.setAttribute('muted','');
            if (!desktop) video.style.cssText = `left:${left}px;top:${top}px;width:${width}px;height:${height}px;`;
            overlay.append(video); (desktop ? frame : body).append(overlay); body.classList.add('auth-cinema-playing');
            // Skip: a real button (the film itself is decorative and aria-hidden), plus Escape.
            const skip = document.createElement('button');
            skip.type = 'button'; skip.className = 'zt-oil-skip'; skip.textContent = 'Skip';
            skip.setAttribute('aria-label','Skip the sign-in animation');
            const onKey = e => { if (e.key === 'Escape') finish(); };
            skip.addEventListener('click',() => finish()); document.addEventListener('keydown',onKey); body.append(skip);
            let finished = false, timer;
            const finish = () => {
                if (finished) return;
                finished = true; clearTimeout(timer);
                observer.disconnect(); reduced.removeEventListener('change',onMotion);
                window.removeEventListener('resize',finish);
                running = null; video.pause(); overlay.remove(); skip.remove(); document.removeEventListener('keydown',onKey); body.classList.remove('auth-cinema-playing');
                resolve();
            };
            const onMotion = () => { if (quiet()) finish(); };
            const observer = new MutationObserver(onMotion);
            observer.observe(body,{attributes:true,attributeFilter:['class']});
            reduced.addEventListener('change',onMotion); window.addEventListener('resize',finish,{once:true});
            // The journey line changes once, on the cut from the olive to the press.
            const line = document.querySelector('.auth-story-label');
            let turned = false;
            video.addEventListener('timeupdate',() => {
                overlay.dataset.phase = video.currentTime < .3 ? 'olive' : video.currentTime < 1 ? 'press' : video.currentTime < 1.9 ? 'oil' : 'release';
                if (line && !turned && video.currentTime >= .3) {
                    turned = true;
                    line.classList.add('is-turning');
                    setTimeout(() => { line.textContent = line.dataset.after || line.textContent; line.classList.remove('is-turning'); },260);
                }
            });
            video.addEventListener('ended',finish,{once:true});
            video.addEventListener('error',finish,{once:true});
            timer = setTimeout(finish,3300); // Network/decoder/CSS events cannot trap sign-in.
            try { const play = video.play(); if (play?.catch) play.catch(finish); } catch { finish(); }
        });
    };
})();
