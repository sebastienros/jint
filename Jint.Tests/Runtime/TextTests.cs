using Jint.Runtime.Interop;
using Jint.Tests.Runtime.Domain;

namespace Jint.Tests.Runtime;

public sealed class TextTests
{
    private readonly Engine _engine;

    public TextTests()
    {
        _engine = new Engine()
               .SetValue("log", new Action<object>(Console.WriteLine))
               .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
               .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())));
        _engine
               .SetValue("TextDecoder", TypeReference.CreateTypeReference<TextDecoder>(_engine))
           ;
    }
    private object RunTest(string source)
    {
        return _engine.Evaluate(source).ToObject();
    }

    [Fact]
    public void CanDecode()
    {
        RunTest($"new TextDecoder().decode()").Should().Be("");
        RunTest($"new TextDecoder().decode(new Uint8Array([102,111,111]))").Should().Be("foo");
        RunTest($"new TextDecoder().decode(new Uint8Array([102,111,111]).buffer)").Should().Be("foo");
        RunTest($"new TextDecoder().decode(new DataView(new Uint8Array([0,102,111,111,0]).buffer,1,3))").Should().Be("foo");
    }
}
