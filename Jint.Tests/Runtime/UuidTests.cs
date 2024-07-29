using Jint.Tests.Runtime.Domain;

namespace Jint.Tests.Runtime;

public class UuidTests : IDisposable
{
    private readonly Engine _engine;

    public UuidTests()
    {
        _engine = new Engine(o => o.AddObjectConverter(new UuidConverter()))
                .SetValue("copy", new Func<Guid, Guid>(v => new Guid(v.ToByteArray())))
            ;
        UuidConstructor.CreateUuidConstructor(_engine);
    }

    void IDisposable.Dispose()
    {
    }

    private object RunTest(string source)
    {
        return _engine.Evaluate(source).ToObject();
    }

    [Fact]
    public void Empty()
    {
        Assert.Equal(Guid.Empty, RunTest($"Uuid.parse('{Guid.Empty}')"));
        Assert.Equal(Guid.Empty, RunTest($"Uuid.Empty"));
    }

    [Fact]
    public void Random()
    {
        var actual = RunTest($"new Uuid()");
        Assert.NotEqual(Guid.Empty, actual);
        Assert.IsType<Guid>(actual);
    }

    [Fact]
    public void Copy()
    {
        _engine.Evaluate("const g = new Uuid();");
        Assert.Equal(_engine.Evaluate("copy(g).toString()").AsString(), _engine.Evaluate("g.toString()").AsString());
    }
}