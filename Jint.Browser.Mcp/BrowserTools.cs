using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Jint.Browser.Mcp;

/// <summary>
/// The tools an agent drives a page with.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every description here is written for a model, not for a reader of documentation.</b> A tool an agent
/// cannot tell from its neighbour is a tool it calls wrongly, so each says what it does, what it takes, and —
/// where there is one — what to call first. The three that matter most are <c>snapshot</c>, which is how the
/// page is read at all, and the pair of ways to name an element: a <c>ref=</c> the snapshot printed, or a CSS
/// selector.
/// </para>
/// <para>
/// <b>No tool throws through the transport.</b> Each answers a <see cref="CallToolResult"/> — the result as
/// JSON on success, and <c>isError</c> with one sentence on failure, so an agent reads what went wrong and
/// decides what to do next rather than seeing a protocol error it cannot act on. That is deliberate rather
/// than incidental: the SDK would turn an ordinary exception into <c>isError</c> with the message
/// <i>redacted</i>, which tells an agent nothing.
/// </para>
/// </remarks>
[McpServerToolType]
public sealed class BrowserTools
{
    private readonly BrowserAgent _agent;

    /// <summary>Creates the tool set over one session's browser.</summary>
    /// <param name="agent">The session's browser, resolved from the host's services.</param>
    /// <exception cref="ArgumentNullException"><paramref name="agent"/> is <see langword="null"/>.</exception>
    public BrowserTools(BrowserAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        _agent = agent;
    }

    /// <summary>Loads a URL.</summary>
    [McpServerTool(Name = "navigate", Title = "Open a URL", OpenWorld = true)]
    [Description("""
        Loads a web page and answers its final URL, its title and its HTTP status. This is the first tool to
        call: every other tool acts on the page this one opened. Redirects are followed and a 404 or a 500
        still loads, because an error page is a page. Call snapshot afterwards to read what arrived.
        """)]
    public Task<CallToolResult> NavigateAsync(
        [Description("An absolute http: or https: URL, for example https://example.org/docs.")] string url,
        [Description("How far to wait: load (default), domcontentloaded, commit, or networkidle for a page that fetches its content after loading.")] string? waitUntil = null)
        => AnswerAsync(() => _agent.NavigateAsync(url, waitUntil), ToolJson.Default.PageState);

    /// <summary>Goes back one page.</summary>
    [McpServerTool(Name = "back", Title = "Go back")]
    [Description("Goes back one entry in the browsing history, as the browser's back button does. Answers whether there was anywhere to go back to.")]
    public Task<CallToolResult> BackAsync()
        => AnswerAsync(_agent.BackAsync, ToolJson.Default.ActionOutcome);

    /// <summary>Goes forward one page.</summary>
    [McpServerTool(Name = "forward", Title = "Go forward")]
    [Description("Goes forward one entry in the browsing history, as the browser's forward button does. Answers whether there was anywhere to go forward to.")]
    public Task<CallToolResult> ForwardAsync()
        => AnswerAsync(_agent.ForwardAsync, ToolJson.Default.ActionOutcome);

    /// <summary>Loads the current page again.</summary>
    [McpServerTool(Name = "reload", Title = "Reload the page")]
    [Description("Fetches the current URL again and replaces the page with the result. Use it when the page shows something stale, or after setting a cookie that changes what the site returns.")]
    public Task<CallToolResult> ReloadAsync()
        => AnswerAsync(_agent.ReloadAsync, ToolJson.Default.PageState);

    /// <summary>Reads the page.</summary>
    [McpServerTool(Name = "snapshot", Title = "Read the page", ReadOnly = true)]
    [Description("""
        Reads the current page. This is the tool to call after navigating and after every action that changes
        the page. Three modes: "ax" (the default) is the accessibility tree — every interactive element with
        its role, its name and a ref= value that click, fill, type, select and hover accept, which is what to
        use when you intend to act on the page; "markdown" is the page as prose, which is what to use when you
        intend to read it; "text" is the same without any formatting. There are no screenshots: this browser
        renders nothing.
        """)]
    public Task<CallToolResult> SnapshotAsync(
        [Description("ax, markdown or text. Defaults to ax.")] string? mode = null,
        [Description("True to skip the navigation and the footer and read only the article or main region, when the page has one.")] bool mainContentOnly = false,
        [Description("A ceiling on the number of characters returned. The server has one of its own and this can only lower it.")] int? maxLength = null)
        => AnswerAsync(() => _agent.SnapshotAsync(mode, mainContentOnly, maxLength), ToolJson.Default.PageSnapshot);

