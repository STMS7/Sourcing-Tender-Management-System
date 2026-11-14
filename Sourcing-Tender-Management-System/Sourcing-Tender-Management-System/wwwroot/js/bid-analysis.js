// AI assistant popup: loads the bid analysis each time the popup opens.
(function () {
    var root = document.getElementById('bid-analysis');
    if (!root) return;
    var modal = document.getElementById('bid-analysis-modal');
    var body = document.getElementById('bid-analysis-body');
    var loading = false;

    function load() {
        if (loading) return;
        loading = true;
        body.innerHTML = '<div class="d-flex align-items-center gap-2 text-muted">' +
            '<div class="spinner-border spinner-border-sm" aria-hidden="true"></div>' +
            '<span>Analyzing bids. This can take up to a minute...</span></div>';
        var token = root.querySelector('input[name="__RequestVerificationToken"]').value;
        fetch(root.dataset.url, { method: 'POST', headers: { 'RequestVerificationToken': token } })
            .then(function (r) {
                if (!r.ok) throw new Error(r.status);
                return r.text();
            })
            .then(function (html) { body.innerHTML = html; })
            .catch(function () {
                body.innerHTML = '<div class="alert alert-danger mb-0">The analysis could not be loaded. Close this window and try again.</div>';
            })
            .finally(function () { loading = false; });
    }

    modal.addEventListener('show.bs.modal', load);
})();
