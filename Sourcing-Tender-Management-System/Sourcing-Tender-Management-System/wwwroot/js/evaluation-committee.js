// Evaluation committee: fill each progress ring and count its number up on page load.
(function () {
  var still = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  document.querySelectorAll('.ec-ring[data-p]').forEach(function (ring) {
    var p = Math.max(0, Math.min(100, Number(ring.dataset.p) || 0));
    var arc = ring.querySelector('.arc');
    var out = ring.querySelector('.ec-ring-count');
    if (!arc || !out) return;
    if (still) { arc.style.strokeDashoffset = 100 - p; out.textContent = p + '%'; return; }
    requestAnimationFrame(function () { arc.style.strokeDashoffset = 100 - p; });
    var start = performance.now() + 200, duration = 1400;
    function tick(now) {
      var k = Math.min(1, Math.max(0, (now - start) / duration));
      out.textContent = Math.round(p * (1 - Math.pow(1 - k, 3))) + '%';
      if (k < 1) requestAnimationFrame(tick);
    }
    requestAnimationFrame(tick);
  });
})();
