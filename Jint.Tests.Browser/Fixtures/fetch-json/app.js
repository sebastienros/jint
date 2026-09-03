(function () {
  'use strict';

  async function load() {
    var response = await fetch('/fetch-json/rows.json', { headers: { 'X-Fixture': 'fetch-json' } });
    document.getElementById('status').textContent = response.status + ' ' + response.headers.get('content-type');

    var body = await response.json();
    var list = document.getElementById('rows');
    list.textContent = '';

    for (var row of body.rows) {
      var item = document.createElement('li');
      item.textContent = row.name + ' (' + row.count + ')';
      list.appendChild(item);
    }
  }

  document.getElementById('reload').addEventListener('click', function () { load(); });

  // A relative URL, resolved against the document, and a promise chain the page's own loop has to run.
  load();
})();
