using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public partial class InteropTests
{
    public class ArrayConversionHost
    {
        public int[] Numbers { get; } = [1, 2, 3];
    }

    [Fact]
    public void ArrayConversionCreatesFreshCopyInCopyMode()
    {
        // Copy is no longer the default (LiveView since 4.14) and the recent-wrapper cache would
        // reuse the first snapshot; opt out of both to lock the fresh-snapshot-per-crossing contract
        var engine = new Engine(options =>
        {
            options.Interop.ArrayConversion = ArrayConversionMode.Copy;
            options.Interop.CacheRecentObjectWrappers = false;
        });
        engine.SetValue("host", new ArrayConversionHost());

        engine.Evaluate("host.Numbers === host.Numbers").AsBoolean().Should().BeFalse();

        // a script-side mutation of one snapshot is not visible in the next read
        engine.Evaluate("var a = host.Numbers; a[0] = 42; host.Numbers[0]").AsNumber().Should().Be(1);
    }

    [Fact]
    public void ArrayConversionReusesSnapshotWithTrackObjectWrapperIdentity()
    {
        var host = new ArrayConversionHost();
        var engine = new Engine(options =>
        {
            options.Interop.ArrayConversion = ArrayConversionMode.Copy;
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

    [Fact]
    public void ArrayConversionReusesSnapshotWithRecentObjectWrapperCache()
    {
        var engine = new Engine(options =>
        {
            options.Interop.ArrayConversion = ArrayConversionMode.Copy;
            options.Interop.CacheRecentObjectWrappers = true;
        });
        engine.SetValue("host", new ArrayConversionHost());

        engine.Evaluate("host.Numbers === host.Numbers").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void IdentityMapHandlesExposedTypeChangeForArrays()
    {
        // the identity map may hold a wrapper for a different exposed view of the same array;
        // a type-guard miss must replace the entry (last view wins), not throw on Add
        var engine = new Engine(options =>
        {
            options.Interop.TrackObjectWrapperIdentity = true;
            options.Interop.WrapObjectHandler = static (e, target, type) =>
                ObjectWrapper.Create(e, target, target is int[] ? typeof(System.Collections.IList) : type);
        });

        var array = new[] { 1, 2, 3 };
        engine.SetValue("a", array);
        engine.Evaluate("a[0]").AsNumber().Should().Be(1);

        engine.Options.Interop.WrapObjectHandler = static (e, target, type) => ObjectWrapper.Create(e, target, type);
        engine.SetValue("b", array);
        engine.Evaluate("b[0]").AsNumber().Should().Be(1);
    }

    [Fact]
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
