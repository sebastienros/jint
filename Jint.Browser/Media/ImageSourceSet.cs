using System.Globalization;
using Jint.HtmlParser;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;

namespace Jint.Browser.Media;

/// <summary>
/// <a href="https://html.spec.whatwg.org/multipage/images.html#update-the-source-set">HTML §4.8.4.3.6</a>'s
/// source set: which of an <c>&lt;img&gt;</c>'s candidates — its own <c>srcset</c>, a <c>&lt;picture&gt;</c>
/// parent's <c>&lt;source&gt;</c> elements, or the plain <c>src</c> — this page is actually asking for.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the binding decides this and not AngleSharp.</b> <c>ElementExtensions.GetImageCandidate</c> asks
/// <c>SourceSet.GetCandidates</c>, which yields every candidate in order and ignores its descriptor
/// altogether — so <c>srcset="small.png 1x, large.png 2x"</c> always resolves to <c>small.png</c> whatever
/// the device pixel ratio, <c>sizes</c> is read and discarded, and a <c>&lt;source&gt;</c>'s <c>media</c> is
/// never evaluated at all, so the first <c>&lt;source&gt;</c> in a <c>&lt;picture&gt;</c> always wins. Those
/// are three separate wrong answers a page can see, and <c>Dom/divergences.md</c> records them. The
/// <i>request</i> is still AngleSharp's — see <c>ParserDriver.FetchImage</c> — and only the URL it ends up
/// fetching is decided here.
/// </para>
/// <para>
/// <b>The environment is the page's own.</b> A <c>&lt;source media&gt;</c> and a <c>sizes</c> media condition
/// are evaluated by <see cref="MediaQuery"/> against <see cref="PageMediaEnvironment"/> — the same value
/// <c>matchMedia</c> answers from, so a client that emulates a viewport moves the selection with it. The
/// density is <see cref="Viewport.DeviceScaleFactor"/>, which is 1 until an emulation says otherwise.
/// </para>
/// <para>
/// <b>What a <c>w</c> descriptor needs is a length, and a length needs a viewport.</b> A width descriptor is
/// turned into a density by dividing by the <i>source size</i> the <c>sizes</c> attribute selects, so
/// <c>sizes</c> is parsed here rather than ignored: <c>px</c>, <c>vw</c>, <c>vh</c>, <c>em</c> and
/// <c>rem</c> resolve, and anything that needs a cascade this browser would have to lay out — a percentage,
/// <c>calc()</c>, <c>ch</c>, <c>ex</c> — makes that entry unusable and the next one is tried. With no usable
/// entry the source size is HTML's own default of <c>100vw</c>.
/// </para>
/// </remarks>
internal static class ImageSourceSet
{
    /// <summary>The root font size every <c>em</c> and <c>rem</c> is resolved against.</summary>
    /// <remarks>
    /// CSS's initial <c>font-size</c> is <c>medium</c>, which every engine renders as 16 px, and it is what
    /// <c>Layout/FlatLayout</c>'s row height is already built from. Reading the cascade instead would make a
    /// source-set selection depend on a computed style that depends on a viewport that this is choosing an
    /// image for.
    /// </remarks>
    private const double RootFontSize = 16;

