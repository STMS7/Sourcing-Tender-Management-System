(() => {
    const contextForm = document.querySelector('[data-message-context]');
    contextForm?.querySelector('select').addEventListener('change', () => {
        if (contextForm.reportValidity()) contextForm.requestSubmit();
    });
    const history = document.querySelector('[data-message-thread]');
    if (history) history.scrollTop = history.scrollHeight;
})();
