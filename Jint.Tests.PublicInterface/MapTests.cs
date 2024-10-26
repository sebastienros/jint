using FluentAssertions;
using Jint.Native;

namespace Jint.Tests.Runtime;

public class MapTests
{
    [Fact]
    public void ConConstructMap()
    {
        var engine = new Engine();

        var map = engine.Intrinsics.Map.Construct();
        map.Set(42, "the meaning of life");
        map.Set("foo", "bar");
        map.Size.Should().Be(2);

        map.Has(42).Should().BeTrue();
        map.Has("foo").Should().BeTrue();
        map.Has(24).Should().BeFalse();

        map.Get(42).Should().Be((JsString) "the meaning of life");
        map.Get("foo").Should().Be((JsString) "bar");
        map.Get(24).Should().Be(JsValue.Undefined);

        engine.SetValue("m", map);
        engine.Evaluate("m.size").Should().Be((JsNumber) 2);
        engine.Evaluate("m.has(42)").Should().Be(JsBoolean.True);
        engine.Evaluate("m.has('foo')").Should().Be(JsBoolean.True);
        engine.Evaluate("m.has(24)").Should().Be(JsBoolean.False);

        map.Should().Contain((JsNumber) 42, (JsString) "the meaning of life");
        map.Remove(42).Should().BeTrue();
        map.Has(42).Should().BeFalse();
        engine.Evaluate("m.has(42)").Should().Be(JsBoolean.False);
        engine.Evaluate("m.size").Should().Be((JsNumber) 1);

        map.Clear();
        map.Should().BeEmpty();
        map.Size.Should().Be(0);
        engine.Evaluate("m.size").Should().Be((JsNumber) 0);
    }
}
