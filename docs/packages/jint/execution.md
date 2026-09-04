# Executing scripts

Use `Execute` for statements and declarations, and `Evaluate` when the resulting JavaScript value matters.

```csharp
var engine = new Engine();

engine.Execute("function square(x) { return x * x; }", "math.js");
var value = engine.Evaluate("square(6)", "request.js");

Console.WriteLine(value.AsNumber()); // 36
```

The optional source name appears in JavaScript stack traces and debugger locations.

## Invoke functions

`Invoke` accepts CLR arguments and converts them. `Call` accepts values already represented as `JsValue`.

```csharp
engine.Execute("function add(a, b) { return a + b; }");

var fromClr = engine.Invoke("add", 2, 3);
var add = engine.GetValue("add");
var fromJsValues = engine.Call(add, 4, 5);
```

A string passed to `Invoke` names one global property; it is not JavaScript source or a dotted path. Read a
nested function first:

```csharp
engine.Execute("var api = { add: (a, b) => a + b };");
var add = engine.GetValue(engine.GetValue("api"), "add");
var result = engine.Invoke(add, 1, 2);
```

Top-level `var` and function declarations create global-object properties. `let`, `const`, and `class` create
global lexical bindings, so retrieve those with `Evaluate("name")`, not `GetValue("name")`.

## Prepare repeated code

Parse once when the same program runs repeatedly:

```csharp
var prepared = Engine.PrepareScript(
    "input.map(x => x * 2)",
    source: "transform.js",
    strict: true);

var result = engine.SetValue("input", new[] { 1, 2, 3 })
    .Evaluate(in prepared);
```

A `Prepared<Script>` is reusable and thread-safe and may be shared across engines. The `JsValue` result is not:
objects belong to the engine and realm that created them. Convert output before crossing that boundary; see
[Working with values](./values.md).

Each top-level `Execute`, `Evaluate`, `Invoke`, or `Call` is a separate run and resets ordinary execution
budgets. See [Execution constraints](./constraints.md) when one host operation makes several calls.
