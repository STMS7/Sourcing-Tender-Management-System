// Biometric sign-in and enrolment (fingerprint through the device, and face recognition).
// Used only by the pages in Views/Biometric. Server side: Controllers/BiometricController.cs.
(function () {
    'use strict';

    // Face recognition runs in the browser; the camera image never leaves the device, only a
    // 128-number descriptor and a few measurements are sent.
    var FACE_CDN = 'https://cdn.jsdelivr.net/npm/@vladmandic/face-api@1.7.13';

    // ---------- Small helpers ----------

    function enc(buffer) {
        var bytes = new Uint8Array(buffer);
        var text = '';
        for (var i = 0; i < bytes.length; i++) { text += String.fromCharCode(bytes[i]); }
        return btoa(text).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    }

    function dec(text) {
        var s = text.replace(/-/g, '+').replace(/_/g, '/');
        while (s.length % 4) { s += '='; }
        var raw = atob(s);
        var bytes = new Uint8Array(raw.length);
        for (var i = 0; i < raw.length; i++) { bytes[i] = raw.charCodeAt(i); }
        return bytes.buffer;
    }

    function sleep(ms) { return new Promise(function (resolve) { setTimeout(resolve, ms); }); }
    // "Skip" on the sign-in page cancels the attempt in progress; the same error name the browser uses for a cancelled request.
    function stopIfSkipped(signal) {
        if (signal && signal.aborted) {
            var error = new Error('Skipped.');
            error.name = 'AbortError';
            throw error;
        }
    }
    function round3(n) { return Math.round(n * 1000) / 1000; }
    function $(id) { return document.getElementById(id); }

    function antiForgeryToken() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    async function post(url, body) {
        var response = await fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': antiForgeryToken() },
            body: JSON.stringify(body || {})
        });
        var data = null;
        try { data = await response.json(); } catch (e) { /* no body */ }
        if (!response.ok) {
            throw new Error((data && data.error) || 'The request failed (' + response.status + ').');
        }
        return data;
    }

    // The browser reports very different problems (cancelled, timed out, no Windows Hello set up,
    // unsupported options) with the same few error names, so the technical detail is kept in the
    // message and in the console to make the real cause visible.
    function friendly(error) {
        console.error('Biometric error:', error);
        var detail = '';   // the technical name stays in the console, not on the page
        if (error && (error.name === 'NotAllowedError' || error.name === 'AbortError')) {
            return new Error('The fingerprint step was cancelled or timed out. Press the button and try again. ' +
                'On the phone, tap Continue and confirm with its fingerprint. You can always sign in with your password.' + detail);
        }
        if (error && error.name === 'SecurityError') {
            return new Error('This web address cannot be used for fingerprint sign-in. Open the site from the same address you ' +
                'registered on (for example https://localhost:7288) and try again.' + detail);
        }
        if (error && error.name === 'InvalidStateError') {
            return new Error('This device is already registered.' + detail);
        }
        if (error && error.name === 'NotSupportedError') {
            return new Error('This device or browser does not support this kind of sign-in. Use your password, or try your phone with the QR code.' + detail);
        }
        return error instanceof Error ? error : new Error(String(error));
    }

    // ---------- What this device can do (used for clear messages instead of a vague failure) ----------

    async function capabilities() {
        var c = {
            secure: window.isSecureContext === true,
            webauthn: !!(window.PublicKeyCredential && navigator.credentials),
            platform: false,
            conditional: false,
            address: window.location.origin,
            browser: navigator.userAgent
        };
        if (c.webauthn) {
            try {
                if (typeof PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable === 'function') {
                    c.platform = await PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable();
                }
            } catch (e) { /* treated as not available */ }
            try {
                if (typeof PublicKeyCredential.isConditionalMediationAvailable === 'function') {
                    c.conditional = await PublicKeyCredential.isConditionalMediationAvailable();
                }
            } catch (e) { /* treated as not available */ }
        }
        return c;
    }

    // One plain sentence about the biggest problem, or null when everything needed is there.
    function problemOf(c) {
        if (!c.secure) {
            return 'Fingerprint needs a secure address. Open the site as https://localhost (not an IP address or plain http).';
        }
        if (!c.webauthn) {
            return 'This browser cannot use fingerprint sign-in. Use a current Chrome, Edge, Safari or Firefox.';
        }
        if (!c.platform) {
            return 'This computer has no fingerprint reader or Windows Hello set up. You can sign in with your face (camera), ' +
                'with your phone (choose "Use a phone or tablet" and scan the QR code), or add a fingerprint, face or PIN in ' +
                'Windows Settings > Accounts > Sign-in options. Your password always works.';
        }
        return null;
    }

    function diagnosticsText(c) {
        function yn(v) { return v ? 'yes' : 'no'; }
        return [
            'Secure address (https or localhost): ' + yn(c.secure),
            'Browser supports passkeys: ' + yn(c.webauthn),
            'Fingerprint / Windows Hello / PIN available: ' + yn(c.platform),
            'Sign-in suggestion in the email box: ' + yn(c.conditional),
            'Address: ' + c.address,
            'Browser: ' + c.browser
        ].join('\n');
    }

    // ---------- Fingerprint / device passkey (WebAuthn) ----------

    function ensureWebAuthn() {
        if (!window.PublicKeyCredential || !navigator.credentials) {
            throw new Error('This browser does not support fingerprint sign-in.');
        }
    }

    // opts.mediation = 'conditional' lets the browser offer the passkey inside the email box (autofill); opts.signal
    // cancels that waiting request. A cancelled waiting request throws the raw AbortError so the caller can tell.
    async function passkeyLogin(returnUrl, opts) {
        opts = opts || {};
        ensureWebAuthn();
        var start = await post('/Biometric/PasskeyLoginOptions');
        var options = start.options;
        options.challenge = dec(options.challenge);

        if (opts.hints) { options.hints = opts.hints; }   // for example ['hybrid'] = go straight to the phone QR code
        var request = { publicKey: options };
        if (opts.mediation) { request.mediation = opts.mediation; }
        if (opts.signal) { request.signal = opts.signal; }

        var credential;
        try {
            credential = await navigator.credentials.get(request);
        } catch (e) {
            if (opts.signal && e && e.name === 'AbortError') { throw e; }
            throw friendly(e);
        }

        var r = credential.response;
        return post('/Biometric/PasskeyLogin', {
            token: start.token,
            id: enc(credential.rawId),
            clientDataJson: enc(r.clientDataJSON),
            authenticatorData: enc(r.authenticatorData),
            signature: enc(r.signature),
            userHandle: r.userHandle ? enc(r.userHandle) : null,
            returnUrl: returnUrl
        });
    }

    // opts.phone = save the fingerprint on the person's phone: the browser shows a QR code to scan with the phone
    // camera, and the phone asks for its own fingerprint or face. Without it the browser offers this computer first.
    async function passkeyRegister(label, opts) {
        opts = opts || {};
        ensureWebAuthn();
        var start = await post('/Biometric/PasskeyRegisterOptions');
        var options = start.options;
        options.challenge = dec(options.challenge);
        options.user.id = dec(options.user.id);
        (options.excludeCredentials || []).forEach(function (c) { c.id = dec(c.id); });
        if (opts.phone) {
            options.authenticatorSelection = Object.assign({}, options.authenticatorSelection, { authenticatorAttachment: 'cross-platform' });
            options.hints = ['hybrid'];
        }
        if (opts.device) {
            options.authenticatorSelection = Object.assign({}, options.authenticatorSelection, { authenticatorAttachment: 'platform' });
            options.hints = ['client-device'];
        }

        var credential;
        try {
            credential = await navigator.credentials.create({ publicKey: options });
        } catch (e) {
            throw friendly(e);
        }

        return post('/Biometric/PasskeyRegister', {
            token: start.token,
            id: enc(credential.rawId),
            clientDataJson: enc(credential.response.clientDataJSON),
            attestationObject: enc(credential.response.attestationObject),
            label: label
        });
    }

    // ---------- Face recognition ----------

    function loadScript(src) {
        return new Promise(function (resolve, reject) {
            var script = document.createElement('script');
            script.src = src;
            script.onload = resolve;
            script.onerror = function () {
                reject(new Error('Could not load the face recognition library. Check your internet connection.'));
            };
            document.head.appendChild(script);
        });
    }

    async function loadFaceApi() {
        if (!window.faceapi) {
            await loadScript(FACE_CDN + '/dist/face-api.js');
        }
        var nets = window.faceapi.nets;
        if (!nets.tinyFaceDetector.isLoaded) { await nets.tinyFaceDetector.loadFromUri(FACE_CDN + '/model'); }
        if (!nets.faceLandmark68Net.isLoaded) { await nets.faceLandmark68Net.loadFromUri(FACE_CDN + '/model'); }
        if (!nets.faceRecognitionNet.isLoaded) { await nets.faceRecognitionNet.loadFromUri(FACE_CDN + '/model'); }
    }

    async function startCamera(video) {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            throw new Error('This browser cannot use the camera here. Open the site over https or on localhost.');
        }
        var stream;
        try {
            stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: 'user', width: { ideal: 640 }, height: { ideal: 480 } },
                audio: false
            });
        } catch (e) {
            throw new Error('The camera is not available. Allow camera access and try again.');
        }
        video.srcObject = stream;
        await video.play();

        // Wait until the camera is really delivering frames (some cameras need a moment after play()).
        for (var i = 0; i < 50 && (!video.videoWidth || video.readyState < 2); i++) { await sleep(60); }
        return stream;
    }

    function stopCamera(stream, video) {
        if (stream) { stream.getTracks().forEach(function (track) { track.stop(); }); }
        if (video) { video.srcObject = null; }
    }

    // The measurements come from the 68 face landmarks.
    function distance(a, b) { return Math.hypot(a.x - b.x, a.y - b.y); }

    function eyeRatio(p) {
        return (distance(p[1], p[5]) + distance(p[2], p[4])) / (2 * distance(p[0], p[3]));
    }

    function measure(landmarks) {
        var p = landmarks.positions;
        return {
            ear: (eyeRatio(p.slice(36, 42)) + eyeRatio(p.slice(42, 48))) / 2,
            yaw: (p[30].x - (p[0].x + p[16].x) / 2) / (p[16].x - p[0].x),
            mar: distance(p[62], p[66]) / distance(p[60], p[64])
        };
    }

    // The same step rules the server applies, so the page can tell the person when a step is done.
    function stepMatcher(action, t) {
        var states = {
            blink: [
                function (f) { return f.ear >= t.earOpen; },
                function (f) { return f.ear <= t.earClosed; },
                function (f) { return f.ear >= t.earOpen; }
            ],
            openMouth: [
                function (f) { return f.mar <= t.marClosed; },
                function (f) { return f.mar >= t.marOpen; }
            ],
            turnHead: [
                function (f) { return Math.abs(f.yaw) <= t.yawCenter; },
                function (f) { return Math.abs(f.yaw) >= t.yawTurn; }
            ]
        }[action];
        var span = { blink: 2500, openMouth: 4000, turnHead: 4000 }[action];
        var state = 0;
        var first = 0;

        return function (frame) {
            if (state > 0 && frame.t - first > span) { state = 0; }
            if (states[state](frame)) {
                if (state === 0) { first = frame.t; }
                state++;
                if (state === states.length) { return true; }
            }
            return false;
        };
    }

    var INSTRUCTIONS = {
        blink: 'Blink once, slowly',
        openMouth: 'Open your mouth wide, then close it',
        turnHead: 'Turn your head to one side, then look back at the camera'
    };

    // Faces are hard to find in dim, grainy or back-lit camera pictures, so the detector adapts:
    // it starts fast and strict, and if no face is found for a few seconds it switches to a more
    // sensitive setting and then to a more accurate (slower) detector. The detector only decides
    // where the face is; the face descriptor itself always comes from the same recognition network.
    var DETECTORS = [
        function (f) { return new f.TinyFaceDetectorOptions({ inputSize: 224, scoreThreshold: 0.5 }); },
        function (f) { return new f.TinyFaceDetectorOptions({ inputSize: 416, scoreThreshold: 0.3 }); },
        function (f) { return new f.SsdMobilenetv1Options({ minConfidence: 0.4 }); }
    ];

    async function ensureSsdModel() {
        var nets = window.faceapi.nets;
        if (!nets.ssdMobilenetv1.isLoaded) { await nets.ssdMobilenetv1.loadFromUri(FACE_CDN + '/model'); }
    }

    async function capture(challenge, video, setHint, signal) {
        var faceapi = window.faceapi;
        var thresholds = challenge.thresholds;
        var detectorLevel = 0;
        var detector = DETECTORS[0](faceapi);
        var missingSince = null;
        var ssdFailed = false;
        var cpuTried = false;
        var LEVEL_NAMES = ['fast', 'sensitive', 'SSD'];

        // The detector reads each frame from a plain 2D canvas instead of straight from the <video>
        // element. On some graphics drivers a video cannot be uploaded to WebGL correctly and the
        // detector then sees black frames, so it never finds a face even though the picture is fine.
        var frameCanvas = document.createElement('canvas');
        var frameContext = frameCanvas.getContext('2d', { willReadFrequently: true });
        function grab() {
            var sourceWidth = video.videoWidth || 640;
            var sourceHeight = video.videoHeight || 480;
            var w = Math.min(sourceWidth, 640);
            var h = Math.round(w * sourceHeight / sourceWidth);
            if (frameCanvas.width !== w || frameCanvas.height !== h) { frameCanvas.width = w; frameCanvas.height = h; }
            frameContext.drawImage(video, 0, 0, w, h);
            return frameCanvas;
        }

        // A short description of what the detector is running on, shown when no face is found so a
        // screenshot is enough to diagnose the problem.
        function diagnostics() {
            var backend = 'unknown';
            try { backend = faceapi.tf.getBackend(); } catch (e) { /* diagnostics only */ }
            return '[' + backend + ', ' + LEVEL_NAMES[detectorLevel] + ', camera ' + (video.videoWidth || 0) + 'x' + (video.videoHeight || 0) + ']';
        }

        console.info('Face detection', diagnostics());
        var frames = [];
        var descriptors = [];
        var step = 0;
        var matcher = stepMatcher(challenge.actions[0], thresholds);
        var started = performance.now();
        var deadline = challenge.expiresInSeconds * 1000 - 4000;
        var lastDescriptor = -1000;

        setHint(INSTRUCTIONS[challenge.actions[0]]);

        while (performance.now() - started < deadline) {
            stopIfSkipped(signal);
            var now = performance.now() - started;
            var detection = await faceapi.detectSingleFace(grab(), detector).withFaceLandmarks();
            if (!detection) {
                if (missingSince === null) { missingSince = now; }
                var missing = now - missingSince;

                if (detectorLevel === 0 && missing > 3000) {
                    detectorLevel = 1;
                    detector = DETECTORS[1](faceapi);
                    console.info('No face found for 3 s: using a more sensitive face detector.');
                } else if (detectorLevel === 1 && !ssdFailed && missing > 6000) {
                    try {
                        setHint('Still looking for your face...');
                        await ensureSsdModel();
                        detectorLevel = 2;
                        detector = DETECTORS[2](faceapi);
                        console.info('No face found for 6 s: using the more accurate SSD face detector.');
                    } catch (e) {
                        console.error('Could not load the SSD face detector:', e);
                        ssdFailed = true; // stay on the sensitive tiny detector
                    }
                } else if (missing > 10000 && !cpuTried) {
                    // Last resort: some graphics drivers give wrong results in WebGL, so compute on the CPU.
                    cpuTried = true;
                    try {
                        if (faceapi.tf.getBackend() !== 'cpu') {
                            setHint('Still looking for your face (switching to a compatible mode)...');
                            await faceapi.tf.setBackend('cpu');
                            await faceapi.tf.ready();
                            console.info('No face found for 10 s: switched the computation to the CPU.');
                        }
                    } catch (e) {
                        console.error('Could not switch to the CPU backend:', e);
                    }
                }

                setHint(missing > 6000
                    ? 'No face found. Face a light (not a window behind you), move a little back, and look straight at the camera. ' + diagnostics()
                    : 'No face found. Look straight at the camera.');
                await sleep(60);
                continue;
            }
            missingSince = null;

            var m = measure(detection.landmarks);
            var frame = { t: Math.round(now), ear: round3(m.ear), yaw: round3(m.yaw), mar: round3(m.mar) };
            frames.push(frame);

            // A face sample is taken whenever the face is neutral: looking ahead, eyes open, mouth closed.
            var neutral = m.ear >= thresholds.earOpen && Math.abs(m.yaw) <= thresholds.yawCenter && m.mar <= thresholds.marClosed;
            if (neutral && descriptors.length < 6 && now - lastDescriptor >= 500) {
                var full = await faceapi.detectSingleFace(grab(), detector).withFaceLandmarks().withFaceDescriptor();
                if (full) {
                    descriptors.push({ t: Math.round(now), v: Array.from(full.descriptor) });
                    lastDescriptor = now;
                }
            }

            if (step < challenge.actions.length && matcher(frame)) {
                step++;
                if (step < challenge.actions.length) {
                    matcher = stepMatcher(challenge.actions[step], thresholds);
                    setHint(INSTRUCTIONS[challenge.actions[step]]);
                } else {
                    setHint('Now look straight at the camera and hold still.');
                }
            }

            var span = descriptors.length ? descriptors[descriptors.length - 1].t - descriptors[0].t : 0;
            if (step >= challenge.actions.length && descriptors.length >= 4 && span >= 1600) {
                return { frames: frames, descriptors: descriptors };
            }

            await sleep(20);
        }

        throw new Error(frames.length === 0
            ? 'No face was detected by the camera. Check the lighting, try another browser, and send this to the developer: ' + diagnostics()
            : 'Time ran out before the steps were completed. Try again.');
    }

    async function runFace(options, challengeUrl, submitUrl, extra) {
        options.setStatus('Loading face recognition...');
        await loadFaceApi();
        stopIfSkipped(options.signal);
        var stream = await startCamera(options.video);
        try {
            stopIfSkipped(options.signal);
            options.setStatus('Follow the instructions under the camera.');
            var challenge = await post(challengeUrl);
            var evidence = await capture(challenge, options.video, options.setHint, options.signal);
            stopIfSkipped(options.signal);
            options.setStatus('Checking...');
            return await post(submitUrl, Object.assign({ token: challenge.token }, evidence, extra));
        } finally {
            stopCamera(stream, options.video);
        }
    }

    function faceLogin(options, email, returnUrl) {
        return runFace(options, '/Biometric/FaceChallenge', '/Biometric/FaceLogin', { email: email, returnUrl: returnUrl });
    }

    function faceEnroll(options, replace) {
        return runFace(options, '/Biometric/FaceEnrollChallenge', '/Biometric/FaceEnroll', { replace: !!replace });
    }

    // ---------- Page wiring ----------

    function init() {
        var root = $('bio-root');
        if (!root) { return; }

        var statusBox = $('bio-status');
        var panel = $('facePanel');
        var video = $('faceVideo');
        var hint = $('faceHint');
        var returnUrl = root.getAttribute('data-return-url') || '/';

        function setStatus(message, kind) {
            statusBox.className = 'alert alert-' + (kind || 'info');
            statusBox.textContent = message;
            if (kind === 'danger' && typeof statusBox.scrollIntoView === 'function') {
                statusBox.scrollIntoView({ block: 'center', behavior: 'smooth' });
            }
        }

        function setHint(text) { hint.textContent = text; }

        // What to do on the phone: the part people stall on ("Device connected! Continue on your device").
        var PHONE_STEPS = 'A QR code will appear. 1) Scan it with your phone camera. 2) On the phone tap Continue. ' +
            '3) Confirm with the phone fingerprint or face. Keep Bluetooth on, on both the computer and the phone, and keep the phone close.';

        function rememberVia(kind) {
            try { window.localStorage.setItem('stms.passkeyVia', kind); } catch (e) { /* ignore */ }
        }
        function viaPhone() {
            try { return window.localStorage.getItem('stms.passkeyVia') === 'phone'; } catch (e) { return false; }
        }

        function busy(buttons, isBusy) {
            buttons.forEach(function (b) { if (b) { b.disabled = isBusy; } });
        }

        try {
            var flash = window.sessionStorage.getItem('stms.flash');
            if (flash) { window.sessionStorage.removeItem('stms.flash'); setStatus(flash, 'success'); }
        } catch (e) { /* ignore */ }

        function deviceName() {
            var ua = navigator.userAgent;
            var browser = /Edg\//.test(ua) ? 'Edge' : /Chrome\//.test(ua) ? 'Chrome' : /Firefox\//.test(ua) ? 'Firefox' : /Safari\//.test(ua) ? 'Safari' : 'browser';
            return ('This computer (' + browser + ')').slice(0, 150);
        }

        var faceOptions = { video: video, setStatus: setStatus, setHint: setHint };
        var allButtons = Array.prototype.slice.call(root.querySelectorAll('button'));

        async function guarded(action, successMessage) {
            busy(allButtons, true);
            try {
                var result = await action();
                if (result && result.redirect) {
                    setStatus('Signed in. Redirecting...', 'success');
                    // The dedicated login page may celebrate a confirmed sign-in.
                    // All other biometric pages keep their original navigation.
                    if (typeof window.stmsLoginSuccess === 'function') {
                        try { await window.stmsLoginSuccess(result.redirect); }
                        catch (animationError) { window.location.href = result.redirect; }
                    } else { window.location.href = result.redirect; }
                    return;
                }
                setStatus(successMessage, 'success');
                return result;
            } catch (e) {
                if (attempt && attempt.signal.aborted && e && e.name === 'AbortError') {
                    // Skipped on purpose: not an error. The sign-in page returns to the password form.
                    setStatus('Skipped. Sign in with your password instead.', 'info');
                    document.dispatchEvent(new CustomEvent('stms:biometric-skip'));
                } else {
                    setStatus(e.message, 'danger');
                }
            } finally {
                panel.classList.add('d-none');
                busy(allButtons, false);
                endAttempt();
            }
        }

        // ----- "Skip": leave a fingerprint or face sign-in that is in progress and use the password -----
        // Only the sign-in page has it. It cancels the waiting browser request or the camera; nothing is sent
        // to the server and no check is bypassed: the person still has to sign in another way.
        var attempt = null;
        var skipButton = null;
        if ($('btnPasskeyLogin') || $('btnFaceLogin')) {
            skipButton = document.createElement('button');
            skipButton.type = 'button';
            skipButton.id = 'btnBioSkip';
            skipButton.className = 'btn btn-outline-secondary w-100 mt-2 d-none';
            skipButton.textContent = 'Skip and use my password';
            skipButton.addEventListener('click', function () { if (attempt) { attempt.abort(); } });
            statusBox.insertAdjacentElement('afterend', skipButton);
        }
        function beginAttempt() {
            attempt = new AbortController();
            if (skipButton) { skipButton.classList.remove('d-none'); }
            return attempt.signal;
        }
        function endAttempt() {
            attempt = null;
            if (skipButton) { skipButton.classList.add('d-none'); }
        }

        // ----- device check: a clear sentence and a details box instead of a vague failure -----
        var hintBox = $('bioHint');
        var diagBox = $('bioDiagText');
        var caps = null;
        var conditional = { controller: null, stopped: false, failures: 0 };
        var AUTO_PASSKEY_SUGGESTION = false;   // set to true to offer the passkey inside the email box again

        capabilities().then(function (c) {
            caps = c;
            var problem = problemOf(c);
            if (hintBox && problem) {
                hintBox.textContent = problem;
                hintBox.classList.remove('d-none');
            }
            if (diagBox) { diagBox.textContent = diagnosticsText(c); }
            // Without any WebAuthn support the buttons cannot work; say so instead of failing on click.
            if (!c.webauthn || !c.secure) {
                ['btnPasskeyLogin', 'btnPasskeyRegister', 'btnPasskeyRegisterDevice'].forEach(function (id) {
                    var b = $(id);
                    if (b) { b.disabled = true; }
                });
            }
            startConditionalLogin();
            showWelcomeBack();
            startFaceSetupIfAsked();
        }).catch(function () { /* the buttons still work and report their own errors */ });

        // ----- "Welcome back": one big button, like a banking app -----
        // The tip script remembers who signed in on this device. If that person has a fingerprint or a face registered,
        // the sign-in page greets them and offers a single button; no e-mail to type, no menu to find.
        function rememberedUser() {
            try {
                var u = JSON.parse(window.localStorage.getItem('stms.lastUser') || 'null');
                return u && u.name && (u.hasPasskey || u.hasFace) ? u : null;
            } catch (e) { return null; }
        }

        function chooseQuickMethod(u) {
            var fingerprintUsable = caps && caps.webauthn && caps.secure;
            if (u.hasPasskey && fingerprintUsable && (caps.platform || !u.hasFace)) { return 'fingerprint'; }
            if (u.hasFace && u.email) { return 'face'; }
            if (u.hasPasskey && fingerprintUsable) { return 'fingerprint'; }
            return null;
        }

        function showWelcomeBack() {
            if (!$('btnPasskeyLogin') || $('welcomeBack')) { return; }   // only on the sign-in page
            var user = rememberedUser();
            var method = user ? chooseQuickMethod(user) : null;
            var form = document.querySelector('form[method="post"]') || document.querySelector('form');
            if (!method || !form || !form.parentNode) { return; }

            var card = document.createElement('div');
            card.id = 'welcomeBack';
            card.className = 'card card-body text-center mb-4 col-md-5';

            var small = document.createElement('div');
            small.className = 'text-muted small';
            small.textContent = 'Welcome back';
            var name = document.createElement('h2');
            name.className = 'h4 mb-3';
            name.textContent = user.name;

            var go = document.createElement('button');
            go.type = 'button';
            go.className = 'btn btn-primary btn-lg';
            var usePhone = method === 'fingerprint' && viaPhone();
            go.textContent = method === 'face' ? 'Continue with face' : (usePhone ? 'Continue with my phone' : 'Continue with fingerprint');

            var notYou = document.createElement('button');
            notYou.type = 'button';
            notYou.className = 'btn btn-link btn-sm mt-2';
            notYou.textContent = 'Not you? Use another account';

            card.appendChild(small);
            card.appendChild(name);
            card.appendChild(go);
            card.appendChild(notYou);
            form.parentNode.insertBefore(card, form);

            go.addEventListener('click', function () {
                pauseConditionalLogin();
                if (method === 'fingerprint') {
                    setStatus(usePhone ? PHONE_STEPS : 'Use your fingerprint, face or PIN when your device asks.');
                    guarded(function () { return passkeyLogin(returnUrl, usePhone ? { hints: ['hybrid'] } : {}); }, 'Signed in.')
                        .then(function () { startConditionalLogin(); });
                } else {
                    panel.classList.remove('d-none');
                    guarded(function () { return faceLogin(faceOptions, user.email, returnUrl); }, 'Signed in.');
                }
            });

            notYou.addEventListener('click', function () {
                try { window.localStorage.removeItem('stms.lastUser'); } catch (e) { /* ignore */ }
                card.remove();
            });
        }

        // The tip sends people with only a camera to Profile > Biometrics?start=face: begin the face setup right away.
        function startFaceSetupIfAsked() {
            var button = $('btnFaceEnroll');
            if (!button || new URLSearchParams(window.location.search).get('start') !== 'face') { return; }
            setTimeout(function () { button.click(); }, 400);
        }

        var copyButton = $('bioDiagCopy');
        if (copyButton) {
            copyButton.addEventListener('click', function () {
                var text = diagBox ? diagBox.textContent : '';
                if (navigator.clipboard && text) {
                    navigator.clipboard.writeText(text).then(function () { copyButton.textContent = 'Copied'; });
                }
            });
        }

        // ----- sign-in suggestion inside the email box (like Google, GitHub and Microsoft) -----
        // The browser shows the saved fingerprint sign-in as a suggestion; choosing it signs the person in with no
        // button and no password. It waits quietly in the background and is paused while a button is used.
        function startConditionalLogin() {
            // Switched off: on Windows this waiting request can open the "Sign in with your passkey" system window by
            // itself, before the person asked for it. A passkey prompt now appears only after pressing the fingerprint button.
            if (!AUTO_PASSKEY_SUGGESTION) { return; }
            var emailBox = $('Email') || $('Input_Email');
            if (!caps || !caps.conditional || !caps.secure || !emailBox || !$('btnPasskeyLogin') || conditional.stopped) { return; }
            if (conditional.controller) { return; }

            emailBox.setAttribute('autocomplete', 'username webauthn');
            conditional.controller = new AbortController();
            var signal = conditional.controller.signal;

            passkeyLogin(returnUrl, { mediation: 'conditional', signal: signal }).then(function (result) {
                conditional.controller = null;
                if (result && result.redirect) {
                    setStatus('Signed in. Redirecting...', 'success');
                    window.location.href = result.redirect;
                }
            }).catch(function (e) {
                conditional.controller = null;
                if (e && e.name === 'AbortError') { return; }   // paused on purpose
                conditional.failures += 1;
                console.warn('Fingerprint suggestion stopped:', e);
                // The waiting request can expire; start a fresh one a few times, then leave the button as the way in.
                if (conditional.failures <= 3) { setTimeout(startConditionalLogin, 4000); }
            });
        }

        function pauseConditionalLogin() {
            if (conditional.controller) {
                conditional.controller.abort();
                conditional.controller = null;
            }
        }

        var passkeyLoginButton = $('btnPasskeyLogin');
        if (passkeyLoginButton) {
            passkeyLoginButton.addEventListener('click', function () {
                pauseConditionalLogin();
                // One button for everything. If the fingerprint is saved only on the phone, go straight to the QR code;
                // otherwise this computer answers (PIN, fingerprint or Touch ID) with no phone and no QR code.
                var phoneOnly = viaPhone();
                setStatus(phoneOnly ? PHONE_STEPS : 'Confirm with your fingerprint or PIN when your device asks.');
                var signal = beginAttempt();
                guarded(function () {
                    return passkeyLogin(returnUrl, phoneOnly ? { hints: ['hybrid'], signal: signal } : { signal: signal });
                }, 'Signed in.').then(function () {
                    // If it did not sign in, bring the email-box suggestion back.
                    startConditionalLogin();
                });
            });
        }

        var faceLoginButton = $('btnFaceLogin');
        if (faceLoginButton) {
            faceLoginButton.addEventListener('click', function () {
                // The page's own email box: "faceEmail" on the biometric page, or the sign-in form's field ("Email").
                var emailBox = $('faceEmail') || $('Email') || $('Input_Email');
                var email = ((emailBox && emailBox.value) || '').trim();
                if (!email) { setStatus('Enter your email first.', 'danger'); return; }
                panel.classList.remove('d-none');
                var signal = beginAttempt();
                guarded(function () { return faceLogin(Object.assign({}, faceOptions, { signal: signal }), email, returnUrl); }, 'Signed in.');
            });
        }

        // ONE way to register, with the real fingerprint: scan the QR code with the phone and confirm with the phone's
        // fingerprint. Nothing else is stored on this computer (no PIN). Signing in later goes straight to the QR code.
        var passkeyRegisterButton = $('btnPasskeyRegister');
        if (passkeyRegisterButton) {
            passkeyRegisterButton.addEventListener('click', function () {
                guarded(async function () {
                    setStatus(PHONE_STEPS);
                    await passkeyRegister('My phone', { phone: true });
                    try {
                        window.sessionStorage.setItem('stms.flash',
                            'Fingerprint registered. Next time press Sign in with fingerprint and scan the QR code with your phone.');
                    } catch (e) { /* ignore */ }
                    rememberVia('phone');
                    return { succeeded: true };
                }, 'Fingerprint registered.').then(function (ok) {
                    if (ok) { window.location.reload(); }
                });
            });
        }

        // For a computer that has its own fingerprint reader: register just this computer, no phone.
        var passkeyRegisterDeviceButton = $('btnPasskeyRegisterDevice');
        if (passkeyRegisterDeviceButton) {
            passkeyRegisterDeviceButton.addEventListener('click', function (event) {
                event.preventDefault();
                setStatus('Use your fingerprint, face or PIN when your device asks.');
                guarded(function () { return passkeyRegister(deviceName(), { device: true }); }, 'Device registered.').then(function (ok) {
                    if (ok) {
                        rememberVia('device');
                        try { window.sessionStorage.setItem('stms.flash', 'Fingerprint registered on this computer.'); } catch (e) { /* ignore */ }
                        window.location.reload();
                    }
                });
            });
        }

        var faceEnrollButton = $('btnFaceEnroll');
        if (faceEnrollButton) {
            faceEnrollButton.addEventListener('click', function () {
                panel.classList.remove('d-none');
                guarded(function () { return faceEnroll(faceOptions, $('faceReplace').checked); }, 'Face registered.').then(function (ok) {
                    if (ok) { window.location.reload(); }
                });
            });
        }

        Array.prototype.forEach.call(root.querySelectorAll('[data-remove-passkey]'), function (button) {
            button.addEventListener('click', function () {
                if (!window.confirm('Remove this device?')) { return; }
                guarded(function () {
                    return post('/Biometric/PasskeyRemove?id=' + encodeURIComponent(button.getAttribute('data-remove-passkey')));
                }, 'Device removed.').then(function (ok) {
                    if (ok) { window.location.reload(); }
                });
            });
        });

        var faceRemoveButton = $('btnFaceRemove');
        if (faceRemoveButton) {
            faceRemoveButton.addEventListener('click', function () {
                if (!window.confirm('Remove your stored face samples?')) { return; }
                guarded(function () { return post('/Biometric/FaceRemove'); }, 'Face samples removed.').then(function (ok) {
                    if (ok) { window.location.reload(); }
                });
            });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
