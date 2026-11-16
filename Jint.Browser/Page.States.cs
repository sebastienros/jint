using System.Text.RegularExpressions;
using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.Browser.Dom.Files;
using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.HtmlParser;

namespace Jint.Browser;

/// <summary>
/// The states a browser test asserts about — the URL, an attribute, focus, a control's value and its
/// checkedness — read and waited for natively, without a JavaScript predicate re-evaluated on every look.
/// </summary>
/// <remarks>
/// <para>
/// <b>Each wait is the same wait as <see cref="WaitForTextAsync(string, string, TimeSpan, int)"/>.</b> The
/// document is looked at from off the loop every few milliseconds, one mailbox request per look, so the page
/// goes on running its timers, its promises and its fetches while the wait lasts; each look resolves the target
/// against the document the page has <i>now</i>, so a navigation, a mutation or a framework re-render is
/// observed rather than a stale element. The timeout answers <see langword="false"/>; so does a page closed
/// while the wait lasts.
/// </para>
/// <para>
/// <b>State, not markup.</b> A control's value and checkedness are the ones its IDL attributes answer, so
/// <c>input.value = 'x'</c> and <c>checkbox.checked = true</c> — which change no content attribute and which no
/// mutation observer sees — are observed exactly as a user's typing or click is.
/// </para>
/// <para>
/// <b>Where caller code runs.</b> A <see cref="Regex"/> or a <see cref="Func{T, TResult}"/> is applied to a
/// detached string or <see cref="Uri"/> off the page's thread, so a slow pattern or predicate delays only the
/// wait, never the page, and an exception it throws faults the returned task.
/// </para>
/// </remarks>
public sealed partial class Page
{
    /// <summary>The roles whose checkedness is their <c>aria-checked</c> attribute, as Playwright's are.</summary>
    private static readonly HashSet<string> AriaCheckedRoles = new(StringComparer.Ordinal)
    {
        "checkbox", "menuitemcheckbox", "menuitemradio", "option", "radio", "switch", "treeitem",
    };

