using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Jint.DevTools.Protocol;

namespace Jint.DevTools.Transport;

/// <summary>
/// One answer to a discovery request: the status a client branches on, and the document it reads.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct HttpDiscoveryResponse(int Status, string Reason, string ContentType, string Body)
{
    /// <summary>The answer to a path no endpoint claims.</summary>
    internal static HttpDiscoveryResponse NotFound { get; } = new(404, "Not Found", "text/plain; charset=UTF-8", "No such endpoint");

    internal static HttpDiscoveryResponse Json(string body) => new(200, "OK", "application/json; charset=UTF-8", body);

    internal static HttpDiscoveryResponse Text(string body) => new(200, "OK", "text/plain; charset=UTF-8", body);
}

/// <summary>
/// The <c>/json</c> documents a client reads before it opens a socket.
/// </summary>
/// <remarks>
/// <para>
/// Chrome serves these from the same port as the WebSocket endpoint, and clients depend on it: one that
/// connects by URL rather than by socket address — Puppeteer's <c>browserURL</c>, Playwright's
/// <c>connectOverCDP</c> — reads <c>webSocketDebuggerUrl</c> out of <c>/json/version</c> and connects to
/// whatever it finds there. Which fields each client reads was recorded rather than guessed; see
/// <c>tools/devtools-protocol/handshakes/</c>.
/// </para>
/// <para>
/// <c>/json/protocol</c> answers what this server implements rather than the whole pinned description. The
/// full one is two megabytes describing mostly commands answered here with <c>-32601</c>, and a client
/// reading it would be told it can call them; this one tells it the truth and costs the package no embedded
/// resource.
/// </para>
/// </remarks>
internal static class HttpDiscovery
{
    /// <summary>Answers <paramref name="request"/>.</summary>
    internal static ValueTask<HttpDiscoveryResponse> AnswerAsync(DevToolsServer server, HttpRequestHead request)
    {
        var path = request.Path;
        var query = path.IndexOf('?', StringComparison.Ordinal);
        if (query >= 0)
        {
            path = path.Substring(0, query);
        }

        path = path.TrimEnd('/');
        if (path.Length == 0)
        {
            path = "/json";
        }

        return path switch
        {
            "/json/version" => Answered(HttpDiscoveryResponse.Json(Version(server))),
            "/json" or "/json/list" => Answered(HttpDiscoveryResponse.Json(List(server))),
            "/json/protocol" => Answered(HttpDiscoveryResponse.Json(Protocol())),
            "/json/new" => Answered(New(server)),
            _ => CommandAsync(server, path),
        };

        static ValueTask<HttpDiscoveryResponse> Answered(HttpDiscoveryResponse response) => new(response);
    }

    private static string Version(DevToolsServer server)
    {
        var version = server.Version;

        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("Browser", version.Product);
            writer.WriteString("Protocol-Version", version.ProtocolVersion);
            writer.WriteString("User-Agent", version.UserAgent);
            writer.WriteString("V8-Version", version.JsVersion);

            // Chrome answers a Blink revision here and clients only ever report it. Zero says "no engine of
            // that kind" rather than borrowing a number from a browser this is not.
            writer.WriteString("WebKit-Version", "0");
            writer.WriteString("webSocketDebuggerUrl", server.BrowserWebSocketUrl);
            writer.WriteEndObject();
        }

