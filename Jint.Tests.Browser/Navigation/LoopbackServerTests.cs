namespace Jint.Tests.Browser.Navigation;

/// <summary>
/// The fixture server itself, where its own contract is what a test depends on.
/// </summary>
public sealed class LoopbackServerTests
{
    /// <summary>
    /// Disposing twice does nothing the second time, which is what <see cref="IDisposable"/> asks for.
    /// </summary>
    /// <remarks>
    /// A suite that hands its server to a <c>LoopbackPage</c> and also writes <c>using var server = …</c>
    /// disposes it twice, and the second call used to <c>Cancel()</c> an already-disposed
    /// <see cref="System.Threading.CancellationTokenSource"/> — an <see cref="ObjectDisposedException"/> out
    /// of a <c>using</c> block, which fails whichever test happened to be shaped that way rather than the
    /// one that owns the mistake ([#3720](https://github.com/sebastienros/jint/issues/3720)).
    /// </remarks>
    [Test]
    public void DisposingTwiceIsNotAnError()
    {
        var server = new LoopbackServer();
        server.Dispose();

        var again = server.Dispose;

        again.Should().NotThrow();
    }
}
