#if NET8_0_OR_GREATER
#nullable enable

using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Jint.Tests.Wpt;

/// <summary>
/// The static-file half of <see cref="WptServer"/>: the content-type guess, the <c>.headers</c> sidecars and
/// the <c>.sub.</c> template language, ported from <c>tools/wptserve/wptserve/</c> at the pinned commit.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is separate from the handlers.</b> Everything in <see cref="WptServer"/> answers one named
/// <c>.py</c> file; everything here applies to <i>every</i> file the server serves, which is what the browser
/// lane needs and what the <c>.any.js</c> lane never asked for. Its three pieces are
/// <c>handlers.py</c>'s <c>guess_content_type</c> and <c>load_headers</c>, and <c>pipes.py</c>'s
/// <c>template</c>, and each is held to that source by a test in <c>WptServerTests</c> in the same way the
/// handler ports are.
/// </para>
/// <para>
/// <b>There is exactly one origin, and that is the whole of what this cannot reproduce.</b> wptserve runs on
/// two http ports and two https ports across several hostnames — <c>{{host}}</c>, <c>{{domains[www1]}}</c>,
/// <c>{{hosts[alt][]}}</c> — because a large part of the corpus is about what happens <i>between</i> origins.
/// <see cref="WptServer"/> is one <c>TcpListener</c> on one loopback port with no TLS, so every host token
/// answers <see cref="LoopbackHost"/> and every port token answers that one port. A file whose subject is a
/// second origin therefore reads as same-origin here, which makes it un-runnable rather than merely
/// different: it belongs in the exclusion table or the not-vendored table when a lane brings it in, and never
/// in a green run. Substitution is still faithful for the many files that only want <i>an</i> origin —
/// <c>common/get-host-info.sub.js</c>, which 68 files in the browser lane's suites include, is the reason
/// this exists at all.
/// </para>
/// <para>
/// <b>Nothing here is reachable from the <c>.any.js</c> lane.</b> No vendored file was a <c>.sub.</c> file, had
/// a <c>.headers</c> sidecar or asked for a <c>?pipe=</c> before this change, so the only behaviour the
/// existing corpus can see is the content-type table — which used to be four extensions with
/// <c>text/plain</c> underneath and is now wptserve's own, with <c>application/octet-stream</c> underneath.
/// </para>
/// </remarks>
internal static class WptServerFiles
{
    /// <summary>
    /// The one host every host-shaped substitution answers. It is the address
    /// <see cref="WptServer"/> binds, so a substituted URL really does reach the server.
    /// </summary>
    internal const string LoopbackHost = "127.0.0.1";

    /// <summary>
    /// <c>tools/wptserve/wptserve/constants.py</c>'s <c>content_types</c>, inverted the way
    /// <c>utils.invert_dict</c> inverts it: extension to media type.
    /// </summary>
    /// <remarks>
    /// Reproduced entire rather than trimmed to what is vendored, because the table is the artefact a
    /// corpus bump can change and a trimmed copy would silently stop being a copy. Nothing binary is
    /// vendored — see <c>Jint.Tests.csproj</c> — so the image, audio and video rows are here to be checked
    /// against upstream, not to be served.
    /// </remarks>
    private static readonly FrozenDictionary<string, string> _contentTypes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["json"] = "application/json",
        ["wasm"] = "application/wasm",
        ["xht"] = "application/xhtml+xml",
        ["xhtm"] = "application/xhtml+xml",
        ["xhtml"] = "application/xhtml+xml",
        ["xml"] = "application/xml",
        ["xpi"] = "application/x-xpinstall",
        ["m4a"] = "audio/mp4",
        ["mp3"] = "audio/mpeg",
        ["oga"] = "audio/ogg",
        ["weba"] = "audio/webm",
        ["wav"] = "audio/x-wav",
        ["avif"] = "image/avif",
        ["bmp"] = "image/bmp",
        ["gif"] = "image/gif",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["jxl"] = "image/jxl",
        ["png"] = "image/png",
        ["svg"] = "image/svg+xml",
        ["manifest"] = "text/cache-manifest",
        ["css"] = "text/css",
        ["event_stream"] = "text/event-stream",
        ["htm"] = "text/html",
        ["html"] = "text/html",
        ["js"] = "text/javascript",
        ["mjs"] = "text/javascript",
        ["txt"] = "text/plain",
        ["md"] = "text/plain",
        ["vtt"] = "text/vtt",
        ["mp4"] = "video/mp4",
        ["m4v"] = "video/mp4",
        ["webm"] = "video/webm",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>What <c>guess_content_type</c> answers when the extension is not in the table.</summary>
    internal const string DefaultContentType = "application/octet-stream";

