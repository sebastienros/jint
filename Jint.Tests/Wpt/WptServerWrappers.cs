#if NET8_0_OR_GREATER
#nullable enable

using System.Text;

namespace Jint.Tests.Wpt;

/// <summary>
/// wptserve's generated wrappers: the <c>&lt;name&gt;.any.html</c> document a browser loads for a
/// <c>&lt;name&gt;.any.js</c> file, which exists on no disk anywhere.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the corpus needs this at all.</b> A <c>.any.js</c> file is not a test a browser can open. Upstream's
/// server manufactures one document per global the file declares — <c>AnyHtmlHandler</c> for a window,
/// <c>WorkersHandler</c> for a dedicated worker, and six more — and the <i>document</i> is what a browser
/// navigates to. So the browser lane's copy of a suite the engine lane already runs is not a second vendoring:
/// it is the same bytes, wrapped the way upstream wraps them, loaded through upstream's real
/// <c>testharness.js</c> in a real <c>Window</c> realm. That is the whole reason the two lanes can disagree
/// about a file and both be right.
/// </para>
/// <para>
/// <b>Only the window wrapper is generated here, and the omission is a decision rather than a gap.</b>
/// <c>WorkersHandler</c>'s document creates a <b>classic</b> dedicated worker whose generated
/// <c>.any.worker.js</c> body opens with <c>importScripts("/resources/testharness.js")</c>. Jint runs module
/// workers only, so <c>importScripts</c> is the <c>TypeError</c> the standard's module-worker step prescribes
/// and the worker throws before registering a test — which is exactly why <c>workers/*.worker.js</c> is a
/// not-vendored row in <c>Vendor/README.md</c> rather than an exclusion. Generating the wrapper would
/// manufacture a red document per file that says only that. The <c>.any.js</c> corpus <i>is</i> run in a real
/// worker, by the engine lane's own worker lane, which builds a module worker instead.
/// </para>
/// <para>
/// <b>The port is held to <c>tools/serve/serve.py</c> at the pin</b> the same way <see cref="WptServerFiles"/>
/// is held to <c>tools/wptserve/wptserve/</c>: <c>WptServerTests</c> reproduces the wrapper text, the metadata
/// reader and the exposure rule case by case, because a wrapper that quietly stopped matching upstream's would
/// make every document in the lane a document no browser serves.
/// </para>
/// </remarks>
internal static class WptServerWrappers
{
    /// <summary>The suffix a request carries when it is asking for a synthesized window wrapper.</summary>
    private const string WindowWrapperSuffix = ".any.html";

    /// <summary>What that suffix is replaced with to find the file on disk — upstream's <c>path_replace</c>.</summary>
    private const string UnderlyingSuffix = ".any.js";

    /// <summary>
    /// The global <c>AnyHtmlHandler</c> exposes, which its <c>check_exposure</c> requires the file's
    /// <c>// META: global=</c> to name.
    /// </summary>
    private const string WindowGlobal = "window";

    /// <summary>
    /// The variants <c>parse_variants("")</c> answers — <c>sourcefile.get_default_any_variants()</c> — which is
    /// what a file with no <c>// META: global=</c> line at all declares.
    /// </summary>
    private static readonly string[] _defaultVariants = ["window", "dedicatedworker"];

    /// <summary>
    /// Whether <paramref name="path"/> is a request for a wrapper this class generates.
    /// </summary>
    internal static bool IsWrapperPath(string path)
        => path.EndsWith(WindowWrapperSuffix, StringComparison.Ordinal);

    /// <summary>
    /// The vendored file a wrapper request is really about: <c>dom/events/x.any.html</c> is
    /// <c>dom/events/x.any.js</c>.
    /// </summary>
    internal static string UnderlyingFile(string wrapperPath)
        => wrapperPath.Substring(0, wrapperPath.Length - WindowWrapperSuffix.Length) + UnderlyingSuffix;

    /// <summary>
    /// The wrapper document for a vendored <c>.any.js</c> file, or <see langword="null"/> when the file does
    /// not declare the <c>window</c> global and upstream would answer 404 instead.
    /// </summary>
    /// <param name="wrapperPath">The path requested, ending in <c>.any.html</c>.</param>
    /// <param name="source">The underlying <c>.any.js</c> file's text.</param>
    internal static string? Window(string wrapperPath, string source)
    {
        var metadata = ReadScriptMetadata(source);

        if (!IsExposedTo(WindowGlobal, metadata))
        {
            return null;
        }

        var meta = new StringBuilder();
        var script = new StringBuilder();

        foreach (var (key, value) in metadata)
        {
            AppendLine(meta, MetaReplacement(key, value));
            AppendLine(script, ScriptReplacement(key, value));
        }

        // AnyHtmlHandler.wrapper, verbatim, with %(meta)s, %(script)s and %(path)s filled in. The blank lines
        // a "\n".join over an empty iterator leaves behind are upstream's too: the wrapper's own newlines
        // stand whether or not the file had any metadata.
        return "<!doctype html>\n"
            + "<meta charset=utf-8>\n"
            + meta + "\n"
            + "<script>\n"
            + "self.GLOBAL = {\n"
            + "  isWindow: function() { return true; },\n"
            + "  isWorker: function() { return false; },\n"
            + "  isShadowRealm: function() { return false; },\n"
            + "};\n"
            + "</script>\n"
            + "<script src=\"/resources/testharness.js\"></script>\n"
            + "<script src=\"/resources/testharnessreport.js\"></script>\n"
            + script + "\n"
            + "<div id=log></div>\n"
            + "<script src=\"/" + UnderlyingFile(wrapperPath) + "\"></script>\n";
    }

