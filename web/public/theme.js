// Sets the colour theme before the first paint. A file (not inline) so a strict CSP can allow it.
(function () {
  var media = window.matchMedia('(prefers-color-scheme: dark)');
  function saved() {
    try { return localStorage.getItem('theme'); } catch (e) { return null; }
  }
  window.applyTheme = function () {
    var choice = saved();
    var dark = choice === 'dark' || (choice !== 'light' && media.matches);
    document.documentElement.classList.toggle('dark', dark);
  };
  window.applyTheme();
  media.addEventListener('change', window.applyTheme);
  window.addEventListener('storage', function (e) { if (e.key === 'theme') window.applyTheme(); });
  // Pages restored from the back/forward cache may show a stale signed-in state: reload them.
  window.addEventListener('pageshow', function (e) { if (e.persisted) location.reload(); });
})();
