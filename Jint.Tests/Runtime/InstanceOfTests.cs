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

        engine.Evaluate("A == A").AsBoolean().Should().BeTrue();
        engine.Evaluate("A === A").AsBoolean().Should().BeTrue();
        engine.Evaluate("A == AToo").AsBoolean().Should().BeTrue();
        engine.Evaluate("A === AToo").AsBoolean().Should().BeTrue();

        engine.Evaluate("A.prototype instanceof A").AsBoolean().Should().BeTrue();
        engine.Evaluate("B.prototype instanceof A").AsBoolean().Should().BeTrue();
        engine.Evaluate("A.prototype instanceof B").AsBoolean().Should().BeFalse();
        engine.Evaluate("C.prototype instanceof A").AsBoolean().Should().BeTrue();
        engine.Evaluate("C.prototype instanceof B").AsBoolean().Should().BeTrue();

        var a = new A();
        var b = new B();
        var c = new C();

        engine.SetValue("a", a);
        engine.SetValue("b", b);
        engine.SetValue("c", c);

        engine.Evaluate("a instanceof A").AsBoolean().Should().BeTrue();
        engine.Evaluate("a instanceof B").AsBoolean().Should().BeFalse();
        engine.Evaluate("a instanceof C").AsBoolean().Should().BeFalse();

        engine.Evaluate("b instanceof A").AsBoolean().Should().BeTrue();
        engine.Evaluate("b instanceof B").AsBoolean().Should().BeTrue();
        engine.Evaluate("b instanceof C").AsBoolean().Should().BeFalse();

        engine.Evaluate("c instanceof A").AsBoolean().Should().BeTrue();
        engine.Evaluate("c instanceof B").AsBoolean().Should().BeTrue();
        engine.Evaluate("c instanceof C").AsBoolean().Should().BeTrue();

        engine.Evaluate("new A() instanceof A").AsBoolean().Should().BeTrue();
        engine.Evaluate("new A() instanceof B").AsBoolean().Should().BeFalse();
        engine.Evaluate("new A() instanceof C").AsBoolean().Should().BeFalse();

        engine.Evaluate("new B() instanceof A").AsBoolean().Should().BeTrue();
        engine.Evaluate("new B() instanceof B").AsBoolean().Should().BeTrue();
        engine.Evaluate("new B() instanceof C").AsBoolean().Should().BeFalse();

        engine.Evaluate("new C() instanceof A").AsBoolean().Should().BeTrue();
        engine.Evaluate("new C() instanceof B").AsBoolean().Should().BeTrue();
        engine.Evaluate("new C() instanceof C").AsBoolean().Should().BeTrue();
    }

    public class A { }

    public class B : A { }

    public class C : B { }
}
