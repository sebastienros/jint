using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public partial class InteropTests
{
    public class ArrayConversionHost
    {
        public int[] Numbers { get; } = [1, 2, 3];
    }

    [Test]
    public void ArrayConversionCreatesFreshCopyByDefaultWithoutCache()
    {
        // The recent-wrapper cache would reuse the first default Copy snapshot; opt out to lock the
        // fresh-snapshot-per-crossing contract.
        var engine = new Engine(options =>
        {
            options.Interop.CacheRecentObjectWrappers = false;
        });
        engine.SetValue("host", new ArrayConversionHost());

        engine.Evaluate("host.Numbers === host.Numbers").AsBoolean().Should().BeFalse();

        // a script-side mutation of one snapshot is not visible in the next read
        engine.Evaluate("var a = host.Numbers; a[0] = 42; host.Numbers[0]").AsNumber().Should().Be(1);
    }

    [Test]
    public void ArrayConversionReusesSnapshotWithTrackObjectWrapperIdentity()
    {
        var host = new ArrayConversionHost();
        var engine = new Engine(options =>
        {
            options.Interop.TrackObjectWrapperIdentity = true;
        });
        engine.SetValue("host", host);

        engine.Evaluate("host.Numbers === host.Numbers").AsBoolean().Should().BeTrue();

        // script-side mutations persist across reads (the identity contract)
        engine.Evaluate("host.Numbers[0] = 42; host.Numbers[0]").AsNumber().Should().Be(42);

        // CLR-side mutations after the first conversion are not re-copied (documented staleness)
        host.Numbers[1] = 99;
        engine.Evaluate("host.Numbers[1]").AsNumber().Should().Be(2);
    }

    [Test]
    public void ArrayConversionReusesSnapshotWithRecentObjectWrapperCache()
    {
        var engine = new Engine();
        engine.SetValue("host", new ArrayConversionHost());

        engine.Evaluate("host.Numbers === host.Numbers").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void IdentityMapHandlesExposedTypeChangeForArrays()
    {
        // the identity map may hold a wrapper for a different exposed view of the same array;
        // a type-guard miss must replace the entry (last view wins), not throw on Add
        // The view the handler chooses is switched host-side rather than by replacing the handler on the
        // engine's Options, which are read-only once the engine has been built from them.
        var exposeAsList = true;
        var engine = new Engine(options =>
        {
            options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
            options.Interop.TrackObjectWrapperIdentity = true;
            options.Interop.WrapObjectHandler = (e, target, type) =>
                ObjectWrapper.Create(e, target, (exposeAsList && target is int[]) ? typeof(System.Collections.IList) : type);
        });

        var array = new[] { 1, 2, 3 };
        engine.SetValue("a", array);
        engine.Evaluate("a[0]").AsNumber().Should().Be(1);

        exposeAsList = false;
        engine.SetValue("b", array);
        engine.Evaluate("b[0]").AsNumber().Should().Be(1);
    }

    [Test]
    public void DisposeReleasesRecentWrapperCache()
    {
        // the recent-wrapper ring is on by default and strongly roots its targets;
        // Dispose must release them even when the opt-in identity map was never created
        var engine = new Engine();
        engine.SetValue("host", new ArrayConversionHost());
        engine.Evaluate("host.Numbers[0]");
        engine._recentObjectWrapperCache.Should().NotBeNull();

        engine.Dispose();
        engine._recentObjectWrapperCache.Should().BeNull();
    }
}