    /// <summary>
    /// The URL <paramref name="element"/> should be fetching, or <see langword="null"/> when it selects no
    /// source at all.
    /// </summary>
    /// <param name="runtime">The page whose viewport and media environment the selection is made against.</param>
    /// <param name="element">An <c>&lt;img&gt;</c>, or an <c>&lt;input type=image&gt;</c>, which has neither
    /// a <c>srcset</c> nor a <c>&lt;picture&gt;</c> parent and is therefore always its own <c>src</c>.</param>
    internal static string? Select(PageRuntime runtime, Element element)
    {
        var work = new DomReadWork(runtime.Dom.NativeReadCheckpoint, runtime.Dom.CancellationToken);
        work.Check();
        if (element is not { NamespaceUri: Namespaces.Html, LocalName: "img" })
        {
            return Resolve(runtime, element, work.Attribute(element, "src"), work);
        }

        var image = element;
        var density = Density(runtime);

        // https://html.spec.whatwg.org/multipage/images.html#update-the-source-set step 2: the <source>
        // elements of a <picture> **parent**, in tree order, up to the img itself. A <picture> further up is
        // not one -- `img-picture-ancestor.html` is the upstream document about exactly that -- and neither
        // is a <source> after the img, which is why this stops rather than skipping.
        if (image.ParentNode is Element parent && parent is { NamespaceUri: Namespaces.Html, LocalName: "picture" })
        {
            for (var child = parent.FirstChild; child is not null; child = child.NextSibling)
            {
                work.Step();
                if (ReferenceEquals(child, image))
                {
                    break;
                }

                if (child is Element { NamespaceUri: Namespaces.Html, LocalName: "source" } source
                    && Selected(runtime, source, work.Attribute(source, "srcset"), density, work) is { } chosen)
                {
                    return chosen;
                }
            }
        }

        // Step 3.1: the element's own srcset, which is consulted before its src and instead of it, and the
        // src as the last candidate of all.
        return Selected(runtime, image, work.Attribute(image, "srcset"), density, work)
            ?? Resolve(runtime, image, work.Attribute(image, "src"), work);
    }

