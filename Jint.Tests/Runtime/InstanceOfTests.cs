using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public class InstanceOfTests
{
    [Fact]
    public void ShouldSupportInheritanceChainUnderInterop()
    {
        var a = new A();
        var b = new B();
        var c = new C();

        var engine = new Engine();

        engine.SetValue("A", TypeReference.CreateTypeReference(engine, typeof(A)));
        engine.SetValue("B", TypeReference.CreateTypeReference(engine, typeof(B)));
        engine.SetValue("C", TypeReference.CreateTypeReference(engine, typeof(C)));

        engine.SetValue("a", a);
        engine.SetValue("b", b);
        engine.SetValue("c", c);

        Assert.True(engine.Evaluate("a instanceof A").AsBoolean());
        Assert.False(engine.Evaluate("a instanceof B").AsBoolean());
        Assert.False(engine.Evaluate("a instanceof C").AsBoolean());

        Assert.True(engine.Evaluate("b instanceof A").AsBoolean());
        Assert.True(engine.Evaluate("b instanceof B").AsBoolean());
        Assert.False(engine.Evaluate("b instanceof C").AsBoolean());

        Assert.True(engine.Evaluate("c instanceof A").AsBoolean());
        Assert.True(engine.Evaluate("c instanceof B").AsBoolean());
        Assert.True(engine.Evaluate("c instanceof C").AsBoolean());
    }

    public class A { }

    public class B : A { }

    public class C : B { }
}
