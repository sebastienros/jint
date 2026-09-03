// The infinite-scroll shape, and the one fixture whose *documented* behaviour is a divergence.
//
// This browser has no layout and no viewport to scroll, so `IntersectionObserver` reports every observed
// target exactly once, fully intersecting, and never again. The consequence is not subtle and is not hidden:
// a lazy list that fetches the next page whenever its sentinel is seen fetches *every* page, immediately.
// That is the behaviour the package chose on purpose -- "never intersecting" would leave every lazy list and
// every reveal-on-scroll panel permanently empty, which is worse for a reader than loading everything -- and
// this fixture is where it is stated and asserted rather than discovered.
(function () {
  'use strict';

  var pagesAvailable = 3;
  var loaded = 0;

  var lazy = new IntersectionObserver(function (entries) {
    for (var entry of entries) {
      if (!entry.isIntersecting) {
        continue;
      }

      var page = Number(entry.target.getAttribute('data-page'));
      lazy.unobserve(entry.target);
      entry.target.remove();
      load(page);
    }
  });

  function load(page) {
    for (var i = 1; i <= 2; i++) {
      var row = document.createElement('li');
      row.textContent = 'page ' + page + ' row ' + i;
      document.getElementById('rows').appendChild(row);
    }

    loaded = page;
    document.getElementById('pages').textContent = String(loaded);

    if (page >= pagesAvailable) {
      return;
    }

    // A fresh sentinel for the next page, which is what a real list does too -- and which is what makes
    // "reported once" load the whole list here rather than stopping after one page.
    var next = document.createElement('div');
    next.className = 'sentinel';
    next.setAttribute('data-page', String(page + 1));
    document.getElementById('sentinel-host').appendChild(next);
    lazy.observe(next);
  }

  lazy.observe(document.querySelector('.sentinel'));

  // The reveal-on-scroll half: an element that is invisible until it is seen. With no scrolling it is seen
  // at once, which is the readable outcome rather than the correct one.
  var reveal = new IntersectionObserver(function (entries, observer) {
    for (var entry of entries) {
      if (entry.isIntersecting) {
        entry.target.classList.add('visible');
        document.getElementById('revealed').textContent =
          'yes at ratio ' + entry.intersectionRatio + ' of ' + Math.round(entry.boundingClientRect.width) + 'px';
        observer.unobserve(entry.target);
      }
    }
  }, { threshold: [0, 0.5, 1] });

  reveal.observe(document.getElementById('reveal'));
})();
