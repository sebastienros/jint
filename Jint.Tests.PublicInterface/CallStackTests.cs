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
}
