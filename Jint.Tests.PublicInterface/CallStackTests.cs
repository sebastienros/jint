using Jint.Runtime;
using SourceMaps;

namespace Jint.Tests.PublicInterface;

public class CallStackTests
{
    [Test]
    public void CanInjectTraceFunction()
    {
        var engine = new Engine();
        engine.Diagnostics.StackTrace.Should().BeEmpty();

        using var stringWriter = new StringWriter();
        engine.SetValue("console", new Console(engine, stringWriter));
        engine.Execute("function x() { console.trace(); }; function y() { x(); } y();");

        const string Expected = """
Trace
    at trace (<anonymous>:1:16)
    at x (<anonymous>:1:16)
    at y (<anonymous>:1:51)
    at <anonymous>:1:58

""";

        var actual = stringWriter.ToString();
        actual.Should().Be(Expected);
    }

    private class Console
    {
        private readonly Engine _engine;
        private readonly StringWriter _output;

        public Console(Engine engine, StringWriter output)
        {
            _engine = engine;
            _output = output;
        }

        public void Log(string message)
        {
            _output.WriteLine(message);
        }

        public void Trace()
        {
            _output.WriteLine($"Trace{Environment.NewLine}{_engine.Diagnostics.StackTrace}");
        }
    }

    [Test]
    public void ShouldReturnTheSourceMapStack()
    {
        var sourceMap = SourceMapParser.Parse("""{"version":3,"file":"custom.js","sourceRoot":"","sources":["custom.ts"],"names":[],"mappings":"AAEA,SAAS,CAAC,CAAC,CAAM;IAChB,MAAM,IAAI,KAAK,CAAC,CAAC,CAAC,CAAC;AACpB,CAAC;AAED,IAAI,CAAC,GAAG,UAAU,CAAM;IACvB,OAAO,CAAC,CAAC,CAAC,CAAC,CAAC;AACb,CAAC,CAAA;AAED,CAAC,CAAC,CAAC,CAAC,CAAC"}""");

        string BuildCallStackHandler(string description, SourceLocation location, string[] arguments)
        {
            if (location.SourceFile != sourceMap.File)
            {
                return null;
            }

            var originalPosition = sourceMap.OriginalPositionFor(location.End.Line, location.Start.Column + 1);

            if (originalPosition is null)
            {
                return null;
            }

            var str = $"    at {
                (!string.IsNullOrWhiteSpace(description) ? $"{description} (" : "")
            }{
                originalPosition.Value.OriginalFileName
            }:{
                originalPosition.Value.OriginalLineNumber + 1
            }:{
                originalPosition.Value.OriginalColumnNumber
            }{
                (!string.IsNullOrWhiteSpace(description) ? ")" : "")
            }{
                Environment.NewLine
            }";

            return str;
        }

        var engine = new Engine(opt =>
        {
            opt.Interop.BuildCallStackHandler = BuildCallStackHandler;
        });

        const string Script = @"function a(v) {
    throw new Error(v);
}
var b = function (v) {
    return a(v);
};
b(7);
//# sourceMappingURL=custom.js.map";
        var ex = Invoking(() => engine.Execute(Script, "custom.js")).Should().ThrowExactly<JavaScriptException>().Which;

        var stack = ex.JavaScriptStackTrace!;
        stack.Replace("\r\n", "\n").Should().Be(@"    at a (custom.ts:4:7)
    at b (custom.ts:8:9)
    at custom.ts:11:1".Replace("\r\n", "\n"));
    }

    [Test]
    public void NestedEvaluationUnhandledThrowShouldNotClearOuterRunCallStack()
    {
        // A host callback invoked from a running script re-enters the engine with
        // Engine.Execute (the browser-host "dynamically inserted <script>" shape) and
        // contains that nested script's unhandled throw. The outer run is still live:
        // its call-stack frames must survive, so stack traces captured by the outer
        // script afterwards are complete and the outer run's pops stay balanced.
        var engine = new Engine();

        string nestedThrowMessageObservedByHost = null;
        engine.SetValue("runNestedScriptContained", () =>
        {
            try
            {
                engine.Execute("throw new Error('nested failure');");
            }
            catch (JavaScriptException nestedScriptException)
            {
                nestedThrowMessageObservedByHost = nestedScriptException.Message;
            }
        });

        engine.Execute("""
            function makeProbeError() {
                // 'new' exercises Construct's call-stack push/pop after the nested throw
                return new Error('outer probe');
            }
            function outerFunction() {
                runNestedScriptContained();
                return makeProbeError().stack;
            }
            globalThis.outerStackAfterNestedFailure = outerFunction();
            """);

        nestedThrowMessageObservedByHost.Should().Be("nested failure");

        var outerStack = engine.Evaluate("outerStackAfterNestedFailure").AsString();
        outerStack.Should().Contain("at makeProbeError");
        outerStack.Should().Contain("at outerFunction");
    }
}
