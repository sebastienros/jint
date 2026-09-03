// The same TodoMVC as the React fixture, on Preact's own `h` and hooks rather than React's -- which is what
// makes the pair worth having: Preact writes to the DOM directly instead of through a synthetic event system
// and a scheduler, so a defect one of them finds and the other does not is a defect in a named layer.
(function () {
  'use strict';

  var h = preact.h;
  var useState = preactHooks.useState;
  var useEffect = preactHooks.useEffect;

  function currentFilter() {
    var hash = location.hash.replace(/^#\/?/, '');
    return hash === 'active' || hash === 'completed' ? hash : 'all';
  }

  function App() {
    var todosState = useState([]);
    var todos = todosState[0];
    var setTodos = todosState[1];

    var draftState = useState('');
    var draft = draftState[0];
    var setDraft = draftState[1];

    var filterState = useState(currentFilter());
    var filter = filterState[0];
    var setFilter = filterState[1];

    useEffect(function () {
      var onHashChange = function () { setFilter(currentFilter()); };
      window.addEventListener('hashchange', onHashChange);
      return function () { window.removeEventListener('hashchange', onHashChange); };
    }, []);

    var visible = todos.filter(function (todo) {
      return filter === 'all' || (filter === 'active' ? !todo.done : todo.done);
    });

    var remaining = todos.filter(function (todo) { return !todo.done; }).length;

    function add(event) {
      if (event.key !== 'Enter') {
        return;
      }

      var title = draft.trim();
      if (title.length === 0) {
        return;
      }

      setTodos(todos.concat([{ id: Date.now() + Math.random(), title: title, done: false }]));
      setDraft('');
    }

    function row(todo) {
      return h('li', { key: todo.id, class: todo.done ? 'completed' : '', 'data-id': String(todo.id) },
        h('input', {
          class: 'toggle',
          type: 'checkbox',
          checked: todo.done,
          onChange: function () {
            setTodos(todos.map(function (each) {
              return each.id === todo.id ? { id: each.id, title: each.title, done: !each.done } : each;
            }));
          },
        }),
        h('label', null, todo.title),
        h('button', {
          class: 'destroy',
          onClick: function () {
            setTodos(todos.filter(function (each) { return each.id !== todo.id; }));
          },
        }, 'x'));
    }

    function link(name, label, href) {
      return h('li', { key: name }, h('a', { class: filter === name ? 'selected' : '', href: href }, label));
    }

    return h('section', { class: 'todoapp' },
      h('header', { class: 'header' },
        h('h1', null, 'todos'),
        h('input', {
          class: 'new-todo',
          placeholder: 'What needs to be done?',
          value: draft,
          onInput: function (event) { setDraft(event.target.value); },
          onKeyDown: add,
        })),
      h('section', { class: 'main' },
        h('ul', { class: 'todo-list' }, visible.map(row))),
      h('footer', { class: 'footer' },
        h('span', { class: 'todo-count' }, h('strong', null, String(remaining)), ' items left'),
        h('ul', { class: 'filters' },
          link('all', 'All', '#/'),
          link('active', 'Active', '#/active'),
          link('completed', 'Completed', '#/completed'))));
  }

  preact.render(h(App), document.getElementById('root'));
})();
