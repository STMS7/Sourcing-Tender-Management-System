// Floating AI assistant. Every piece of text from the server is inserted with textContent, never as HTML.
(function () {
    'use strict';
    const root = document.getElementById('aiw');
    if (!root) return;

    const $ = s => root.querySelector(s);
    const fab = $('.aiw-fab'), panel = $('.aiw-panel'), log = $('.aiw-log'),
          form = $('.aiw-form'), input = form.elements.question, send = $('.aiw-send');
    const urls = root.dataset;                 // askUrl, excelUrl, pdfUrl come from the partial
    const KEY = 'aiw-state';
    const calm = matchMedia('(prefers-reduced-motion: reduce)').matches;
    const fresh = () => ({ user: urls.user, open: false, ctx: '', items: [] });

    // The conversation survives page changes for this browser tab, but belongs to the logged-in user:
    // a different account on the same tab starts empty, and logging out wipes it.
    let state;
    try { state = JSON.parse(sessionStorage.getItem(KEY)) ?? fresh(); } catch { state = fresh(); }
    if (state.user !== urls.user) state = fresh();
    state.items ??= [];
    document.addEventListener('submit', e => {
        if (/\/auth\/logout/i.test(e.target.action ?? '')) { try { sessionStorage.removeItem(KEY); } catch { } }
    }, true);
    const save = () => {
        try { sessionStorage.setItem(KEY, JSON.stringify({ ...state, items: state.items.slice(-30) })); } catch { }
    };

    const el = (tag, text, cls) => {
        const e = document.createElement(tag);
        if (text) e.textContent = text;
        if (cls) e.className = cls;
        return e;
    };
    const scrollEnd = () => log.scrollTo({ top: log.scrollHeight, behavior: calm ? 'auto' : 'smooth' });
    const bubble = kind => { const m = el('div', '', 'aiw-msg ' + kind); log.append(m); scrollEnd(); return m; };

    // Chart.js is only fetched the first time an answer needs a chart.
    let chartLib;
    const loadChart = () => chartLib ??= window.Chart ? Promise.resolve() : new Promise((ok, fail) => {
        const s = document.createElement('script');
        s.src = 'https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js';
        s.onload = ok; s.onerror = fail;
        document.head.append(s);
    });

    function drawChart(box, r) {
        const canvas = document.createElement('canvas');
        box.append(canvas);
        const ink = getComputedStyle(root).getPropertyValue('--aiw-ink').trim() || '#17402A';
        loadChart().then(() => new Chart(canvas, {
            type: 'bar',
            data: { labels: r.labels, datasets: [{ label: r.title, data: r.values, backgroundColor: ink, borderRadius: 6 }] },
            options: { maintainAspectRatio: false, plugins: { legend: { display: false } },
                       scales: { y: { beginAtZero: true, ticks: { precision: 0 } } } }
        })).catch(() => box.replaceChildren(el('span', 'The chart could not be loaded.', 'aiw-muted')));
    }

    function renderReply(m, r) {
        const parts = [el('strong', r.title)];
        if (r.summary) parts.push(el('p', r.summary));
        if (r.text) parts.push(el('p', r.text));
        if (r.cards?.length) {
            const box = el('div', '', 'aiw-cards');
            r.cards.forEach(c => box.append(el('span', `${c.label}: ${c.value}`)));
            parts.push(box);
        }
        if (r.type === 'table' && r.headers) {
            const table = el('table', '', 'aiw-table');
            [r.headers, ...r.rows].forEach((row, i) => {
                const tr = table.insertRow();
                row.forEach(v => { const c = document.createElement(i === 0 ? 'th' : 'td'); c.textContent = v; tr.append(c); });
            });
            const wrap = el('div', '', 'aiw-table-wrap');
            wrap.append(table);
            parts.push(wrap);
        }
        let chartBox;
        if (r.type === 'chart') { chartBox = el('div', '', 'aiw-chart'); parts.push(chartBox); }
        if (r.notes?.length) {
            const ul = el('ul', '', 'aiw-notes');
            r.notes.forEach(n => ul.append(el('li', n)));
            parts.push(ul);
        }
        const links = [];
        if (r.link) links.push(['Open full report', r.link]);
        if (r.tenderId) {
            links.push(['Excel', `${urls.excelUrl}?tenderId=${r.tenderId}`]);
            links.push(['PDF', `${urls.pdfUrl}?tenderId=${r.tenderId}`]);
        }
        if (links.length) {
            const box = el('div', '', 'aiw-actions');
            links.forEach(([label, href]) => { const a = el('a', label, 'aiw-btn'); a.href = href; box.append(a); });
            parts.push(box);
        }
        m.classList.toggle('wide', r.type === 'table' || r.type === 'chart');
        m.replaceChildren(...parts);
        if (chartBox) drawChart(chartBox, r);
    }

    function dots() {
        const d = el('span', '', 'aiw-dots');
        d.setAttribute('role', 'status');
        d.setAttribute('aria-label', 'Thinking');
        for (let i = 0; i < 3; i++) d.append(document.createElement('span'));
        return d;
    }

    let welcomeBox;
    function showWelcome() {
        welcomeBox = bubble('bot');
        welcomeBox.append(el('span', 'Hi. I answer from live system data, within your role. I only advise; I never decide or change anything.'));
        const chips = el('div', '', 'aiw-chips');
        ['Tender totals', 'Tenders by status', 'What can I ask?'].forEach(q => {
            const b = el('button', q, 'aiw-chip');
            b.type = 'button';
            b.addEventListener('click', () => ask(q));
            chips.append(b);
        });
        welcomeBox.append(chips);
    }

    async function ask(q) {
        q = q.trim();
        if (!q || send.disabled) return;
        welcomeBox?.remove(); welcomeBox = null;
        bubble('user').textContent = q;
        state.items.push({ u: q });
        const m = bubble('bot');
        m.append(dots());
        send.disabled = true;
        input.value = '';

        const fd = new FormData(form);          // carries the anti-forgery token
        fd.set('question', q);
        fd.set('context', state.ctx);
        try {
            const res = await fetch(urls.askUrl, { method: 'POST', body: fd });
            if (res.ok) {
                const r = await res.json();
                state.ctx = r.context ?? state.ctx;     // lets "and the lowest one?" refer back
                renderReply(m, r);
                state.items.push({ r });
            } else {
                m.classList.add('err');
                m.textContent = (await res.text()) || 'Something went wrong. Please try again.';
            }
        } catch {
            m.classList.add('err');
            m.textContent = 'Could not reach the server.';
        }
        send.disabled = false;
        save();
        scrollEnd();
        input.focus();
    }

    function setOpen(open, focusInput) {
        state.open = open;
        root.classList.toggle('is-open', open);
        fab.setAttribute('aria-expanded', String(open));
        fab.setAttribute('aria-label', open ? 'Close AI assistant' : 'Open AI assistant');
        panel.inert = !open;
        save();
        if (open) { log.scrollTop = log.scrollHeight; if (focusInput) setTimeout(() => input.focus(), 120); }
    }

    fab.addEventListener('click', () => setOpen(!state.open, true));
    root.addEventListener('keydown', e => {
        if (e.key === 'Escape' && state.open) { setOpen(false); fab.focus(); }
    });
    form.addEventListener('submit', e => { e.preventDefault(); ask(input.value); });
    $('.aiw-clear').addEventListener('click', () => {
        state = { ...fresh(), open: true };
        log.replaceChildren();
        showWelcome();
        save();
        input.focus();
    });

    // Restore the conversation, then show the panel without animating it on page load.
    root.classList.add('aiw-boot');
    state.items.forEach(it => {
        if (it.u) bubble('user').textContent = it.u;
        else if (it.r) renderReply(bubble('bot'), it.r);
    });
    if (!state.items.length) showWelcome();
    setOpen(!!state.open);
    log.scrollTop = log.scrollHeight;
    requestAnimationFrame(() => requestAnimationFrame(() => root.classList.remove('aiw-boot')));
})();
