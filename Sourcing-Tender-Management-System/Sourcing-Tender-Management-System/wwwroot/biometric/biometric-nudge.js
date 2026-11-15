/*
 * "Sign in faster next time" tip, like the ones Google, GitHub and Microsoft show after a password sign-in.
 *
 * Shown only when all of these are true: the person is signed in, this device has a fingerprint / face / PIN
 * (Windows Hello, Touch ID...), the account has no fingerprint device yet, and the tip was not dismissed lately.
 * It never blocks anything: one button goes to the setup page, the other hides the tip for two weeks.
 */
(function () {
    'use strict';

    var KEY = 'stms.fingerprintTip.hiddenUntil';
    var HIDE_DAYS = 14;

    // The biometric pages already explain everything; do not repeat the tip there.
    if (document.getElementById('bio-root')) { return; }
    if (!window.PublicKeyCredential || !window.isSecureContext || typeof fetch !== 'function') { return; }

    try {
        var hiddenUntil = Number(window.localStorage.getItem(KEY) || 0);
        if (hiddenUntil > Date.now()) { return; }
    } catch (e) { /* storage blocked: show the tip anyway */ }

    function show() {
        if (document.getElementById('fingerprintTip')) { return; }

        var tip = document.createElement('div');
        tip.id = 'fingerprintTip';
        tip.className = 'alert alert-info d-flex flex-wrap align-items-center justify-content-between gap-2 mx-3 mt-3 mb-0';
        tip.setAttribute('role', 'status');

        var text = document.createElement('div');
        var title = document.createElement('strong');
        title.textContent = 'Sign in faster next time. ';
        text.appendChild(title);
        text.appendChild(document.createTextNode('Set up fingerprint sign-in on this device. It takes about 10 seconds and your password keeps working.'));

        var buttons = document.createElement('div');
        buttons.className = 'd-flex gap-2';

        var setup = document.createElement('a');
        setup.className = 'btn btn-sm btn-primary';
        setup.href = '/Identity/Account/Manage/Biometrics';
        setup.textContent = 'Set up now';

        var later = document.createElement('button');
        later.type = 'button';
        later.className = 'btn btn-sm btn-outline-secondary';
        later.textContent = 'Not now';
        later.addEventListener('click', function () {
            try { window.localStorage.setItem(KEY, String(Date.now() + HIDE_DAYS * 24 * 60 * 60 * 1000)); } catch (e) { /* ignore */ }
            tip.remove();
        });

        buttons.appendChild(setup);
        buttons.appendChild(later);
        tip.appendChild(text);
        tip.appendChild(buttons);

        var host = document.querySelector('main') || document.body;
        host.insertBefore(tip, host.firstChild);
    }

    fetch('/Biometric/Status', { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
        .then(function (response) { return response.ok ? response.json() : null; })
        .then(function (status) {
            if (!status || status.hasPasskey) { return null; }
            if (typeof PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable !== 'function') { return false; }
            return PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable();
        })
        .then(function (hasDeviceBiometrics) { if (hasDeviceBiometrics) { show(); } })
        .catch(function () { /* the tip is optional; stay silent */ });
})();
