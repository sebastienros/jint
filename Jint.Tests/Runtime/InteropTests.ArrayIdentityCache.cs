namespace Jint.Tests.Runtime;

public partial class InteropTests
{
    public class ArrayConversionHost
    {
        public int[] Numbers { get; } = [1, 2, 3];
    }

    [Fact]
    public void ClrArrayConversionCreatesFreshCopyByDefault()
    {
        var engine = new Engine();
        engine.SetValue("host", new ArrayConversionHost());

        Assert.False(engine.Evaluate("host.Numbers === host.Numbers").AsBoolean());

        // a script-side mutation of one snapshot is not visible in the next read
        Assert.Equal(1, engine.Evaluate("var a = host.Numbers; a[0] = 42; host.Numbers[0]").AsNumber());
    }

    [Fact]
    public void ClrArrayConversionReusesSnapshotWithTrackObjectWrapperIdentity()
    {
        var host = new ArrayConversionHost();
        var engine = new Engine(options => options.Interop.TrackObjectWrapperIdentity = true);
        engine.SetValue("host", host);

        Assert.True(engine.Evaluate("host.Numbers === host.Numbers").AsBoolean());

        // script-side mutations persist across reads (the identity contract)
        Assert.Equal(42, engine.Evaluate("host.Numbers[0] = 42; host.Numbers[0]").AsNumber());

        // CLR-side mutations after the first conversion are not re-copied (documented staleness)
        host.Numbers[1] = 99;
        Assert.Equal(2, engine.Evaluate("host.Numbers[1]").AsNumber());
    }

    [Fact]
    public void ClrArrayConversionReusesSnapshotWithRecentObjectWrapperCache()
    {
        var engine = new Engine(options => options.Interop.CacheRecentObjectWrappers = true);
        engine.SetValue("host", new ArrayConversionHost());

        Assert.True(engine.Evaluate("host.Numbers === host.Numbers").AsBoolean());
    }
}
