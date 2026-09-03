// TodoMVC on React 18, written against React.createElement so the fixture needs no build step and no
// transform: what is checked in is what runs. The markup is TodoMVC's, which is what makes the four
// framework fixtures one test body rather than four.
(function () {
  'use strict';

  var h = React.createElement;
  var useState = React.useState;
  var useEffect = React.useEffect;

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

    function toggle(id) {
      setTodos(todos.map(function (todo) {
        return todo.id === id ? { id: todo.id, title: todo.title, done: !todo.done } : todo;
      }));
    }

    function destroy(id) {
      setTodos(todos.filter(function (todo) { return todo.id !== id; }));
    }

    function row(todo) {
      return h('li', { key: todo.id, className: todo.done ? 'completed' : '', 'data-id': String(todo.id) }, [
        h('input', {
          key: 'toggle',
          className: 'toggle',
          type: 'checkbox',
          checked: todo.done,
          onChange: function () { toggle(todo.id); },
        }),
        h('label', { key: 'label' }, todo.title),
        h('button', { key: 'destroy', className: 'destroy', onClick: function () { destroy(todo.id); } }, 'x'),
      ]);
    }

    function link(name, label, href) {
      return h('li', { key: name }, h('a', { className: filter === name ? 'selected' : '', href: href }, label));
    }

    return h('section', { className: 'todoapp' }, [
      h('header', { key: 'header', className: 'header' }, [
        h('h1', { key: 'title' }, 'todos'),
        h('input', {
          key: 'new',
          className: 'new-todo',
          placeholder: 'What needs to be done?',
          value: draft,
          onChange: function (event) { setDraft(event.target.value); },
          onKeyDown: add,
        }),
      ]),
      h('section', { key: 'main', className: 'main' },
        h('ul', { className: 'todo-list' }, visible.map(row))),
      h('footer', { key: 'footer', className: 'footer' }, [
        h('span', { key: 'count', className: 'todo-count' }, [
          h('strong', { key: 'n' }, String(remaining)),
          ' items left',
        ]),
        h('ul', { key: 'filters', className: 'filters' }, [
          link('all', 'All', '#/'),
          link('active', 'Active', '#/active'),
          link('completed', 'Completed', '#/completed'),
        ]),
      ]),
    ]);
  }

  ReactDOM.createRoot(document.getElementById('root')).render(h(App));
})();
