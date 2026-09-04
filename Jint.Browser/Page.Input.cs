using AngleSharp.Html.Dom;
using Jint.Browser.Events;
using Jint.Browser.Extraction;
using Jint.Browser.Runtime;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.Browser;

/// <summary>
/// Driving a page the way a user does: a click, a fill, a key, a scroll, and waiting for what they cause.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every one of these is the path the protocol's own <c>Input</c> domain takes.</b> A click is a hit test
/// against the flat box model and then the pointer, mouse and activation sequence
/// <c>Events/InputDispatcher</c> fires; a key is the same dispatcher's <c>keydown</c> / <c>keypress</c> /
/// <c>beforeinput</c> / <c>input</c> chain over the same editor. So a caller in this process and a Puppeteer
/// or Playwright client on the socket cannot make a page do two different things, and everything either of
/// them fires is <c>isTrusted</c>, because both stand in for a user.
/// </para>
/// <para>
/// <b>What a target may be.</b> A CSS selector, or <c>ref=<i>n</i></c> — an identifier
/// <see cref="AccessibilitySnapshotAsync"/> printed, which is what lets a caller act on something it found by
/// role and name rather than having to invent a selector for it. A target that matches nothing answers
/// <see langword="false"/> rather than throwing: "there is no Save button" is an answer, not a fault.
/// </para>
/// </remarks>
public sealed partial class Page
{
    /// <summary>How long a wait sleeps between two looks at the document.</summary>
    /// <remarks>
    /// Off the loop, so the page runs its timers, its promises and its fetches in between — a wait that held
    /// the loop would be a wait for something the page could never do.
    /// </remarks>
    private static readonly TimeSpan WaitPoll = TimeSpan.FromMilliseconds(25);

    /// <summary>Clicks what <paramref name="target"/> names, and waits for any navigation it causes.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="options">How far to wait for a navigation the click causes; the defaults when omitted.</param>
    /// <returns><see langword="true"/> when an element matched and was clicked.</returns>
    /// <remarks>
    /// <para>
    /// The element is scrolled into view first, the way every client library's click path does, and the click
    /// lands at the centre of its box — so what is clicked is what a hit test at that point would find, which
    /// for a container is a descendant, exactly as in a browser, and the click bubbles back up.
    /// </para>
    /// <para>
    /// <b>The activation behaviour runs.</b> A link is followed, a submit button submits, a checkbox toggles,
    /// a <c>&lt;label&gt;</c> forwards to its control and a <c>&lt;summary&gt;</c> opens its
    /// <c>&lt;details&gt;</c> — and a navigation any of that starts is awaited before this task completes, so
    /// the page has committed the next document by the time a caller reads <see cref="Url"/>.
    /// </para>
    /// <para>
    /// An element with no box — <c>hidden</c>, <c>display: none</c>, <c>visibility: hidden</c> — is not
    /// clicked, and answers <see langword="false"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> ClickAsync(string target, NavigationOptions? options = null) => ClickAsync(target, 0, options);

    /// <summary>Clicks the indexed element that <paramref name="target"/> names.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <param name="options">How far to wait for a navigation the click causes; the defaults when omitted.</param>
    /// <returns><see langword="true"/> when the indexed element matched and was clicked.</returns>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public async Task<bool> ClickAsync(string target, int index, NavigationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_closed, this);

        var captured = await _loop.PostAsync((bool Clicked, NavigationRequest? Navigation) (engine) =>
        {
            if (PageRuntime.Find(engine) is not { } runtime || ElementLocator.Find(runtime.Document, target, index) is not { } element)
            {
                return (Clicked: false, Navigation: (NavigationRequest?) null);
            }

            runtime.Layout.ScrollIntoView(element, "nearest");

            if (runtime.Layout.Current().ClientBoxOf(element) is not { } box)
            {
                return (false, null);
            }

            var x = box.X + (box.Width / 2);
            var y = box.Y + (box.Height / 2);

            // Captured rather than started, for the reason SubmitFormAsync captures: the navigation a click
            // causes is one this caller can be handed instead of one that runs off on its own.
            _capturingNavigation = true;
            _capturedNavigation = null;

            try
            {
                InputDispatcher.DispatchMouse(runtime, new MouseInput(MouseInputKind.Moved, x, y, 0, 0, 0, EventModifiers.None, 0, 0));
                InputDispatcher.DispatchMouse(runtime, new MouseInput(MouseInputKind.Pressed, x, y, 0, 1, 1, EventModifiers.None, 0, 0));
                InputDispatcher.DispatchMouse(runtime, new MouseInput(MouseInputKind.Released, x, y, 0, 0, 1, EventModifiers.None, 0, 0));
                return (true, _capturedNavigation);
            }
            finally
            {
                _capturingNavigation = false;
                _capturedNavigation = null;
            }
        }).ConfigureAwait(false);

