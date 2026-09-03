// A router of the shape every SPA has: links intercepted, `history.pushState` for the move, `popstate` for
// the traversal back. Hand-written rather than vendored, because what is under test is the four browser
// pieces it stands on -- a click's default action, pushState, the URL the document reads afterwards, and the
// event a traversal fires -- and no router library adds anything to that.
(function () {
  'use strict';

  var views = {
    '/spa-router/index.html': 'the home view',
    '/spa-router/about': 'the about view',
    '/spa-router/contact': 'the contact view',
  };

  var pops = 0;

  function render() {
    var path = location.pathname;
    document.getElementById('view').textContent = views[path] || ('no view for ' + path);
    document.getElementById('path').textContent = path;
    document.getElementById('pops').textContent = String(pops);
  }

  document.addEventListener('click', function (event) {
    var link = event.target.closest ? event.target.closest('a.route') : null;
    if (link === null) {
      return;
    }

    // Exactly what a router does: refuse the navigation the browser was about to make, and make its own.
    event.preventDefault();
    history.pushState({ from: 'router' }, '', link.getAttribute('href'));
    render();
  });

  window.addEventListener('popstate', function () {
    pops += 1;
    render();
  });

  render();
})();