    /// <summary>
    /// The extensions <c>wrap_pipeline</c> calls markup, and so escapes substituted values for.
    /// </summary>
    private static readonly string[] _markupExtensions = [".html", ".htm", ".xht", ".xhtml", ".xml", ".svg"];

    private static readonly Regex _template = new(@"\{\{([^}]*)\}\}", RegexOptions.Compiled);

    /// <summary>
    /// <c>handlers.py</c>'s <c>guess_content_type</c>: the media type for a path's extension, or
    /// <see cref="DefaultContentType"/>.
    /// </summary>
    /// <remarks>
    /// Case-sensitive, as upstream's dictionary lookup is, and over the <i>last</i> extension only — so
    /// <c>get-host-info.sub.js</c> is a <c>.js</c> and <c>a.tar.gz</c> would be neither a <c>.tar</c> nor a
    /// known type.
    /// </remarks>
    internal static string GuessContentType(string path)
    {
        var extension = ExtensionOf(path);
        return extension.Length > 0 && _contentTypes.TryGetValue(extension, out var type)
            ? type
            : DefaultContentType;
    }

    /// <summary>
    /// Whether <c>wrap_pipeline</c> would apply the <c>sub</c> pipe to this path: <c>.sub.</c> anywhere in
    /// it, which is how <c>get-host-info.sub.js</c> and every <c>.sub.html</c> ask for substitution.
    /// </summary>
    internal static bool WantsSubstitution(string path) => path.Contains(".sub.", StringComparison.Ordinal);