    /// <summary>Clicks an element.</summary>
    [McpServerTool(Name = "click", Title = "Click an element")]
    [Description("""
        Clicks a link, a button, a checkbox or anything else on the page, running whatever the page does in
        response — a link navigates and the navigation is finished by the time this answers. Name the element
        with a ref= value from an ax snapshot, or with a CSS selector. Take a snapshot afterwards to see what
        changed.
        """)]
    public Task<CallToolResult> ClickAsync(
        [Description("A ref= value from an ax snapshot, for example \"ref=42\", or a CSS selector such as \"#submit\".")] string target)
        => AnswerAsync(() => _agent.ClickAsync(target), ToolJson.Default.ActionOutcome);

    /// <summary>Sets the value of a form control.</summary>
    [McpServerTool(Name = "fill", Title = "Fill in a field")]
    [Description("Replaces everything in a text field with the given value, as pasting into it would. Use this for filling forms; use type when the page reacts to each keystroke, as a search box with suggestions does.")]
    public Task<CallToolResult> FillAsync(
        [Description("A ref= value from an ax snapshot, or a CSS selector.")] string target,
        [Description("The value to leave in the field.")] string text)
        => AnswerAsync(() => _agent.FillAsync(target, text), ToolJson.Default.ActionOutcome);

    /// <summary>Types into a form control.</summary>
    [McpServerTool(Name = "type", Title = "Type into a field")]
    [Description("Types text one character at a time into a field, without clearing what is already there. The page sees every keystroke, which is what a search box with live suggestions is waiting for.")]
    public Task<CallToolResult> TypeAsync(
        [Description("A ref= value from an ax snapshot, or a CSS selector.")] string target,
        [Description("The characters to type.")] string text)
        => AnswerAsync(() => _agent.TypeAsync(target, text), ToolJson.Default.ActionOutcome);

    /// <summary>Presses one key.</summary>
    [McpServerTool(Name = "press", Title = "Press a key")]
    [Description("Presses one key wherever the page has the cursor. \"Enter\" in a single-line field submits the form it is in, and the navigation is finished by the time this answers. \"Tab\" moves to the next field, \"Escape\" closes a dialog the page opened.")]
    public Task<CallToolResult> PressAsync(
        [Description("A key name: Enter, Tab, Escape, Backspace, ArrowDown, or a single character.")] string key)
        => AnswerAsync(() => _agent.PressAsync(key), ToolJson.Default.ActionOutcome);

    /// <summary>Chooses an option in a drop-down.</summary>
    [McpServerTool(Name = "select", Title = "Choose an option")]
    [Description("Chooses an option in a drop-down list. The value is the option's value attribute, or the text shown for it when no option carries that value.")]
    public Task<CallToolResult> SelectAsync(
        [Description("A ref= value from an ax snapshot, or a CSS selector, naming the <select> element.")] string target,
        [Description("The option's value, or the text shown for it.")] string value)
        => AnswerAsync(() => _agent.SelectAsync(target, value), ToolJson.Default.ActionOutcome);

    /// <summary>Moves the pointer over an element.</summary>
    [McpServerTool(Name = "hover", Title = "Hover over an element")]
    [Description("Moves the mouse pointer over an element. Only pages that listen for mousemove react; a menu that opens on mouseenter will not, because this browser tracks no pointer position between calls.")]
    public Task<CallToolResult> HoverAsync(
        [Description("A ref= value from an ax snapshot, or a CSS selector.")] string target)
        => AnswerAsync(() => _agent.HoverAsync(target), ToolJson.Default.ActionOutcome);

    /// <summary>Scrolls the page.</summary>
    [McpServerTool(Name = "scroll", Title = "Scroll the page")]
    [Description("Scrolls the page to a vertical offset in pixels, which is what a page that loads more content as you scroll is waiting for. Use 0 to go back to the top. There is no layout behind it, so the offset is a number the page reads rather than a place on a screen.")]
    public Task<CallToolResult> ScrollAsync(
        [Description("The offset from the top of the page, in pixels.")] double y)
        => AnswerAsync(() => _agent.ScrollAsync(y), ToolJson.Default.ActionOutcome);

