(() => {
    'use strict';
    const body = document.body;
    const email = document.getElementById('Email');
    const password = document.getElementById('Password');
    const toggle = document.querySelector('[data-password-toggle]');
    const story = document.querySelector('.auth-story');
    const dialog = document.getElementById('auth-options-dialog');
    const reduced = matchMedia('(prefers-reduced-motion: reduce)');
    const motionControl = document.querySelector('[data-auth-motion]');
    let paused = false, frame = null, pointer = { x: 0, y: 0 }, lastTrigger;
    let growth = .04;
    const trees = [...document.querySelectorAll('[data-auth-tree]')];
    let signingIn = false, celebrating = false;
    try { paused = localStorage.getItem('zt.login.motion') === 'paused'; } catch { }
    function syncMotion() {
        const quiet = paused || reduced.matches;
        body.classList.toggle('auth-motion-paused', quiet);
        motionControl?.setAttribute('aria-pressed', String(quiet));
        motionControl?.setAttribute('aria-label', quiet ? 'Enable decorative motion' : 'Pause decorative motion');
        if (motionControl) motionControl.innerHTML = quiet ? 'Motion paused <span aria-hidden="true">▷</span>' : 'Pause motion <span aria-hidden="true">Ⅱ</span>';
    }
    syncMotion();
    motionControl?.addEventListener('click', () => {
        paused = !paused;
        try { localStorage.setItem('zt.login.motion', paused ? 'paused' : 'enabled'); } catch { }
        syncMotion();
    });
    reduced.addEventListener('change', syncMotion);
    function paint() {
        frame = null;
        if (!story || paused || reduced.matches || !story.getClientRects().length) return;
        const focus = body.dataset.authFocus;
        story.style.setProperty('--auth-photo-x', `${pointer.x * -5}px`);
        story.style.setProperty('--auth-photo-y', `${pointer.y * -4}px`);
        story.style.setProperty('--auth-branch-x', `${pointer.x * 17 + (focus === 'email' ? -12 : 0)}px`);
        story.style.setProperty('--auth-branch-y', `${pointer.y * 12}px`);
        story.style.setProperty('--auth-branch-tilt', `${pointer.x * 2 + (focus === 'email' ? -1 : 0)}deg`);
    }
    function schedule() { if (frame === null) frame = requestAnimationFrame(paint); }
    document.addEventListener('pointermove', event => {
        if (event.pointerType !== 'mouse' || paused || reduced.matches) return;
        pointer = { x: Math.max(-1, Math.min(1, event.clientX / innerWidth * 2 - 1)), y: Math.max(-1, Math.min(1, event.clientY / innerHeight * 2 - 1)) };
        schedule();
    }, { passive: true });
    document.documentElement.addEventListener('pointerleave', () => { pointer = { x: 0, y: 0 }; schedule(); });
    document.addEventListener('focusin', event => {
        body.dataset.authFocus = event.target === email ? 'email' : event.target === password || event.target.closest?.('.auth-password') ? 'password' : 'idle';
        if (event.isTrusted) grow(.008);
        schedule();
    });
    function renderGrowth() {
        body.style.setProperty('--auth-tree-growth', String(growth));
        story?.style.setProperty('--auth-growth', String(growth));
        trees.forEach(tree => {
            tree.querySelectorAll('[data-tree-stage]').forEach(group => {
                const amount = Math.min(1, Math.max(0, (growth - Number(group.dataset.treeStage)) / .18));
                group.style.setProperty('--tree-bloom', String(amount));
            });
            tree.querySelectorAll('[data-tree-limb]').forEach(limb => {
                const amount = Math.min(1, Math.max(0, (growth - Number(limb.dataset.treeLimb)) / .24));
                limb.style.setProperty('--limb-growth', String(amount));
            });
        });
    }
    function grow(amount) { if (!celebrating) { growth = Math.min(1, growth + amount); renderGrowth(); } }
    renderGrowth();
    document.addEventListener('input', event => {
        if (event.target.matches('input:not([type=hidden]),textarea')) grow(.022);
    });
    document.addEventListener('click', event => {
        if (event.isTrusted && event.target.closest('button,input,a,summary')) grow(.012);
    });
    toggle?.addEventListener('click', () => {
        const visible = password.type === 'password';
        password.type = visible ? 'text' : 'password';
        toggle.setAttribute('aria-pressed', String(visible));
        toggle.setAttribute('aria-label', visible ? 'Hide password' : 'Show password');
    });
    function openOptions(trigger) {
        if (!dialog) return;
        lastTrigger = trigger;
        if (!dialog.open) dialog.showModal();
    }
    document.querySelectorAll('[data-auth-biometric]').forEach(button => button.addEventListener('click', () => {
        openOptions(button);
        const method = document.getElementById(button.dataset.authBiometric);
        if (method && !method.disabled) method.click();
    }));
    document.querySelectorAll('[data-auth-options]').forEach(button => button.addEventListener('click', () => openOptions(button)));
    document.querySelector('[data-auth-close]')?.addEventListener('click', () => dialog.close());
    dialog?.addEventListener('click', event => {
        if (event.target !== dialog) return;
        const bounds = dialog.getBoundingClientRect();
        if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) dialog.close();
    });
    dialog?.addEventListener('close', () => {
        const video = document.getElementById('faceVideo');
        video?.srcObject?.getTracks().forEach(track => track.stop());
        document.getElementById('facePanel')?.classList.add('d-none');
        lastTrigger?.focus({ preventScroll: true });
    });
    // Existing biometric code can insert a remembered-user card. Keep that extra
    // content in the options dialog so it cannot move the password form.
    const area = document.querySelector('.auth-form-area');
    const moveWelcome = () => {
        const card = area?.querySelector('#welcomeBack');
        if (card && dialog) { card.classList.remove('col-md-5'); dialog.insertBefore(card, dialog.querySelector('#bio-root')); }
    };
    if (area) new MutationObserver(moveWelcome).observe(area, { childList: true });
    moveWelcome();
    const form = document.querySelector('.auth-form');
    const submit = form?.querySelector('.auth-submit');
    const defaultSubmit = submit?.innerHTML;
    const summary = form?.querySelector('.auth-validation');
    function showErrors(errors) {
        if (!summary) return;
        summary.replaceChildren();
        const list = document.createElement('ul');
        for (const message of errors) {
            const item = document.createElement('li');
            item.textContent = message;
            list.append(item);
        }
        summary.append(list);
        summary.classList.remove('validation-summary-valid');
        summary.classList.add('validation-summary-errors');
    }
    async function completeSignIn(destination) {
        if (typeof destination !== 'string' || !destination.trim()) throw new Error('Missing workspace destination.');
        const target = new URL(destination, location.origin);
        if (target.origin !== location.origin || !['http:', 'https:'].includes(target.protocol)) throw new Error('Invalid workspace destination.');
        if (celebrating) return;
        celebrating = true;
        // Decoration must never gate successful navigation. The destination page
        // consumes this one-use flag and plays the olives while it is usable.
        try {
            growth = 1;
            renderGrowth();
            window.stmsQueueLoginCelebration?.();
            if (submit) { submit.disabled = true; submit.textContent = 'Opening workspace…'; }
        } finally { location.replace(target.href); }
    }
    // Called only by the existing biometric handler after its server confirms success.
    window.stmsLoginSuccess = completeSignIn;
    form?.addEventListener('submit', async event => {
        if (!window.fetch || !window.FormData) return; // Original POST/redirect works without enhancement.
        event.preventDefault();
        if (signingIn || celebrating) return;
        if (!form.checkValidity()) { form.reportValidity(); return; }
        if (window.jQuery?.validator && !window.jQuery(form).valid()) return;
        signingIn = true;
        submit.style.height = `${submit.getBoundingClientRect().height}px`;
        submit.disabled = true;
        submit.textContent = 'Signing in…';
        form.setAttribute('aria-busy', 'true');
        const controller = new AbortController();
        const deadline = setTimeout(() => controller.abort(), 20000);
        try {
            const response = await fetch(form.action, {
                method: 'POST', body: new FormData(form), credentials: 'same-origin', signal: controller.signal,
                headers: { 'X-Login-Animation': '1', 'Accept': 'application/json' }
            });
            if (response.redirected) {
                const target = new URL(response.url);
                if (target.origin === location.origin && !/\/Auth\/Login\/?$/i.test(target.pathname)) {
                    celebrating = true;
                    location.replace(target.href);
                    return;
                }
            }
            if (!response.headers.get('content-type')?.includes('application/json')) {
                showErrors([response.status === 400 ? 'Your session may have expired. Refresh the page and try again.' : 'Could not confirm sign-in. Please try again.']);
                return;
            }
            const result = await response.json();
            if (!response.ok || result.succeeded !== true) {
                showErrors(Array.isArray(result.errors) && result.errors.length ? result.errors : ['Check your sign-in details and try again.']);
                return;
            }
            await completeSignIn(result.redirect);
        } catch (error) {
            celebrating = false;
            showErrors([error.name === 'AbortError' ? 'Sign-in took too long. Please try again.' : 'Could not complete sign-in. Check your connection and try again.']);
        } finally {
            clearTimeout(deadline);
            if (!celebrating) {
                signingIn = false;
                submit.disabled = false;
                submit.innerHTML = defaultSubmit;
                submit.style.height = '';
                form.removeAttribute('aria-busy');
            }
        }
    });
})();