        if (captured.Navigation is { } navigation)
        {
            await NavigateCoreAsync(navigation with { Options = options ?? NavigationOptions.Default }).ConfigureAwait(false);
        }

        return captured.Clicked;
    }

    /// <summary>Moves the pointer over what <paramref name="target"/> names.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <returns><see langword="true"/> when an element matched and had a box to move to.</returns>
    /// <remarks>
    /// One <c>pointermove</c> and one <c>mousemove</c> at the centre of the element's box. <b>No boundary
    /// events</b> — no <c>mouseover</c>, <c>mouseenter</c>, <c>mouseout</c> or <c>mouseleave</c> — because
    /// those need the pointer's previous position and the ancestor chains of both elements, which this model
    /// does not keep. A hover menu written against <c>mouseenter</c> therefore does not open; one written
    /// against <c>mousemove</c> does.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> HoverAsync(string target) => HoverAsync(target, 0);

    /// <summary>Moves the pointer over the indexed element that <paramref name="target"/> names.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <returns><see langword="true"/> when the indexed element matched and had a box to move to.</returns>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> HoverAsync(string target, int index)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_closed, this);

        return _loop.PostAsync(engine =>
        {
            if (PageRuntime.Find(engine) is not { } runtime || ElementLocator.Find(runtime.Document, target, index) is not { } element)
            {
                return false;
            }

            if (runtime.Layout.Current().ClientBoxOf(element) is not { } box)
            {
                return false;
            }

            InputDispatcher.DispatchMouse(
                runtime,
                new MouseInput(MouseInputKind.Moved, box.X + (box.Width / 2), box.Y + (box.Height / 2), 0, 0, 0, EventModifiers.None, 0, 0));

            return true;
        });
    }

    /// <summary>Replaces the value of the control <paramref name="target"/> names with <paramref name="text"/>.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="text">The value to leave in the control.</param>
    /// <returns><see langword="true"/> when an editable element matched.</returns>
    /// <remarks>
    /// Focus, select all, then insert — which is one <c>beforeinput</c> and one <c>input</c> rather than the
    /// one per character <see cref="TypeAsync(string, string)"/> fires, and is what a client library's
    /// <c>fill</c> does. Use it when the value is what matters; use <see cref="TypeAsync(string, string)"/>
    /// when the page is listening for the
    /// keys.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> FillAsync(string target, string text) => FillAsync(target, 0, text);

    /// <summary>Replaces the value of the indexed control that <paramref name="target"/> names.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <param name="text">The value to leave in the control.</param>
    /// <returns><see langword="true"/> when the indexed editable element matched.</returns>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> FillAsync(string target, int index, string text)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(text);
        ObjectDisposedException.ThrowIf(_closed, this);

        return _loop.PostAsync(engine =>
        {
            if (PageRuntime.Find(engine) is not { } runtime || ElementLocator.Find(runtime.Document, target, index) is not { } element)
            {
                return false;
            }

            FocusController.Focus(runtime.Dom, element);

            // The dispatcher's own select-all, which is what a client sends as commands: ["selectAll"], so a
            // fill and a client's fill clear the control the same way rather than two ways.
            InputDispatcher.DispatchKey(
                runtime,
                KeyOptions.For("a") with { Modifiers = EventModifiers.Control, Commands = ["selectAll"] },
                KeyInputKind.RawKeyDown);

            InputDispatcher.InsertText(runtime, text);
            return true;
        });
    }

    /// <summary>Types <paramref name="text"/> into what <paramref name="target"/> names, one key at a time.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="text">The characters to type.</param>
    /// <returns><see langword="true"/> when an element matched and was focused.</returns>
    /// <remarks>
    /// The control is focused and each character becomes a <c>keydown</c>, a <c>keypress</c>, the insertion
    /// with its <c>beforeinput</c> and <c>input</c>, and a <c>keyup</c> — so a page filtering keystrokes, an
    /// autocomplete listening for <c>input</c> and a form's <c>maxlength</c> all see what they would see from
    /// a user. It does <b>not</b> clear the control first; <see cref="FillAsync(string, string)"/> is the one
    /// that does.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> TypeAsync(string target, string text) => TypeAsync(target, 0, text);

    /// <summary>Types into the indexed element that <paramref name="target"/> names.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <param name="text">The characters to type.</param>
    /// <returns><see langword="true"/> when the indexed element matched and was focused.</returns>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> TypeAsync(string target, int index, string text)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(text);
        ObjectDisposedException.ThrowIf(_closed, this);

        return _loop.PostAsync(engine =>
        {
            if (PageRuntime.Find(engine) is not { } runtime || ElementLocator.Find(runtime.Document, target, index) is not { } element)
            {
                return false;
            }

            FocusController.Focus(runtime.Dom, element);

            foreach (var rune in text.EnumerateRunes())
            {
                var key = rune.ToString();
                InputDispatcher.DispatchKey(runtime, KeyOptions.For(key), KeyInputKind.KeyDown);
                InputDispatcher.DispatchKey(runtime, KeyOptions.For(key), KeyInputKind.KeyUp);
            }

            return true;
        });
    }

    /// <summary>Presses one key at whatever the page has focused.</summary>
    /// <param name="key">A <c>KeyboardEvent.key</c> value: a character, or a name such as <c>Enter</c> or <c>Tab</c>.</param>
    /// <param name="options">How far to wait for a navigation the key causes; the defaults when omitted.</param>
    /// <returns><see langword="true"/> always, because a key always reaches something — the body when nothing is focused.</returns>
    /// <remarks>
    /// <c>Enter</c> in a single-line control is HTML's implicit submission, so a form it submits is navigated
    /// and awaited here the way <see cref="ClickAsync(string, NavigationOptions)"/> awaits a link's.
    /// <c>Tab</c> moves focus along the
    /// sequential focus order, the editing keys edit, and everything else fires its events and does nothing.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public async Task<bool> PressAsync(string key, NavigationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ObjectDisposedException.ThrowIf(_closed, this);

        var captured = await _loop.PostAsync(NavigationRequest? (engine) =>
        {
            if (PageRuntime.Find(engine) is not { } runtime)
            {
                return null;
            }

            _capturingNavigation = true;
            _capturedNavigation = null;

            try
            {
                InputDispatcher.DispatchKey(runtime, KeyOptions.For(key), KeyInputKind.KeyDown);
                InputDispatcher.DispatchKey(runtime, KeyOptions.For(key), KeyInputKind.KeyUp);
                return _capturedNavigation;
            }
            finally
            {
                _capturingNavigation = false;
                _capturedNavigation = null;
            }
        }).ConfigureAwait(false);

        if (captured is { } navigation)
        {
            await NavigateCoreAsync(navigation with { Options = options ?? NavigationOptions.Default }).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>Selects the option of a <c>&lt;select&gt;</c> whose value or label is <paramref name="value"/>.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="value">The option's <c>value</c>, or its text when no option carries that value.</param>
    /// <returns><see langword="true"/> when a <c>&lt;select&gt;</c> matched and had such an option.</returns>
    /// <remarks>
    /// The control is focused, the option is selected, and <c>input</c> and <c>change</c> fire in that order,
    /// which is what HTML's <i>send select update notifications</i> says and what every framework's
    /// <c>onChange</c> is bound to.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> SelectAsync(string target, string value) => SelectAsync(target, 0, value);

    /// <summary>Selects an option in the indexed <c>&lt;select&gt;</c> that <paramref name="target"/> names.</summary>
    /// <param name="target">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <param name="value">The option's value, or its text when no option carries that value.</param>
    /// <returns><see langword="true"/> when the indexed select matched and had such an option.</returns>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> SelectAsync(string target, int index, string value)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(value);
        ObjectDisposedException.ThrowIf(_closed, this);

        return _loop.PostAsync(engine =>
        {
            if (PageRuntime.Find(engine) is not { } runtime || ElementLocator.Find(runtime.Document, target, index) is not IHtmlSelectElement select)
            {
                return false;
            }

            var option = select.Options.FirstOrDefault(o => string.Equals(o.Value, value, StringComparison.Ordinal))
                ?? select.Options.FirstOrDefault(o => string.Equals(o.Text.Trim(), value, StringComparison.Ordinal));

            if (option is null)
            {
                return false;
            }

            FocusController.Focus(runtime.Dom, select);

            // The option's own activation behaviour, so a selection made here and one made by clicking the
            // option change the same state and fire the same two events at the same element.
            ActivationBehaviors.SelectOption(runtime.Dom, option);
            return true;
        });
    }

    /// <summary>Scrolls the page to <paramref name="y"/>, clamped to the document.</summary>
    /// <param name="y">The offset from the top of the document, in CSS pixels.</param>
    /// <remarks>
    /// The scroll is virtual — there is no layout, so the offset is a number every client rectangle subtracts
    /// — and it queues one <c>scroll</c> event at the document, which is what a page's own infinite-scroll
    /// listener is waiting for. <c>scrollX</c> is always zero, because every box is exactly the viewport wide.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task ScrollToAsync(double y)
    {
        ObjectDisposedException.ThrowIf(_closed, this);

        return _loop.PostAsync(engine =>
        {
            PageRuntime.Find(engine)?.Layout.ScrollTo(y);
            return true;
        });
    }

    /// <summary>Waits until an element matching <paramref name="selector"/> is in the document.</summary>
    /// <param name="selector">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="timeout">The ceiling on the wait.</param>
    /// <returns><see langword="true"/> when it appeared, <see langword="false"/> when the timeout won.</returns>
    /// <remarks>
    /// <b>Present, not visible.</b> An element that is in the document but has no box satisfies this: a
    /// visibility question needs a rendering, and this browser has none. The document is looked at every few
    /// milliseconds from off the loop, so the page goes on running its timers, its promises and its fetches
    /// while the wait lasts.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> WaitForSelectorAsync(string selector, TimeSpan timeout) => WaitForSelectorAsync(selector, 0, timeout);

    /// <summary>Waits until the indexed element matching <paramref name="selector"/> is in the document.</summary>
    /// <param name="selector">A CSS selector, or a <c>ref=</c> from an accessibility snapshot.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <param name="timeout">The ceiling on the wait.</param>
    /// <returns><see langword="true"/> when it appeared, <see langword="false"/> when the timeout won.</returns>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> WaitForSelectorAsync(string selector, int index, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ObjectDisposedException.ThrowIf(_closed, this);

        return WaitForAsync(engine => ElementLocator.Find(PageRuntime.Find(engine)?.Document, selector, index) is not null, timeout);
    }

    /// <summary>Waits until <paramref name="text"/> appears in the document's rendered text.</summary>
    /// <param name="text">The text to look for, compared literally.</param>
    /// <param name="timeout">The ceiling on the wait.</param>
    /// <returns><see langword="true"/> when it appeared, <see langword="false"/> when the timeout won.</returns>
    /// <remarks>
    /// The text is the one <see cref="TextAsync"/> answers — the document's <c>innerText</c>, so the contents
    /// of a <c>&lt;script&gt;</c> and of a hidden subtree do not count, and neither does an attribute.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> WaitForTextAsync(string text, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(text);
        ObjectDisposedException.ThrowIf(_closed, this);

        return WaitForAsync(
            engine => PageRuntime.Find(engine)?.Document is { } document
                && PageContent.Text(document, mainContentOnly: false, maxLength: 0).Contains(text, StringComparison.Ordinal),
            timeout);
    }

    /// <summary>Waits until <paramref name="expression"/> answers something truthy in the page.</summary>
    /// <param name="expression">A JavaScript expression, evaluated in the page between looks.</param>
    /// <param name="timeout">The ceiling on the wait.</param>
    /// <returns>
    /// <see langword="true"/> when it became truthy, <see langword="false"/> when the timeout won or the
    /// page closed first.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The general form of the two waits above, for a condition neither an element nor a piece of text can
    /// state — <c>location.pathname === '/next'</c>, a list that has reached a length, a flag a framework
    /// set. It is the same wait: the page is looked at from off the loop, so it goes on running its timers,
    /// its promises and its fetches while this lasts.
    /// </para>
    /// <para>
    /// <b>An expression that throws is not yet true</b>, so
    /// <c>document.querySelector('#x').textContent === 'y'</c> is a usable condition while <c>#x</c> is
    /// still being rendered. If the timeout wins and the last evaluation threw, that failure is what comes
    /// out rather than <see langword="false"/> — a typo in the expression is a failure with a reason, not a
    /// silent wait.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public async Task<bool> WaitForAsync(string expression, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ObjectDisposedException.ThrowIf(_closed, this);

        Exception? lastFailure = null;

        var answered = await WaitForAsync(
            engine =>
            {
                try
                {
                    var truthy = TypeConverter.ToBoolean(engine.Evaluate(expression));
                    lastFailure = null;
                    return truthy;
                }
                catch (JavaScriptException exception)
                {
                    // Not yet true: a condition written against an element a framework has not rendered
                    // throws until it has. Kept so that a timeout can say why rather than only that.
                    lastFailure = exception;
                    return false;
                }
            },
            timeout).ConfigureAwait(false);

        return answered || lastFailure is null ? answered : throw lastFailure;
    }
    /// <summary>Looks at the document until <paramref name="condition"/> holds, or until the time runs out.</summary>
    private async Task<bool> WaitForAsync(Func<Engine, bool> condition, TimeSpan timeout)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();

        while (true)
        {
            if (await _loop.PostAsync(condition).ConfigureAwait(false))
            {
                return true;
            }

            if (_closed
                || (timeout != System.Threading.Timeout.InfiniteTimeSpan
                    && System.Diagnostics.Stopwatch.GetElapsedTime(started) >= timeout))
            {
                return false;
            }

            await Task.Delay(WaitPoll).ConfigureAwait(false);
        }
    }
}
