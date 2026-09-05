# Error handling

JavaScript throws surface as `JavaScriptException`:

```csharp
try
{
    engine.Execute("throw new TypeError('invalid input')", "rules.js");
}
catch (JavaScriptException exception)
{
    Console.WriteLine(exception.Location);
    Console.WriteLine(exception.GetJavaScriptErrorString());
}
```

Use the overload taking `ResultLimits` when error text itself must be bounded.

Promise rejection through async APIs produces the corresponding faulted `Task`; an explicitly unwrapped
rejected promise raises `PromiseRejectedException`. Constraint and parsing-limit exceptions are host control
flow, not catchable script errors.

## CLR exceptions

By default, an exception from a host delegate, reflected member, or proxy trap bubbles directly to the host.
The script cannot catch it:

```csharp
var engine = new Engine()
    .SetValue("parse", new Action<string>(ParseDocument));
```

Opt in when selected CLR failures should become JavaScript `Error` values:

```csharp
var engine = new Engine(options =>
    options.Interop.ExceptionHandler =
        exception => exception is not OperationCanceledException);
```

Use `options.CatchClrExceptions()` when every CLR exception should be converted. Cancellation, timeout, memory,
statement, and recursion-limit exceptions always remain host control flow.

Script-visible messages are redacted by default. The original CLR exception remains host-only and can be
recovered:

```csharp
try
{
    engine.Evaluate(script);
}
catch (JavaScriptException exception)
    when (JintException.TryGetClrException(exception, out var clrException))
{
    logger.LogError(clrException, "Host call failed");
}
```

`ExposeDetailedErrors` restores host exception, loader, and overload-resolution details. Enable it only for
trusted development output because messages may contain paths, URLs, type names, signatures, stack traces, or
secrets.

The JavaScript error retains the original exception and everything reachable from it for as long as that error
remains reachable.

## Throw a JavaScript error from host code

Choose the matching intrinsic so script receives a real standard error:

```csharp
engine.SetValue("itemAt", new Func<int, string>(index =>
{
    if ((uint) index >= (uint) items.Count)
    {
        throw new JavaScriptException(
            engine.Intrinsics.RangeError,
            $"index {index} is out of range");
    }

    return items[index];
}));
```

Available public error constructors include `Error`, `RangeError`, `TypeError`, `ReferenceError`,
`SyntaxError`, `EvalError`, and `UriError`. An overload accepting a CLR exception preserves it for
`TryGetClrException`.

Do not project a CLR exception with `JsValue.FromObject` and throw that value: it is not a JavaScript `Error`
and exposes CLR exception members to script.
