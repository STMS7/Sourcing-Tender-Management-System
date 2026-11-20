// Top bar search and notification bell. All server text is inserted with textContent, never as HTML.
(function () {
    'use strict';
    const root = document.getElementById('tt');
    if (!root) return;

    const $ = s => root.querySelector(s);
    const cfg = root.dataset;                       // searchUrl, recentUrl, readUrl, allUrl
    const input = $('.tt-input'), results = $('#tt-results'), form = $('.tt-search');
    const bell = $('.tt-btn'), panel = $('#tt-panel'), list = $('.tt-list'), count = $('.tt-count');
    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;

    const el = (tag, text, cls) => {
        const e = document.createElement(tag);
        if (text) e.textContent = text;
        if (cls) e.className = cls;
        return e;
    };
    const sameSite = url => { try { return new URL(url, location.origin).origin === location.origin; } catch { return false; } };

    // ---- Search ----------------------------------------------------------------------------
    let timer, controller;
    const hits = () => [...results.querySelectorAll('a')];

    function setResultsOpen(open) {
        results.hidden = !open;
        input.setAttribute('aria-expanded', String(open));
    }
    function message(text) { results.replaceChildren(el('p', text, 'tt-msg')); setResultsOpen(true); }

    function render(groups, q) {
        const nodes = [];
        groups.forEach(g => {
            nodes.push(el('div', g.label, 'tt-group'));
            g.hits.forEach(h => {
                if (!sameSite(h.url)) return;
                const a = el('a', '', 'tt-hit');
                a.href = h.url;
                a.setAttribute('role', 'option');
                a.append(el('span', h.title, 'tt-hit-title'), el('span', h.sub, 'tt-hit-sub'));
                nodes.push(a);
            });
            if (g.moreUrl && sameSite(g.moreUrl)) {
                const more = el('a', `See all in ${g.label.toLowerCase()}`, 'tt-more');
                more.href = g.moreUrl;
                nodes.push(more);
            }
        });
        if (!nodes.length) return message(`No matches for "${q}".`);
        results.replaceChildren(...nodes);
        setResultsOpen(true);
    }

    async function search(q) {
        controller?.abort();
        controller = new AbortController();
        message('Searching...');
        try {
            const res = await fetch(`${cfg.searchUrl}?q=${encodeURIComponent(q)}`, { signal: controller.signal });
            if (!res.ok) throw new Error(res.status);
            render((await res.json()).groups, q);
        } catch (e) {
            if (e.name !== 'AbortError') message('Search is not available right now.');
        }
    }

    input.addEventListener('input', () => {
        clearTimeout(timer);
        const q = input.value.trim();
        if (q.length < 2) { controller?.abort(); setResultsOpen(false); return; }
        timer = setTimeout(() => search(q), 200);
    });
    input.addEventListener('focus', () => { if (hits().length) setResultsOpen(true); });
    form.addEventListener('submit', e => { e.preventDefault(); hits()[0]?.click(); });

    // ---- Notifications ---------------------------------------------------------------------
    const ago = iso => {
        const secs = Math.round((new Date(iso) - Date.now()) / 1000);
        const rtf = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' });
        for (const [unit, size] of [['day', 86400], ['hour', 3600], ['minute', 60]])
            if (Math.abs(secs) >= size) return rtf.format(Math.round(secs / size), unit);
        return 'just now';
    };

    async function openItem(n) {
        if (!n.isRead && token) {
            const fd = new FormData();
            fd.set('id', n.id);
            fd.set('__RequestVerificationToken', token);
            try { await fetch(cfg.readUrl, { method: 'POST', body: fd, redirect: 'manual' }); } catch { }
        }
        location.href = n.link && sameSite(n.link) ? n.link : cfg.allUrl;
    }

    function renderBell(d) {
        count.hidden = d.unread === 0;
        count.textContent = d.unread > 99 ? '99+' : String(d.unread);
        bell.setAttribute('aria-label', d.unread ? `Notifications, ${d.unread} unread` : 'Notifications');
        list.replaceChildren(...(d.items.length ? d.items.map(n => {
            const b = el('button', '', 'tt-item' + (n.isRead ? '' : ' is-unread'));
            b.type = 'button';
            b.append(el('span', n.title, 'tt-item-title'), el('span', n.message, 'tt-item-msg'), el('span', ago(n.when), 'tt-item-when'));
            b.addEventListener('click', () => openItem(n));
            return b;
        }) : [el('p', 'You have no notifications yet.', 'tt-empty')]));
    }

    async function loadRecent() {
        try {
            const res = await fetch(cfg.recentUrl);
            if (res.ok) renderBell(await res.json());
        } catch { }
    }

    function setPanel(open) {
        panel.hidden = !open;
        bell.setAttribute('aria-expanded', String(open));
        if (open) { setResultsOpen(false); loadRecent(); }
    }
    bell.addEventListener('click', () => setPanel(panel.hidden));

    loadRecent();
    setInterval(() => { if (!document.hidden) loadRecent(); }, 60000);

    // ---- Keyboard and outside clicks ---------------------------------------------------------
    root.addEventListener('keydown', e => {
        if (e.key === 'Escape') {
            const wasPanel = !panel.hidden;
            setResultsOpen(false);
            setPanel(false);
            (wasPanel ? bell : input).focus();
        } else if ((e.key === 'ArrowDown' || e.key === 'ArrowUp') && !results.hidden) {
            const list = hits();
            if (!list.length) return;
            e.preventDefault();
            const i = list.indexOf(document.activeElement);
            const next = e.key === 'ArrowDown' ? i + 1 : i - 1;
            (list[(next + list.length) % list.length]).focus();
        }
    });
    document.addEventListener('click', e => { if (!root.contains(e.target)) { setResultsOpen(false); setPanel(false); } });
    document.addEventListener('keydown', e => {
        const tag = document.activeElement?.tagName;
        if (e.key === '/' && !/INPUT|TEXTAREA|SELECT/.test(tag) && !document.activeElement?.isContentEditable) {
            e.preventDefault();
            input.focus();
        }
    });
})();
