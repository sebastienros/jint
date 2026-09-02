namespace Jint.Browser;

/// <summary>
/// The response the page's current document came from: its final URL, its status, and its headers.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is a plain CLR value taken on the transport, so it is safe to read from any thread and
/// outlives the engine the document was parsed into.
/// </para>
/// <para>
/// A document a page reached without the network — <c>about:blank</c>, a <c>data:</c> URL,
/// <see cref="Page.SetContentAsync"/> — has no response, and <see cref="Page.Response"/> is
/// <see langword="null"/> for it.
/// </para>
/// </remarks>
public sealed class PageResponse
{
    internal PageResponse(string url, int status, string statusText, IReadOnlyList<PageHeader> headers, bool redirected)
    {
        Url = url;
        Status = status;
        StatusText = statusText;
        Headers = headers;
        Redirected = redirected;
    }

    /// <summary>The URL the response came from — the last hop of a redirect chain, not the one asked for.</summary>
    public string Url { get; }

    /// <summary>The HTTP status code.</summary>
    public int Status { get; }

    /// <summary>The HTTP reason phrase, which may be empty.</summary>
    public string StatusText { get; }

    /// <summary>The response headers, one entry per value and each name byte-lowercased.</summary>
    /// <remarks>
    /// A header sent more than once — <c>Set-Cookie</c> above all — is more than one entry rather than one
    /// joined value, which is how the Fetch Standard keeps them apart.
    /// </remarks>
    public IReadOnlyList<PageHeader> Headers { get; }

    /// <summary>Whether the request followed at least one redirect to get here.</summary>
    public bool Redirected { get; }

    /// <summary>Whether the status is in the <c>2xx</c> range.</summary>
    public bool Ok => Status >= 200 && Status <= 299;

    /// <summary>The first value of <paramref name="name"/>, or <see langword="null"/> when absent.</summary>
    /// <param name="name">The header name, compared ASCII-case-insensitively.</param>
    public string? Header(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        foreach (var header in Headers)
        {
            if (string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return header.Value;
            }
        }

        return null;
    }
}

/// <summary>One header of a request or a response.</summary>
/// <param name="Name">The name, byte-lowercased the way the Fetch Standard stores it.</param>
/// <param name="Value">The value.</param>
public readonly record struct PageHeader(string Name, string Value);
