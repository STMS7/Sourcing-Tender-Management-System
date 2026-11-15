// Request decision shortcuts keep the original POST forms and their reasons.
document.addEventListener('click', event => {
    const link = event.target.closest?.('[data-open-officer-section]');
    if (!link) return;
    const section = document.getElementById(link.hash.slice(1));
    if (section?.tagName === 'DETAILS') section.open = true;
});
