using Jint.Runtime;
using SourceMaps;

namespace Jint.Tests.PublicInterface;

public class CallStackTests
{
    [Fact]
    public void CanInjectTraceFunction()
    {
        var engine = new Engine();
        Assert.Empty(engine.Advanced.StackTrace);

        using var stringWriter = new StringWriter();
        engine.SetValue("console", new Console(engine, stringWriter));
        engine.Execute("function x() { console.trace(); }; function y() { x(); } y();");

        const string Expected = """
Trace
   at trace <anonymous>:1:16
   at x () <anonymous>:1:16
   at y () <anonymous>:1:51
   at <anonymous>:1:58

""";

        var actual = stringWriter.ToString();
        Assert.Equal(Expected, actual);
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
            _output.WriteLine($"Trace{Environment.NewLine}{_engine.Advanced.StackTrace}");
        }
    }

    [Fact]
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

            var str = $"   at{
                (!string.IsNullOrWhiteSpace(description) ? $" {description}" : "")
            } {
                originalPosition.Value.OriginalFileName
            }:{
                originalPosition.Value.OriginalLineNumber + 1
            }:{
                originalPosition.Value.OriginalColumnNumber
            }{
                Environment.NewLine
            }";

            return str;
        }

        var engine = new Engine(opt =>
        {
            opt.SetBuildCallStackHandler(BuildCallStackHandler);
        });

        const string Script = @"function a(v) {
    throw new Error(v);
}
var b = function (v) {
    return a(v);
};
b(7);
//# sourceMappingURL=custom.js.map";
        var ex = Assert.Throws<JavaScriptException>(() => engine.Execute(Script, "custom.js"));

        var stack = ex.JavaScriptStackTrace!;
        Assert.Equal(@"   at a custom.ts:4:7
   at b custom.ts:8:9
   at custom.ts:11:1".Replace("\r\n", "\n"), stack.Replace("\r\n", "\n"));
    }

}
