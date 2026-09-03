using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Jint.Browser.Mcp;

/// <summary>
/// The current page as a resource, for a client that attaches context rather than calling a tool.
/// </summary>
/// <remarks>
/// <para>
/// Two direct resources over the same session the tools drive: what the page says, and what it fetched.
/// They answer about the page as it is <i>now</i> — there is one page per session, so there is nothing to
/// address in the URI and neither is a template.
/// </para>
/// <para>
/// <b>They are a second door onto the same room, not a second room.</b> Both go through
/// <see cref="BrowserAgent"/>, so a resource read and the matching tool call cannot say different things,
/// and a resource read on a session that has navigated nowhere answers about <c>about:blank</c> rather than
/// failing.
/// </para>
/// </remarks>
[McpServerResourceType]
public sealed class BrowserResources
{
    private readonly BrowserAgent _agent;

    /// <summary>Creates the resource set over one session's browser.</summary>
    /// <param name="agent">The session's browser, resolved from the host's services.</param>
    /// <exception cref="ArgumentNullException"><paramref name="agent"/> is <see langword="null"/>.</exception>
    public BrowserResources(BrowserAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        _agent = agent;
    }

    /// <summary>The current page as CommonMark.</summary>
    [McpServerResource(UriTemplate = "jint://page/markdown", Name = "page_markdown", Title = "The current page as markdown", MimeType = "text/markdown")]
    [Description("The page currently open in this session, rendered as CommonMark for reading.")]
    public async Task<string> MarkdownAsync()
        => (await _agent.SnapshotAsync("markdown").ConfigureAwait(false)).Content;

    /// <summary>The current page's request log.</summary>
    [McpServerResource(UriTemplate = "jint://page/requests", Name = "page_requests", Title = "The current page's network requests", MimeType = "application/json")]
    [Description("Every request the page currently open in this session has made, with the status each answered.")]
    public async Task<string> RequestsAsync()
    {
        var requests = await _agent.RequestsAsync().ConfigureAwait(false);
        return System.Text.Json.JsonSerializer.Serialize(requests, ToolJson.Default.IReadOnlyListRequestLine);
    }
}