    /// <summary>
    /// Whether <paramref name="element"/> names any image source at all: a non-empty <c>src</c>, a
    /// non-empty <c>srcset</c>, or a <c>&lt;source&gt;</c> with one in its <c>&lt;picture&gt;</c> parent.
    /// </summary>
    /// <remarks>
    /// https://html.spec.whatwg.org/multipage/images.html#update-the-image-data steps 4 and 9: an element
    /// that uses neither <c>srcset</c> nor <c>&lt;picture&gt;</c> and whose <c>src</c> is absent or empty
    /// never obtains a request, so there is nothing to fetch and nothing to record as unfetched.
    /// </remarks>
    internal static bool HasCandidates(PageRuntime runtime, Element element)
    {
        var work = new DomReadWork(runtime.Dom.NativeReadCheckpoint, runtime.Dom.CancellationToken);
        if (!IsBlank(work.Attribute(element, "src"), work))
        {
            return true;
        }

        if (element is not { NamespaceUri: Namespaces.Html, LocalName: "img" })
        {
            return false;
        }

        if (!IsBlank(work.Attribute(element, "srcset"), work))
        {
            return true;
        }

        if (element.ParentNode is Element parent && parent is { NamespaceUri: Namespaces.Html, LocalName: "picture" })
        {
            for (var child = parent.FirstChild; child is not null && !ReferenceEquals(child, element); child = child.NextSibling)
            {
                work.Step();
                if (child is Element { NamespaceUri: Namespaces.Html, LocalName: "source" } source
                    && !IsBlank(work.Attribute(source, "srcset"), work))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsBlank(string? value, DomReadWork work) => value is null || Trim(value, work).Length == 0;

    /// <summary>
    /// Steps 3.4 to 3.8 for one candidate element: its <c>media</c>, its <c>type</c>, its parsed
    /// <c>srcset</c> normalised against its <c>sizes</c>, and the selection over the result.
    /// </summary>
    private static string? Selected(PageRuntime runtime, Element element, string? srcset, double density, DomReadWork work)
    {
        if (srcset is null || Trim(srcset, work).Length == 0)
        {
            return null;
        }

        // Step 3.4: a media attribute that does not match the environment takes the element out.
        if (work.Attribute(element, "media") is { Length: > 0 } media
            && !MediaQuery.Matches(media, runtime.Media))
        {
            return null;
        }

        // Step 3.7: and so does a type this browser could not read a size out of. It is the same question
        // ImageHeader answers about the bytes, asked of the declaration instead. An attribute that is absent
        // or holds only white space states nothing and takes nothing out.
        if (Trim(work.Attribute(element, "type") ?? "", work) is { Length: > 0 } type && !ImageHeader.SupportsType(type))
        {
            return null;
        }

        var candidates = Parse(srcset!, work);

        if (candidates.Count == 0)
        {
            return null;
        }

        var sourceSize = SourceSize(runtime, work.Attribute(element, "sizes"), work);
        Normalise(candidates, sourceSize, work);

        return Resolve(runtime, element, Best(candidates, density, work), work);
    }

    /// <summary>
    /// <a href="https://html.spec.whatwg.org/multipage/images.html#parse-a-srcset-attribute">Parsing a
    /// srcset attribute</a>: comma-separated URLs, each with an optional width or pixel-density descriptor.
    /// </summary>
    /// <remarks>
    /// A descriptor that is neither — the <c>h</c> descriptor HTML removed, or anything unparseable — makes
    /// that one candidate invalid rather than the whole attribute, which is the forgiving direction and the
    /// one that keeps a page's other candidates usable.
    /// </remarks>
    private static List<Candidate> Parse(string srcset, DomReadWork work)
    {
        var candidates = new List<Candidate>();
        var position = 0;

        while (position < srcset.Length)
        {
            // Splitting loop step 1: leading white space and commas are separators, however many there are.
            while (position < srcset.Length && (IsWhiteSpace(srcset[position]) || srcset[position] == ','))
            {
                work.Step();
                position++;
            }

            if (position >= srcset.Length)
            {
                break;
            }

            // **A URL is delimited by white space and not by a comma**, which is the whole reason this is a
            // tokenizer rather than a `Split(',')`: `srcset="data:,b"` is one candidate whose URL contains a
            // comma, and splitting on commas turns it into `data:` — which is a URL, so nothing downstream
            // could notice. `update-the-source-set.html` is what found it.
            var start = position;

            while (position < srcset.Length && !IsWhiteSpace(srcset[position]))
            {
                work.Step();
                position++;
            }

            var url = srcset.Substring(start, position - start);
            string descriptor;

            if (url.EndsWith(','))
            {
                // Step 4: a URL that ends in commas ends the candidate, and every trailing comma goes.
                url = url.TrimEnd(',');
                descriptor = "";
            }
            else
            {
                descriptor = Descriptor(srcset, ref position, work);
            }

            if (url.Length == 0)
            {
                continue;
            }

            if (Describe(url, descriptor, work) is { } candidate)
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    /// <summary>
    /// The descriptor tokens between the end of a candidate's URL and the comma that ends it, consuming that
    /// comma.
    /// </summary>
    /// <remarks>
    /// A comma inside parentheses does not end the candidate. Nothing HTML defines today puts one there, and
    /// the descriptor tokenizer allows it anyway so that a future descriptor taking a function does not
    /// silently split every candidate that uses it.
    /// </remarks>
    private static string Descriptor(string srcset, ref int position, DomReadWork work)
    {
        var start = position;
        var nested = false;

        while (position < srcset.Length)
        {
            var character = srcset[position];

            if (nested)
            {
                if (character == ')')
                {
                    nested = false;
                }
            }
            else if (character == ',')
            {
                var text = srcset.Substring(start, position - start);
                work.Step();
                position++;
                return text;
            }
            else if (character == '(')
            {
                nested = true;
            }

            work.Step();
            position++;
        }

        return srcset.Substring(start, position - start);
    }

    /// <summary>
    /// One candidate's descriptor turned into a width or a density, or <see langword="null"/> for a
    /// descriptor HTML makes the candidate invalid for.
    /// </summary>
    /// <remarks>
    /// <b>Two descriptors is not one descriptor.</b> <c>srcset="a.png 1x 1x"</c> has zero valid candidates
    /// and therefore selects nothing at all — it does not fall back to the first token — which is what makes
    /// a <c>&lt;source&gt;</c> carrying it drop through to the next one.
    /// </remarks>
    private static Candidate? Describe(string url, string descriptor, DomReadWork work)
    {
        work.Check();
        var tokens = descriptor.Split([' ', '\t', '\n', '\r', '\f'], StringSplitOptions.RemoveEmptyEntries);

        work.Check();
        if (tokens.Length == 0)
        {
            return new Candidate(url, Width: 0, Density: 0);
        }

        if (tokens.Length > 1)
        {
            return null;
        }

        var token = tokens[0];
        var value = token.Substring(0, token.Length - 1);

        if (token.EndsWith('w')
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var width)
            && width > 0)
        {
            return new Candidate(url, width, Density: 0);
        }

        if (token.EndsWith('x')
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)
            && scale > 0)
        {
            return new Candidate(url, Width: 0, scale);
        }

        return null;
    }

    /// <summary>The five characters HTML calls ASCII white space.</summary>
    private static bool IsWhiteSpace(char character)
        => character is ' ' or '\t' or '\n' or '\r' or '\f';

    /// <summary>
    /// <a href="https://html.spec.whatwg.org/multipage/images.html#normalising-the-source-densities">Normalising
    /// the source densities</a>: a width descriptor becomes a density against the source size, and a
    /// candidate with no descriptor at all is 1.
    /// </summary>
    private static void Normalise(List<Candidate> candidates, double sourceSize, DomReadWork work)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            work.Step();
            var candidate = candidates[i];

            if (candidate.Density > 0)
            {
                continue;
            }

            candidates[i] = candidate with
            {
                Density = candidate.Width > 0 && sourceSize > 0 ? candidate.Width / sourceSize : 1,
            };
        }
    }

    /// <summary>
    /// <a href="https://html.spec.whatwg.org/multipage/images.html#select-an-image-source">Selecting an image
    /// source from a source set</a>, whose choice among the candidates HTML deliberately does not make: step
    /// 1 drops a duplicate density and step 2 is "in an implementation-defined manner, choose one image
    /// source".
    /// </summary>
    /// <remarks>
    /// <b>So this rounds up, because every browser does.</b> The candidate taken is the one with the smallest
    /// density at or above the device's, and the largest density there is when none reaches it. Rounding
    /// <i>down</i> is just as conformant and answers a candidate whose pixels do not cover the box it was
    /// chosen for — <c>srcset="small.png 400w, large.png 2000w"</c> in a 1280 px slot would select
    /// <c>small.png</c>, which is the one outcome responsive images exist to avoid, and an automation client
    /// comparing <c>currentSrc</c> against a real browser's would read a different URL.
    /// </remarks>
    private static string Best(List<Candidate> candidates, double density, DomReadWork work)
    {
        var best = candidates[0];
        var found = false;

        foreach (var candidate in candidates)
        {
            work.Step();
            // Strictly less than, so an earlier candidate wins a tie: HTML's step 1 removes the *later* of
            // two entries with the same density before anything is chosen.
            if (candidate.Density >= density && (!found || candidate.Density < best.Density))
            {
                best = candidate;
                found = true;
            }
        }

        if (found)
        {
            return best.Url;
        }

        foreach (var candidate in candidates)
        {
            work.Step();
            if (candidate.Density > best.Density)
            {
                best = candidate;
            }
        }

        return best.Url;
    }

    /// <summary>
    /// <a href="https://html.spec.whatwg.org/multipage/images.html#parse-a-sizes-attribute">Parsing a sizes
    /// attribute</a>: the first entry whose media condition matches gives the source size, and the default is
    /// <c>100vw</c>.
    /// </summary>
    private static double SourceSize(PageRuntime runtime, string? sizes, DomReadWork work)
    {
        var viewport = runtime.Media.Viewport;

        if (sizes is { Length: > 0 })
        {
            foreach (var entry in SizeEntries(sizes, work))
            {
                var trimmed = Trim(entry, work);

                if (trimmed.Length == 0)
                {
                    continue;
                }

                var condition = "";
                var length = trimmed;

                if (trimmed.StartsWith('('))
                {
                    var close = MatchingParenthesis(trimmed, work);

                    if (close < 0)
                    {
                        continue;
                    }

                    condition = trimmed.Substring(0, close + 1);
                    length = Trim(trimmed.Substring(close + 1), work);
                }

                if (condition.Length > 0 && !MediaQuery.Matches(condition, runtime.Media))
                {
                    continue;
                }

                if (Length(length, viewport, work) is { } resolved)
                {
                    return resolved;
                }
            }
        }

        return viewport.Width;
    }

    /// <summary>The index of the parenthesis that closes the one at index 0, or −1.</summary>
    /// <remarks>
    /// A media condition nests — <c>(min-width: 10px) and (max-width: 20px)</c> is one entry — so the split
    /// between the condition and the length cannot be the first <c>)</c>.
    /// </remarks>
    private static int MatchingParenthesis(string text, DomReadWork work)
    {
        var depth = 0;

        for (var i = 0; i < text.Length; i++)
        {
            work.Step();
            if (text[i] == '(')
            {
                depth++;
            }
            else if (text[i] == ')' && --depth == 0)
            {
                // A condition may be a conjunction of parenthesised terms; the length is what follows the
                // last of them, which is the last ')' before a token that is not `and`/`or`/`not`/`(`.
                var rest = Trim(text.Substring(i + 1), work);

                if (rest.StartsWith("and ", StringComparison.OrdinalIgnoreCase)
                    || rest.StartsWith("or ", StringComparison.OrdinalIgnoreCase)
                    || rest.StartsWith("not ", StringComparison.OrdinalIgnoreCase)
                    || rest.StartsWith('('))
                {
                    continue;
                }

                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// A <c>&lt;source-size-value&gt;</c> in CSS pixels, or <see langword="null"/> for one that needs a
    /// layout this browser has none of.
    /// </summary>
    private static double? Length(string text, Viewport viewport, DomReadWork work)
    {
        text = Trim(text, work);

        if (text.Length == 0)
        {
            return null;
        }

        (string Unit, double Scale)[] units =
        [
            ("px", 1),
            ("vw", viewport.Width / 100.0),
            ("vh", viewport.Height / 100.0),
            ("vmin", Math.Min(viewport.Width, viewport.Height) / 100.0),
            ("vmax", Math.Max(viewport.Width, viewport.Height) / 100.0),
            ("rem", RootFontSize),
            ("em", RootFontSize),
        ];

        foreach (var (unit, scale) in units)
        {
            if (!text.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = Trim(text.Substring(0, text.Length - unit.Length), work);

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number > 0
                ? number * scale
                : null;
        }

        return null;
    }

    /// <summary>The device pixel ratio a density descriptor is selected against.</summary>
    private static double Density(PageRuntime runtime)
    {
        var factor = runtime.Media.Viewport.DeviceScaleFactor;
        return factor > 0 ? factor : 1;
    }

    /// <summary>A candidate URL resolved against the element that declared it.</summary>
    /// <remarks>
    /// A <c>&lt;source&gt;</c>'s candidates resolve against the <c>&lt;source&gt;</c>, which is what
    /// <c>&lt;base href&gt;</c> inside a <c>&lt;picture&gt;</c> would move; nothing else in the selection
    /// depends on which element a candidate came from.
    /// </remarks>
    private static string? Resolve(PageRuntime runtime, Element element, string? url, DomReadWork work)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        var baseUrl = DomDocumentState.BaseUri(element.OwnerDocument!, runtime.Engine.Constraints.Check, work.Token);

        work.Check();
        var resolved = string.IsNullOrEmpty(baseUrl) ? url : PageUrl.Resolve(url!, baseUrl!) ?? url;
        work.Check();
        return resolved;
    }

    private static string Trim(string text, DomReadWork work)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && char.IsWhiteSpace(text[start])) { work.Step(); start++; }
        while (end > start && char.IsWhiteSpace(text[end - 1])) { work.Step(); end--; }
        work.Check();
        var value = text.Substring(start, end - start);
        work.Check();
        return value;
    }

    private static IEnumerable<string> SizeEntries(string sizes, DomReadWork work)
    {
        var start = 0;
        for (var i = 0; i < sizes.Length; i++)
        {
            work.Step();
            if (sizes[i] != ',') continue;
            yield return sizes.Substring(start, i - start);
            start = i + 1;
        }
        work.Check();
        yield return sizes.Substring(start);
        work.Check();
    }

    /// <summary>One entry of a parsed <c>srcset</c>, before and after normalisation.</summary>
    /// <remarks>
    /// <see cref="Width"/> is the <c>w</c> descriptor and <see cref="Density"/> the <c>x</c> one; exactly one
    /// of them is set by the parse, and normalisation leaves only <see cref="Density"/> meaningful.
    /// </remarks>
    private readonly record struct Candidate(string Url, double Width, double Density);
}
