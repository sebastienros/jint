using System.Diagnostics;
using Jint.Browser.Extraction;
using Jint.Browser.Runtime;

namespace Jint.Browser;

/// <summary>
/// What a browser that renders nothing answers "show me the page" with, and the wait a client spells
/// <c>networkidle</c>.
/// </summary>
/// <remarks>
/// These are the host-side halves of the custom <c>Jint</c> protocol domain — <c>Jint.getMarkdown</c>,
/// <c>Jint.getText</c> and <c>Jint.getAccessibilitySnapshot</c> — so a caller in the same process reads the
/// page the way an attached client does, over the same <c>Extraction/</c> and <c>Accessibility/</c> code and
/// with no protocol in between. None of them runs a line of the page's script.
/// </remarks>
public sealed partial class Page
{
    /// <summary>The document rendered as CommonMark, for a reader whose budget is tokens rather than pixels.</summary>
    /// <param name="mainContentOnly">
    /// Whether to render only the document's main content — the first <c>&lt;main&gt;</c>, <c>[role=main]</c>
    /// or <c>&lt;article&gt;</c> — when it has one.
    /// </param>
    /// <param name="maxLength">The greatest number of characters to return, or zero for no limit.</param>
    /// <returns>The markdown, or an empty string when the page holds no document.</returns>
    /// <remarks>
    /// A result cut short by <paramref name="maxLength"/> ends at the last white space before the limit and
    /// carries a <c>[truncated]</c> marker, so a short page and a cut one are told apart without counting.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<string> MarkdownAsync(bool mainContentOnly = false, int maxLength = 0)
        => _loop.PostAsync(engine => PageRuntime.Find(engine)?.Document is { } document
            ? PageContent.Markdown(document, mainContentOnly, maxLength)
            : "");

    /// <summary>The document's rendered text, which is <c>innerText</c> over the whole document.</summary>
    /// <param name="mainContentOnly">Whether to read only the document's main content, when it has one.</param>
    /// <param name="maxLength">The greatest number of characters to return, or zero for no limit.</param>
    /// <returns>The text, or an empty string when the page holds no document.</returns>
    /// <remarks>
    /// It is the text of the document rather than of a rendering of it: the required line breaks, the
    /// <c>&lt;br&gt;</c>s, the cell tabs and the white-space processing are all there, but nothing wraps, so
    /// a paragraph is one line however wide it would have been.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<string> TextAsync(bool mainContentOnly = false, int maxLength = 0)
        => _loop.PostAsync(engine => PageRuntime.Find(engine)?.Document is { } document
            ? PageContent.Text(document, mainContentOnly, maxLength)
            : "");

    /// <summary>The document's accessibility tree, rendered as an indented snapshot.</summary>
    /// <param name="mainContentOnly">Whether to walk only the document's main content, when it has one.</param>
    /// <param name="maxLength">The greatest number of characters to return, or zero for no limit.</param>
    /// <param name="includeReferences">
    /// Whether each element node carries <c>[ref=<i>n</i>]</c>, which
    /// <see cref="ClickAsync(string, NavigationOptions)"/> and the rest
    /// of the input members accept in place of a selector.
    /// </param>
    /// <returns>The snapshot, or an empty string when the page holds no document.</returns>
    /// <remarks>
    /// <para>
    /// One line per interesting node — its role, its accessible name and the states that matter — with the
    /// text between them, which is the shape an agent reads a page from. It is computed per call and never
    /// maintained, and it has no layout behind it: an element that is off screen, clipped or covered is not
    /// one this can tell from an element that is not.
    /// </para>
    /// <para>
    /// <b>A reference belongs to the document it was printed from.</b> It stays valid for as long as that
    /// document does, and a navigation ends every one of them: resolving an old reference afterwards finds
    /// nothing rather than the wrong element.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<string> AccessibilitySnapshotAsync(bool mainContentOnly = false, int maxLength = 0, bool includeReferences = false)
        => _loop.PostAsync(engine => PageRuntime.Find(engine)?.Document is { } document
            ? PageContent.AccessibilitySnapshot(document, mainContentOnly, maxLength, includeReferences)
            : "");

    /// <summary>Waits until the page has made no request for half a second.</summary>
    /// <param name="timeout">The ceiling on how long to wait.</param>
    /// <returns>
    /// <see langword="true"/> when the network went quiet, <see langword="false"/> when the timeout won or
    /// the page closed first.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is what a client library spells <c>waitUntil: "networkidle"</c>, and it is the same half-second
    /// quiet period the protocol's own <c>networkIdle</c> lifecycle event is timed with — Chrome's number,
    /// and the one every client was written against. Await it after a navigation rather than instead of one:
    /// a page whose document has not started loading is quiet by this measure.
    /// </para>
    /// <para>
    /// <b>It does not hold the page's thread.</b> The quiet is timed off the loop, from the request log,
    /// because a page with nothing scheduled never turns its loop and would never notice — which is also why
    /// this and <see cref="WaitForIdleAsync"/> answer different questions. That one waits for the
    /// <i>engine</i> to have nothing left to run, and a page with a <c>setInterval</c> never does.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public async Task<bool> WaitForNetworkIdleAsync(TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_closed, this);

        var started = Stopwatch.GetTimestamp();

        while (true)
        {
            var (inFlight, lastChange) = _requests.Activity;
            if (inFlight == 0 && Stopwatch.GetElapsedTime(lastChange) >= NetworkQuietPeriod)
            {
                return true;
            }

            if (_closed
                || (timeout != System.Threading.Timeout.InfiniteTimeSpan
                    && Stopwatch.GetElapsedTime(started) >= timeout))
            {
                return false;
            }

            await Task.Delay(NetworkQuietPoll).ConfigureAwait(false);
        }
    }
}
