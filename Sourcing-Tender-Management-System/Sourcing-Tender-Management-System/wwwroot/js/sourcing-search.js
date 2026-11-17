// Shared bilingual search for every role workspace; the original asset URL stays compatible.
(() => {
    let activeVoice;
    const preferenceKey = 'stms.voiceLanguage';
    const normalize = value => value.normalize('NFKD').replace(/\p{M}/gu, '').replace(/ـ/g, '')
        .replace(/ى/g, 'ي').replace(/[٠-٩]/g, c => String(c.charCodeAt(0) - 1632))
        .replace(/[۰-۹]/g, c => String(c.charCodeAt(0) - 1776)).toLocaleLowerCase().trim();
    const messages = {
        'en-US': {
            results: n => n + ' result(s) found.', tasks: n => n + ' matching task(s).',
            updating: 'Updating results…', ready: 'Search updates as you type.',
            listening: 'Listening… Speak in the selected language.', start: 'Search by voice', stop: 'Stop listening',
            unsupported: ' Voice search is unavailable in this browser; type to search.',
            insecure: ' Voice search requires HTTPS or localhost; type to search.',
            failed: 'Results could not update. Check your connection or sign in again, then retry Search or Filter.',
            voiceFailed: 'Voice search could not start. Try again or type your search.',
            downloading: 'Downloading the on-device speech pack once… then speak.',
            noMatch: 'No matching records. Clear the search to show all records.',
            errors: {
                'not-allowed': 'Microphone permission was denied. Allow microphone access in browser settings, or type your search.',
                'service-not-allowed': 'The speech service is unavailable. Type your search.',
                'audio-capture': 'No microphone is available. Connect a microphone or type your search.',
                'no-speech': 'No speech was detected. Try again or type your search.',
                network: 'The browser speech service could not connect. Use Chrome or Edge with internet access (not Brave or Opera, and no VPN blocking Google/Microsoft), or type your search.',
                'language-not-supported': 'This voice language is not supported by your browser. Choose another language or type your search.'
            }
        },
        'ar-JO': {
            results: n => 'عدد النتائج: ' + n + '.', tasks: n => 'عدد المهام المطابقة: ' + n + '.',
            updating: 'جارٍ تحديث النتائج…', ready: 'تتحدث النتائج أثناء الكتابة.',
            listening: 'جارٍ الاستماع… تحدث باللغة المختارة.', start: 'البحث بالصوت', stop: 'إيقاف الاستماع',
            unsupported: ' البحث الصوتي غير متاح في هذا المتصفح؛ يمكنك البحث بالكتابة.',
            insecure: ' البحث الصوتي يحتاج HTTPS أو localhost؛ يمكنك البحث بالكتابة.',
            failed: 'تعذر تحديث النتائج. تحقق من الاتصال أو تسجيل الدخول، ثم أعد البحث.',
            voiceFailed: 'تعذر بدء البحث الصوتي. حاول مجددًا أو ابحث بالكتابة.',
            downloading: 'جارٍ تنزيل حزمة الصوت على الجهاز لمرة واحدة… ثم تحدث.',
            noMatch: 'لا توجد سجلات مطابقة. امسح البحث لعرض جميع السجلات.',
            errors: {
                'not-allowed': 'تم رفض إذن الميكروفون. اسمح باستخدامه من إعدادات المتصفح، أو ابحث بالكتابة.',
                'service-not-allowed': 'خدمة التعرف على الصوت غير متاحة. يمكنك البحث بالكتابة.',
                'audio-capture': 'لا يوجد ميكروفون متاح. وصّل ميكروفونًا أو ابحث بالكتابة.',
                'no-speech': 'لم يتم التقاط كلام. حاول مجددًا أو ابحث بالكتابة.',
                network: 'تعذر الاتصال بخدمة الصوت في المتصفح. استخدم Chrome أو Edge مع اتصال بالإنترنت، أو ابحث بالكتابة.',
                'language-not-supported': 'المتصفح لا يدعم لغة الصوت المختارة. اختر لغة أخرى أو ابحث بالكتابة.'
            }
        }
    };
    document.querySelectorAll('[data-live-search]').forEach(form => {
        if (form.dataset.searchInitialized) return;
        const input = form.querySelector('[data-search-input]');
        const results = document.getElementById(form.dataset.results);
        const status = document.getElementById(form.dataset.searchStatus);
        const button = form.querySelector('[data-voice-search]');
        const buttonLabel = button?.querySelector('[data-voice-label]');
        const language = form.querySelector('[data-voice-language]');
        const languagePicker = form.querySelector('[data-language-picker]');
        const languageToggle = form.querySelector('[data-language-toggle]');
        const languageMenu = form.querySelector('[data-language-menu]');
        const languageSymbol = form.querySelector('[data-language-symbol]');
        const languageOptions = [...form.querySelectorAll('[data-language-option]')];
        if (!input || !results || !status || !button || !language) return;
        form.dataset.searchInitialized = 'true';
        form.hidden = false;
        input.dir = 'auto';
        input.setAttribute('aria-controls', results.id);
        // Razor always renders data-* attributes (a null value becomes ""), so a server search is told apart by its response mode.
        const local = form.hasAttribute('data-local-search') && form.dataset.searchResponse !== 'document';
        const selector = form.dataset.rowSelector || '[data-task]';
        const empty = document.getElementById(form.dataset.emptyState);
        const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        let timer, pending, voice, revision = 0, lastMessage = '', latestCount;
        try {
            const saved = localStorage.getItem(preferenceKey);
            if (messages[saved]) language.value = saved;
        } catch { /* Search remains available when storage is blocked. */ }
        const copy = () => messages[language.value] || messages['en-US'];
        const syncLanguagePicker = () => {
            if (languageSymbol) languageSymbol.textContent = language.value === 'ar-JO' ? 'ع' : 'A';
            languageOptions.forEach(option => option.setAttribute('aria-selected', option.dataset.languageOption === language.value ? 'true' : 'false'));
        };
        const closeLanguageMenu = () => {
            if (languageMenu) languageMenu.hidden = true;
            languageToggle?.setAttribute('aria-expanded', 'false');
        };
        const setButtonText = text => {
            if (buttonLabel) buttonLabel.textContent = text;
            else button.textContent = text;
            button.setAttribute('aria-label', text);
            button.title = text;
        };
        const unavailable = () => !Recognition ? copy().unsupported : !window.isSecureContext ? copy().insecure : '';
        const announce = message => {
            status.textContent = message + unavailable();
            status.lang = language.value;
            status.dir = language.value === 'ar-JO' ? 'rtl' : 'ltr';
        };
        const resultMessage = count => local && selector === '[data-task]' ? copy().tasks(count) : copy().results(count);
        const feedback = message => { lastMessage = message; announce(message); };
        const rowText = row => {
            const values = [...row.querySelectorAll('input:not([type=hidden]):not([type=password]), select')]
                .map(control => control.tagName === 'SELECT' ? control.selectedOptions[0]?.textContent || '' :
                    ['checkbox', 'radio'].includes(control.type) ? '' : control.value);
            // Select options which were not chosen must not create false matches in editable catalog rows.
            const clone = row.cloneNode(true);
            clone.querySelectorAll('input, select, textarea, button').forEach(control => control.remove());
            return normalize((row.dataset.searchText || clone.textContent || '') + ' ' + values.join(' '));
        };
        const filterLocal = () => {
            const rows = [...results.querySelectorAll(selector)];
            const term = normalize(input.value);
            rows.forEach(row => { row.hidden = !rowText(row).includes(term); });
            latestCount = rows.filter(row => !row.hidden).length;
            if (empty) {
                empty.hidden = latestCount > 0 || rows.length === 0;
                empty.textContent = copy().noMatch;
                empty.dir = language.value === 'ar-JO' ? 'rtl' : 'ltr';
            }
            feedback(resultMessage(latestCount));
        };
        const refresh = async current => {
            const url = new URL(form.action, location.href);
            const fields = new FormData(form);
            for (const key of new Set(fields.keys())) url.searchParams.delete(key);
            for (const [key, value] of fields) {
                if (typeof value === 'string' && value.trim()) url.searchParams.append(key, value.trim());
            }
            const controller = new AbortController();
            pending = controller;
            results.setAttribute('aria-busy', 'true');
            feedback(copy().updating);
            try {
                const response = await fetch(url, { headers: { 'X-Sourcing-Live': '1' }, signal: controller.signal, cache: 'no-store' });
                if (!response.ok || response.redirected) throw new Error('Search unavailable');
                const html = await response.text();
                if (current !== revision) return;
                if (form.dataset.searchResponse === 'document') {
                    const documentResult = new DOMParser().parseFromString(html, 'text/html').getElementById(results.id);
                    if (!documentResult) throw new Error('Results missing');
                    results.innerHTML = documentResult.innerHTML;
                } else {
                    if (response.headers.get('X-Sourcing-Fragment') !== '1') throw new Error('Invalid fragment');
                    results.innerHTML = html;
                }
                history.replaceState(null, '', url);
                latestCount = results.querySelector('[data-result-count]')?.dataset.resultCount ??
                    results.querySelectorAll('[data-search-item]').length;
                feedback(resultMessage(latestCount));
            } catch (error) {
                if (error.name !== 'AbortError' && current === revision) feedback(copy().failed);
            } finally {
                if (current === revision) results.setAttribute('aria-busy', 'false');
            }
        };
        const search = (immediate = false) => {
            clearTimeout(timer); pending?.abort(); revision++;
            if (local) { filterLocal(); return; }
            const current = revision;
            if (immediate) refresh(current);
            else timer = setTimeout(() => refresh(current), 250);
        };
        const cancelVoice = () => {
            const session = voice;
            if (!session) return;
            voice = null;
            if (activeVoice === cancelVoice) activeVoice = null;
            try { session.abort(); } catch { /* Session may already have ended. */ }
            setButtonText(copy().start);
            button.setAttribute('aria-pressed', 'false');
            language.disabled = false;
            if (languageToggle) languageToggle.disabled = false;
            announce(lastMessage || copy().ready);
        };
        input.addEventListener('input', () => { cancelVoice(); search(); });
        form.querySelectorAll('select[name]').forEach(select => select.addEventListener('change', () => search(true)));
        form.addEventListener('submit', event => { event.preventDefault(); cancelVoice(); search(true); });
        form.querySelector('[data-search-reset]')?.addEventListener('click', event => {
            event.preventDefault(); cancelVoice(); input.value = '';
            form.querySelectorAll('select[name]').forEach(select => { select.value = ''; });
            search(true); input.focus();
        });
        language.addEventListener('change', () => {
            cancelVoice();
            syncLanguagePicker();
            try { localStorage.setItem(preferenceKey, language.value); } catch { /* Optional preference. */ }
            setButtonText(copy().start);
            if (local) filterLocal();
            else feedback(latestCount !== undefined ? resultMessage(latestCount) : copy().ready);
        });
        button.disabled = !Recognition || !window.isSecureContext;
        languageToggle?.addEventListener('click', event => {
            event.stopPropagation();
            if (!languageMenu) return;
            languageMenu.hidden = !languageMenu.hidden;
            languageToggle.setAttribute('aria-expanded', languageMenu.hidden ? 'false' : 'true');
        });
        languageOptions.forEach(option => option.addEventListener('click', event => {
            event.stopPropagation();
            language.value = option.dataset.languageOption;
            language.dispatchEvent(new Event('change', { bubbles: true }));
            closeLanguageMenu();
            languageToggle?.focus();
        }));
        document.addEventListener('click', event => {
            if (languagePicker && !languagePicker.contains(event.target)) closeLanguageMenu();
        });
        syncLanguagePicker();
        setButtonText(copy().start);
        // When the browser's online speech service is unreachable ('network'), retry once with on-device
        // recognition (newer Chrome/Edge), downloading the language pack if the browser offers it.
        const tryOnDevice = async () => {
            const options = { langs: [language.value], processLocally: true };
            let state = 'unavailable';
            try { state = await Recognition.available(options); } catch { /* Not supported. */ }
            if (state === 'downloadable' || state === 'downloading') {
                announce(copy().downloading);
                try { state = await Recognition.install(options) ? 'available' : 'unavailable'; } catch { state = 'unavailable'; }
            }
            if (state === 'available') begin(true);
            else announce(copy().errors.network);
        };
        button.addEventListener('click', () => {
            if (voice) { cancelVoice(); feedback(lastMessage || copy().ready); return; }
            activeVoice?.();
            begin(false);
        });
        function begin(onDevice) {
            let session;
            try { session = new Recognition(); } catch { feedback(copy().voiceFailed); return; }
            voice = session; activeVoice = cancelVoice;
            let failed = false, retryOnDevice = false;
            session.lang = language.value;
            if (onDevice) session.processLocally = true;
            session.interimResults = true; session.continuous = false;
            setButtonText(copy().stop);
            button.setAttribute('aria-pressed', 'true'); language.disabled = true;
            if (languageToggle) { languageToggle.disabled = true; closeLanguageMenu(); }
            session.onstart = () => { if (voice === session) announce(copy().listening); };
            session.onresult = event => {
                if (voice !== session) return;
                input.value = Array.from(event.results).map(result => result[0].transcript).join(' ').trim();
                search();
            };
            session.onerror = event => {
                if (voice !== session || event.error === 'aborted') return;
                failed = true;
                if (event.error === 'network' && !onDevice && typeof Recognition.available === 'function') { retryOnDevice = true; return; }
                announce(copy().errors[event.error] || copy().voiceFailed);
            };
            session.onend = () => {
                if (voice !== session) return;
                voice = null;
                if (activeVoice === cancelVoice) activeVoice = null;
                setButtonText(copy().start);
                button.setAttribute('aria-pressed', 'false'); language.disabled = false;
                if (languageToggle) languageToggle.disabled = false;
                if (retryOnDevice) { tryOnDevice(); return; }
                if (!failed) announce(lastMessage || copy().ready);
            };
            try { session.start(); }
            catch { cancelVoice(); feedback(copy().voiceFailed); }
        }
        window.addEventListener('pagehide', () => {
            clearTimeout(timer); pending?.abort(); revision++; cancelVoice();
            results.setAttribute('aria-busy', 'false');
        });
        window.addEventListener('pageshow', event => {
            if (event.persisted) feedback(latestCount !== undefined ? resultMessage(latestCount) : copy().ready);
        });
        if (local) filterLocal(); else feedback(copy().ready);
    });
})();
