using Jint;
using Jint.Native;
using Jint.Runtime;

namespace Documentation.Samples;

public static class CoreSamples
{
    public static string Home()
    {
        #region docs:home-first-script

        var result = new Engine()
            .SetValue("name", "World")
            .Evaluate("`Hello, ${name}!`")
            .AsString();

        #endregion

        return result;
    }

    public static double ReadmeEvaluate()
    {
        #region docs:readme-evaluate

        var engine = new Engine();
        var result = engine.Evaluate("40 + 2").AsNumber();

        #endregion

        return result;
    }

    public static string ReadmeExposeAndInvoke()
    {
        #region docs:readme-expose-and-invoke

        var engine = new Engine()
            .SetValue("log", new Action<string>(Console.WriteLine))
            .Execute("""
                function greet(name) {
                    const message = `Hello, ${name}!`;
                    log(message);
                    return message;
                }
                """);

        var greeting = engine.Invoke("greet", "Ada").AsString();

        #endregion

        return greeting;
    }

    public static double ReadmePrepare()
    {
        #region docs:readme-prepare

        var script = Engine.PrepareScript(
            "items.reduce((sum, value) => sum + value, 0)",
            source: "sum.js",
            strict: true);

        var engine = new Engine();
        engine.SetValue("items", new[] { 1, 2, 3 });

        var total = engine.Evaluate(in script).AsNumber();

        #endregion

        return total;
    }

    public static object? ReadmeUntrustedCode(string source, CancellationToken cancellationToken)
    {
        #region docs:readme-untrusted-code

        var limits = UntrustedCodeLimits.Default with
        {
            TimeoutInterval = TimeSpan.FromSeconds(1),
            MaxStatements = 50_000,
            MemoryLimit = 16_000_000
        };

        var options = new Options().ForUntrustedCode(limits);
        using var engine = new Engine(options);

        using (limits.BeginOperation(engine, cancellationToken))
        {
            var value = engine.Evaluate(source);
            return engine.ConvertResult(value, limits.ResultLimits);
        }

        #endregion
    }

    public static double GuideEvaluate()
    {
        #region docs:guide-evaluate

        var answer = new Engine()
            .Evaluate("6 * 7")
            .AsNumber();

        #endregion

        return answer;
    }

    public static Engine GuideExposeHostFunction()
    {
        #region docs:guide-expose-host-function

        var engine = new Engine()
            .SetValue("log", new Action<string>(Console.WriteLine));

        engine.Execute("log('Hello from JavaScript')");

        #endregion

        return engine;
    }

    public static double GuideInvoke()
    {
        #region docs:guide-invoke

        var result = new Engine()
            .Execute("function add(a, b) { return a + b; }")
            .Invoke("add", 2, 3)
            .AsNumber();

        #endregion

        return result;
    }

    public static object? GuideBoundedResult(string source)
    {
        #region docs:guide-bounded-result

        var engine = new Engine(options => options.LimitMemory(16_000_000));
        var value = engine.Evaluate(source);

        // Unbounded: copies every string in full, whatever it shares.
        var copied = value.ToObject();

        // Bounded: checks each string's length before copying it.
        var detached = engine.ConvertResult(value, ResultLimits.Conservative);

        #endregion

        return detached ?? copied;
    }

    #region docs:guide-job-callback-hooks

    public sealed class FlowHooks : JobCallbackHooks
    {
        public static readonly AsyncLocal<string?> Current = new();

        // HostMakeJobCallback: runs when script registers a callback (then, await, ...).
        protected override object? Capture(Engine engine) => Current.Value;

        // HostCallJobCallback: runs around the callback, restoring even if it throws.
        protected override object? Enter(Engine engine, object hostDefined)
        {
            var previous = Current.Value;
            Current.Value = (string) hostDefined;
            return previous;
        }

        protected override void Exit(Engine engine, object? token) => Current.Value = (string?) token;
    }

    #endregion

    public static async Task<List<string>> GuideJobCallbacks()
    {
        var log = new List<string>();

        #region docs:guide-job-callback-usage

        var engine = new Engine(options => options.Host.JobCallbacks = new FlowHooks());

        // Runs body inside a named flow; an async body returns at its first await.
        engine.SetValue("scope", new Func<string, JsValue, JsValue>((name, body) =>
        {
            var previous = FlowHooks.Current.Value;
            FlowHooks.Current.Value = name;
            try
            {
                return body.Call();
            }
            finally
            {
                FlowHooks.Current.Value = previous;
            }
        }));

        // Any host function can now ask which flow it was called from.
        engine.SetValue("hostOperation", new Action<int>(item =>
            log.Add($"{item} belongs to {FlowHooks.Current.Value}")));

        await engine.EvaluateAsync("""
            (async () => {
                await Promise.all([1, 2, 3].map(async (item) => {
                    await scope(`Item ${item}`, async () => {
                        await null;
                        await hostOperation(item); // "2 belongs to Item 2", ...
                    });
                }));
            })()
            """);

        #endregion

        return log;
    }

    public static double GuideEvaluateWithContext()
    {
        #region docs:guide-evaluate-with-context

        var engine = new Engine();
        var expression = "ctx.price * ctx.quantity";

        // Compile once. Evaluating a function expression defines no globals.
        var total = engine.Evaluate($"(ctx) => ({expression})");

        // Invoke converts a CLR argument; Call takes a JsValue, such as one a host function received.
        var fromClr = engine.Invoke(total, new { price = 4, quantity = 3 }); // 12
        var fromScript = total.Call(engine.Evaluate("({ price: 5, quantity: 2 })")); // 10

        #endregion

        return fromClr.AsNumber() + fromScript.AsNumber();
    }

    public static double GuideEvaluateWithScope()
    {
        var engine = new Engine();

        #region docs:guide-evaluate-with-scope

        var total = engine.Evaluate("(function (scope) { with (scope) { return (price * quantity); } })");
        var result = engine.Invoke(total, new { price = 4, quantity = 3 }); // 12

        #endregion

        return result.AsNumber();
    }

    #region docs:guide-context-across-engines

    // Parsed once and shared by every engine.
    private static readonly Prepared<Script> Total =
        Engine.PrepareScript("(ctx) => (ctx.price * ctx.quantity)");

    public static JsValue EvaluateTotal(Engine engine, JsValue ctx)
    {
        // The function belongs to this engine; cache it per engine, never in a static.
        var total = engine.Evaluate(in Total);
        return total.Call(ctx);
    }

    #endregion
}
