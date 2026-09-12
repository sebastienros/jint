using Jint.Tests.Wpt;

namespace Jint.Tests.Browser.Wpt;

/// <summary>
/// Holds <see cref="WptBrowserVariants"/> to <c>SourceFile.test_variants</c>, and the lane's case list to
/// the variants the vendored corpus actually declares.
/// </summary>
/// <remarks>
/// <para>
/// <b>The corpus-facing tests are the ones a bump will meet.</b> A document that arrives declaring a variant
/// is a document upstream runs several times, and a lane that served the bare path would run one of them and
/// call that the file — which is exactly what
/// <c>dom/ranges/Range-in-shadow-after-the-shadow-removed.html</c> did until this existed, failing both its
/// tests on an <c>attachShadow({mode: null})</c> the document never meant to make.
/// </para>
/// <para>
/// <b>The parsing tests use synthetic sources on purpose.</b> Two vendored documents declare variants today
/// and between them they exercise one shape; upstream's rules cover several more, and a rule nothing runs is
/// a rule that will be wrong the first time the corpus needs it.
/// </para>
/// </remarks>
public class WptBrowserVariantTests
{
    private const string Range = "dom/ranges/Range-in-shadow-after-the-shadow-removed.html";
    private const string HandlerCount = "dom/events/handler-count.html";

    /// <summary>
    /// The two vendored documents that declare variants are one case per declaration, and the bare path is
    /// not a case of either.
    /// </summary>
    /// <remarks>
    /// The bare path matters as much as the variants do: it is the run this lane used to have, and a case
    /// list that kept it would run a document at a query its own script has no branch for.
    /// </remarks>
    [Test]
    public void ADocumentThatDeclaresVariantsIsOneCasePerVariant()
    {
        var ranges = WptBrowserCorpus.Cases("dom/ranges");
        ranges.Should().Contain(Range + "?mode=closed").And.Contain(Range + "?mode=open");
        ranges.Should().NotContain(Range, "the bare path is not a run upstream has");

        var events = WptBrowserCorpus.Cases("dom/events");
        events.Should().Contain(HandlerCount + "?document")
            .And.Contain(HandlerCount + "?window")
            .And.Contain(HandlerCount + "?element");
        events.Should().NotContain(HandlerCount, "the bare path is not a run upstream has");

        // Declaration order, and adjacent to their own document, so the theory's cases stay stable.
        WptBrowserVariants.Of(HandlerCount).Should().Equal("?document", "?window", "?element");
        WptBrowserVariants.Of(Range).Should().Equal("?mode=closed", "?mode=open");
    }

    /// <summary>
    /// A document that declares variants is still <i>one</i> document, which is what the census's
    /// <c>Documents</c> column counts.
    /// </summary>
    [Test]
    public void AVariantIsAnotherCaseAndNotAnotherDocument()
    {
        var documents = WptBrowserCorpus.Documents("dom/ranges");

        documents.Should().Contain(Range).And.OnlyHaveUniqueItems();
        documents.Count.Should().Be(WptCorpus.BrowserTestFiles("dom/ranges").Count,
            "every vendored document of the suite is a document of it exactly once");
        WptBrowserCorpus.Cases("dom/ranges").Count.Should().Be(documents.Count + 1, "one of them declares two variants");
    }

    /// <summary>
    /// Every vendored document that declares a variant is reached at every one of them.
    /// </summary>
    /// <remarks>
    /// The check a corpus bump runs into. It walks the whole vendored tree rather than the two documents
    /// above, so a suite that arrives with a variant-declaring document either becomes cases or is one of the
    /// answers the lane already has for a document it does not run — a not-vendored row, a frame body, or a
    /// helper below a suite rather than in it.
    /// </remarks>
    [Test]
    public void EveryVariantDeclaredByTheCorpusIsACase()
    {
        var problems = new List<string>();
        var cases = new HashSet<string>(StringComparer.Ordinal);
        var declaring = 0;

        foreach (var suite in WptCorpus.BrowserSuites)
        {
            foreach (var name in WptBrowserCorpus.Cases(suite))
            {
                cases.Add(name);
            }
        }

        foreach (var path in WptCorpus.Paths)
        {
            if (!WptCorpus.IsBrowserTestFile(path)
                || !Array.Exists(WptCorpus.BrowserSuites, suite => string.Equals(WptCorpus.DirectoryOf(path), suite, StringComparison.Ordinal))
                || WptBrowserCorpus.IsFrameBody(path))
            {
                continue;
            }

            var variants = WptBrowserVariants.Of(path);
            if (variants.Count == 1 && variants[0].Length == 0)
            {
                continue;
            }

            declaring++;

            foreach (var variant in variants)
            {
                if (!cases.Contains(path + variant))
                {
                    problems.Add($"{path} declares the variant \"{variant}\" and {path + variant} is not a case");
                }
            }
        }

        string.Join(Environment.NewLine, problems).Should().BeEmpty();
        declaring.Should().Be(2, "handler-count.html and Range-in-shadow-after-the-shadow-removed.html declare variants at this pin");
    }

    /// <summary>
    /// No vendored <c>.any.js</c> under a browser-lane suite declares a variant at this pin, which is why the
    /// wrapper half of the rule reads nothing today.
    /// </summary>
    /// <remarks>
    /// Recorded rather than assumed: the engine lane deliberately ignores <c>// META: variant=</c> because
    /// one unsharded run of a <c>.any.js</c> file is the union of its shards, and this lane deliberately does
    /// not, because it is upstream's manifest it follows. The two lanes disagreeing about a file is allowed;
    /// nobody having noticed that they do is not.
    /// </remarks>
    [Test]
    public void NoWrappedScriptDeclaresAVariantAtThisPin()
    {
        foreach (var suite in WptCorpus.BrowserSuites)
        {
            foreach (var wrapper in WptBrowserCorpus.SynthesizedCases(suite))
            {
                WptBrowserVariants.Of(wrapper).Should().Equal([""], $"{wrapper} wraps a script that declares no variant");
            }
        }
    }

