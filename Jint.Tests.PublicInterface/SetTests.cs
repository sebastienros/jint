using AwesomeAssertions;
using Jint.Native;

namespace Jint.Tests.Runtime;

public class SetTests
{
    [Fact]
    public void ConConstructSet()
    {
        var engine = new Engine();

        var set = engine.Intrinsics.Set.Construct();
        set.Add(42);
        set.Add("foo");
        set.Size.Should().Be(2);

        set.AsEnumerable().Should().ContainInOrder(42, "foo");

        set.Has(42).Should().BeTrue();
        set.Has("foo").Should().BeTrue();
        set.Has(24).Should().BeFalse();

        engine.SetValue("s", set);
        engine.Evaluate("s.size").Should().Be(2);
        engine.Evaluate("s.has(42)").Should().BeTrue();
        engine.Evaluate("s.has('foo')").Should().BeTrue();
        engine.Evaluate("s.has(24)").Should().BeFalse();

        set.Delete(42).Should().BeTrue();
        set.Has(42).Should().BeFalse();
        engine.Evaluate("s.has(42)").Should().BeFalse();
        engine.Evaluate("s.size").Should().Be(1);

        set.Clear();
        set.AsEnumerable().Should().BeEmpty();
        set.Size.Should().Be(0);
        engine.Evaluate("s.size").Should().Be(0);
    }
}
