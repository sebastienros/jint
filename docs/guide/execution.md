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

## Evaluate an expression against a context object

To evaluate an expression against a context object without defining globals, compile it once as a function
that takes the context as a parameter, then call it with each context:

<!-- snippet: guide-evaluate-with-context -->
```csharp
var engine = new Engine();
var expression = "ctx.price * ctx.quantity";

// Compile once. Evaluating a function expression defines no globals.
var total = engine.Evaluate($"(ctx) => ({expression})");

// Invoke converts a CLR argument; Call takes a JsValue, such as one a host function received.
var fromClr = engine.Invoke(total, new { price = 4, quantity = 3 }); // 12
var fromScript = total.Call(engine.Evaluate("({ price: 5, quantity: 2 })")); // 10
```
<!-- endSnippet -->

For bare names (`price * quantity` instead of `ctx.price * ctx.quantity`), wrap the expression in `with`. A
name the context lacks falls through to the globals, and `with` is a syntax error in strict code, so this
works only when `Options.Strict` is off, the default:

<!-- snippet: guide-evaluate-with-scope -->
```csharp
var total = engine.Evaluate("(function (scope) { with (scope) { return (price * quantity); } })");
var result = engine.Invoke(total, new { price = 4, quantity = 3 }); // 12
```
<!-- endSnippet -->

When each run creates a new engine, prepare the function once in a static field. Each engine evaluates it into
its own function, because a `JsValue` must never be shared across engines:

<!-- snippet: guide-context-across-engines -->
```csharp
// Parsed once and shared by every engine.
private static readonly Prepared<Script> Total =
    Engine.PrepareScript("(ctx) => (ctx.price * ctx.quantity)");

public static JsValue EvaluateTotal(Engine engine, JsValue ctx)
{
    // The function belongs to this engine; cache it per engine, never in a static.
    var total = engine.Evaluate(in Total);
    return total.Call(ctx);
}
```
<!-- endSnippet -->

A host function cannot see the local variables of the script that called it; only `eval` captures the
caller's scope. Pass the context explicitly, as above.
