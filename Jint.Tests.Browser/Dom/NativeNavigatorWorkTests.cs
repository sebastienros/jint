using Jint.Browser.Dom;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeNavigatorWorkTests
{
    [Test]
    public void AppVersionChargesEveryCopiedCharacterAndCancelsDuringTheCopy()
    {
        var userAgent = "Mozilla/5.0 (" + new string('x', 8192) + ")";
        var copied = 0;
        var result = DomNativeNavigatorMembers.AppVersion(userAgent, new DomReadWork(count => copied += count, default));
        copied.Should().Be(userAgent.Length - "Mozilla/".Length);
        result.AsString().Should().Be(userAgent["Mozilla/".Length..]);

        using var cancellation = new CancellationTokenSource();
        copied = 0;
        var work = new DomReadWork(count =>
        {
            copied += count;
            if (copied >= 512) cancellation.Cancel();
        }, cancellation.Token);
        Assert.Throws<OperationCanceledException>(() => DomNativeNavigatorMembers.AppVersion(userAgent, work));
        copied.Should().Be(512);
    }
}