    /// <summary>
    /// The escaping <c>wrap_pipeline</c> chooses for a path: HTML for a markup extension, none otherwise.
    /// </summary>
    internal static bool EscapesAsHtml(string path)
    {
        var extension = ExtensionOf(path);
        foreach (var markup in _markupExtensions)
        {
            if (string.Equals("." + extension, markup, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <c>handlers.py</c>'s <c>load_headers</c> followed by <c>FileHandler.get_headers</c>: the
    /// directory-level sidecar's lines, then the file's own, and a guessed <c>Content-Type</c> in front of
    /// both only if neither named one.
    /// </summary>
    /// <param name="path">The path in the tree whose headers are wanted.</param>
    /// <param name="context">What a <c>.sub.headers</c> sidecar substitutes against.</param>
    /// <param name="read">Reads a sidecar, or answers <see langword="null"/> when there is none.</param>
    /// <remarks>
    /// <para>
    /// Two details are upstream's and are easy to get wrong. The <c>.sub.headers</c> spelling <b>wins over</b>
    /// the plain one for the same base name rather than adding to it, and it is substituted with escaping
    /// <i>off</i> — a header value is not markup. And the guess is inserted at the front, not appended, so a
    /// sidecar that names anything at all still leaves <c>Content-Type</c> first.
    /// </para>
    /// <para>
    /// Duplicates are kept: <c>load_headers</c> returns a list and <c>ResponseHeaders.update</c> appends each
    /// pair, so two <c>Set-Cookie</c> lines are two response headers. What <c>update</c> also does is group
    /// them — a repeated name keeps the position of its <i>first</i> appearance and collects its values there
    /// — which is what <see cref="Group"/> reproduces.
    /// </para>
    /// </remarks>
    internal static List<(string Name, string Value)> LoadHeaders(
        string path,
        in WptSubstitutionContext context,
        Func<string, string?> read)
    {
        var headers = new List<(string Name, string Value)>();

        var directory = WptCorpus.DirectoryOf(path);
        Load(headers, directory.Length == 0 ? "__dir__" : directory + "/__dir__", context, read);
        Load(headers, path, context, read);

        var hasContentType = false;
        foreach (var (name, _) in headers)
        {
            if (string.Equals(name, "content-type", StringComparison.OrdinalIgnoreCase))
            {
                hasContentType = true;
                break;
            }
        }

        if (!hasContentType)
        {
            headers.Insert(0, ("Content-Type", GuessContentType(path)));
        }

        headers.RemoveAll(static header => IsFraming(header.Name));

        return Group(headers);

        static void Load(
            List<(string Name, string Value)> into,
            string basePath,
            in WptSubstitutionContext context,
            Func<string, string?> read)
        {
            var data = read(basePath + ".sub.headers");
            if (data is not null)
            {
                data = Substitute(context, data, escapeAsHtml: false);
            }
            else
            {
                data = read(basePath + ".headers");
                if (data is null)
                {
                    return;
                }
            }

            foreach (var line in data.Split('\n'))
            {
                var trimmed = line.TrimEnd('\r');
                if (trimmed.Length == 0)
                {
                    continue;
                }

                var colon = trimmed.IndexOf(':');
                if (colon < 0)
                {
                    // Upstream builds a one-element tuple here and then crashes unpacking it, so a
                    // colon-less line is a 500 there and nothing anybody writes on purpose. Skipping it is
                    // the one place this port is deliberately kinder than its source.
                    continue;
                }

                into.Add((trimmed.Substring(0, colon).Trim(), trimmed.Substring(colon + 1).Trim()));
            }
        }
    }

    /// <summary>
    /// The three headers that are this server's framing rather than the file's, and which a sidecar
    /// therefore cannot set.
    /// </summary>
    /// <remarks>
    /// Every response here is delimited by its own close and writes its own <c>Content-Length</c> and
    /// <c>Connection</c>, so a sidecar naming one would put two of it on the wire and a client would read
    /// the wrong body length or leave the socket open. wptserve has the same headers under its own control
    /// and simply serves from a real socket per response; here the honest answer is to drop them and say so.
    /// A vendored file whose whole subject <i>is</i> a wrong <c>Content-Length</c> uses <c>.asis</c>
    /// instead, which bypasses all of this and puts the response on the wire byte for byte — that is what
    /// the six the xhr corpus vendors are for.
    /// </remarks>
    private static bool IsFraming(string name)
        => string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Connection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>ResponseHeaders.update</c>'s ordering: a name keeps the position of its first appearance and every
    /// value for it follows there, whatever order the lines arrived in.
    /// </summary>
    internal static List<(string Name, string Value)> Group(List<(string Name, string Value)> headers)
    {
        var order = new List<string>();
        var grouped = new Dictionary<string, List<(string Name, string Value)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            if (!grouped.TryGetValue(header.Name, out var values))
            {
                values = [];
                grouped[header.Name] = values;
                order.Add(header.Name);
            }

            values.Add(header);
        }

        var result = new List<(string Name, string Value)>(headers.Count);
        foreach (var name in order)
        {
            result.AddRange(grouped[name]);
        }

        return result;
    }

    /// <summary>
    /// <c>pipes.py</c>'s <c>template</c>: every <c>{{…}}</c> in <paramref name="content"/> replaced by what
    /// this server can say about itself and about the request.
    /// </summary>
    /// <exception cref="WptServerFileException">
    /// For anything upstream raises on — an undefined variable, a token where an identifier was expected, an
    /// index into something that has none. Upstream turns such a raise into a 500 and so does
    /// <see cref="WptServer"/>: a placeholder that silently stayed a placeholder would reach script as a
    /// literal <c>{{host}}</c> and fail the test somewhere else entirely.
    /// </exception>
    internal static string Substitute(in WptSubstitutionContext context, string content, bool escapeAsHtml)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        var local = context;

        return _template.Replace(content, match =>
        {
            var value = Resolve(local, match.Groups[1].Value, variables);
            return escapeAsHtml ? EscapeHtml(value) : value;
        });
    }

    /// <summary>
    /// One <c>{{…}}</c>, resolved the way <c>config_replacement</c> resolves it: an optional
    /// <c>$name:</c> assignment prefix, then an identifier, then any number of index and argument tokens.
    /// </summary>
    private static string Resolve(in WptSubstitutionContext context, string body, Dictionary<string, string> variables)
    {
        var tokens = Tokenize(body);
        if (tokens.Count == 0)
        {
            throw new WptServerFileException($"nothing to substitute in \"{{{{{body}}}}}\"");
        }

        var next = 0;
        string? assignTo = null;
        if (tokens[0].Kind == WptTemplateTokenKind.Variable)
        {
            assignTo = tokens[0].Text;
            next++;
        }

        if (next >= tokens.Count || tokens[next].Kind != WptTemplateTokenKind.Identifier)
        {
            throw new WptServerFileException($"expected an identifier in \"{{{{{body}}}}}\"");
        }

        var identifier = tokens[next].Text;
        next++;

        var value = variables.TryGetValue(identifier, out var assigned)
            ? Indexed(assigned, identifier, tokens, ref next, body)
            : ResolveIdentifier(context, identifier, tokens, ref next, body);

        if (next != tokens.Count)
        {
            throw new WptServerFileException($"unexpected trailing token in \"{{{{{body}}}}}\"");
        }

        if (assignTo is not null)
        {
            variables[assignTo] = value;
        }

        return value;
    }

    private static string ResolveIdentifier(
        in WptSubstitutionContext context,
        string identifier,
        List<WptTemplateToken> tokens,
        ref int next,
        string body)
    {
        switch (identifier)
        {
            // SubFunctions.uuid: a fresh UUID per call, which is what a stash key is made of.
            case "uuid":
                RequireArguments(tokens, ref next, 0, body);
                return Guid.NewGuid().ToString();

            // SubFunctions.header_or_default(name, default).
            case "header_or_default":
            {
                var arguments = RequireArguments(tokens, ref next, 2, body);
                return context.Request is not null && context.Request.TryGetHeader(arguments[0], out var header)
                    ? header
                    : arguments[1];
            }

            // request.headers, indexed by name. Upstream's dictionary raises KeyError for a name the request
            // did not send, which wptserve reports as a 500; the same is loud here.
            case "headers":
            {
                var name = RequireIndex(tokens, ref next, body);
                if (context.Request is null || !context.Request.TryGetHeader(name, out var value))
                {
                    throw new WptServerFileException($"the request has no \"{name}\" header, asked for by \"{{{{{body}}}}}\"");
                }

                return value;
            }

            // FirstWrapper(request.GET): the first value for a name, and the empty string for a name that
            // was not passed at all — which is the one lookup here that deliberately does not raise.
            case "GET":
            {
                var name = RequireIndex(tokens, ref next, body);
                return context.Request is not null && context.Request.Query.TryGetValue(name, out var value) ? value : "";
            }

            // config.all_domains — {alternate host: {subdomain: host}} — and all_domains[""] under it. There
            // is one host here, so every spelling answers it; see the class remarks for what that costs.
            case "hosts":
                RequireIndex(tokens, ref next, body);
                RequireIndex(tokens, ref next, body);
                return LoopbackHost;

            case "domains":
                RequireIndex(tokens, ref next, body);
                return LoopbackHost;

            case "host":
            case "browser_host":
            case "server_host":
                return LoopbackHost;

            // config["ports"]: {protocol: [port, …]}. One listener, no TLS and no WebSocket origin, so every
            // protocol and every index answer the one port. A file that wants two distinct ports gets two
            // identical ones, which is a divergence a lane records rather than a substitution that fails.
            case "ports":
                RequireIndex(tokens, ref next, body);
                RequireIndex(tokens, ref next, body);
                return context.Port.ToString(CultureInfo.InvariantCulture);

            case "url_base":
                return "/";

            // The parts of the request URL, undecoded, exactly as urlsplit hands them over.
            case "location":
            {
                var key = RequireIndex(tokens, ref next, body);
                var authority = LoopbackHost + ":" + context.Port.ToString(CultureInfo.InvariantCulture);
                return key switch
                {
                    "server" => "http://" + authority,
                    "scheme" => "http",
                    "host" => authority,
                    "hostname" => LoopbackHost,
                    "port" => context.Port.ToString(CultureInfo.InvariantCulture),
                    "path" or "pathname" => context.Request?.RawPath ?? "/",
                    "query" => "?" + (context.Request?.RawQuery ?? ""),
                    _ => throw new WptServerFileException($"\"{key}\" is not a part of the request URL, asked for by \"{{{{{body}}}}}\""),
                };
            }

            default:
                throw new WptServerFileException(
                    $"undefined template variable \"{identifier}\" in \"{{{{{body}}}}}\"");
        }
    }

    private static string Indexed(string value, string identifier, List<WptTemplateToken> tokens, ref int next, string body)
    {
        if (next < tokens.Count)
        {
            throw new WptServerFileException($"\"{identifier}\" is a string and cannot be indexed, in \"{{{{{body}}}}}\"");
        }

        return value;
    }

    private static string RequireIndex(List<WptTemplateToken> tokens, ref int next, string body)
    {
        if (next >= tokens.Count || tokens[next].Kind != WptTemplateTokenKind.Index)
        {
            throw new WptServerFileException($"expected an index in \"{{{{{body}}}}}\"");
        }

        return tokens[next++].Text;
    }

    private static string[] RequireArguments(List<WptTemplateToken> tokens, ref int next, int count, string body)
    {
        if (next >= tokens.Count || tokens[next].Kind != WptTemplateTokenKind.Arguments)
        {
            throw new WptServerFileException($"expected a call in \"{{{{{body}}}}}\"");
        }

        var arguments = tokens[next++].Arguments!;
        if (arguments.Length != count)
        {
            throw new WptServerFileException(
                $"expected {count.ToString(CultureInfo.InvariantCulture)} arguments in \"{{{{{body}}}}}\"");
        }

        return arguments;
    }

    /// <summary>
    /// <c>ReplacementTokenizer</c>'s scanner, whose four patterns are tried in order at each position and
    /// which stops — rather than skipping — at the first character none of them match.
    /// </summary>
    /// <remarks>
    /// The stopping is the part worth reproducing. <c>{{ host }}</c> tokenizes to nothing at all upstream,
    /// because a space matches no pattern, and the caller then fails on an empty token list; a tokenizer that
    /// skipped whitespace would quietly accept a spelling wptserve rejects and let a corpus file be written
    /// that only works here.
    /// </remarks>
    internal static List<WptTemplateToken> Tokenize(string body)
    {
        var tokens = new List<WptTemplateToken>();
        var i = 0;

        while (i < body.Length)
        {
            // \$\w+:
            if (body[i] == '$')
            {
                var end = i + 1;
                while (end < body.Length && IsWord(body[end]))
                {
                    end++;
                }

                if (end > i + 1 && end < body.Length && body[end] == ':')
                {
                    // Upstream's `var` keeps the dollar — it is `token[:-1]`, the colon and nothing else
                    // removed — and its `ident` keeps it too, which is exactly why a later `{{$id}}` finds
                    // what `{{$id:uuid()}}` stored.
                    tokens.Add(new WptTemplateToken(WptTemplateTokenKind.Variable, body.Substring(i, end - i), null));
                    i = end + 1;
                    continue;
                }
            }

            // \$?\w+
            if (body[i] == '$' || IsWord(body[i]))
            {
                var end = body[i] == '$' ? i + 1 : i;
                while (end < body.Length && IsWord(body[end]))
                {
                    end++;
                }

                if (end > i)
                {
                    // `ident` decodes the whole match, dollar included, so "$id" is the identifier "$id".
                    tokens.Add(new WptTemplateToken(WptTemplateTokenKind.Identifier, body.Substring(i, end - i), null));
                    i = end;
                    continue;
                }
            }

            // \[[^\]]*\]
            if (body[i] == '[')
            {
                var close = body.IndexOf(']', i);
                if (close >= 0)
                {
                    tokens.Add(new WptTemplateToken(WptTemplateTokenKind.Index, body.Substring(i + 1, close - i - 1), null));
                    i = close + 1;
                    continue;
                }
            }

            // \([^)]*\)
            if (body[i] == '(')
            {
                var close = body.IndexOf(')', i);
                if (close >= 0)
                {
                    var inner = body.Substring(i + 1, close - i - 1);
                    tokens.Add(new WptTemplateToken(WptTemplateTokenKind.Arguments, inner, SplitArguments(inner)));
                    i = close + 1;
                    continue;
                }
            }

            break;
        }

        return tokens;
    }

    /// <summary><c>re.split(r",\s*", …)</c>, with the empty argument list an empty string produces.</summary>
    private static string[] SplitArguments(string inner)
    {
        if (inner.Length == 0)
        {
            return [];
        }

        var arguments = new List<string>();
        var start = 0;
        for (var i = 0; i < inner.Length; i++)
        {
            if (inner[i] != ',')
            {
                continue;
            }

            arguments.Add(inner.Substring(start, i - start));
            start = i + 1;
            while (start < inner.Length && char.IsWhiteSpace(inner[start]))
            {
                start++;
            }

            i = start - 1;
        }

        arguments.Add(inner.Substring(start));
        return arguments.ToArray();
    }

    /// <summary>
    /// Python's <c>html.escape(x, quote=True)</c>: the three markup characters and both quotes, and
    /// pointedly nothing else — <c>WebUtility.HtmlEncode</c> would also turn every non-ASCII character into a
    /// numeric entity, which upstream does not.
    /// </summary>
    internal static string EscapeHtml(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '&': builder.Append("&amp;"); break;
                case '<': builder.Append("&lt;"); break;
                case '>': builder.Append("&gt;"); break;
                case '"': builder.Append("&quot;"); break;
                case '\'': builder.Append("&#x27;"); break;
                default: builder.Append(c); break;
            }
        }

        return builder.ToString();
    }

    private static string ExtensionOf(string path)
    {
        var slash = path.LastIndexOf('/');
        var dot = path.LastIndexOf('.');
        return dot > slash && dot >= 0 ? path.Substring(dot + 1) : "";
    }

    private static bool IsWord(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';
}

/// <summary>What a <c>{{…}}</c> is resolved against: this server's port, and the request being answered.</summary>
internal readonly record struct WptSubstitutionContext(int Port, WptServerRequest? Request);

internal enum WptTemplateTokenKind
{
    Variable,
    Identifier,
    Index,
    Arguments,
}

/// <summary>One token of a <c>{{…}}</c> body.</summary>
internal readonly record struct WptTemplateToken(WptTemplateTokenKind Kind, string Text, string[]? Arguments);

/// <summary>
/// A request this server understands the shape of and cannot answer: an undefined substitution, a pipe it
/// does not implement. It becomes a 500 with the message as its body, because the alternative — serving the
/// file with its placeholders intact — fails the test somewhere far away from the cause.
/// </summary>
internal sealed class WptServerFileException(string message) : Exception(message);
#endif
