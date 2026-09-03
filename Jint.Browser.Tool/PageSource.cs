namespace Jint.Browser.Tool;

/// <summary>What the command line's first positional argument turned out to name.</summary>
/// <param name="Url">The URL the document reports and resolves relative references against.</param>
/// <param name="FileContent">
/// The markup read off disk, or <see langword="null"/> when the URL is one the browser fetches itself.
/// </param>
internal readonly record struct PageSource(string Url, string? FileContent)
{
    /// <summary>Whether the document came off disk rather than off the network.</summary>
    internal bool IsFile => FileContent is not null;

    /// <summary>Reads the argument as a URL, or as the path of a file to show.</summary>
    /// <remarks>
    /// <para>
    /// <b>No scheme is guessed.</b> A tool that turned <c>example.com/admin</c> into an <c>http://</c>
    /// request would make a typo into a request to somewhere, so an argument that is neither an absolute URL
    /// nor a file that exists is a usage error naming both.
    /// </para>
    /// <para>
    /// <b>A file is shown, not fetched.</b> <c>Page.NavigateAsync</c> loads <c>http</c>, <c>https</c>,
    /// <c>data:</c> and <c>about:</c> — the schemes a browsing context has a transport for — so a
    /// <c>file:</c> argument is read here and handed over as content with its own <c>file:</c> URL as the
    /// base. The document is exactly what a fetched one would be, with one difference worth knowing: a
    /// relative <c>&lt;script src&gt;</c> or style sheet resolves to a <c>file:</c> URL that the transport
    /// does not load, and shows up in the request log as a failure rather than silently.
    /// </para>
    /// </remarks>
    internal static PageSource Resolve(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ToolUsageException("a URL or a file path is needed");
        }

        // Scheme.Length > 1 keeps a Windows drive letter out of this: "C:\pages\index.html" parses as an
        // absolute URI whose scheme is "c", and treating it as one would refuse every absolute path on the
        // platform this is most often run on.
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme.Length > 1)
        {
            switch (uri.Scheme)
            {
                case "http" or "https" or "data" or "about":
                    return new PageSource(target, null);

                case "file":
                    return FromFile(uri.LocalPath);

                default:
                    throw new ToolUsageException(
                        $"'{target}' is a {uri.Scheme}: URL, and this browser loads http:, https:, file:, data: and about: ones");
            }
        }

        return FromFile(Path.GetFullPath(target));
    }

    private static PageSource FromFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new ToolUsageException($"'{path}' is neither an absolute URL nor a file that exists");
        }

        return new PageSource(new Uri(path).AbsoluteUri, File.ReadAllText(path));
    }
}
