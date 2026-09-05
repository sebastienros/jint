using Jint;

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

}
