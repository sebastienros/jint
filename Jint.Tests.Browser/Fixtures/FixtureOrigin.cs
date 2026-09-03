using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Fixtures;

/// <summary>
/// Serves <see cref="FixtureCorpus"/> from a <see cref="LoopbackServer"/>, so a fixture is a real origin
/// over a real socket rather than a string handed to <c>SetContentAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a socket.</b> Half of what the course is for happens between the page and an origin — a script
/// element's fetch, a module graph, a <c>303</c> that turns a <c>POST</c> into a <c>GET</c>, a
/// <c>Set-Cookie</c> the next request carries, an <c>hx-get</c> swapping a fragment. A fixture inlined into
/// one document would test none of it.
/// </para>
/// <para>
/// <b>And no network.</b> Every byte a fixture loads is vendored, and the page's <c>UrlFilter</c> is pinned
/// to this server — so a fixture that reached for a CDN would fail rather than pass slowly on a machine that
/// happened to have one.
/// </para>
/// </remarks>
internal static class FixtureOrigin
{
    /// <summary>Serves the corpus from <paramref name="server"/>, under the paths it is stored at.</summary>
    /// <remarks>
    /// It is the <see cref="LoopbackServer.Fallback"/> rather than a route per file, which leaves
    /// <see cref="LoopbackServer.Map"/> free for the paths a fixture asks the server to <i>compute</i>. A
    /// mapped route therefore wins over a file of the same name, which is what lets a fixture keep a static
    /// stand-in beside the dynamic answer a test registers.
    /// </remarks>
    internal static LoopbackServer Serve(LoopbackServer server)
    {
        server.Fallback = request =>
        {
            var path = request.Path.TrimStart('/');

            if (path.Length == 0 || path.EndsWith('/'))
            {
                path += "index.html";
            }

            return FixtureCorpus.TryRead(path, out var content)
                ? new LoopbackResponse { Body = content }.With("Content-Type", ContentTypeOf(path))
                : null;
        };

        return server;
    }

    /// <summary>The URL of a fixture's entry document.</summary>
    internal static string Url(LoopbackServer server, string fixture) => server.Url("/" + fixture + "/index.html");

    /// <summary>
    /// The content type a file is served with, which is the whole of what this server knows about a type.
    /// </summary>
    /// <remarks>
    /// A wrong one is not cosmetic: AngleSharp refuses a style sheet whose type is not <c>text/css</c>, and
    /// the module loader refuses a module whose type is not a JavaScript one — so the table is the fixture's
    /// contract with the parser as much as with the network.
    /// </remarks>
    private static string ContentTypeOf(string path)
    {
        var extension = Path.GetExtension(path);

        return extension switch
        {
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".js" or ".mjs" => "text/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".json" => "application/json",
            ".svg" => "image/svg+xml",
            _ => "text/plain; charset=utf-8",
        };
    }
}
