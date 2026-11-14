(() => {
    document.querySelectorAll('[data-live-search]').forEach(form => {
        const input = form.querySelector('[data-search-input]');
        const results = document.getElementById(form.dataset.results);
        const status = document.getElementById(form.dataset.searchStatus);
        const button = form.querySelector('[data-voice-search]');
        const language = form.querySelector('[data-voice-language]');
        const local = form.hasAttribute('data-local-search');
        const rows = local ? [...results.querySelectorAll('[data-task]')] : [];
        const empty = document.getElementById(form.dataset.emptyState);
        let timer;
        let pending;
        let revision = 0;
        let listening = false;
        let voiceError = false;
        let recognition;
        let unavailable = '';
        const announce = message => { status.textContent = message + unavailable; };
        const filterLocal = () => {
            const term = input.value.trim().toLocaleLowerCase();
            rows.forEach(row => { row.hidden = !row.dataset.task.toLocaleLowerCase().includes(term); });
            const count = rows.filter(row => !row.hidden).length;
            empty.hidden = count > 0;
            announce(count + ' matching task(s).');
        };
        const refresh = async current => {
            const url = new URL(form.action, location.href);
            for (const [key, value] of new FormData(form)) {
                if (typeof value === 'string' && value.trim()) url.searchParams.set(key, value.trim());
            }
            pending = new AbortController();
            results.setAttribute('aria-busy', 'true');
            announce('Updating results…');
            try {
                const response = await fetch(url, {
                    headers: { 'X-Sourcing-Live': '1' },
                    signal: pending.signal,
                    cache: 'no-store'
                });
                if (!response.ok || response.redirected || response.headers.get('X-Sourcing-Fragment') !== '1')
                    throw new Error('Search unavailable');
                const html = await response.text();
                if (current !== revision) return;
                results.innerHTML = html;
                history.replaceState(null, '', url);
                const count = results.querySelector('[data-result-count]')?.dataset.resultCount ?? '0';
                announce(count + ' result(s) found.');
            } catch (error) {
                if (error.name !== 'AbortError' && current === revision)
                    announce('Results could not update. Check your connection or sign in again, then retry Search or Filter.');
            } finally {
                if (current === revision) results.setAttribute('aria-busy', 'false');
            }
        };
        const search = (immediate = false) => {
            clearTimeout(timer);
            pending?.abort();
            revision++;
            if (local) { filterLocal(); return; }
            const current = revision;
            // Database requests are debounced; older responses are invalidated immediately.
            if (immediate) refresh(current);
            else timer = setTimeout(() => refresh(current), 250);
        };
        input.addEventListener('input', () => search());
        form.querySelectorAll('select[name]').forEach(select => select.addEventListener('change', () => search(true)));
        form.addEventListener('submit', event => { event.preventDefault(); search(true); });
        form.querySelector('[data-search-reset]')?.addEventListener('click', event => {
            event.preventDefault();
            recognition?.abort();
            input.value = '';
            form.querySelectorAll('select[name]').forEach(select => { select.value = ''; });
            search(true);
        });
        const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!Recognition || !window.isSecureContext) {
            button.disabled = true;
            unavailable = !Recognition ? ' Voice search is unavailable in this browser; type to search.'
                : ' Voice search requires HTTPS or localhost; type to search.';
            announce('Search updates as you type.');
        } else {
            recognition = new Recognition();
            recognition.interimResults = true;
            recognition.continuous = false;
            recognition.onstart = () => {
                listening = true;
                button.textContent = 'Stop listening';
                button.setAttribute('aria-pressed', 'true');
                language.disabled = true;
                announce('Listening… Speak in the selected language.');
            };
            recognition.onresult = event => {
                input.value = Array.from(event.results).map(result => result[0].transcript).join(' ').trim();
                search();
            };
            recognition.onerror = event => {
                voiceError = true;
                const messages = {
                    'not-allowed': 'Microphone permission was denied. Allow microphone access in browser settings, or type your search.',
                    'service-not-allowed': 'The speech service is unavailable. Type your search.',
                    'audio-capture': 'No microphone is available. Connect a microphone or type your search.',
                    'no-speech': 'No speech was detected. Try again or type your search.',
                    'network': 'The speech service could not connect. Check your connection or type your search.',
                    'language-not-supported': 'This voice language is not supported by your browser. Choose another language or type your search.'
                };
                if (event.error !== 'aborted') announce(messages[event.error] ?? 'Voice search failed. Try again or type your search.');
            };
            recognition.onend = () => {
                listening = false;
                button.textContent = 'Search by voice';
                button.setAttribute('aria-pressed', 'false');
                language.disabled = false;
                if (!voiceError) announce('Listening ended. You can edit the search text or speak again.');
            };
            button.addEventListener('click', () => {
                if (listening) { recognition.stop(); return; }
                voiceError = false;
                recognition.lang = language.value;
                try { recognition.start(); }
                catch { announce('Voice search could not start. Try again or type your search.'); }
            });
        }
        window.addEventListener('pagehide', () => {
            clearTimeout(timer);
            pending?.abort();
            recognition?.abort();
        });
        if (local) filterLocal();
    });
})();
