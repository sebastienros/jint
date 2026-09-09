using System.Collections.Concurrent;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Jint.Tests.Wpt;

namespace Jint.Tests.Browser.Wpt;

/// <summary>
/// The variants a document declares, and the two halves of a case name that carries one.
/// </summary>
/// <remarks>
/// <para>
/// <b>A document may ask to be run more than once.</b>
/// <see href="https://web-platform-tests.org/writing-tests/testharness.html#variants">testharness.js's
/// variants</see> are <c>&lt;meta name="variant" content="?query"&gt;</c> elements; upstream's manifest turns
/// each one into a test of its own whose URL is the document's path with that string appended, and the
/// document reads which one it is out of <c>location.search</c>. Two vendored documents declare them —
/// <c>dom/ranges/Range-in-shadow-after-the-shadow-removed.html</c> takes its shadow-root mode that way, and
/// <c>dom/events/handler-count.html</c> its event target — and until this existed the lane served the bare
/// path, so the first of them called <c>attachShadow({mode: null})</c> and failed WebIDL's enum conversion
/// before it reached its subject, while the second quietly ran one of its three.
/// </para>
/// <para>
/// <b>A case is therefore a path <i>and</i> a variant, spelled exactly as upstream's manifest spells it:</b>
/// <c>dom/ranges/Range-in-shadow-after-the-shadow-removed.html?mode=open</c>. That string is the key
/// everything else in the lane is found by — the minimum-test table, an exclusion, a cause's file, the
/// census's own bookkeeping — and there is deliberately <b>no spelling that means "every variant"</b>: two
/// variants are two runs of two different documents as far as the harness is concerned, so a divergence
/// measured in one is not evidence about the other, and a row that covered both would be a blanket of
/// exactly the kind <see cref="WptExclusion"/>'s two-sided rule exists to refuse. Upstream's own expectation
/// files name the full URL for the same reason.
/// </para>
/// <para>
/// <b>The engine lane's answer is the opposite one, and that is not a contradiction.</b>
/// <c>Jint.Tests/Wpt/AGENTS.md</c> records that <c>// META: variant=</c> sharding is ignored there: the shim
/// leaves <c>location.search</c> empty, <c>subsetTest</c> then runs everything, and one run of a file is the
/// union of all of its <c>?1-1000</c> shards. That works because a shard variant selects a <i>subset of the
/// same tests</i>. A query the document itself branches on selects <i>different</i> tests, and no single run
/// is their union — which is why this lane, whose whole point is that the harness and the environment are
/// the real ones, follows upstream's manifest instead.
/// </para>
/// </remarks>
internal static class WptBrowserVariants
{
    /// <summary>What a document that declares no variant runs at: the bare path, once.</summary>
    private static readonly string[] _bare = [""];

    private static readonly ConcurrentDictionary<string, string[]> _declared = new(StringComparer.Ordinal);

    /// <summary>The document a case names — the case name with its variant removed.</summary>
    internal static string DocumentOf(string caseName)
    {
        var cut = caseName.AsSpan().IndexOfAny('?', '#');
        return cut < 0 ? caseName : caseName.Substring(0, cut);
    }

    /// <summary>
    /// The variant a case names, including its leading <c>?</c> or <c>#</c>, or the empty string.
    /// </summary>
    internal static string VariantOf(string caseName)
    {
        var cut = caseName.AsSpan().IndexOfAny('?', '#');
        return cut < 0 ? string.Empty : caseName.Substring(cut);
    }

    /// <summary>
    /// The variants <paramref name="document"/> declares, in the order it declares them, or one empty string
    /// when it declares none — which is <c>SourceFile.test_variants</c>'s own <c>if not rv: rv = [""]</c>.
    /// </summary>
    /// <remarks>
    /// Cached, because the answer is a property of an embedded file that cannot change while the process
    /// lives and every enumeration of the lane's cases asks for it.
    /// </remarks>
    internal static IReadOnlyList<string> Of(string document) => _declared.GetOrAdd(document, Read);

    /// <summary>
    /// What to add to "… is not a case of any suite" when the row names a document that is only ever run at
    /// a variant, or the empty string when it does not.
    /// </summary>
    /// <remarks>
    /// That message reads as "delete this row", and for such a document the answer is the opposite: append
    /// the variant, once per case the row is about. There is no spelling that means every variant, so the
    /// fix is a row per case and the message has to name them.
    /// </remarks>
    internal static string HintFor(string named)
    {
        var document = DocumentOf(named);
        var underlying = WptServerWrappers.IsWrapperPath(document)
            ? WptServerWrappers.UnderlyingFile(document)
            : document;

        // A row can also name a file the corpus no longer holds, which is a different failure and needs no
        // help from here: asking for its variants would throw over the top of the report.
        if (!WptCorpus.Contains(underlying))
        {
            return string.Empty;
        }

        var declared = Of(document);
        if (declared.Count == 1 && declared[0].Length == 0)
        {
            return string.Empty;
        }

        var names = new List<string>(declared.Count);
        foreach (var variant in declared)
        {
            names.Add(document + variant);
        }

        return $" — it declares variants, so its cases are {string.Join(", ", names)}";
    }

