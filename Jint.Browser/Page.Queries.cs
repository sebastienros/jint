using Jint.Browser.Dom;
using Jint.Browser.Extraction;
using Jint.Browser.Layout;
using Jint.Browser.Runtime;
using Jint.HtmlParser;

namespace Jint.Browser;

public sealed partial class Page
{
    /// <summary>Counts target matches, optionally requiring checkVisibility with visibilityProperty enabled.</summary>
    /// <param name="selector">
    /// A target: a CSS selector, a <c>ref=</c> from an accessibility snapshot, or a <c>role=</c>,
    /// <c>label=</c> or <c>text=</c> locator, optionally chained with <c>&gt;&gt;</c>.
    /// </param>
    /// <param name="visibleOnly">Whether only matches that pass checkVisibility are counted.</param>
    /// <remarks>
    /// No script or DOM wrappers are needed. An invalid selector or malformed locator throws a
    /// <c>SyntaxError</c> <see cref="DomException"/>; opacity and occlusion do not affect visibility.
    /// </remarks>
    public Task<int> CountAsync(string selector, bool visibleOnly = false)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ObjectDisposedException.ThrowIf(_closed, this);
        return _loop.PostAsync(engine => Count(PageRuntime.Find(engine)!, ElementLocator.Parse(selector), visibleOnly));
    }

    /// <summary>Reports whether the indexed target passes checkVisibility with visibilityProperty enabled.</summary>
    /// <remarks>
    /// A target is a CSS selector, a snapshot reference, or a <c>role=</c>, <c>label=</c> or <c>text=</c>
    /// locator, optionally chained with <c>&gt;&gt;</c>. A missing or malformed target is not visible.
    /// </remarks>
    public Task<bool> IsVisibleAsync(string target, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_closed, this);
        return _loop.PostAsync(engine => IsVisible(PageRuntime.Find(engine)!, target, index));
    }

    /// <summary>Reads the indexed target's rendered innerText, or null when no target matches.</summary>
    /// <remarks>Uses the same renderless text extraction as the DOM innerText getter, not visual line wrapping.</remarks>
    public Task<string?> InnerTextAsync(string target, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_closed, this);
        return _loop.PostAsync(engine => InnerText(PageRuntime.Find(engine)!, target, index));
    }

    /// <summary>Snapshots each target match's textContent in document order in one page-loop request.</summary>
    /// <param name="selector">
    /// A target: a CSS selector, a <c>ref=</c> from an accessibility snapshot, or a <c>role=</c>,
    /// <c>label=</c> or <c>text=</c> locator, optionally chained with <c>&gt;&gt;</c>.
    /// </param>
    /// <param name="visibleOnly">Whether only matches that pass checkVisibility are read.</param>
    /// <remarks>
    /// Text is not trimmed or rendered: hidden descendant text is included. visibleOnly filters the matched
    /// elements using checkVisibility with visibilityProperty enabled, not their text descendants.
    /// Invalid selectors and malformed locators throw. The returned array contains only detached CLR strings.
    /// </remarks>
    public Task<string[]> TextsAsync(string selector, bool visibleOnly = false)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ObjectDisposedException.ThrowIf(_closed, this);
        return _loop.PostAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            if (runtime.Document is not { } document) return [];
            var elements = ElementLocator.Parse(selector).FindAll(runtime.Dom, document);
            var texts = new List<string>(elements.Length);
            foreach (var element in elements)
            {
                engine.Constraints.Check();
                if (!visibleOnly || IsVisible(runtime, element))
                    texts.Add(DomDescendantText.Read(element, runtime.Dom.NativeReadCheckpoint, runtime.Dom.CancellationToken));
            }
            return texts.ToArray();
        });
    }

    /// <summary>Waits for an exact match count, optionally counting only visible matches.</summary>
    public Task<bool> WaitForCountAsync(string selector, int count, TimeSpan timeout, bool visibleOnly = false)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ObjectDisposedException.ThrowIf(_closed, this);
        ElementLocator? locator = null;
        return WaitForAsync(engine =>
        {
            locator ??= ElementLocator.Parse(selector);
            return Count(PageRuntime.Find(engine)!, locator, visibleOnly) == count;
        }, timeout);
    }

    /// <summary>Waits for the indexed target to become visible, or hidden/missing when visible is false.</summary>
    public Task<bool> WaitForVisibleAsync(string target, TimeSpan timeout, bool visible = true, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_closed, this);
        var locator = ElementLocator.TryParse(target);
        return WaitForAsync(engine => IsVisible(PageRuntime.Find(engine)!, locator, index) == visible, timeout);
    }

    /// <summary>Waits for literal ordinal text within the indexed target's rendered innerText.</summary>
    public Task<bool> WaitForTextAsync(string target, string text, TimeSpan timeout, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(text);
        ObjectDisposedException.ThrowIf(_closed, this);
        var locator = ElementLocator.TryParse(target);
        return WaitForAsync(engine => InnerText(PageRuntime.Find(engine)!, locator, index)?.Contains(text, StringComparison.Ordinal) == true, timeout);
    }

    /// <summary>Waits until the indexed target's rendered innerText matches <paramref name="pattern"/>.</summary>
    /// <param name="target">A target: a CSS selector, a <c>ref=</c> from an accessibility snapshot, or a <c>role=</c>, <c>label=</c> or <c>text=</c> locator, optionally chained with <c>&gt;&gt;</c>.</param>
    /// <param name="pattern">A pattern searched for in the text; anchor it to match the whole text.</param>
    /// <param name="timeout">The ceiling on the wait.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <returns><see langword="true"/> when the text matched, <see langword="false"/> when the timeout won.</returns>
    /// <remarks>
    /// The text is the one <see cref="InnerTextAsync"/> reads, on the page's thread; the pattern is matched off
    /// it, so a slow expression never holds the page. A missing target matches nothing.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="pattern"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> WaitForTextAsync(string target, System.Text.RegularExpressions.Regex pattern, TimeSpan timeout, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(pattern);
        ObjectDisposedException.ThrowIf(_closed, this);
        var locator = ElementLocator.TryParse(target);
        return WaitForAsync(engine => InnerText(PageRuntime.Find(engine)!, locator, index), text => text is not null && pattern.IsMatch(text), timeout);
    }

    /// <summary>
    /// Waits for a response to a request the page sends from now on that <paramref name="predicate"/> accepts.
    /// </summary>
    /// <param name="predicate">
    /// Called off the page's thread with each request once it has a response or has failed; the first it
    /// accepts is the answer.
    /// </param>
    /// <param name="timeout">The ceiling on the wait.</param>
    /// <returns>The request, its status and response headers in, or <see langword="null"/> when the timeout won.</returns>
    /// <remarks>
    /// <para>
    /// <b>Only requests sent after the call count</b>, so the wait is started before the action that sends the
    /// request and awaited after it: <c>var saved = page.WaitForResponseAsync(...); await page.ClickAsync(...);
    /// await saved;</c>. A request the log has already recorded, completed or not, is never the answer.
    /// </para>
    /// <para>
    /// It reads <see cref="Requests"/>, so a request is complete when its headers are in, not when its body
    /// has been read, and a failed request — refused, aborted, timed out — completes it too.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public async Task<PageRequest?> WaitForResponseAsync(Func<PageRequest, bool> predicate, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ObjectDisposedException.ThrowIf(_closed, this);

        var after = 0L;
        foreach (var request in _requests.Requests)
        {
            after = Math.Max(after, request.Id);
        }

        PageRequest? answer = null;
        var answered = await WaitForAsync(static _ => true, _ =>
        {
            foreach (var request in _requests.Requests)
            {
                if (request.Id > after && (request.Status != 0 || request.Failed) && predicate(request))
                {
                    answer = request;
                    return true;
                }
            }

            return false;
        }, timeout).ConfigureAwait(false);

        return answered ? answer : null;
    }

    private static int Count(PageRuntime runtime, ElementLocator locator, bool visibleOnly)
    {
        if (runtime.Document is not { } document) return 0;
        var matches = locator.FindAll(runtime.Dom, document);
        if (!visibleOnly) return matches.Length;
        var count = 0;
        foreach (var element in matches)
        {
            runtime.Dom.Engine.Constraints.Check();
            if (IsVisible(runtime, element)) count++;
        }
        return count;
    }

    private static bool IsVisible(PageRuntime runtime, string target, int index)
        => IsVisible(runtime, ElementLocator.TryParse(target), index);

    // A wait parses its target once, before it starts polling, and resolves the parsed form on every look.
    private static bool IsVisible(PageRuntime runtime, ElementLocator? locator, int index)
        => locator?.TryFind(runtime.Dom, runtime.Document, index) is { } element && IsVisible(runtime, element);

    private static bool IsVisible(PageRuntime runtime, Element element)
        => LayoutMembers.IsCheckVisible(runtime.Dom, element, checkOpacity: false, checkVisibility: true);

    private static string? InnerText(PageRuntime runtime, string target, int index)
        => InnerText(runtime, ElementLocator.TryParse(target), index);

    private static string? InnerText(PageRuntime runtime, ElementLocator? locator, int index)
        => locator?.TryFind(runtime.Dom, runtime.Document, index) is { } element
            ? TextExtractor.InnerText(element, useComputedStyle: true, runtime.Dom.NativeReadCheckpoint, runtime.Dom.CancellationToken)
            : null;
}
