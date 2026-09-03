<script>
  let todos = $state([]);
  let draft = $state('');
  let filter = $state(readFilter());
  let nextId = 1;

  function readFilter() {
    const hash = (typeof location === 'undefined' ? '' : location.hash).replace(/^#\/?/, '');
    return hash === 'active' || hash === 'completed' ? hash : 'all';
  }

  $effect(() => {
    const onHashChange = () => { filter = readFilter(); };
    window.addEventListener('hashchange', onHashChange);
    return () => window.removeEventListener('hashchange', onHashChange);
  });

  const visible = $derived(todos.filter(t => filter === 'all' || (filter === 'active' ? !t.done : t.done)));
  const remaining = $derived(todos.filter(t => !t.done).length);

  function add(event) {
    if (event.key !== 'Enter') return;
    const text = draft.trim();
    if (text.length === 0) return;
    todos.push({ id: nextId++, title: text, done: false });
    draft = '';
  }

  function destroy(id) {
    const at = todos.findIndex(t => t.id === id);
    if (at >= 0) todos.splice(at, 1);
  }
</script>

<section class="todoapp">
  <header class="header">
    <h1>todos</h1>
    <input class="new-todo" placeholder="What needs to be done?" bind:value={draft} onkeydown={add} />
  </header>
  <section class="main">
    <ul class="todo-list">
      {#each visible as todo (todo.id)}
        <li class:completed={todo.done} data-id={todo.id}>
          <input class="toggle" type="checkbox" bind:checked={todo.done} />
          <label>{todo.title}</label>
          <button class="destroy" onclick={() => destroy(todo.id)}>x</button>
        </li>
      {/each}
    </ul>
  </section>
  <footer class="footer">
    <span class="todo-count"><strong>{remaining}</strong> items left</span>
    <ul class="filters">
      <li><a class:selected={filter === 'all'} href="#/">All</a></li>
      <li><a class:selected={filter === 'active'} href="#/active">Active</a></li>
      <li><a class:selected={filter === 'completed'} href="#/completed">Completed</a></li>
    </ul>
  </footer>
</section>
