/*
 * Marks the page title (the first h1 or h2 of the page, unless it sits inside a card) so the heritage skin can put the
 * olive sprig and the stitched band on it. Without this script, h1 titles still get them from the stylesheet alone.
 */
(function () {
    'use strict';
    var main = document.querySelector('main.zt-heritage');
    if (!main) { return; }
    var title = main.querySelector('h1, h2');
    if (title && !title.closest('.card, .modal, .alert, .table-responsive')) {
        title.classList.add('zt-page-title');
    }
})();
