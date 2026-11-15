(() => {
    'use strict';
    const body = document.body;
    const email = document.getElementById('Email');
    const password = document.getElementById('Password');
    const toggle = document.querySelector('[data-password-toggle]');
    const dialog = document.getElementById('auth-options-dialog');
    const reduced = matchMedia('(prefers-reduced-motion: reduce)');
    let paused = false, lastTrigger;
    let signingIn = false, celebrating = false;

    try { paused = localStorage.getItem('zt.login.motion') === 'paused'; } catch { }
    // Login has no continuous motion left, so it has no pause button of its own. It still
    // honours the system setting and a pause chosen on the supplier registration page.
    function syncMotion() { body.classList.toggle('auth-motion-paused', paused || reduced.matches); }
    syncMotion();
    reduced.addEventListener('change', syncMotion);
    document.addEventListener('focusin', event => {
        body.dataset.authFocus = event.target === email ? 'email' : event.target === password || event.target.closest?.('.auth-password') ? 'password' : 'idle';
    });
    toggle?.addEventListener('click', () => {
        const visible = password.type === 'password';
        password.type = visible ? 'text' : 'password';
        toggle.setAttribute('aria-pressed', String(visible));
        toggle.setAttribute('aria-label', visible ? 'Hide password' : 'Show password');
    });
    let activeBiometric = null, suppressBiometricDialog = false;
    const bioStatus = document.getElementById('bio-status');
    const faceEmail = document.getElementById('faceEmail');
    function prepareBiometric(method, trigger) {
        activeBiometric = method; suppressBiometricDialog = false;
        if (trigger) lastTrigger = trigger;
        dialog.dataset.method = method;
        document.getElementById('auth-options-title').textContent = method === 'face' ? 'Face recognition' : 'Fingerprint sign-in';
        document.getElementById('auth-options-description').textContent = method === 'face'
            ? 'Use your camera to sign in as ' + email.value.trim() + '.'
            : 'Confirm on your device or phone. You can retry or use your password.';
        document.getElementById('btnPasskeyLogin').hidden = method !== 'fingerprint';
        document.querySelector('.auth-face-method').hidden = method !== 'face';
    }
    function startBiometric(method, trigger) {
        if (method === 'face' && (!email.value.trim() || !email.checkValidity())) {
            showErrors(['Enter your email address above before using face recognition.']);
            email.focus(); email.reportValidity(); return;
        }
        prepareBiometric(method,trigger);
        if (faceEmail) faceEmail.value = email.value.trim();
        // Fingerprint uses the system's own prompt. Face needs the camera preview.
        if (method === 'face' && !dialog.open) dialog.showModal();
        const button = document.getElementById(method === 'face' ? 'btnFaceLogin' : 'btnPasskeyLogin');
        if (!button.disabled) button.click();
    }
    document.querySelectorAll('[data-auth-biometric]').forEach(button => button.addEventListener('click', () => {
        startBiometric(button.dataset.authBiometric === 'btnFaceLogin' ? 'face' : 'fingerprint',button);
    }));
    document.querySelectorAll('[data-auth-options]').forEach(button => button.addEventListener('click', () => {
        // Compact layouts use this existing fallback to expose the two page controls.
        document.querySelector('.auth-methods')?.classList.add('auth-methods-expanded');
        button.hidden = true;
    }));
    document.querySelector('[data-auth-close]')?.addEventListener('click', () => dialog.close());
    document.querySelector('[data-auth-use-password]')?.addEventListener('click', () => {
        suppressBiometricDialog = true;
        document.getElementById('btnBioSkip')?.click();
        document.dispatchEvent(new Event('stms:biometric-skip'));
    });
    // Failure details are shown only when needed, never as another method picker.
    if (bioStatus) new MutationObserver(() => {
        if (activeBiometric && !suppressBiometricDialog && !celebrating &&
            bioStatus.classList.contains('alert-danger') && !dialog.open) dialog.showModal();
    }).observe(bioStatus,{attributes:true,childList:true,characterData:true,subtree:true});
    dialog?.addEventListener('click', event => {
        if (event.target !== dialog) return;
        const bounds = dialog.getBoundingClientRect();
        if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) dialog.close();
    });
    dialog?.addEventListener('close', () => {
        suppressBiometricDialog = true;
        document.getElementById('btnBioSkip')?.click();
        const video = document.getElementById('faceVideo');
        video?.srcObject?.getTracks().forEach(track => track.stop());
        document.getElementById('facePanel')?.classList.add('d-none');
        lastTrigger?.focus({ preventScroll: true });
    });
    // "Skip" in the biometric module: back to the password form, cursor where typing continues.
    document.addEventListener('stms:biometric-skip', () => {
        lastTrigger = email?.value.trim() ? password : email;
        if (dialog?.open) dialog.close(); else lastTrigger?.focus({ preventScroll: true });
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
        // Show success here, then open a clean workspace. Decoration has a hard
        // deadline; quiet/reduced-motion users skip it and navigate immediately.
        let deadline;
        try {
            if (dialog?.open) dialog.close();
            if (submit) { submit.disabled = true; submit.textContent = 'Signed in. Opening workspace...'; }
            await Promise.race([
                window.stmsPlayLoginCelebration?.(),
                new Promise(resolve => { deadline = setTimeout(resolve, 3500); })
            ]);
        } finally {
            clearTimeout(deadline);
            location.replace(target.href);
        }
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
                    await completeSignIn(target.href);
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
