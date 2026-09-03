(function () {
  'use strict';

  var h = React.createElement;

  // Every node the server wrote is stamped before hydration begins. React adopts a matching node and
  // replaces a mismatching one, so a stamp that survives is proof that this really was a hydration and not a
  // silent re-render of the same markup -- which is the one thing a DOM assertion alone cannot tell apart.
  window.__served = [];
  var served = document.querySelectorAll('#root *');
  for (var i = 0; i < served.length; i++) {
    served[i].__fromServer = true;
    window.__served.push(served[i]);
  }

  // React reports a hydration mismatch here rather than throwing, so this is the assertion the fixture is
  // built around: it has to stay empty.
  window.__recovered = [];

  function App(props) {
    var state = React.useState(props.start);
    var count = state[0];
    var setCount = state[1];

    return h('div', { className: 'app' },
      h('h1', { key: 'h' }, 'Hydrated'),
      h('p', { key: 'p', id: 'count' }, 'count: ' + count),
      h('button', { key: 'b', id: 'inc', onClick: function () { setCount(count + 1); } }, 'increment'),
      h('ul', { key: 'u', className: 'notes' }, props.notes.map(function (note) {
        return h('li', { key: note }, note);
      })));
  }

  ReactDOM.hydrateRoot(
    document.getElementById('root'),
    h(App, { start: 2, notes: ['alpha', 'beta'] }),
    {
      onRecoverableError: function (error) {
        window.__recovered.push(String(error && error.message ? error.message : error));
      },
    });

  window.__stampsKept = function () {
    return window.__served.filter(function (node) { return node.__fromServer === true; }).length;
  };
})();
