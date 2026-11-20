(() => {
    'use strict';
    document.querySelectorAll('[data-an-toggle]').forEach(button => {
        button.addEventListener('click', () => {
            const share = button.getAttribute('aria-pressed') !== 'true';
            button.setAttribute('aria-pressed', String(share));
            button.textContent = share ? 'Show counts' : 'Show share';
            button.closest('.an-chart').querySelectorAll('[data-an-count]').forEach(value => {
                value.textContent = share ? value.dataset.anShare : value.dataset.anCount;
            });
        });
    });
    const form = document.getElementById('analytics-filters');
    if (!form) return;
    const from = form.querySelector('[name="From"]');
    const to = form.querySelector('[name="To"]');
    const validate = () => {
        to.setCustomValidity(from.value && to.value && from.value > to.value ? 'The end date must be on or after the start date.' : '');
    };
    from.addEventListener('input', validate);
    to.addEventListener('input', validate);
    validate();
})();