    private static string[] Read(string document) => WptServerWrappers.IsWrapperPath(document)
        ? InScript(document, WptCorpus.Read(WptServerWrappers.UnderlyingFile(document)))
        : InDocument(document, WptCorpus.Read(document));

    /// <summary>
    /// The <c>content</c> of every <c>&lt;meta name="variant"&gt;</c> in the document, in tree order.
    /// </summary>
    /// <remarks>
    /// Upstream reads them off a real parse — <c>SourceFile.variant_nodes</c> is
    /// <c>root.findall(".//{http://www.w3.org/1999/xhtml}meta[@name='variant']")</c> over an html5lib tree —
    /// so this parses too rather than scanning the source for a substring. A <c>&lt;meta&gt;</c> the parser
    /// puts in a foreign-content tree is not one of these, and neither is a string in a script that happens
    /// to spell the markup; both are differences a scanner would get wrong and a parse gets right for free.
    /// </remarks>
    internal static string[] InDocument(string path, string source)
    {
        var variants = new List<string>();
        var document = new HtmlParser().ParseDocument(source);

        foreach (var element in document.All)
        {
            if (!string.Equals(element.LocalName, "meta", StringComparison.Ordinal)
                || !string.Equals(element.NamespaceUri, NamespaceNames.HtmlUri, StringComparison.Ordinal)
                || !string.Equals(element.GetAttribute("name"), "variant", StringComparison.Ordinal))
            {
                continue;
            }

            // Upstream's `if "content" in element.attrib`: a declaration with no content attribute is
            // skipped rather than read as the empty variant.
            if (element.GetAttribute("content") is { } content)
            {
                variants.Add(content);
            }
        }

        return Validate(path, variants);
    }

    /// <summary>
    /// The <c>// META: variant=</c> values of the script a wrapper wraps, which is the other half of
    /// <c>SourceFile.test_variants</c> — the <c>.js</c> branch of it.
    /// </summary>
    /// <remarks>
    /// No vendored <c>.any.js</c> under a browser-lane suite declares one today, so this reads nothing at
    /// this pin. It is here because upstream's manifest multiplies a <c>.any.html</c> URL by its variants
    /// exactly as it multiplies a document's, and a corpus bump that brought one in would otherwise have the
    /// lane run one shard of it and call that the file.
    /// </remarks>
    internal static string[] InScript(string path, string source)
    {
        var variants = new List<string>();

        foreach (var (key, value) in WptServerWrappers.ReadScriptMetadata(source))
        {
            if (string.Equals(key, "variant", StringComparison.Ordinal))
            {
                variants.Add(value);
            }
        }

        return Validate(path, variants);
    }

    /// <summary>
    /// Upstream's two rules on a declared variant, plus the one this lane needs on top of them.
    /// </summary>
    /// <remarks>
    /// <c>SourceFile.test_variants</c> raises for a non-empty variant that does not start with <c>?</c> or
    /// <c>#</c> and for one whose query or fragment is empty, so a corpus that grew either would fail
    /// upstream's own manifest build and has to fail here rather than produce a case name nothing can serve.
    /// The third rule is this lane's: a case name is a dictionary key in four tables, so two identical
    /// declarations would be one row silently standing for two runs.
    /// </remarks>
    private static string[] Validate(string document, List<string> declared)
    {
        // The one case upstream spells `if not rv: rv = [""]`: no declaration is one run at the bare path.
        if (declared.Count == 0)
        {
            return _bare;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var variant in declared)
        {
            if (variant.Length != 0)
            {
                if (variant[0] != '?' && variant[0] != '#')
                {
                    throw new InvalidOperationException(
                        $"{document} declares the variant \"{variant}\", and a non-empty variant must start with a ? or a #.");
                }

                if (variant.Length == 1 || (variant[0] == '?' && variant[1] == '#'))
                {
                    throw new InvalidOperationException(
                        $"{document} declares the variant \"{variant}\", which has an empty query or fragment; omit the empty part instead.");
                }
            }

            if (!seen.Add(variant))
            {
                throw new InvalidOperationException(
                    $"{document} declares the variant \"{variant}\" twice, and a case name is the key its exclusion, its minimum-test entry and its census row are all found by.");
            }
        }

        return declared.ToArray();
    }
}
