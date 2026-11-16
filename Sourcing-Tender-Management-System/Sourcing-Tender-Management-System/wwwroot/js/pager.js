// Next / Previous paging for long tables and lists. Rows are hidden with a CSS class (never the hidden attribute), so the live
// search, which uses hidden, keeps working: only rows that match the search are counted and paged.
(function () {
    'use strict';
    var SIZE = 4;
    var main = document.querySelector('main.zt-heritage');
    if (!main) { return; }

    function targets() {
        var found = [];
        main.querySelectorAll('table').forEach(function (table) {
            if (table.closest('dialog, [data-no-pager]') || table.hasAttribute('data-no-pager')) { return; }
            var body = table.tBodies[0];
            if (!body) { return; }
            found.push({ holder: table.closest('.table-responsive') || table, items: function () { return Array.prototype.slice.call(body.rows); }, root: body });
        });
        main.querySelectorAll('ul.list-group, ul.ow-tasks').forEach(function (list) {
            if (list.closest('[data-no-pager], dialog, .ow-inbox-list')) { return; }
            found.push({ holder: list, items: function () { return Array.prototype.slice.call(list.children).filter(function (c) { return c.tagName === 'LI'; }); }, root: list });
        });
        return found;
    }

    function enhance(t) {
        if (t.items().length <= SIZE) { return; }
        var page = 1, lastCount = -1;
        var bar = document.createElement('nav');
        bar.className = 'zt-pager';
        bar.setAttribute('aria-label', 'Pagination');
        bar.innerHTML = '<p class="zt-pager-status" role="status" aria-live="polite"></p>' +
            '<div class="zt-pager-buttons"><button type="button" class="zt-pager-prev">Previous</button><span class="zt-pager-pages"></span><button type="button" class="zt-pager-next">Next</button></div>';
        t.holder.insertAdjacentElement('afterend', bar);
        var status = bar.querySelector('.zt-pager-status'), pages = bar.querySelector('.zt-pager-pages');
        var prev = bar.querySelector('.zt-pager-prev'), next = bar.querySelector('.zt-pager-next');

        function render() {
            var visible = t.items().filter(function (r) { return !r.hidden; });
            var count = visible.length, total = Math.max(1, Math.ceil(count / SIZE));
            if (count !== lastCount) { page = 1; lastCount = count; }
            if (page > total) { page = total; }
            t.items().forEach(function (r) { r.classList.remove('zt-paged-out'); });
            visible.forEach(function (r, i) { r.classList.toggle('zt-paged-out', i < (page - 1) * SIZE || i >= page * SIZE); });
            bar.hidden = count <= SIZE;
            var from = count === 0 ? 0 : (page - 1) * SIZE + 1, to = Math.min(count, page * SIZE);
            status.textContent = 'Showing ' + from + '\u2013' + to + ' of ' + count;
            prev.disabled = page <= 1; next.disabled = page >= total;
            pages.innerHTML = '';
            var shown = [];
            for (var n = 1; n <= total; n++) { if (n === 1 || n === total || Math.abs(n - page) <= 1) { shown.push(n); } }
            var last = 0;
            shown.forEach(function (n) {
                if (n - last > 1) { var gap = document.createElement('span'); gap.textContent = '\u2026'; gap.className = 'zt-pager-gap'; pages.appendChild(gap); }
                var b = document.createElement('button');
                b.type = 'button'; b.textContent = n; b.className = 'zt-pager-num';
                b.setAttribute('aria-label', 'Page ' + n);
                if (n === page) { b.setAttribute('aria-current', 'page'); }
                b.addEventListener('click', function () { page = n; render(); });
                pages.appendChild(b); last = n;
            });
        }
        prev.addEventListener('click', function () { if (page > 1) { page--; render(); } });
        next.addEventListener('click', function () { page++; render(); });

        var timer;
        new MutationObserver(function () { clearTimeout(timer); timer = setTimeout(render, 30); })
            .observe(t.root, { subtree: true, attributes: true, attributeFilter: ['hidden'] });
        render();
    }

    targets().forEach(enhance);
})();
