using Jint.Browser.Runtime;

namespace Jint.Browser;

public sealed partial class Page
{
    /// <summary>
    /// Emulates a media type and the user preferences the page's media queries are answered from.
    /// </summary>
    /// <param name="emulation">
    /// What to change; a property left <see langword="null"/> keeps the override already in force, and an
    /// empty one clears it, back to the browser or context default the page opened with. See
    /// <see cref="MediaEmulation"/>.
    /// </param>
    /// <returns>A task that completes once the loaded document has been re-evaluated against the change.</returns>
    /// <remarks>
    /// <para>
    /// <b>This is <c>Emulation.setEmulatedMedia</c>, in process.</b> Both write the page's one emulation
    /// state, so a host here and a protocol client on the socket cannot leave a page in two different media
    /// environments. The difference is the shape of a call: the protocol command replaces the media type and
    /// the whole feature list, while this merges, as Playwright's <c>page.emulateMedia</c> does — emulating
    /// reduced motion and then a dark scheme leaves both in force.
    /// </para>
    /// <para>
    /// <b>The document that is already loaded moves.</b> Every <c>matchMedia</c> list is re-evaluated, and
    /// each whose answer changed fires <c>change</c> before this task completes; style sheet <c>@media</c>
    /// rules follow on the next style read, through <c>getComputedStyle</c> and everything else that reads
    /// the cascade. No <c>resize</c> event is fired.
    /// </para>
    /// <para>
    /// It is the page's rather than a document's, so it survives every navigation after it — the next
    /// document parses in it. <see cref="BrowserOptions.MediaEmulation"/> and
    /// <see cref="BrowserContextOptions.MediaEmulation"/> are the same setting a level up, for a page whose
    /// first document must already parse in it.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="emulation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task EmulateMediaAsync(MediaEmulation emulation)
    {
        ArgumentNullException.ThrowIfNull(emulation);
        ObjectDisposedException.ThrowIf(_closed, this);

        return _loop.PostAsync(engine =>
        {
            Emulation.EmulateMedia(emulation, PageRuntime.Find(engine));
            return true;
        });
    }
}
