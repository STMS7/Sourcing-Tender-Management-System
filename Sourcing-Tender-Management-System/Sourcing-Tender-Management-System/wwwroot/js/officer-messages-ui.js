// Officer Messages: the New message dialog and the inbox filters. Plain JS, one namespace, no dependencies.
(function () {
    'use strict';
    var dialog = document.querySelector('[data-compose-dialog]');

    function openDialog() {
        if (!dialog || dialog.open) { return; }
        if (typeof dialog.showModal === 'function') { dialog.showModal(); } else { dialog.setAttribute('open', ''); }
        var first = dialog.querySelector('select, input:not([type=hidden]), textarea');
        if (first) { first.focus(); }
    }
    function closeDialog() {
        if (!dialog || !dialog.open) { return; }
        if (typeof dialog.close === 'function') { dialog.close(); } else { dialog.removeAttribute('open'); }
    }

    document.addEventListener('click', function (event) {
        var target = event.target;
        if (!(target instanceof Element)) { return; }
        if (target.closest('[data-open-compose]')) { openDialog(); return; }
        if (target.closest('[data-close-compose]')) { closeDialog(); return; }
        // a click on the dimmed backdrop (the dialog element itself, outside its content) closes it
        if (dialog && target === dialog) { closeDialog(); return; }

        var filter = target.closest('[data-filter]');
        if (filter) { applyFilter(filter.getAttribute('data-filter')); }
    });

    // Filters use their own class so they never fight the live search, which toggles the hidden attribute.
    function applyFilter(name) {
        document.querySelectorAll('[data-filter]').forEach(function (button) {
            button.setAttribute('aria-pressed', button.getAttribute('data-filter') === name ? 'true' : 'false');
        });
        var shown = 0;
        document.querySelectorAll('[data-inbox-list] > li').forEach(function (item) {
            var match = name === 'all' || item.getAttribute('data-state') === name;
            item.classList.toggle('is-filtered', !match);
            if (match) { shown++; }
        });
        var empty = document.querySelector('[data-filter-empty]');
        if (empty) { empty.hidden = shown > 0; }
    }

    // After a failed or chosen-context reload the server marks the dialog to open again.
    if (dialog && dialog.getAttribute('data-open') === 'true') { openDialog(); }
})();
