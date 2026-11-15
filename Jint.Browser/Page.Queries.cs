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
