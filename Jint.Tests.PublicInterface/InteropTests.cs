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
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("equal", new Action<object, object>(Assert.Equal))
            ;
    }

    void IDisposable.Dispose()
    {
    }
}