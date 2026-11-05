using System.Diagnostics.CodeAnalysis;
using Jint;
using Jint.Native;
using Jint.Runtime.Interop;

var reflectionEnabled = !AppContext.TryGetSwitch("Jint.Runtime.Interop.ReflectionEnabled", out var configured) || configured;
if (reflectionEnabled)
{
    using var engine = new Engine();
    engine.SetValue("host", JsValue.FromObject(engine, new HostValue()));
    engine.SetValue("numbers", JsValue.FromObject(engine, new[] { 21, 42 }));
    if (engine.Evaluate("host.Value").AsNumber() != 42 ||
        engine.Evaluate("numbers[1]").AsNumber() != 42)
    {
        throw new InvalidOperationException("Default CLR interop changed.");
    }
    Console.WriteLine("Default reflection-enabled CLR interop: 2 checks passed.");
    return;
}

var passed = 0;
void Check(bool success, string label)
{
    if (!success)
    {
        throw new InvalidOperationException(label);
    }
    passed++;
}

using (var engine = new Engine())
{
    Check(engine.Evaluate("JSON.stringify({ answer: [21, 42][1] })").AsString() == "{\"answer\":42}", "native objects");
    engine.SetValue("twice", (JsValue) new ClrFunction(engine, "twice", (_, arguments) => arguments[0].AsNumber() * 2));
    Check(engine.Evaluate("twice(21)").AsNumber() == 42, "native callback");
    Check(JsValue.FromObject(engine, (object) 42).AsNumber() == 42, "boxed scalar");
    var rejected = false;
    try
    {
        JsValue.FromObject(engine, new object());
    }
    catch (NotSupportedException)
    {
        rejected = true;
    }
    Check(rejected, "unknown CLR object must fail closed");
}

using (var engine = new Engine(options => options.AddObjectConverter(new HostValueConverter())))
{
    engine.SetValue("value", JsValue.FromObject(engine, new HostValue()));
    Check(engine.Evaluate("value").AsNumber() == 42, "explicit object adapter");
}

using (var engine = new Engine(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop))
{
    var callbacks = 0;
    engine.SetValue("note", (JsValue) new ClrFunction(engine, "note", (_, _) =>
    {
        callbacks++;
        return JsValue.Undefined;
    }));
    var promise = engine.Tasks.RegisterPromise();
    engine.SetValue("pending", (JsValue) promise.Promise);
    var result = engine.Evaluate("pending.then(value => { note(); return value + 1; })");
    Task.Run(() => engine.Tasks.Post(() => promise.Resolve(41))).GetAwaiter().GetResult();
    Check(callbacks == 0, "worker must not enter JavaScript");
    Check(result.UnwrapIfPromise(TimeSpan.FromSeconds(5)).AsNumber() == 42 && callbacks == 1, "posted Promise settlement");
}

foreach (var enableOperators in new[] { false, true })
{
    var rejected = false;
    try
    {
        using var engine = new Engine(options =>
        {
            options.Interop.Enabled = !enableOperators;
            options.Interop.AllowOperatorOverloading = enableOperators;
        });
    }
    catch (NotSupportedException)
    {
        rejected = true;
    }
    Check(rejected, "reflection-dependent options must fail closed");
}

Console.WriteLine($"Reflection-free managed trimming: {passed} checks passed.");

internal sealed class HostValue
{
    public int Value => 42;
}

internal sealed class HostValueConverter : ObjectConverter
{
    protected override Type[] HandledTypes => [typeof(HostValue)];

    public override bool TryConvert(Engine engine, object value, [NotNullWhen(true)] out JsValue? result)
    {
        if (value is HostValue host)
        {
            result = host.Value;
            return true;
        }
        result = null;
        return false;
    }
}