    /// <summary>Runs an expression in the page.</summary>
    [McpServerTool(Name = "evaluate", Title = "Run JavaScript in the page")]
    [Description("""
        Runs one JavaScript expression in the page and answers its JSON value, serialized by the page itself.
        Use it for what the other tools cannot read — a value on window, an attribute, a computed count — and
        prefer snapshot for reading the page and click or fill for changing it, because those go through the
        events a page is listening for.
        """)]
    public Task<CallToolResult> EvaluateAsync(
        [Description("A JavaScript expression, for example document.querySelectorAll('article').length.")] string expression)
        => AnswerTextAsync(() => _agent.EvaluateAsync(expression));

    /// <summary>Waits for something to appear.</summary>
    [McpServerTool(Name = "wait_for", Title = "Wait for the page to change", ReadOnly = true)]
    [Description("Waits until an element matching a CSS selector is in the page, or until some text appears in it. Use it after an action that starts something the page finishes later — a search, a form that saves in the background. Give exactly one of selector or text.")]
    public Task<CallToolResult> WaitForAsync(
        [Description("A CSS selector to wait for.")] string? selector = null,
        [Description("Text to wait for anywhere on the page.")] string? text = null,
        [Description("How long to wait, in seconds. The server's own ceiling applies and this can only lower it.")] double? timeoutSeconds = null)
        => AnswerAsync(() => _agent.WaitForAsync(selector, text, timeoutSeconds), ToolJson.Default.ActionOutcome);

    /// <summary>Lists the requests the page made.</summary>
    [McpServerTool(Name = "network_requests", Title = "List the page's network requests", ReadOnly = true)]
    [Description("Lists every request the page has made — the document, its scripts and style sheets, and everything its own code fetched — with the status each answered. Use it to find the API a page reads its data from, or to see which request failed.")]
    public Task<CallToolResult> RequestsAsync()
        => AnswerAsync(_agent.RequestsAsync, ToolJson.Default.IReadOnlyListRequestLine);

    /// <summary>Lists the cookies.</summary>
    [McpServerTool(Name = "cookies", Title = "List cookies", ReadOnly = true)]
    [Description("Lists the cookies this session holds for an origin, which is how to check whether a sign-in worked.")]
    public Task<CallToolResult> CookiesAsync(
        [Description("The http: or https: URL whose cookies to list. Defaults to the current page's.")] string? url = null)
        => AnswerAsync(() => _agent.CookiesAsync(url), ToolJson.Default.IReadOnlyListCookieLine);

    /// <summary>Sets a cookie.</summary>
    [McpServerTool(Name = "set_cookie", Title = "Set a cookie")]
    [Description("Sets one cookie for an origin, which is how to carry a session token you already have rather than signing in through a form. Reload the page afterwards for the site to see it.")]
    public Task<CallToolResult> SetCookieAsync(
        [Description("The cookie's name.")] string name,
        [Description("The cookie's value.")] string value,
        [Description("The http: or https: URL it belongs to. Defaults to the current page's.")] string? url = null)
        => AnswerAsync(() => _agent.SetCookieAsync(name, value, url), ToolJson.Default.ActionOutcome);

    /// <summary>Ends the browsing session.</summary>
    [McpServerTool(Name = "close", Title = "Close the page", Idempotent = true)]
    [Description("Closes the page and forgets its cookies, its storage and its history. Call it when the task is done; the next navigate starts a clean session.")]
    public Task<CallToolResult> CloseAsync()
        => AnswerAsync(_agent.CloseAsync, ToolJson.Default.ActionOutcome);

    /// <summary>Runs one tool and shapes its answer, or the reason it has none.</summary>
    private static async Task<CallToolResult> AnswerAsync<T>(Func<Task<T>> work, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> shape)
    {
        try
        {
            return ToolJson.Ok(await work().ConfigureAwait(false), shape);
        }
        catch (BrowserToolException failure)
        {
            return ToolJson.Failed(failure.Message);
        }
    }

    /// <summary>The same, for a tool whose answer is already the text to return.</summary>
    private static async Task<CallToolResult> AnswerTextAsync(Func<Task<string>> work)
    {
        try
        {
            return ToolJson.Text(await work().ConfigureAwait(false));
        }
        catch (BrowserToolException failure)
        {
            return ToolJson.Failed(failure.Message);
        }
    }
}
