# Evaluate

`eval` loads a page, evaluates one JavaScript expression, and writes JSON:

```bash
jint-browser eval https://example.org/ "document.title"
jint-browser eval https://example.org/ \
  "[...document.querySelectorAll('article')].map(x => x.textContent)"
```

The expression is parenthesized and evaluated in the page. `JSON.stringify` also runs in the page, so `Date`, `toJSON`, `NaN`, and other JavaScript semantics apply. Values for which `JSON.stringify` returns `undefined`—such as top-level `undefined`, functions, and symbols—are written as `null`.

This command evaluates an expression, not an arbitrary statement list. A bare `await` is not enabled. Use an expression that returns the current value after the selected load state.

`eval` accepts the same loading and browser options as `fetch`:

```bash
jint-browser eval https://example.org/app \
  "document.querySelectorAll('.row').length" \
  --wait-until networkidle \
  --timeout 30s \
  --header 'X-Test: true' \
  --cookie session=abc
```

If an expression begins with a dash, end option parsing first:

```bash
jint-browser eval https://example.org/ -- "-1 + 2"
```

An expression that throws exits with code `4` and writes the error to standard error.
