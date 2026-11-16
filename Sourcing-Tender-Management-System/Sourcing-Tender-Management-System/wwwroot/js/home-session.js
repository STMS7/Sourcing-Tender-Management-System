(() => {
    'use strict';
    const exit = document.querySelector('form[data-home-exit]');
    if (exit) {
        // Logout remains a same-origin POST with its server-generated CSRF token.
        // submit() is intentional: this is a form with no user-editable fields.
        HTMLFormElement.prototype.submit.call(exit);
        return;
    }
    // A history snapshot is not evidence of a live session. Revalidate the page
    // with the server, including a Home snapshot created before the user signed in.
    addEventListener('pageshow', event => {
        if (event.persisted) location.reload();
    });
})();