        return Utf8(buffer);
    }

    private static string List(DevToolsServer server)
    {
        var authority = server.Authority;

        var buffer = new ArrayBufferWriter<byte>(512);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var target in server.AllTargets)
            {
                // Chrome's own /json/list leaves tab targets out: a tab is a handle a protocol client
                // attaches through, and a person reading this document is looking for pages.
                if (!string.Equals(target.Type, "tab", StringComparison.Ordinal))
                {
                    WriteTarget(writer, authority, target);
                }
            }

            writer.WriteEndArray();
        }

        return Utf8(buffer);
    }

    private static HttpDiscoveryResponse New(DevToolsServer server)
    {
        if (server.Options.EngineFactory is null)
        {
            // Chrome answers 500 with a body; 501 says the thing a host can act on — this server was not
            // configured to make engines, rather than it tried and failed.
            return new HttpDiscoveryResponse(
                501,
                "Not Implemented",
                "text/plain; charset=UTF-8",
                "No engine factory is configured; set DevToolsServerOptions.EngineFactory for a client to be able to create targets");
        }

        var target = server.CreateTarget();
        var authority = server.Authority;

        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteTarget(writer, authority, target);
        }

        return HttpDiscoveryResponse.Json(Utf8(buffer));
    }

    private static string Protocol()
    {
        var buffer = new ArrayBufferWriter<byte>(1024);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            var version = ProtocolManifest.ProtocolVersion;
            var dot = version.IndexOf('.', StringComparison.Ordinal);

            writer.WriteStartObject();
            writer.WriteStartObject("version");
            writer.WriteString("major", dot < 0 ? version : version.Substring(0, dot));
            writer.WriteString("minor", dot < 0 ? "0" : version.Substring(dot + 1));
            writer.WriteEndObject();

            writer.WriteStartArray("domains");
            foreach (var domain in ProtocolManifest.ReportedDomains)
            {
                writer.WriteStartObject();
                writer.WriteString("domain", domain.Name);
                WriteMembers(writer, "commands", ProtocolManifest.ImplementedMethods, domain.Name);
                WriteMembers(writer, "events", ProtocolManifest.ImplementedEvents, domain.Name);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Utf8(buffer);

        static void WriteMembers(Utf8JsonWriter writer, string list, IReadOnlyList<string> qualified, string domain)
        {
            var prefix = domain + ".";

            writer.WriteStartArray(list);
            foreach (var name in qualified)
            {
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", name.AsSpan(prefix.Length));
                    writer.WriteEndObject();
                }
            }

            writer.WriteEndArray();
        }
    }

    /// <summary>
    /// The two endpoints that name a target in the path. Asynchronous because closing one may stop a thread,
    /// and blocking a transport thread on that is how a listener stops accepting.
    /// </summary>
    private static async ValueTask<HttpDiscoveryResponse> CommandAsync(DevToolsServer server, string path)
    {
        if (Suffix(path, "/json/activate/") is { } activate)
        {
            // Nothing to raise or focus, so the honest answer is that the target is there. A client sends
            // this before driving a target and reads a failure as the target being gone.
            return server.FindTarget(activate) is null
                ? HttpDiscoveryResponse.NotFound
                : HttpDiscoveryResponse.Text("Target activated");
        }

        if (Suffix(path, "/json/close/") is { } close)
        {
            var target = server.FindTarget(close);
            if (target is null)
            {
                return HttpDiscoveryResponse.NotFound;
            }

            await server.CloseTargetAsync(target).ConfigureAwait(false);
            return HttpDiscoveryResponse.Text("Target is closing");
        }

        return HttpDiscoveryResponse.NotFound;
    }

    /// <summary>
    /// Writes one target exactly as <c>Target.getTargets</c> describes it, plus the two addresses only the
    /// discovery document carries.
    /// </summary>
    /// <remarks>
    /// The description comes from <see cref="DevToolsTarget.Describe"/>, the same one the socket answers
    /// with: a client that reads <c>/json/list</c> and then asks the same question over the socket must not
    /// be told two different things about one target.
    /// </remarks>
    private static void WriteTarget(Utf8JsonWriter writer, string authority, DevToolsTarget target)
    {
        var info = target.Describe(attached: false);

        writer.WriteStartObject();
        writer.WriteString("description", "");
        writer.WriteString("devtoolsFrontendUrl", FrontendUrl(authority, info.TargetId, info.Type));
        writer.WriteString("id", info.TargetId);
        writer.WriteString("title", info.Title);
        writer.WriteString("type", info.Type);
        writer.WriteString("url", info.Url);
        writer.WriteString("webSocketDebuggerUrl", PageUrl(authority, info.TargetId));
        writer.WriteEndObject();
    }

    private static string? Suffix(string path, string prefix)
    {
        return path.StartsWith(prefix, StringComparison.Ordinal) && path.Length > prefix.Length
            ? path.Substring(prefix.Length)
            : null;
    }

    /// <summary>
    /// The address of the DevTools front end for one target, in the flavour that target's type calls for.
    /// </summary>
    /// <remarks>
    /// Two layouts, and picking the wrong one is what a client sees. <c>js_app.html?v8only=true</c> is Node's:
    /// Sources, Console and Memory, and the front end never asks the target for a page. A <c>page</c> target
    /// has one, so it gets <c>inspector.html</c> — Chrome's own page-flavoured address, with the Elements and
    /// Network panels the layout above deliberately leaves out.
    /// </remarks>
    private static string FrontendUrl(string authority, string targetId, string type)
    {
        return string.Equals(type, "page", StringComparison.Ordinal)
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"devtools://devtools/bundled/inspector.html?experiments=true&ws={authority}/devtools/page/{targetId}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"devtools://devtools/bundled/js_app.html?experiments=true&v8only=true&ws={authority}/devtools/page/{targetId}");
    }

    private static string PageUrl(string authority, string targetId)
    {
        return string.Create(CultureInfo.InvariantCulture, $"ws://{authority}/devtools/page/{targetId}");
    }

    private static string Utf8(ArrayBufferWriter<byte> buffer) => Encoding.UTF8.GetString(buffer.WrittenSpan);
}