    /// <summary>Reads an attribute of the indexed target.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="name">The attribute's qualified name, matched the way <c>getAttribute</c> matches it.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <returns>
    /// The attribute's value, or <see langword="null"/> when the element has no such attribute or no element
    /// matches.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="name"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<string?> GetAttributeAsync(string target, string name, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(name);
        ObjectDisposedException.ThrowIf(_closed, this);
        return _loop.PostAsync(engine => ReadAttribute(engine, target, name, index).Value);
    }

    /// <summary>Reads the current value of the indexed <c>input</c>, <c>textarea</c> or <c>select</c>.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <returns>
    /// What the control's <c>value</c> IDL attribute answers, or <see langword="null"/> when no element matches
    /// or the element is not one of those three controls.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<string?> InputValueAsync(string target, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_closed, this);
        return _loop.PostAsync(engine => ReadValue(engine, target, index));
    }

    /// <summary>Reads whether the indexed checkbox or radio button is checked.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <returns>
    /// The checkedness of an <c>&lt;input type=checkbox|radio&gt;</c> — its <c>checked</c> IDL attribute — or,
    /// for an element whose explicit <c>role</c> is <c>checkbox</c>, <c>radio</c>, <c>switch</c>, <c>option</c>,
    /// <c>treeitem</c>, <c>menuitemcheckbox</c> or <c>menuitemradio</c>, whether its <c>aria-checked</c> is
    /// <c>true</c>. <see langword="null"/> when no element matches, the element is neither, or its
    /// <c>aria-checked</c> is <c>mixed</c>. A native checkbox's <c>indeterminate</c> flag does not change its
    /// checkedness, so it answers its <c>checked</c> state.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool?> IsCheckedAsync(string target, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_closed, this);
        return _loop.PostAsync(engine => ReadChecked(engine, target, index));
    }

    /// <summary>Reads whether the indexed target is the page's focused element.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <returns>
    /// <see langword="true"/> when the element itself holds focus — the element user input reaches, so a
    /// focused control inside a shadow tree is focused and its host is not. A missing target is not focused:
    /// removing the focused element from the document is one of the ways focus leaves it.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> IsFocusedAsync(string target, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_closed, this);
        return _loop.PostAsync(engine => IsFocused(engine, target, index));
    }

    /// <summary>Waits until the page's <see cref="Url"/> is <paramref name="url"/>.</summary>
    /// <param name="url">
    /// The URL to wait for, compared exactly once both are serialized. A relative URL is resolved against the
    /// URL each look compares it with, so <c>"?page=2"</c> waits for the current document's path with that
    /// query and <c>"/dashboard"</c> waits for that path on whichever origin the page reaches.
    /// </param>
    /// <param name="timeout">The ceiling on the wait.</param>
    /// <returns><see langword="true"/> when the URL matched, <see langword="false"/> when the timeout won.</returns>
    /// <remarks>
    /// The URL is the one <see cref="Url"/> answers, so a same-document <c>history.pushState</c>,
    /// <c>history.replaceState</c>, fragment navigation or traversal satisfies the wait as well as a real
    /// navigation.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> WaitForUrlAsync(string url, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(url);
        ObjectDisposedException.ThrowIf(_closed, this);
        return WaitForAsync(
            ReadUrl,
            current => string.Equals(current, PageUrl.Resolve(ResolveHostUrl(url), current), StringComparison.Ordinal),
            timeout);
    }

    /// <summary>Waits until the page's <see cref="Url"/> matches <paramref name="pattern"/>.</summary>
    /// <param name="pattern">A pattern searched for in the serialized URL; anchor it to match the whole URL.</param>
    /// <param name="timeout">The ceiling on the wait.</param>
    /// <returns><see langword="true"/> when the URL matched, <see langword="false"/> when the timeout won.</returns>
    /// <remarks>Same-document URL changes count, as they do for <see cref="WaitForUrlAsync(string, TimeSpan)"/>.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> WaitForUrlAsync(Regex pattern, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ObjectDisposedException.ThrowIf(_closed, this);
        return WaitForAsync(ReadUrl, pattern.IsMatch, timeout);
    }

    /// <summary>Waits until <paramref name="predicate"/> holds for the page's <see cref="Url"/>.</summary>
    /// <param name="predicate">
    /// Called off the page's thread with the URL on every look; a URL <see cref="Uri"/> cannot represent is
    /// not passed to it and does not match.
    /// </param>
    /// <param name="timeout">The ceiling on the wait.</param>
    /// <returns><see langword="true"/> when the predicate held, <see langword="false"/> when the timeout won.</returns>
    /// <remarks>Same-document URL changes count, as they do for <see cref="WaitForUrlAsync(string, TimeSpan)"/>.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> WaitForUrlAsync(Func<Uri, bool> predicate, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ObjectDisposedException.ThrowIf(_closed, this);
        return WaitForAsync(
            ReadUrl,
            current => Uri.TryCreate(current, UriKind.Absolute, out var uri) && predicate(uri),
            timeout);
    }

    /// <summary>Waits until an attribute of the indexed target has <paramref name="value"/>, or is absent.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="name">The attribute's qualified name, matched the way <c>getAttribute</c> matches it.</param>
    /// <param name="value">
    /// The exact value to wait for, compared ordinally, or <see langword="null"/> to wait for the attribute to
    /// be absent.
    /// </param>
    /// <param name="timeout">The ceiling on the wait.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <returns><see langword="true"/> when the state was reached, <see langword="false"/> when the timeout won.</returns>
    /// <remarks>
    /// <para>
    /// The target has to be in the document either way: a missing element has no attributes to be absent, and
    /// a wait for absence that an element not yet rendered satisfied would answer before the page had.
    /// </para>
    /// <para>
    /// A literal <see langword="null"/> is spelled <c>value: null</c>: written positionally it converts to the
    /// <see cref="Regex"/> overload's pattern too, and the compiler refuses the ambiguous call rather than
    /// choosing one.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="name"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> WaitForAttributeAsync(string target, string name, string? value, TimeSpan timeout, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(name);
        ObjectDisposedException.ThrowIf(_closed, this);
        return WaitForAsync(
            engine => ReadAttribute(engine, target, name, index),
            read => read.Found && string.Equals(read.Value, value, StringComparison.Ordinal),
            timeout);
    }

    /// <summary>Waits until an attribute of the indexed target is present and matches <paramref name="pattern"/>.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="name">The attribute's qualified name, matched the way <c>getAttribute</c> matches it.</param>
    /// <param name="pattern">A pattern searched for in the attribute's value; anchor it to match the whole value.</param>
    /// <param name="timeout">The ceiling on the wait.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <returns><see langword="true"/> when the value matched, <see langword="false"/> when the timeout won.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/>, <paramref name="name"/> or <paramref name="pattern"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> WaitForAttributeAsync(string target, string name, Regex pattern, TimeSpan timeout, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(pattern);
        ObjectDisposedException.ThrowIf(_closed, this);
        return WaitForAsync(
            engine => ReadAttribute(engine, target, name, index).Value,
            read => read is not null && pattern.IsMatch(read),
            timeout);
    }

    /// <summary>Waits until the indexed target holds focus, or no longer does when <paramref name="focused"/> is false.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="timeout">The ceiling on the wait.</param>
    /// <param name="focused">Whether to wait for focus to arrive (the default) or to leave.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <returns><see langword="true"/> when the state was reached, <see langword="false"/> when the timeout won.</returns>
    /// <remarks>Focus is the one <see cref="IsFocusedAsync"/> reads; a missing target is not focused.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> WaitForFocusAsync(string target, TimeSpan timeout, bool focused = true, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_closed, this);
        return WaitForAsync(engine => IsFocused(engine, target, index) == focused, timeout);
    }

    /// <summary>Waits until the indexed control's current value is <paramref name="value"/>.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="value">The exact value to wait for, compared ordinally.</param>
    /// <param name="timeout">The ceiling on the wait.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <returns><see langword="true"/> when the value matched, <see langword="false"/> when the timeout won.</returns>
    /// <remarks>
    /// The value is the one <see cref="InputValueAsync"/> reads, so the target has to be an <c>input</c>, a
    /// <c>textarea</c> or a <c>select</c>, and a script's <c>input.value = ...</c> counts as well as typing.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="value"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> WaitForValueAsync(string target, string value, TimeSpan timeout, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(value);
        ObjectDisposedException.ThrowIf(_closed, this);
        return WaitForAsync(engine => string.Equals(ReadValue(engine, target, index), value, StringComparison.Ordinal), timeout);
    }

    /// <summary>Waits until the indexed control's current value matches <paramref name="pattern"/>.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="pattern">A pattern searched for in the value; anchor it to match the whole value.</param>
    /// <param name="timeout">The ceiling on the wait.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <returns><see langword="true"/> when the value matched, <see langword="false"/> when the timeout won.</returns>
    /// <remarks>The value is the one <see cref="InputValueAsync"/> reads.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="pattern"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> WaitForValueAsync(string target, Regex pattern, TimeSpan timeout, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(pattern);
        ObjectDisposedException.ThrowIf(_closed, this);
        return WaitForAsync(
            engine => ReadValue(engine, target, index),
            read => read is not null && pattern.IsMatch(read),
            timeout);
    }

    /// <summary>Waits until the indexed checkbox or radio button is checked, or unchecked.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="timeout">The ceiling on the wait.</param>
    /// <param name="isChecked">The checkedness to wait for: checked (the default) or unchecked.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <returns><see langword="true"/> when the state was reached, <see langword="false"/> when the timeout won.</returns>
    /// <remarks>
    /// Checkedness is the one <see cref="IsCheckedAsync"/> reads, so the target has to be present and
    /// checkable either way, and a script's <c>checkbox.checked = true</c> counts as well as a click.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> WaitForCheckedAsync(string target, TimeSpan timeout, bool isChecked = true, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_closed, this);
        return WaitForAsync(engine => ReadChecked(engine, target, index) == isChecked, timeout);
    }

    /// <summary>One look's URL, read on the loop so it is ordered after the requests before it.</summary>
    private string ReadUrl(Engine engine) => _url;

    private static Element? Locate(Engine engine, string target, int index)
        => PageRuntime.Find(engine) is { } runtime ? ElementLocator.Find(runtime.Dom, runtime.Document, target, index) : null;

    private static (bool Found, string? Value) ReadAttribute(Engine engine, string target, string name, int index)
        => Locate(engine, target, index) is { } element ? (true, element.GetAttribute(name)) : (false, null);

    /// <summary>https://html.spec.whatwg.org/multipage/form-elements.html — each control's <c>value</c> getter.</summary>
    private static string? ReadValue(Engine engine, string target, int index)
    {
        if (Locate(engine, target, index) is not { NamespaceUri: Namespaces.Html } element)
        {
            return null;
        }

        var realm = DomRealm.Of(engine);
        return element.LocalName switch
        {
            "input" => FileTransferMembers.InputValue(realm, element).AsString(),
            "textarea" => DomTextAreaMembers.Value(realm, element),
            "select" => DomSelectMembers.State(realm, element).GetValue(realm.NativeReadCheckpoint, realm.CancellationToken),
            _ => null,
        };
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/input.html#concept-fe-checked for a checkbox or radio button,
    /// https://w3c.github.io/aria/#aria-checked for an element whose explicit role takes it.
    /// </summary>
    /// <remarks>
    /// A native control answers its checkedness, which <c>indeterminate</c> does not change: HTML keeps the two
    /// apart, and so does Playwright's <c>toBeChecked</c>. The accessibility tree reports such a checkbox as
    /// <c>mixed</c>, because that is what assistive technology announces. <c>aria-checked="mixed"</c> has no
    /// checkedness to fall back on and answers <see langword="null"/>, so it satisfies neither wait.
    /// </remarks>
    private static bool? ReadChecked(Engine engine, string target, int index)
    {
        if (Locate(engine, target, index) is not { } element)
        {
            return null;
        }

        if (element is { NamespaceUri: Namespaces.Html, LocalName: "input" }
            && HtmlInputTypes.Get(element) is HtmlInputType.Checkbox or HtmlInputType.Radio)
        {
            return DomInputMembers.Checked(DomRealm.Of(engine), element);
        }

        if (AriaRoles.Explicit(element.GetAttribute("role")) is not { } role || !AriaCheckedRoles.Contains(role))
        {
            return null;
        }

        var state = element.GetAttribute("aria-checked")?.Trim();
        return string.Equals(state, "mixed", StringComparison.OrdinalIgnoreCase)
            ? null
            : string.Equals(state, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether the located element holds focus: the realm's focused element, still connected to the document
    /// whose focus store names it — the facts <c>document.activeElement</c> checks, without its retargeting walk
    /// and without clearing a stale focus, which is the getter's business rather than a reader's.
    /// </summary>
    private static bool IsFocused(Engine engine, string target, int index)
    {
        if (PageRuntime.Find(engine) is not { Document: { } document } runtime
            || ElementLocator.Find(runtime.Dom, document, target, index) is not { } element
            || !ReferenceEquals(BrowserEventRealm.Of(engine).FocusedElement, element)
            || !ReferenceEquals(BrowserEventRealm.FocusedElementOf(document), element))
        {
            return false;
        }

        // A snapshot reference can outlive its element's place in the tree; a selector match cannot.
        for (Node? node = element; node is not null; node = node.ParentNode ?? (node as ShadowRoot)?.Host)
        {
            engine.Constraints.Check();
            if (ReferenceEquals(node, document))
            {
                return true;
            }
        }

        return false;
    }
}
