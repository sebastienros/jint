using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public partial class InteropTests
{
    public sealed class PropertyMemoHost
    {
        // stable reference-typed property: the getter returns the same array instance every read
        public int[] stable { get; } = [10, 20, 30];

        private int _counter;

        // volatile property: a new instance whose first element increments on every read
        public int[] fresh => [_counter++, 0, 0];

        // value-typed property (boxes a fresh object per read, never eligible for the memo)
        public decimal price { get; set; } = 1.5m;

        // stable reference-typed non-array property
        public PropertyMemoChild child { get; } = new();
    }

    public sealed class PropertyMemoChild
    {
        public int x { get; set; } = 7;
    }

    [Fact]
    public void PropertyValueMemoReturnsStableWrapperAndValues()
    {
        var engine = new Engine();
        engine.SetValue("host", new PropertyMemoHost());

        // repeated reads of a stable-instance property produce correct values...
        engine.Evaluate("var s = 0; for (var i = 0; i < 50; i++) { s += host.stable[0] + host.stable[1] + host.stable[2]; } s")
            .AsNumber().Should().Be(50 * 60);

        // ...and reuse the same wrapper (default recent-wrapper cache is on)
        engine.Evaluate("host.stable === host.stable").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void PropertyValueMemoInvalidatesWhenGetterReturnsNewInstance()
    {
        var engine = new Engine();
        engine.SetValue("host", new PropertyMemoHost());

        // each read of `fresh` returns a new array whose first element increments; the memo must
        // invalidate on the new instance and never serve a stale converted value
        engine.Evaluate("host.fresh[0]").AsNumber().Should().Be(0);
        engine.Evaluate("host.fresh[0]").AsNumber().Should().Be(1);
        engine.Evaluate("var a = host.fresh[0]; var b = host.fresh[0]; a + ',' + b").AsString().Should().Be("2,3");
    }

    [Fact]
    public void PropertyValueMemoDoesNotHideInPlaceMutation()
    {
        var host = new PropertyMemoHost();
        var engine = new Engine();
        engine.SetValue("host", host);

        engine.Evaluate("host.stable[0]").AsNumber().Should().Be(10);

        // a CLR-side in-place mutation of the same array instance must remain visible: the memo caches
        // the wrapper, not element values, and the default wrapper is a live view
        host.stable[0] = 99;
        engine.Evaluate("host.stable[0]").AsNumber().Should().Be(99);
    }

    [Fact]
    public void PropertyValueMemoNeverCachesValueTypeProperty()
    {
        var host = new PropertyMemoHost();
        var engine = new Engine();
        engine.SetValue("host", host);

        engine.Evaluate("host.price").AsNumber().Should().Be(1.5);

        // a value-type member boxes a fresh object each read, so a CLR-side change is always observed
        host.price = 2.5m;
        engine.Evaluate("host.price").AsNumber().Should().Be(2.5);
    }

    [Fact]
    public void PropertyValueMemoRespectsCacheOptOut()
    {
        // Copy mode with the recent-wrapper cache off is the fresh-snapshot-per-crossing contract
        // (host.X !== host.X, and a mutation of one snapshot is invisible to the next). The memo must
        // not silently reintroduce reuse and undo it.
        var engine = new Engine(static options =>
        {
            options.Interop.ArrayConversion = ArrayConversionMode.Copy;
            options.Interop.CacheRecentObjectWrappers = false;
        });
        engine.SetValue("host", new PropertyMemoHost());

        engine.Evaluate("host.stable === host.stable").AsBoolean().Should().BeFalse();
        engine.Evaluate("var a = host.stable; a[0] = 42; host.stable[0]").AsNumber().Should().Be(10);
    }

    [Fact]
    public void PropertyValueMemoDisabledUnderIdentityTracking()
    {
        // the ConditionalWeakTable is authoritative when identity tracking is on; the memo stays out
        // of the way and behavior is unchanged
        var engine = new Engine(static options => options.Interop.TrackObjectWrapperIdentity = true);
        engine.SetValue("host", new PropertyMemoHost());

        engine.Evaluate("host.stable === host.stable").AsBoolean().Should().BeTrue();

        // a changing-instance getter is still reflected (the map keys on the instance)
        engine.Evaluate("host.fresh[0]").AsNumber().Should().Be(0);
        engine.Evaluate("host.fresh[0]").AsNumber().Should().Be(1);
    }

    [Fact]
    public void PropertyValueMemoWorksForObjectProperty()
    {
        var host = new PropertyMemoHost();
        var engine = new Engine();
        engine.SetValue("host", host);

        engine.Evaluate("host.child.x").AsNumber().Should().Be(7);

        // the memo returns the same wrapped child instance, so a script-side write reaches the CLR object
        engine.Evaluate("host.child.x = 42; host.child.x").AsNumber().Should().Be(42);
        host.child.x.Should().Be(42);
    }
}
