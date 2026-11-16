// AI assistant popup: loads the bid analysis each time the popup opens.
(function () {
    var root = document.getElementById('bid-analysis');
    if (!root) return;
    var modal = document.getElementById('bid-analysis-modal');
    var body = document.getElementById('bid-analysis-body');
    var loading = false;
    var controller = null;

    // A page ancestor creates a stacking context, which puts Bootstrap's body-level backdrop above the popup
    // (dark screen, nothing clickable). Move the popup to <body> so it sits above the backdrop.
    document.body.appendChild(modal);

    function load() {
        if (loading) return;
        loading = true;
        controller = new AbortController();
        body.innerHTML = '<div class="d-flex align-items-center gap-2 text-muted">' +
            '<div class="spinner-border spinner-border-sm" aria-hidden="true"></div>' +
            '<span>Analyzing bids. This can take up to a minute...</span></div>';
        var token = root.querySelector('input[name="__RequestVerificationToken"]').value;
        fetch(root.dataset.url, { method: 'POST', headers: { 'RequestVerificationToken': token }, signal: controller.signal })
            .then(function (r) {
                if (!r.ok) throw new Error(r.status);
                return r.text();
            })
            .then(function (html) { body.innerHTML = html; })
            .catch(function (err) {
                if (err && err.name === 'AbortError') return; // popup closed: nothing to show
                body.innerHTML = '<div class="alert alert-danger mb-0">The analysis could not be loaded. Close this window and try again.</div>';
            })
            .finally(function () { loading = false; });
    }

    // Closing the popup cancels the running analysis so the server stops too and reopening starts fresh.
    modal.addEventListener('hidden.bs.modal', function () { if (controller) controller.abort(); });
    modal.addEventListener('show.bs.modal', load);
})();
