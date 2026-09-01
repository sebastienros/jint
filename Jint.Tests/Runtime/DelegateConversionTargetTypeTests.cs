#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A JavaScript function converted to a CLR delegate is cached so that <c>host.on(f)</c> and
/// <c>host.off(f)</c> hand the host the same <see cref="Delegate"/> instance. These cover the other half of
/// that contract: the cached delegate is the one built for the target type being asked for, not for whichever
/// target type asked first (https://github.com/sebastienros/jint/issues/3434).
/// </summary>
public class DelegateConversionTargetTypeTests
{
    public delegate void Notify(int value);

    public delegate string Transform(string value);

    public sealed class NotifyHost
    {
        public string Call(Notify f)
        {
            f(1);
            return "notify";
        }
    }

    public sealed class TransformHost
    {
        public string Call(Transform f) => "transform:" + f("x");
    }

    public sealed class BothHost
    {
        public Notify? LastNotify { get; private set; }

        public Transform? LastTransform { get; private set; }

        public string A(Notify f)
        {
            LastNotify = f;
            f(1);
            return "notify";
        }

        public string B(Transform f)
        {
            LastTransform = f;
            return "transform:" + f("x");
        }
    }

    /// <summary>Control: each host alone works, and always did.</summary>
    [Fact]
    public void ControlEachHostAlone()
    {
        var one = new Engine();
        one.SetValue("host", new NotifyHost());
        one.Evaluate("host.call(function (x) { return String(x); })").AsString().Should().Be("notify");

        var two = new Engine();
        two.SetValue("host", new TransformHost());
        two.Evaluate("host.call(function (x) { return String(x); })").AsString().Should().Be("transform:x");
    }

    /// <summary>The bound-delegate cache: one function instance, two delegate types, one engine.</summary>
    [Fact]
    public void OneFunctionInstanceTwoDelegateTypes()
    {
        var engine = new Engine();
        engine.SetValue("host", new BothHost());

        var result = engine.Evaluate("""
            var f = function (x) { return String(x); };
            host.a(f) + '|' + host.b(f);
            """).AsString();

        result.Should().Be("notify|transform:x");
    }

    /// <summary>The binder cache: two instances of one AST node, two delegate types, one engine.</summary>
    [Fact]
    public void TwoInstancesOfOneAstNodeTwoDelegateTypes()
    {
        var engine = new Engine();
        engine.SetValue("host", new BothHost());

        var result = engine.Evaluate("""
            function make() { return function (x) { return String(x); }; }
            host.a(make()) + '|' + host.b(make());
            """).AsString();

        result.Should().Be("notify|transform:x");
    }

    /// <summary>
    /// The reason the cache exists: one function instance converted twice to the same delegate type is the
    /// same <see cref="Delegate"/> instance, which is the identity <c>-=</c> needs. Per target type now,
    /// rather than for the first target type only.
    /// </summary>
    [Fact]
    public void SameFunctionAndSameTargetTypeIsTheSameDelegateInstance()
    {
        var host = new BothHost();
        var engine = new Engine();
        engine.SetValue("host", host);

        engine.Execute("var f = function (x) { return String(x); };");

        engine.Execute("host.a(f);");
        var firstNotify = host.LastNotify;
        engine.Execute("host.b(f);");
        var firstTransform = host.LastTransform;

        engine.Execute("host.a(f);");
        engine.Execute("host.b(f);");

        host.LastNotify.Should().BeSameAs(firstNotify);
        host.LastTransform.Should().BeSameAs(firstTransform);
    }
}
