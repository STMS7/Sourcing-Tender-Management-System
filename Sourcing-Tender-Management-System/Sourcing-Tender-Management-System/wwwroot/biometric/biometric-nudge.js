/*
 * Quick sign-in, the way banking apps do it.
 *
 * On every page for a signed-in person this script:
 *   1. remembers who is signed in on this device (name and what is registered), so the sign-in page can greet them
 *      with "Welcome back" and a single "Continue with fingerprint / face" button;
 *   2. shows a short tip, only if they have no fingerprint and no face registered yet and the device can do one:
 *        - device with fingerprint / face unlock (Windows Hello, Touch ID...): "Turn on" registers it right here, one click;
 *        - computer with only a camera: "Turn on" goes straight to face setup.
 * It never blocks anything. "Not now" hides the tip for two weeks. The password always keeps working.
 */
(function () {
    'use strict';

    var HIDE_KEY = 'stms.quickSignIn.hiddenUntil';
    var USER_KEY = 'stms.lastUser';
    var HIDE_DAYS = 14;

    if (typeof fetch !== 'function') { return; }

    // ---------- small helpers ----------

    function bytesToBase64Url(buffer) {
        var bytes = new Uint8Array(buffer), s = '';
        for (var i = 0; i < bytes.length; i++) { s += String.fromCharCode(bytes[i]); }
        return btoa(s).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    }

    function base64UrlToBytes(text) {
        var s = String(text).replace(/-/g, '+').replace(/_/g, '/');
        while (s.length % 4) { s += '='; }
        var raw = atob(s), bytes = new Uint8Array(raw.length);
        for (var i = 0; i < raw.length; i++) { bytes[i] = raw.charCodeAt(i); }
        return bytes.buffer;
    }

    function remember(user) {
        try {
            window.localStorage.setItem(USER_KEY, JSON.stringify({
                name: user.name || '',
                email: user.email || '',
                hasPasskey: !!user.hasPasskey,
                hasFace: !!user.hasFace
            }));
        } catch (e) { /* storage blocked: the welcome card just will not appear */ }
    }

    function hiddenLately() {
        try { return Number(window.localStorage.getItem(HIDE_KEY) || 0) > Date.now(); } catch (e) { return false; }
    }

    function deviceLabel() {
        var platform = (navigator.userAgentData && navigator.userAgentData.platform) || navigator.platform || 'device';
        return ('This ' + platform + ' (' + (/Edg\//.test(navigator.userAgent) ? 'Edge' : /Chrome\//.test(navigator.userAgent) ? 'Chrome' : /Firefox\//.test(navigator.userAgent) ? 'Firefox' : /Safari\//.test(navigator.userAgent) ? 'Safari' : 'browser') + ')').slice(0, 150);
    }

    function explain(error) {
        var name = error && error.name;
        if (name === 'NotAllowedError' || name === 'AbortError') {
            return 'It was cancelled or timed out. Press "Turn on" to try again and confirm with your fingerprint, face or PIN.';
        }
        if (name === 'InvalidStateError') { return 'This device is already registered.'; }
        if (name === 'SecurityError') { return 'Open the site from https://localhost:7288 and try again.'; }
        return (error && error.message) || 'Could not turn it on. You can try again from Profile > Biometrics.';
    }

    async function post(url, token, body) {
        var response = await fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify(body || {})
        });
        var data = null;
        try { data = await response.json(); } catch (e) { /* no body */ }
        if (!response.ok) { throw new Error((data && data.error) || 'The request failed (' + response.status + ').'); }
        return data;
    }

    // Register this device's fingerprint / face unlock with the same endpoints the Biometrics page uses.
    async function registerFingerprint(token) {
        var start = await post('/Biometric/PasskeyRegisterOptions', token);
        var options = start.options;
        options.challenge = base64UrlToBytes(options.challenge);
        options.user.id = base64UrlToBytes(options.user.id);
        (options.excludeCredentials || []).forEach(function (c) { c.id = base64UrlToBytes(c.id); });

        var credential = await navigator.credentials.create({ publicKey: options });
        return post('/Biometric/PasskeyRegister', token, {
            token: start.token,
            id: bytesToBase64Url(credential.rawId),
            clientDataJson: bytesToBase64Url(credential.response.clientDataJSON),
            attestationObject: bytesToBase64Url(credential.response.attestationObject),
            label: deviceLabel()
        });
    }

    // ---------- the tip ----------

    function showTip(kind, status) {
        if (document.getElementById('quickSignInTip')) { return; }

        var tip = document.createElement('div');
        tip.id = 'quickSignInTip';
        tip.className = 'alert alert-info d-flex flex-wrap align-items-center justify-content-between gap-2 mx-3 mt-3 mb-0';
        tip.setAttribute('role', 'status');

        var text = document.createElement('div');
        var title = document.createElement('strong');
        title.textContent = kind === 'fingerprint' ? 'Sign in with one tap next time. ' : 'Sign in with your face next time. ';
        text.appendChild(title);
        var message = document.createElement('span');
        message.textContent = kind === 'fingerprint'
            ? 'Turn on fingerprint sign-in on this device. One click, and your password keeps working.'
            : 'Turn on face sign-in. It uses your camera and takes about 10 seconds; your password keeps working.';
        text.appendChild(message);

        var buttons = document.createElement('div');
        buttons.className = 'd-flex gap-2';

        var turnOn;
        if (kind === 'fingerprint') {
            turnOn = document.createElement('button');
            turnOn.type = 'button';
            turnOn.addEventListener('click', async function () {
                turnOn.disabled = true;
                message.textContent = 'Confirm with your fingerprint, face or PIN when your device asks...';
                try {
                    await registerFingerprint(status.token);
                    remember({ name: status.name, email: status.email, hasPasskey: true, hasFace: status.hasFace });
                    tip.className = 'alert alert-success mx-3 mt-3 mb-0';
                    tip.textContent = 'Done. Next time, just tap "Continue with fingerprint" on the sign-in page.';
                    setTimeout(function () { tip.remove(); }, 8000);
                } catch (e) {
                    console.warn('Turning on fingerprint failed:', e);
                    message.textContent = explain(e);
                    turnOn.disabled = false;
                }
            });
        } else {
            turnOn = document.createElement('a');
            turnOn.href = '/Identity/Account/Manage/Biometrics?start=face';
        }
        turnOn.className = 'btn btn-sm btn-primary';
        turnOn.textContent = 'Turn on';

        var later = document.createElement('button');
        later.type = 'button';
        later.className = 'btn btn-sm btn-outline-secondary';
        later.textContent = 'Not now';
        later.addEventListener('click', function () {
            try { window.localStorage.setItem(HIDE_KEY, String(Date.now() + HIDE_DAYS * 24 * 60 * 60 * 1000)); } catch (e) { /* ignore */ }
            tip.remove();
        });

        buttons.appendChild(turnOn);
        buttons.appendChild(later);
        tip.appendChild(text);
        tip.appendChild(buttons);

        var host = document.querySelector('main') || document.body;
        host.insertBefore(tip, host.firstChild);
    }

    async function hasCamera() {
        try {
            if (!navigator.mediaDevices || !navigator.mediaDevices.enumerateDevices) { return false; }
            var devices = await navigator.mediaDevices.enumerateDevices();
            return devices.some(function (d) { return d.kind === 'videoinput'; });
        } catch (e) { return false; }
    }

    async function run() {
        var response = await fetch('/Biometric/Status', { credentials: 'same-origin', headers: { 'Accept': 'application/json' } });
        if (!response.ok) { return; }
        var status = await response.json();
        if (!status) { return; }

        remember(status);   // so the sign-in page can say "Welcome back"

        // The biometric pages already explain everything; nothing to offer to people who already have one.
        if (document.getElementById('bio-root') || status.hasPasskey || status.hasFace) { return; }
        if (hiddenLately() || !window.isSecureContext) { return; }

        var platform = false;
        if (window.PublicKeyCredential && typeof PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable === 'function') {
            try { platform = await PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable(); } catch (e) { platform = false; }
        }
        if (platform) { showTip('fingerprint', status); return; }
        if (await hasCamera()) { showTip('face', status); }
    }

    run().catch(function () { /* the tip is optional; stay silent */ });
})();
