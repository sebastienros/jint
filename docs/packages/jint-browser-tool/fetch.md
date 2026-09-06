# Fetch

Load a page and write its final DOM in a text-first form:

```bash
jint-browser fetch https://example.org/article
jint-browser fetch ./page.html --dump text
jint-browser fetch https://example.org/ --dump ax
```

`--dump` accepts:

- `markdown` — CommonMark; the default.
- `text` — document text without markdown formatting.
- `html` — serialized document markup after scripts ran.
- `ax` — an indented accessibility outline.

Narrow and cap text-based output:

```bash
jint-browser fetch https://example.org/article \
  --main-content \
  --max-length 4000
```

`--main-content` selects the first `<main>`, `[role=main]`, or `<article>`. Truncated output ends with `[truncated]`. Both options are rejected with `--dump html`, because narrowed or truncated markup would not be the whole document.

## Loading options

```bash
jint-browser fetch https://example.org/app \
  --wait-until networkidle \
  --timeout 60s \
  --header 'Authorization: Bearer token' \
  --cookie session=abc
```

`--wait-until` is `commit`, `domcontentloaded`, `load` (default), or `networkidle`. The last waits for load and then half a second without request activity. `--header` and `--cookie` are repeatable.

Page diagnostics go to standard error; the requested document still goes to standard output when possible.
