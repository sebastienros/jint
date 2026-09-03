(function () {
  'use strict';

  function currentFilter() {
    var hash = location.hash.replace(/^#\/?/, '');
    return hash === 'active' || hash === 'completed' ? hash : 'all';
  }

  Vue.createApp({
    data: function () {
      return { todos: [], draft: '', filter: currentFilter(), nextId: 1 };
    },
    computed: {
      visible: function () {
        var filter = this.filter;
        return this.todos.filter(function (todo) {
          return filter === 'all' || (filter === 'active' ? !todo.done : todo.done);
        });
      },
      remaining: function () {
        return this.todos.filter(function (todo) { return !todo.done; }).length;
      },
    },
    mounted: function () {
      var self = this;
      window.addEventListener('hashchange', function () { self.filter = currentFilter(); });
    },
    methods: {
      add: function () {
        var title = this.draft.trim();
        if (title.length === 0) {
          return;
        }

        this.todos.push({ id: this.nextId++, title: title, done: false });
        this.draft = '';
      },
      destroy: function (todo) {
        this.todos.splice(this.todos.indexOf(todo), 1);
      },
    },
  }).mount('#app');
})();
