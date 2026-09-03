(function () {
  'use strict';

  var batches = 0;

  var observer = new MutationObserver(function (records) {
    batches += 1;

    for (var record of records) {
      if (record.type === 'attributes' && record.attributeName === 'data-state') {
        document.getElementById('state').textContent =
          record.oldValue + ' -> ' + record.target.getAttribute('data-state');
      }

      if (record.type === 'characterData') {
        document.getElementById('text').textContent = record.oldValue + ' -> ' + record.target.data;
      }
    }

    document.getElementById('count').textContent = String(document.querySelectorAll('#items li').length);
    document.getElementById('batches').textContent = String(batches);
  });

  observer.observe(document.getElementById('widget'), {
    childList: true,
    subtree: true,
    attributes: true,
    attributeOldValue: true,
    attributeFilter: ['data-state'],
    characterData: true,
    characterDataOldValue: true,
  });

  // What the page itself does to the tree, so a test can make the same mutations from outside and see the
  // same widget react.
  window.__add = function (text) {
    var item = document.createElement('li');
    item.textContent = text;
    document.getElementById('items').appendChild(item);
  };

  window.__records = function () {
    return observer.takeRecords().length;
  };
})();
