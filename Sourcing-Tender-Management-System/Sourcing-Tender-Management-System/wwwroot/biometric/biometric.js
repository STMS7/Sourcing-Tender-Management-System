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
        var detail = error && error.name
            ? ' [' + error.name + (error.message ? ': ' + error.message : '') + ']'
            : '';
        if (error && (error.name === 'NotAllowedError' || error.name === 'AbortError')) {
            return new Error('The request was cancelled or timed out. If you did not cancel it, make sure Windows Hello ' +
                '(fingerprint, face or PIN) is set up on this device.' + detail);
        }
        if (error && error.name === 'InvalidStateError') {
            return new Error('This device is already registered.' + detail);
        }
        if (error && error.name === 'NotSupportedError') {
            return new Error('This device or browser does not support this kind of sign-in.' + detail);
        }
        return error instanceof Error ? error : new Error(String(error));
    }

    // ---------- Fingerprint / device passkey (WebAuthn) ----------

    function ensureWebAuthn() {
        if (!window.PublicKeyCredential || !navigator.credentials) {
            throw new Error('This browser does not support fingerprint sign-in.');
        }
    }

    async function passkeyLogin(returnUrl) {
        ensureWebAuthn();
        var start = await post('/Biometric/PasskeyLoginOptions');
        var options = start.options;
        options.challenge = dec(options.challenge);

        var credential;
        try {
            credential = await navigator.credentials.get({ publicKey: options });
        } catch (e) {
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

    async function passkeyRegister(label) {
        ensureWebAuthn();
        var start = await post('/Biometric/PasskeyRegisterOptions');
        var options = start.options;
        options.challenge = dec(options.challenge);
        options.user.id = dec(options.user.id);
        (options.excludeCredentials || []).forEach(function (c) { c.id = dec(c.id); });

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

    async function capture(challenge, video, setHint) {
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
        var stream = await startCamera(options.video);
        try {
            options.setStatus('Follow the instructions under the camera.');
            var challenge = await post(challengeUrl);
            var evidence = await capture(challenge, options.video, options.setHint);
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
        }

        function setHint(text) { hint.textContent = text; }

        function busy(buttons, isBusy) {
            buttons.forEach(function (b) { if (b) { b.disabled = isBusy; } });
        }

        var faceOptions = { video: video, setStatus: setStatus, setHint: setHint };
        var allButtons = Array.prototype.slice.call(root.querySelectorAll('button'));

        async function guarded(action, successMessage) {
            busy(allButtons, true);
            try {
                var result = await action();
                if (result && result.redirect) {
                    setStatus('Signed in. Redirecting...', 'success');
                    window.location.href = result.redirect;
                    return;
                }
                setStatus(successMessage, 'success');
                return result;
            } catch (e) {
                setStatus(e.message, 'danger');
            } finally {
                panel.classList.add('d-none');
                busy(allButtons, false);
            }
        }

        var passkeyLoginButton = $('btnPasskeyLogin');
        if (passkeyLoginButton) {
            passkeyLoginButton.addEventListener('click', function () {
                setStatus('Use your fingerprint, face or PIN when your device asks.');
                guarded(function () { return passkeyLogin(returnUrl); }, 'Signed in.');
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
                guarded(function () { return faceLogin(faceOptions, email, returnUrl); }, 'Signed in.');
            });
        }

        var passkeyRegisterButton = $('btnPasskeyRegister');
        if (passkeyRegisterButton) {
            passkeyRegisterButton.addEventListener('click', function () {
                var label = ($('passkeyLabel').value || '').trim() || 'My device';
                setStatus('Use your fingerprint, face or PIN when your device asks.');
                guarded(function () { return passkeyRegister(label); }, 'Device registered.').then(function (ok) {
                    if (ok) { window.location.reload(); }
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
