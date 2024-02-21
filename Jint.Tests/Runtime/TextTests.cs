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
               .SetValue("assert", new Action<bool>(Assert.True))
               .SetValue("equal", new Action<object, object>(Assert.Equal));
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
        Assert.Equal("", RunTest($"new TextDecoder().decode()"));
        Assert.Equal("foo", RunTest($"new TextDecoder().decode(new Uint8Array([102,111,111]))"));
        Assert.Equal("foo", RunTest($"new TextDecoder().decode(new Uint8Array([102,111,111]).buffer)"));
        Assert.Equal("foo", RunTest($"new TextDecoder().decode(new DataView(new Uint8Array([0,102,111,111,0]).buffer,1,3))"));
    }
}
