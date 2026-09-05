# Architecture

Jint parses JavaScript with Acornima and interprets the resulting abstract syntax tree directly. It does not use
the DLR, generate bytecode, or require a native JavaScript runtime.

```text
JavaScript source
      ↓
Acornima parser
      ↓
ECMAScript AST
      ↓
Jint interpreter
      ↓
Runtime, host interop, and optional platform APIs
```

## Architecture topics

Start with the execution model, then use the detailed design records for substantial runtime work:

- [Execution model](execution-model.md)
- [Async Context](../design/async-context.md)
- [Prepared script serialization](../design/prepared-serialization.md)
- [Chrome DevTools Protocol](../design/devtools-protocol.md)
- [Headless browser](../design/headless-browser.md)
- [Web Workers](../design/web-workers.md)

Each record states its own implementation status. Design-only and feasibility documents describe decisions and
tradeoffs; they are not promises that the proposed API is available.