    [Test]
    public void ADocumentWithNoDeclarationRunsOnceAtTheBarePath()
    {
        WptBrowserVariants.InDocument("a.html", "<!doctype html><title>x</title>").Should().Equal([""]);
        WptBrowserVariants.InScript("a.any.html", "// META: global=window\n").Should().Equal([""]);
    }

    /// <summary>Every shape of the declaration upstream's <c>variant_nodes</c> finds, and two it does not.</summary>
    [TestCase("<meta name=variant content=?a><meta name=variant content=?b>", "?a|?b", TestName = "unquoted attributes, in tree order")]
    [TestCase("<meta name=\"variant\" content=\"?a\">", "?a", TestName = "quoted attributes")]
    [TestCase("<meta name=variant content=#frag>", "#frag", TestName = "a fragment variant")]
    [TestCase("<meta name=variant>", "", TestName = "no content attribute is not a declaration")]
    [TestCase("<meta name=Variant content=?a>", "", TestName = "the name is matched exactly")]
    [TestCase("<script>const s = '<meta name=variant content=?a>';</script>", "", TestName = "markup spelled inside a script is not one")]
    [TestCase("<body><meta name=variant content=?a>", "?a", TestName = "a declaration the parser moves is still one")]
    public void TheDeclarationIsWhatAParseSaysItIs(string body, string expected)
    {
        var declared = WptBrowserVariants.InDocument("a.html", "<!doctype html><title>x</title>" + body);
        var wanted = expected.Length == 0 ? [""] : expected.Split('|');

        declared.Should().Equal(wanted);
    }

    [Test]
    public void AWrappedScriptDeclaresItsVariantsInMetadata()
    {
        const string source = """
            // META: global=window
            // META: variant=?1-1000
            // META: variant=?1001-last
            test(() => {}, "x");
            """;

        WptBrowserVariants.InScript("a.any.html", source).Should().Equal("?1-1000", "?1001-last");
    }

    // Upstream's own two rules, from SourceFile.test_variants: a non-empty variant starts with ? or #, and
    // neither part may be present and empty. A corpus that grew one would fail wpt's manifest build, so it
    // has to fail here rather than make a case name nothing can serve.
    [TestCase("mode=open", "must start with a ? or a #")]
    [TestCase("?", "empty query or fragment")]
    [TestCase("#", "empty query or fragment")]
    [TestCase("?#", "empty query or fragment")]
    public void AVariantUpstreamWouldRefuseIsRefusedHere(string variant, string expected)
    {
        var declaring = $"<!doctype html><meta name=variant content=\"{variant}\">";

        var thrown = Caught(() => WptBrowserVariants.InDocument("a.html", declaring));

        thrown.Should().BeOfType<InvalidOperationException>();
        thrown!.Message.Should().Contain(expected).And.Contain("a.html");
    }

    /// <summary>
    /// A variant declared twice is refused, which is this lane's rule rather than upstream's.
    /// </summary>
    /// <remarks>
    /// A case name is the key its minimum-test entry, its exclusion and its cause row are all found by, so
    /// two identical declarations would be one row standing for two runs — and the duplicate would surface
    /// far away, as a case NUnit reaches twice.
    /// </remarks>
    [Test]
    public void AVariantDeclaredTwiceIsRefused()
    {
        var thrown = Caught(() => WptBrowserVariants.InDocument(
            "a.html", "<!doctype html><meta name=variant content=?a><meta name=variant content=?a>"));

        thrown.Should().BeOfType<InvalidOperationException>();
        thrown!.Message.Should().Contain("twice");
    }

    [TestCase("a/b.html", "a/b.html", "")]
    [TestCase("a/b.html?mode=open", "a/b.html", "?mode=open")]
    [TestCase("a/b.html#frag", "a/b.html", "#frag")]
    [TestCase("a/b.html?q=1#frag", "a/b.html", "?q=1#frag")]
    public void ACaseNameSplitsIntoItsDocumentAndItsVariant(string name, string document, string variant)
    {
        WptBrowserVariants.DocumentOf(name).Should().Be(document);
        WptBrowserVariants.VariantOf(name).Should().Be(variant);
        (WptBrowserVariants.DocumentOf(name) + WptBrowserVariants.VariantOf(name)).Should().Be(name);
    }

    /// <summary>
    /// A table row that names a variant-declaring document rather than one of its cases is told which case
    /// names exist, because "not a case of any suite" otherwise reads as "delete this row".
    /// </summary>
    [Test]
    public void ARowThatNamesADocumentRatherThanACaseIsToldItsCases()
    {
        WptBrowserVariants.HintFor(HandlerCount).Should()
            .Contain(HandlerCount + "?document")
            .And.Contain(HandlerCount + "?window")
            .And.Contain(HandlerCount + "?element");

        WptBrowserVariants.HintFor("dom/ranges/Range-attributes.html").Should().BeEmpty(
            "a document with no variant needs no help");
        WptBrowserVariants.HintFor("dom/ranges/gone.html").Should().BeEmpty(
            "a row naming a file the corpus does not hold is a different failure, and asking for its variants would throw over it");
    }

    private static Exception? Caught(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}
