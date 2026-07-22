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
        RunTest($"Uuid.parse('{Guid.Empty}')").Should().Be(Guid.Empty);
        RunTest($"Uuid.Empty").Should().Be(Guid.Empty);
    }

    [Fact]
    public void Random()
    {
        var actual = RunTest($"new Uuid()");
        actual.Should().NotBe(Guid.Empty);
        actual.Should().BeOfType<Guid>();
    }

    [Fact]
    public void Copy()
    {
        _engine.Evaluate("const g = new Uuid();");
        _engine.Evaluate("g.toString()").AsString().Should().Be(_engine.Evaluate("copy(g).toString()").AsString());
    }
}