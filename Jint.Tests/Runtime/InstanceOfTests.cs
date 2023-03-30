using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public class InstanceOfTests
{
    [Fact]
    public void ShouldSupportInheritanceChainUnderInterop()
    {
        var engine = new Engine();

        engine.SetValue("A", TypeReference.CreateTypeReference(engine, typeof(A)));
        engine.SetValue("AToo", TypeReference.CreateTypeReference(engine, typeof(A)));
        engine.SetValue("B", TypeReference.CreateTypeReference(engine, typeof(B)));
        engine.SetValue("C", TypeReference.CreateTypeReference(engine, typeof(C)));

        Assert.True(engine.Evaluate("A == A").AsBoolean());
        Assert.True(engine.Evaluate("A === A").AsBoolean());
        Assert.True(engine.Evaluate("A == AToo").AsBoolean());
        Assert.True(engine.Evaluate("A === AToo").AsBoolean());

        Assert.True(engine.Evaluate("A.prototype instanceof A").AsBoolean());
        Assert.True(engine.Evaluate("B.prototype instanceof A").AsBoolean());
        Assert.False(engine.Evaluate("A.prototype instanceof B").AsBoolean());
        Assert.True(engine.Evaluate("C.prototype instanceof A").AsBoolean());
        Assert.True(engine.Evaluate("C.prototype instanceof B").AsBoolean());

        var a = new A();
        var b = new B();
        var c = new C();

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

        Assert.True(engine.Evaluate("new A() instanceof A").AsBoolean());
        Assert.False(engine.Evaluate("new A() instanceof B").AsBoolean());
        Assert.False(engine.Evaluate("new A() instanceof C").AsBoolean());

        Assert.True(engine.Evaluate("new B() instanceof A").AsBoolean());
        Assert.True(engine.Evaluate("new B() instanceof B").AsBoolean());
        Assert.False(engine.Evaluate("new B() instanceof C").AsBoolean());

        Assert.True(engine.Evaluate("new C() instanceof A").AsBoolean());
        Assert.True(engine.Evaluate("new C() instanceof B").AsBoolean());
        Assert.True(engine.Evaluate("new C() instanceof C").AsBoolean());
    }

    public class A { }

    public class B : A { }

    public class C : B { }
}
