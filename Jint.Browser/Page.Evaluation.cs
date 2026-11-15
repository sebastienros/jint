using System.Text.Json;
using Acornima.Ast;
using Jint.Native;
using Jint.Runtime;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Jint.Browser;

public sealed partial class Page
{
    /// <summary>Invokes a JavaScript function expression with one JSON-data argument, without source interpolation.</summary>
    /// <remarks>
    /// The argument is snapshotted as JSON data before posting, then converted directly on the page thread.
    /// No CLR object is exposed to script. Results use JSON snapshot semantics: objects become dictionaries,
    /// arrays become object arrays, numbers become doubles, and null/undefined become null.
    /// Primitive results convert directly; structured results serialize and parse JSON.
    /// Functions and other non-JSON members are omitted as by JSON.stringify; cycles and BigInt throw.
    /// Promises are not awaited. Neither DOM handles nor callable delegates leave the page thread.
    /// </remarks>
    public Task<object?> EvaluateAsync(string function, object? argument)
    {
        ArgumentNullException.ThrowIfNull(function);
        ObjectDisposedException.ThrowIf(_closed, this);
        var snapshot = JsonSerializer.SerializeToElement(argument);
        return _loop.PostAsync(engine => SnapshotResult(engine, InvokeWithArgument(engine, function, snapshot)));
    }

    /// <summary>Invokes a JavaScript function with one JSON-data argument and converts its detached result to T.</summary>
    /// <remarks>Uses the JSON snapshot semantics of the argument-taking non-generic overload.</remarks>
    public Task<T?> EvaluateAsync<T>(string function, object? argument)
    {
        ArgumentNullException.ThrowIfNull(function);
        ObjectDisposedException.ThrowIf(_closed, this);
        var snapshot = JsonSerializer.SerializeToElement(argument);
        return _loop.PostAsync(engine => ConvertResult<T>(SnapshotResult(engine, InvokeWithArgument(engine, function, snapshot))));
    }

    /// <summary>Invokes a JavaScript function with one JSON-data argument and awaits a promise result.</summary>
    /// <remarks>
    /// Uses the argument snapshot and detached result semantics of the argument-taking EvaluateAsync overload.
    /// The page loop remains available while a promise is pending. Rejection faults the task; cancellation,
    /// closing the page or replacing its document ends a pending wait without rebinding it to another engine.
    /// </remarks>
    public Task<object?> EvaluateAndAwaitAsync(string function, object? argument, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(function);
        ObjectDisposedException.ThrowIf(_closed, this);
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = JsonSerializer.SerializeToElement(argument);
        return EvaluateAndAwaitCoreAsync(engine => InvokeWithArgument(engine, function, snapshot), SnapshotResult, cancellationToken);
    }

    /// <summary>Invokes a JavaScript function with one JSON-data argument, awaits its result and converts the detached data to T.</summary>
    /// <remarks>Uses the snapshot, promise ownership and cancellation semantics of the argument-taking non-generic overload.</remarks>
    public Task<T?> EvaluateAndAwaitAsync<T>(string function, object? argument, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(function);
        ObjectDisposedException.ThrowIf(_closed, this);
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = JsonSerializer.SerializeToElement(argument);
        return EvaluateAndAwaitCoreAsync(engine => InvokeWithArgument(engine, function, snapshot),
            static (engine, value) => ConvertResult<T>(SnapshotResult(engine, value)), cancellationToken);
    }

    /// <summary>Waits for a JavaScript predicate function invoked with one snapshotted JSON-data argument.</summary>
    /// <remarks>
    /// Parses the function once per document, then invokes a fresh function and argument on each poll.
    /// Navigation rebinds against the new document. Transient JavaScript errors follow the expression wait's
    /// retry policy; a final error is rethrown at timeout. The argument is never interpolated into source.
    /// </remarks>
    public Task<bool> WaitForAsync(string predicate, object? argument, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ObjectDisposedException.ThrowIf(_closed, this);
        var snapshot = JsonSerializer.SerializeToElement(argument);
        return WaitForScriptAsync("(" + predicate + "\n)", snapshot, timeout);
    }

    private static JsValue InvokeWithArgument(Engine engine, string function, JsonElement argument)
        => engine.Call(engine.Evaluate("(" + function + "\n)"), JsValue.Undefined, [BindArgument(engine, argument)]);

    private async Task<bool> WaitForScriptAsync(string source, JsonElement? argument, TimeSpan timeout)
    {
        Engine? preparedFor = null;
        Prepared<Script> prepared = default;
        Exception? lastFailure = null;
        var answered = await WaitForAsync(engine =>
        {
            try
            {
                if (!ReferenceEquals(preparedFor, engine))
                {
                    prepared = engine.ParseForExecution(source, source: null, parsingOptions: null);
                    preparedFor = engine;
                }
                var result = engine.Evaluate(prepared);
                if (argument is { } snapshot)
                    result = engine.Call(result, JsValue.Undefined, [BindArgument(engine, snapshot)]);
                lastFailure = null;
                return TypeConverter.ToBoolean(result);
            }
            catch (JavaScriptException exception)
            {
                lastFailure = exception;
                return false;
            }
        }, timeout).ConfigureAwait(false);
        return answered || lastFailure is null || _closed ? answered : throw lastFailure;
    }

    private static JsValue BindArgument(Engine engine, JsonElement element)
    {
        engine.Constraints.Check();
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return JsValue.Null;
            case JsonValueKind.True:
                return JsBoolean.True;
            case JsonValueKind.False:
                return JsBoolean.False;
            case JsonValueKind.String:
                return JsString.Create(element.GetString()!);
            case JsonValueKind.Number:
                return JsNumber.Create(element.GetDouble());
            case JsonValueKind.Array:
                var values = new JsValue[element.GetArrayLength()];
                var index = 0;
                foreach (var item in element.EnumerateArray()) values[index++] = BindArgument(engine, item);
                return new JsArray(engine, values);
            default:
                var entries = new List<KeyValuePair<string, JsValue>>();
                foreach (var property in element.EnumerateObject())
                    entries.Add(new(property.Name, BindArgument(engine, property.Value)));
                return JsObject.CreateFromEntries(engine, entries.ToArray());
        }
    }

    private static object? SnapshotResult(Engine engine, JsValue value)
    {
        if (value.IsNullOrUndefined()) return null;
        if (value.IsString()) return value.AsString();
        if (value.IsBoolean()) return value.AsBoolean();
        if (value.IsNumber())
        {
            var number = value.AsNumber();
            if (!double.IsFinite(number)) return null;
            return number == 0 ? 0d : number;
        }
        var serialized = new Jint.Native.Json.JsonSerializer(engine).Serialize(value);
        if (serialized.IsUndefined()) return null;
        using var document = JsonDocument.Parse(serialized.AsString());
        return Snapshot(engine, document.RootElement);
    }

    private static object? Snapshot(Engine engine, JsonElement element)
    {
        engine.Constraints.Check();
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                return element.GetDouble();
            case JsonValueKind.Array:
                var values = new object?[element.GetArrayLength()];
                var index = 0;
                foreach (var item in element.EnumerateArray()) values[index++] = Snapshot(engine, item);
                return values;
            default:
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject()) result.Add(property.Name, Snapshot(engine, property.Value));
                return result;
        }
    }
}