    /// <summary>
    /// <c>sourcefile.read_script_metadata</c>: the <c>// META: key=value</c> lines a file <b>opens</b> with,
    /// stopping at the first line that is not one.
    /// </summary>
    /// <remarks>
    /// Stopping is the part that matters and the part a looser reader would get wrong: a <c>// META:</c>
    /// comment further down the file is a comment, and upstream's <c>for line in f: … if not m: break</c> says
    /// so. The regular expression is <c>js_meta_re</c>, <c>//\s*META:\s*(\w*)=(.*)$</c>.
    /// </remarks>
    internal static List<(string Key, string Value)> ReadScriptMetadata(string source)
    {
        var metadata = new List<(string, string)>();

        foreach (var rawLine in source.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (!TryReadMetaLine(line, out var key, out var value))
            {
                break;
            }

            metadata.Add((key, value));
        }

        return metadata;
    }

    /// <summary>
    /// One line against <c>js_meta_re</c>. Written out rather than expressed as a <c>Regex</c> so that the
    /// three pieces upstream's groups capture — the optional space, the word key, the rest of the line — are
    /// each visible.
    /// </summary>
    private static bool TryReadMetaLine(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        if (!line.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        var at = 2;
        while (at < line.Length && char.IsWhiteSpace(line[at]))
        {
            at++;
        }

        if (string.CompareOrdinal(line, at, "META:", 0, 5) != 0)
        {
            return false;
        }

        at += 5;
        while (at < line.Length && char.IsWhiteSpace(line[at]))
        {
            at++;
        }

        // `(\w*)` — letters, digits and underscore, and it may legitimately match nothing.
        var start = at;
        while (at < line.Length && (char.IsLetterOrDigit(line[at]) || line[at] == '_'))
        {
            at++;
        }

        if (at >= line.Length || line[at] != '=')
        {
            return false;
        }

        key = line.Substring(start, at - start);
        value = line.Substring(at + 1);
        return true;
    }

    /// <summary>
    /// <c>HtmlWrapperHandler.check_exposure</c> for one global: the <b>first</b> <c>global=</c> line decides,
    /// and a file with none takes <c>parse_variants("")</c>'s default of window plus dedicated worker.
    /// </summary>
    internal static bool IsExposedTo(string global, List<(string Key, string Value)> metadata)
    {
        foreach (var (key, value) in metadata)
        {
            if (string.Equals(key, "global", StringComparison.Ordinal))
            {
                return Array.IndexOf(ParseVariants(value), global) >= 0;
            }
        }

        return Array.IndexOf(_defaultVariants, global) >= 0;
    }

    /// <summary>
    /// <c>sourcefile.parse_variants</c>: a comma-separated list, each item expanded through
    /// <c>get_any_variants</c> — which is the identity for every name but the three longhands.
    /// </summary>
    /// <remarks>
    /// Only the longhands that can produce a <c>window</c> or a dedicated worker are expanded, because those
    /// are the only two globals anything here asks about; <c>shadowrealm</c>'s six-way expansion would answer
    /// the same "no" either way. An unknown name expands to nothing at all, which is upstream's
    /// <c>get_any_variants</c> answering an empty set rather than raising.
    /// </remarks>
    internal static string[] ParseVariants(string value)
    {
        if (value.Length == 0)
        {
            return _defaultVariants;
        }

        var variants = new List<string>();

        foreach (var item in value.Split(','))
        {
            var name = item.Trim();

            if (string.Equals(name, "worker", StringComparison.Ordinal))
            {
                variants.Add("dedicatedworker");
                variants.Add("sharedworker");
                variants.Add("serviceworker");
                continue;
            }

            // Every other name in _any_variants is its own longhand. `shadowrealm` expands to six names, none
            // of which is window or dedicatedworker, so leaving it as itself answers the same question.
            variants.Add(name);
        }

        return variants.ToArray();
    }

    /// <summary>
    /// <c>HtmlWrapperHandler._meta_replacement</c>: <c>timeout=long</c> and <c>title=</c>, and nothing else.
    /// </summary>
    private static string? MetaReplacement(string key, string value)
    {
        if (string.Equals(key, "timeout", StringComparison.Ordinal) && string.Equals(value, "long", StringComparison.Ordinal))
        {
            return "<meta name=\"timeout\" content=\"long\">";
        }

        if (string.Equals(key, "title", StringComparison.Ordinal))
        {
            // Upstream escapes exactly these two and not the quote, because the value lands in text content.
            return "<title>" + value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal) + "</title>";
        }

        return null;
    }

    /// <summary>
    /// <c>HtmlWrapperHandler._script_replacement</c>: one <c>&lt;script src&gt;</c> per <c>// META: script=</c>.
    /// </summary>
    private static string? ScriptReplacement(string key, string value)
    {
        if (!string.Equals(key, "script", StringComparison.Ordinal))
        {
            return null;
        }

        // Upstream escapes the ampersand and the quote here and not the less-than, because the value lands in
        // an attribute rather than in text.
        return "<script src=\"" + value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal) + "\"></script>";
    }

    /// <summary>
    /// <c>"\n".join(...)</c> over the replacements one key produced, which puts a newline <i>between</i> them
    /// and never after the last.
    /// </summary>
    private static void AppendLine(StringBuilder builder, string? replacement)
    {
        if (replacement is null)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append('\n');
        }

        builder.Append(replacement);
    }
}
#endif
