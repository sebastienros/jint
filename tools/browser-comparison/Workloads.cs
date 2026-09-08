namespace BrowserComparison;

/// <summary>Each lane loads the same offline bytes and checks an independently specified result.</summary>
internal sealed record WorkloadDefinition(string Html, string Script, string Expected);

internal static class Workloads
{
    internal static readonly string[] Names = ["parse-extract", "mutate-query", "event-form", "fetch-update", "interpreter-control"];

    internal static WorkloadDefinition Get(string name) => name switch
    {
        "parse-extract" => new("<!doctype html><title>Static inventory</title><main>" +
            string.Concat(Enumerable.Range(0, 1000).Select(i => $"<article data-value='{i}'><h2>Item {i}</h2><p>Inventory</p></article>")) + "</main>",
            "(() => { const a = [...document.querySelectorAll('article')]; return a.length + '|' + a.reduce((s, n) => s + Number(n.dataset.value), 0) + '|' + a[999].querySelector('h2').textContent; })()",
            "1000|499500|Item 999"),
        "mutate-query" => new("<!doctype html><title>Mutable inventory</title><main><h1>Inventory</h1><ul id='items'></ul></main>",
            """
            (() => {
                const list = document.querySelector('#items');
                for (let i = 0; i < 1000; i++) {
                    const item = document.createElement('li');
                    item.dataset.value = String(i); item.textContent = 'Item ' + i;
                    list.appendChild(item);
                }
                for (const item of [...list.children]) if (Number(item.dataset.value) % 2 === 0) item.remove();
                const items = [...document.querySelectorAll('#items li')];
                document.querySelector('h1').textContent = 'Verified ' + items.length;
                return items.length + '|' + items.reduce((s, n) => s + Number(n.dataset.value), 0) + '|' + document.querySelector('h1').textContent;
            })()
            """, "500|250000|Verified 500"),
        "event-form" => new("<!doctype html><title>Form events</title><form><input name='query'><button type='button'>Apply</button><output></output></form>",
            """
            (() => {
                const form = document.querySelector('form'), input = form.querySelector('input');
                let count = 0;
                form.addEventListener('input', e => { if (e.target === input) count++; });
                for (let i = 0; i < 100; i++) { input.value = 'query-' + i; input.dispatchEvent(new Event('input', { bubbles: true })); }
                form.querySelector('output').textContent = input.value;
                return count + '|' + new FormData(form).get('query') + '|' + form.querySelector('output').textContent;
            })()
            """, "100|query-99|query-99"),
        "fetch-update" => new("<!doctype html><title>Async inventory</title><main><output></output></main>",
            """
            (async () => {
                const data = await (await fetch('/data')).json();
                const sum = data.values.reduce((s, n) => s + n, 0);
                document.querySelector('output').textContent = String(sum);
                await Promise.resolve();
                return data.values.length + '|' + document.querySelector('output').textContent;
            })()
            """, "100|4950"),
        "interpreter-control" => new("<!doctype html><title>Script control</title>",
            "(() => { let sum = 0; for (let i = 0; i < 100000; i++) sum += i % 97; return String(sum); })()", "4799685"),
        _ => throw new ArgumentException($"Unknown workload: {name}"),
    };
}
