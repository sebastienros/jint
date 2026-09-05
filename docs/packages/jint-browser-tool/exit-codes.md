# Exit codes

The tool's exit codes distinguish caller, navigation, budget, and page-expression failures:

| Code | Meaning |
| ---: | --- |
| `0` | The command completed and answered |
| `1` | Usage was wrong, or a named file, option, address, or scheme was invalid |
| `2` | No document was available: URL refusal, transport failure, or navigation timeout |
| `3` | The page loaded, but a page turn exceeded its time or allocation budget |
| `4` | The expression passed to `eval` threw |

An HTTP `404` or `500` returns code `0` when its response body becomes a document. Status codes describe the loaded page; they are not transport failures.

Page script errors that do not prevent a result are printed to standard error. A budget error can therefore accompany useful `fetch` output on standard output and still return `3`.

Example shell handling:

```bash
set +e
jint-browser fetch "$url" > page.md
code=$?
if [ "$code" -ne 0 ]; then
  echo "jint-browser failed with $code" >&2
fi
```

Do not blindly retry code `1`; the command line must change. Code `2` may represent a transient navigation failure.
