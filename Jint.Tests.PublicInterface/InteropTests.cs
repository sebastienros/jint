using System.Reflection;

namespace Jint.Tests.PublicInterface;

public partial class InteropTests : IDisposable
{
    private readonly Engine _engine;

    public InteropTests()
    {
        _engine = new Engine(cfg => cfg.AllowClr(
                    typeof(Console).GetTypeInfo().Assembly,
                    typeof(File).GetTypeInfo().Assembly))
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
                .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())))
            ;
    }

    void IDisposable.Dispose()
    {
    }
}