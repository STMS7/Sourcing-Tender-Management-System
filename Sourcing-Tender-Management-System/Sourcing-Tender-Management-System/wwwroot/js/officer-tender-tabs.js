// Tender details tabs. Without this script every section is simply shown one after another.
(function () {
    'use strict';
    var tablist = document.querySelector('[data-od-tabs]');
    var root = document.querySelector('.od');
    if (!tablist || !root) { return; }
    var tabs = Array.prototype.slice.call(tablist.querySelectorAll('[role="tab"]'));
    var panels = Array.prototype.slice.call(root.querySelectorAll('[data-panel]'));
    var storeKey = 'stms.tender.' + (root.getAttribute('data-tender-id') || '') + '.tab';
    var searching = false;

    function remember(name) { try { sessionStorage.setItem(storeKey, name); } catch (e) { /* optional */ } }
    function recall() { try { return sessionStorage.getItem(storeKey); } catch (e) { return null; } }

    function show(name, focus, allowHidden) {
        if (name === 'evaluation-handoff') { name = 'evaluation'; }
        if (name.indexOf('tab-') === 0) { name = name.substring(4); }
        if (!panels.some(function (p) { return p.getAttribute('data-panel') === name; })) { name = 'overview'; }
        if (!allowHidden && !tabs.some(function (tab) { return tab.getAttribute('data-tab') === name; })) { name = 'overview'; }
        tabs.forEach(function (tab) {
            var on = tab.getAttribute('data-tab') === name;
            tab.setAttribute('aria-selected', on ? 'true' : 'false');
            tab.tabIndex = on ? 0 : -1;
            if (on && focus) { tab.focus(); }
        });
        panels.forEach(function (panel) { panel.hidden = !searching && panel.getAttribute('data-panel') !== name; });
        remember(name);
    }

    tabs.forEach(function (tab, index) {
        tab.addEventListener('click', function () { show(tab.getAttribute('data-tab')); });
        tab.addEventListener('keydown', function (event) {
            var next = event.key === 'ArrowRight' ? index + 1 : event.key === 'ArrowLeft' ? index - 1 :
                event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1 : -1;
            if (next < 0) { return; }
            event.preventDefault();
            show(tabs[(next + tabs.length) % tabs.length].getAttribute('data-tab'), true);
        });
    });

    // links elsewhere on the page that open a tab (quick actions)
    document.addEventListener('click', function (event) {
        var link = event.target instanceof Element ? event.target.closest('[data-goto-tab]') : null;
        if (!link) { return; }
        event.preventDefault();
        show(link.getAttribute('data-goto-tab'), false, true);
        tablist.scrollIntoView({ block: 'start', behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
    });

    // while a search term is typed every section stays visible so results are never hidden in another tab
    var search = document.getElementById('tender-details-search');
    if (search) {
        search.addEventListener('input', function () {
            searching = search.value.trim().length > 0;
            var current = tablist.querySelector('[aria-selected="true"]');
            show(current ? current.getAttribute('data-tab') : 'overview');
        });
    }

    tablist.hidden = false;
    var wanted = (location.hash || '').replace('#', '') || recall() || 'overview';
    show(wanted);
    window.addEventListener('hashchange', function () { show((location.hash || '#overview').slice(1)); });
})();
