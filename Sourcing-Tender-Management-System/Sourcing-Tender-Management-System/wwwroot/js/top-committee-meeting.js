/*
 * Top Committee video meeting (Jitsi IFrame API).
 *
 * Join meeting       -> asks the server for this tender's private room, loads the video library from the video
 *                       server and opens the room inside the page.
 * Open in a new tab  -> opens the same room as a normal page of the video server.
 * Leave meeting      -> closes the room and returns to the page.
 *
 * Every failure shows a plain message and leaves the buttons usable, so the page never gets stuck.
 */
(function () {
    'use strict';

    var card = document.getElementById('meeting-card');
    if (!card) return;

    var startUrl = card.getAttribute('data-start-url');
    var startBtn = document.getElementById('meeting-start');
    var newTabBtn = document.getElementById('meeting-newtab');
    var leaveBtn = document.getElementById('meeting-leave');
    var countBadge = document.getElementById('meeting-count');
    var statusBox = document.getElementById('meeting-status');
    var errorBox = document.getElementById('meeting-error');
    var frameBox = document.getElementById('meet');

    var HOST_PATTERN = /^[a-z0-9]([a-z0-9.-]{0,251}[a-z0-9])?$/;
    var ROOM_PATTERN = /^[A-Za-z0-9-]{8,64}$/;
    var LOAD_TIMEOUT_MS = 20000;

    var api = null;        // the running meeting, or null
    var busy = false;      // a start request is in progress
    var participants = 1;  // including me
    var loadedDomain = null;

    function antiForgeryToken() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function setStatus(text) {
        statusBox.textContent = text;
    }

    function showError(text) {
        errorBox.textContent = text;
        errorBox.classList.remove('d-none');
    }

    function clearError() {
        errorBox.textContent = '';
        errorBox.classList.add('d-none');
    }

    function setBusy(value) {
        busy = value;
        startBtn.disabled = value || api !== null;
        newTabBtn.disabled = value;
    }

    function updateCount() {
        countBadge.textContent = participants + (participants === 1 ? ' person in the meeting' : ' people in the meeting');
    }

    // Ask the server for the private room. Resolves with the room details or rejects with a readable Error.
    function requestRoom() {
        var controller = typeof AbortController === 'function' ? new AbortController() : null;
        var timer = setTimeout(function () { if (controller) controller.abort(); }, 15000);

        return fetch(startUrl, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'RequestVerificationToken': antiForgeryToken(), 'X-Requested-With': 'XMLHttpRequest' },
            signal: controller ? controller.signal : undefined
        }).then(function (response) {
            clearTimeout(timer);
            return response.text().then(function (text) {
                var data = null;
                try { data = text ? JSON.parse(text) : null; } catch (e) { data = null; }

                if (response.status === 401 || response.redirected) {
                    throw new Error('Your session has ended. Please sign in again.');
                }
                if (response.status === 400) {
                    throw new Error('The page expired. Please reload it and try again.');
                }
                if (response.status === 403) {
                    throw new Error((data && data.error) || 'You are not allowed to join this meeting.');
                }
                if (!response.ok || !data || !data.ok) {
                    throw new Error((data && data.error) || 'The meeting could not be prepared. Please try again.');
                }
                if (!HOST_PATTERN.test(data.domain || '') || !ROOM_PATTERN.test(data.room || '')) {
                    throw new Error('The server returned an invalid meeting room.');
                }
                return data;
            });
        }, function (error) {
            clearTimeout(timer);
            if (error && error.name === 'AbortError') {
                throw new Error('The server took too long to answer. Please try again.');
            }
            throw new Error('Could not reach the server. Check your connection and try again.');
        });
    }

    // Load the video library from the video server (once per server name).
    function loadLibrary(domain) {
        if (typeof window.JitsiMeetExternalAPI === 'function' && loadedDomain === domain) {
            return Promise.resolve();
        }

        return new Promise(function (resolve, reject) {
            var script = document.createElement('script');
            var timer = setTimeout(function () {
                script.remove();
                reject(new Error('The video service did not answer in time.'));
            }, LOAD_TIMEOUT_MS);

            script.src = 'https://' + domain + '/external_api.js';
            script.async = true;
            script.onload = function () {
                clearTimeout(timer);
                if (typeof window.JitsiMeetExternalAPI === 'function') {
                    loadedDomain = domain;
                    resolve();
                } else {
                    reject(new Error('The video library loaded but is not usable.'));
                }
            };
            script.onerror = function () {
                clearTimeout(timer);
                script.remove();
                reject(new Error('The video service could not be reached. Check your internet connection, or use "Open in a new tab".'));
            };
            document.head.appendChild(script);
        });
    }

    function closeMeeting(message) {
        if (api) {
            try { api.dispose(); } catch (e) { /* already closed */ }
            api = null;
        }
        frameBox.innerHTML = '';
        frameBox.classList.add('d-none');
        leaveBtn.classList.add('d-none');
        countBadge.classList.add('d-none');
        participants = 1;
        startBtn.disabled = false;
        startBtn.textContent = 'Join meeting';
        newTabBtn.disabled = false;
        setStatus(message || 'You left the meeting. Press Join meeting to join again.');
    }

    function openMeeting(room) {
        frameBox.innerHTML = '';
        frameBox.classList.remove('d-none');

        api = new window.JitsiMeetExternalAPI(room.domain, {
            roomName: room.room,
            parentNode: frameBox,
            width: '100%',
            height: '100%',
            lang: 'en',
            userInfo: { displayName: room.displayName },
            configOverwrite: {
                subject: room.subject,
                disableDeepLinking: true,   // no "open the mobile app" screen
                prejoinPageEnabled: true    // let people check camera and microphone first
            }
        });

        participants = 1;
        updateCount();
        countBadge.classList.remove('d-none');
        leaveBtn.classList.remove('d-none');
        startBtn.disabled = true;
        startBtn.textContent = 'In the meeting';
        setStatus('Connecting to the meeting. Allow the camera and microphone when your browser asks, then press Join meeting. '
            + 'If it says "waiting for a moderator", the first person must press Log in on the video screen.');

        api.addEventListener('videoConferenceJoined', function () {
            participants = 1;
            updateCount();
            setStatus('You are in the meeting.');
        });
        api.addEventListener('participantJoined', function () { participants += 1; updateCount(); });
        api.addEventListener('participantLeft', function () { participants = Math.max(1, participants - 1); updateCount(); });
        api.addEventListener('videoConferenceLeft', function () { closeMeeting(); });
        api.addEventListener('readyToClose', function () { closeMeeting(); });
        api.addEventListener('errorOccurred', function (event) {
            var name = event && event.error && event.error.name ? event.error.name : 'error';
            showError('The video service reported a problem (' + name + '). You can leave and start again, or use "Open in a new tab".');
        });
    }

    startBtn.addEventListener('click', function () {
        if (busy || api) return;
        clearError();
        setBusy(true);
        setStatus('Preparing the meeting room...');

        requestRoom()
            .then(function (room) {
                return loadLibrary(room.domain).then(function () { openMeeting(room); });
            })
            .catch(function (error) {
                closeMeeting('The meeting did not start.');
                showError(error.message || 'The meeting could not start.');
            })
            .then(function () { setBusy(false); });
    });

    newTabBtn.addEventListener('click', function () {
        if (busy) return;
        clearError();

        // Open the tab now, inside the click, so the browser does not treat it as a blocked pop-up.
        var tab = window.open('', '_blank');
        if (tab) {
            try { tab.opener = null; } catch (e) { /* ignore */ }
            tab.document.title = 'Opening the meeting...';
        }
        setBusy(true);
        setStatus('Preparing the meeting room...');

        requestRoom()
            .then(function (room) {
                var url = 'https://' + room.domain + '/' + encodeURIComponent(room.room)
                    + '#userInfo.displayName=' + encodeURIComponent(JSON.stringify(room.displayName))
                    + '&config.subject=' + encodeURIComponent(JSON.stringify(room.subject));
                if (tab && !tab.closed) {
                    tab.location.replace(url);
                    setStatus('The meeting opened in a new tab.');
                } else {
                    setStatus('Your browser blocked the new tab. Allow pop-ups for this site and try again.');
                    showError('The new tab was blocked by your browser.');
                }
            })
            .catch(function (error) {
                if (tab && !tab.closed) tab.close();
                setStatus('The meeting did not open.');
                showError(error.message || 'The meeting could not open.');
            })
            .then(function () { setBusy(false); });
    });

    leaveBtn.addEventListener('click', function () {
        if (!api) return;
        try { api.executeCommand('hangup'); } catch (e) { /* closed below anyway */ }
        closeMeeting();
    });

    // Leaving the page ends the call cleanly.
    window.addEventListener('beforeunload', function () {
        if (api) {
            try { api.dispose(); } catch (e) { /* ignore */ }
        }
    });
})();
