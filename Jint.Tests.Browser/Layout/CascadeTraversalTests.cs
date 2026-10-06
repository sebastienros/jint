using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using Jint.Browser.Layout;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.Browser.Layout;

public sealed class CascadeTraversalTests
{
    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(false, 192)]
    [TestCase(true, 192)]
    public void EquivalentSiblingsComputeTheirLiteralDeclarationsOncePerQuery(bool layout, int precedingStyles)
    {
        using var fixture = Create(
            "<style>.item { display:block }</style><main>"
            + string.Concat(Enumerable.Range(1, precedingStyles)
                .Select(width => $"<i style='display:block;width:{width}px' data-width='{width}'></i>"))
            + string.Concat(Enumerable.Repeat("<div class='item'></div>", 64)) + "</main>");
        var document = fixture.Document;
        var rule = NativeCssParsing.ApplicableRules(
            NativeCssStyleSheets.Get(document, new CssValueWork(default)).Single().Sheet,
            new CssMediaEnvironment(), new CssValueWork(default)).OfType<CssStyleRule>().Single();
        var authored = rule.Style.GetDeclaration(0).Value;
        var diagnostics = new NativeCssQueryDiagnostics(captureDetails: true);
        var scope = layout ? CssCascade.StyleScope.Layout : CssCascade.StyleScope.Visibility;
        var elements = Select(document, "main > *");
        for (var query = 1; query <= 2; query++)
        {
            var traversal = CssCascade.Traversal.For(document, scope, diagnostics)!;
            foreach (var element in elements)
            {
                var computed = traversal.Of(element);
                computed.GetPropertyValue("display").Should().Be("block");
                if (ContentDom.ClassNames(element).Contains("item"))
                    computed.GetProperty("display").Text.Should().BeSameAs(authored,
                        "literal declarations share the resolved immutable value, while results belong to each receiver");
                if (layout && element.GetAttribute("data-width") is { } width)
                    computed.GetPropertyValue("width").Should().Be(width + "px");
            }
            var record = diagnostics.Queries[query - 1];
            record.Rules![rule].Matches.Should().Be(64);
            record.Rules[rule].Attempts.Should().Be(64, "the rule index offers .item only to elements carrying that class");
            foreach (var element in elements)
            {
                record.Elements![element].StatePublications.Should().Be(1);
                record.Elements[element].ComputedPublications["display"].Should().Be(1);
            }
            var attempts = record.RuleAttempts;
            var publications = record.ComputedPublications.ToArray();
            foreach (var element in elements) traversal.Of(element).GetPropertyValue("display").Should().Be("block");
            record.RuleAttempts.Should().Be(attempts);
            record.ComputedPublications.ToArray().Should().BeEquivalentTo(publications);
        }
        diagnostics.Queries.Count.Should().Be(2);
        diagnostics.Queries[1].Should().NotBeSameAs(diagnostics.Queries[0]);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SharedDeclarationsPreserveSpecificityInlineStylesAndInheritance(bool layout)
    {
        using var fixture = Create(
            """
            <style>
              #strong, .item { display:none }
              .item[data-x] { display:block }
              .important { display:flex !important }
              .hidden { visibility:hidden }
              .shown { visibility:visible }
              .variable { display:var(--shown) }
              .inherited { display:inherit }
            </style>
            <main>
              <div id="strong" class="item" data-x></div><div class="item" data-x></div>
              <div class="item" data-x style="display:inline"></div>
              <div class="item important" data-x style="display:inline"></div>
              <section class="hidden"><span></span><span></span></section>
              <section class="shown"><span></span><span></span></section>
              <section style="--shown:block"><span class="variable"></span><span class="variable"></span></section>
              <section style="--shown:none"><span class="variable"></span><span class="variable"></span></section>
              <section style="display:flex"><article><span class="inherited"></span></article></section>
              <section style="display:none"><article><span class="inherited"></span></article></section>
            </main>
            """);
        var document = fixture.Document;
        var scope = layout ? CssCascade.StyleScope.Layout : CssCascade.StyleScope.Visibility;
        var traversal = CssCascade.Traversal.For(document, scope)!;
        var complete = CssCascade.Traversal.For(document)!;
        foreach (var element in ContentDom.Descendants(document))
        {
            var actual = traversal.Of(element)!;
            var expected = complete.Of(element)!;
            foreach (var property in new[] { "display", "visibility" })
            {
                actual.GetPropertyValue(property).Should().Be(expected.GetPropertyValue(property), "native element " + element.LocalName);
                actual.GetPropertyPriority(property).Should().Be(expected.GetPropertyPriority(property));
            }
        }
        Select(document, ".item").Select(element => traversal.Of(element)!.GetPropertyValue("display"))
            .Should().Equal("none", "block", "inline", "flex");
        Select(document, ".variable").Select(element => traversal.Of(element)!.GetPropertyValue("display"))
            .Should().Equal("block", "block", "none", "none");
        Select(document, ".inherited").Select(element => traversal.Of(element)!.GetPropertyValue("display"))
            .Should().Equal("block", "block"); // display inherits the immediate article’s computed UA value.
    }

    [Test]
    public void SharedClassCandidatesStillMatchEachElementsAttributesAndAncestors()
    {
        using var fixture = Create(
            "<style>.item { display:block } .item[data-hide], .hidden > .item { display:none }</style>"
            + "<main><div class='item'></div><div class='item' data-hide></div></main>"
            + "<main class='hidden'><div class='item'></div></main>");
        var document = fixture.Document;
        var traversal = CssCascade.Traversal.For(document, CssCascade.StyleScope.Visibility)!;

        Select(document, ".item").Select(element => traversal.Of(element)!.GetPropertyValue("display"))
            .Should().Equal("block", "none", "none");
    }

    [TestCase(":unsupported-jint-pseudo")]
    [TestCase(".a")]
    [TestCase(".a.b")]
    [TestCase(".ancestor .a")]
    [TestCase(".a > .b")]
    [TestCase(".a + .b")]
    [TestCase(".a ~ .b")]
    [TestCase(":not(.a)")]
    [TestCase(":is(.a,.b)")]
    [TestCase(".a, .b")]
    [TestCase("[data-x].a")]
    [TestCase(@".\31 23")]
    [TestCase(".a:hover")]
    [TestCase(".a:nth-child(2)")]
    public void ClassCandidatesPreserveNativeSelectorMatching(string selector)
    {
        using var fixture = Create(
            $"<style>{selector} {{ display:none }} .b {{ display:flex }} {selector} {{ visibility:hidden }}</style>"
            + "<main class='ancestor'><div class='a' data-x><span class='b'></span></div>"
            + "<div class='a b'></div><div class='123'></div><div class='b'></div></main>");
        var document = fixture.Document;
        var complete = CssCascade.Traversal.For(document)!;
        var scoped = CssCascade.Traversal.For(document, CssCascade.StyleScope.Visibility)!;
        // Independent expected values for main, its first div/span, and its three remaining divs.
        var displays = selector switch
        {
            // CSS Cascade 5 §6.1: later .b wins its equal-specificity tie with .a on class="a b".
            ".a" => new[] { "block", "none", "flex", "flex", "block", "flex" },
            ".ancestor .a" => new[] { "block", "none", "flex", "none", "block", "flex" },
            ".a.b" or ".a + .b" or ".a:nth-child(2)" => new[] { "block", "block", "flex", "none", "block", "flex" },
            ".a > .b" => new[] { "block", "block", "none", "flex", "block", "flex" },
            ".a ~ .b" => new[] { "block", "block", "flex", "none", "block", "none" },
            ":not(.a)" => new[] { "none", "block", "flex", "flex", "none", "flex" },
            ":is(.a,.b)" or ".a, .b" or "[data-x].a" => new[] { "block", "none", "flex", "flex", "block", "flex" },
            @".\31 23" => new[] { "block", "block", "flex", "flex", "none", "flex" },
            _ => new[] { "block", "block", "flex", "flex", "block", "flex" }
        };
        var visibilities = selector switch
        {
            ".a" or ".ancestor .a" => new[] { "visible", "hidden", "hidden", "hidden", "visible", "visible" },
            ".a.b" or ".a + .b" or ".a:nth-child(2)" => new[] { "visible", "visible", "visible", "hidden", "visible", "visible" },
            ".a > .b" => new[] { "visible", "visible", "hidden", "visible", "visible", "visible" },
            ".a ~ .b" => new[] { "visible", "visible", "visible", "hidden", "visible", "hidden" },
            ":not(.a)" => Enumerable.Repeat("hidden", 6).ToArray(),
            ":is(.a,.b)" or ".a, .b" => new[] { "visible", "hidden", "hidden", "hidden", "visible", "hidden" },
            "[data-x].a" => new[] { "visible", "hidden", "hidden", "visible", "visible", "visible" },
            @".\31 23" => new[] { "visible", "visible", "visible", "visible", "hidden", "visible" },
            _ => Enumerable.Repeat("visible", 6).ToArray()
        };
        var receivers = Select(document, "main, main div, main span");
        receivers.Select(element => scoped.Of(element).GetPropertyValue("display")).Should().Equal(displays);
        receivers.Select(element => scoped.Of(element).GetPropertyValue("visibility")).Should().Equal(visibilities);
        foreach (var element in ContentDom.Descendants(document))
        {
            var expected = complete.Of(element)!;
            var actual = scoped.Of(element)!;
            actual.GetPropertyValue("display").Should().Be(expected.GetPropertyValue("display"));
            actual.GetPropertyValue("visibility").Should().Be(expected.GetPropertyValue("visibility"));
            foreach (var property in new[] { "display", "visibility" })
            {
                CssCascade.Of(element)!.GetPropertyValue(property)
                    .Should().Be(expected.GetPropertyValue(property));
            }
        }
    }

    [Test]
    public void ScopedCascadeKeepsNestedRulesUnderEmptyParents()
    {
        using var fixture = Create(
            "<style>main { & > button { display:none; color:red } }</style>"
            + "<main><button>hidden</button></main>");
        var document = fixture.Document;
        var button = ContentDom.First(document, "button")!;
        foreach (var scope in new[] { CssCascade.StyleScope.All, CssCascade.StyleScope.Visibility, CssCascade.StyleScope.Layout })
        {
            var actual = CssCascade.Traversal.For(document, scope)!.Of(button);
            actual.GetPropertyValue("display").Should().Be("none");
            actual.GetPropertyValue("color").Should().Be("red");
        }
    }

    [TestCase("block")]
    [TestCase("flex")]
    public void BoundedHeightPreservesExactMeasurements(string display)
    {
        using var fixture = Create(
            $"<main style='display:{display};flex-direction:row-reverse'>"
            + "<div><button>one</button><button>two</button></div><div hidden>hidden</div>"
            + "<div><div><button>three</button></div></div></main>");
        var document = fixture.Document;
        var visibility = new ElementVisibility(useComputedStyle: true);
        var root = document.DocumentElement!;
        var full = FlatLayout.Of(document, visibility, 1280, 32, 0);
        var expected = full.DocumentBoxOf(root)!.Value.Height;
        foreach (var bound in new[] { 1d, 16, 33, expected - 1, expected, expected + 16 })
        {
            var sizes = new FlatLayout.SizeQuery(document, visibility, 1280, visibility.CreateTraversal(document));
            sizes.HeightUpTo(root, bound).Should().Be(Math.Min(expected, Math.Ceiling(bound / 16) * 16));
            foreach (var element in ContentDom.Descendants(document))
            {
                if (full.DocumentBoxOf(element) is { } box)
                {
                    sizes.Place(element).Should().Be(box);
                }
            }
        }
    }

    [Test]
    public async Task TheAdminFormMatchesSelectorsOnlyOnceForEachElement()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(AdminSettingsDocument.Create());
        var counts = await page.RunOnLoopAsync(engine =>
        {
            var document = PageRuntime.Find(engine)!.Document!;
            var diagnostics = new NativeCssQueryDiagnostics(captureDetails: true);
            var traversal = CssCascade.Traversal.For(document, diagnostics: diagnostics)!;
            var elements = ContentDom.Descendants(document).ToArray();
            foreach (var element in elements)
            {
                var actual = traversal.Of(element);
                actual.GetPropertyValue("display").Should().NotBeEmpty();
                actual.GetPropertyValue("visibility").Should().NotBeEmpty();
            }
            var record = diagnostics.Queries.Single();
            record.StatePublications.Should().Be(elements.Length);
            record.Elements!.Values.Should().OnlyContain(detail => detail.StatePublications == 1);
            foreach (var (rule, detail) in record.Rules!)
            {
                var owner = rule.ParentStyleSheet?.Attachment.OwnerNode;
                var eligible = elements.Count(element => owner is null
                    ? element.NamespaceUri == Namespaces.Html
                    : ReferenceEquals(owner.TreeShadowRoot, element.TreeShadowRoot));
                detail.Attempts.Should().BeLessThanOrEqualTo(eligible, "each eligible rule/receiver pair is matched at most once");
            }
            var attempts = record.RuleAttempts;
            var publications = record.ComputedPublications.ToArray();
            foreach (var element in elements)
            {
                traversal.Of(element).GetPropertyValue("display");
                traversal.Of(element).GetPropertyValue("visibility");
            }
            record.RuleAttempts.Should().Be(attempts);
            record.ComputedPublications.ToArray().Should().BeEquivalentTo(publications);
            var baseline = new NativeCssQueryDiagnostics();
            foreach (var element in elements)
            {
                var actual = CssCascade.Traversal.For(document, diagnostics: baseline)!.Of(element);
                actual.GetPropertyValue("display");
                actual.GetPropertyValue("visibility");
            }
            return (Elements: elements.Length, Shared: attempts, Independent: baseline.Queries.Sum(query => query.RuleAttempts));
        });
        counts.Independent.Should().BeGreaterThan(counts.Shared);
        TestContext.Out.WriteLine($"Admin form: {counts.Elements} elements; {counts.Independent} independent-query attempts, {counts.Shared} shared-query attempts.");
    }

    [Test]
    public void EachLayoutBuildsOneQueryAndDoesNotKeepItForTheNextOperation()
    {
        using var fixture = Create("<div><div><div><button>Save</button></div></div></div>");
        var document = fixture.Document;
        var diagnostics = new NativeCssQueryDiagnostics();
        var visibility = new ElementVisibility(useComputedStyle: true, diagnostics: diagnostics);
        FlatLayout.Of(document, visibility, 1280, 720, 0).Count.Should().Be(6);
        diagnostics.Queries.Count.Should().Be(1);
        ContentDom.First(document, "button")!.SetAttribute("hidden", "");
        FlatLayout.Of(document, visibility, 1280, 720, 0).Count.Should().Be(5);
        diagnostics.Queries.Count.Should().Be(2);
        diagnostics.Queries[1].Should().NotBeSameAs(diagnostics.Queries[0]);
    }

    [TestCase(8)]
    [TestCase(32)]
    public void SelectorsAreMatchedOncePerElementRatherThanOncePerAncestorPerElement(int depth)
    {
        using var fixture = Create(
            "<style>:root{--colour:red;--bs-heading-color:inherit} div{color:var(--colour)}</style>"
            + string.Concat(Enumerable.Repeat("<div>", depth)) + "<button>Save</button>"
            + string.Concat(Enumerable.Repeat("</div>", depth)));
        var document = fixture.Document;
        var diagnostics = new NativeCssQueryDiagnostics(captureDetails: true);
        var traversal = CssCascade.Traversal.For(document, diagnostics: diagnostics)!;
        var elements = ContentDom.Descendants(document).ToArray();
        foreach (var element in elements)
        {
            var actual = traversal.Of(element);
            actual.GetPropertyValue("display").Should().NotBeEmpty();
            actual.GetPropertyValue("visibility").Should().Be("visible");
            if (element.LocalName is "div" or "button") actual.GetPropertyValue("color").Should().Be("red");
            actual.GetPropertyValue("--bs-heading-color").Should().Be("inherit");
        }
        var record = diagnostics.Queries.Single();
        record.StatePublications.Should().Be(elements.Length);
        record.Elements!.Values.Should().OnlyContain(detail => detail.StatePublications == 1);
        var eligibleRules = NativeCssBrowserDefaults.Sheet(document, new CssValueWork(default)).Sheet
            .ApplicableStyleRules(new CssMediaEnvironment(), new CssValueWork(default)).Length
            + NativeCssStyleSheets.Get(document, new CssValueWork(default)).Single().Sheet
                .ApplicableStyleRules(new CssMediaEnvironment(), new CssValueWork(default)).Length;
        record.Rules!.Values.Should().OnlyContain(detail => detail.Attempts <= elements.Length);
        var attempts = record.RuleAttempts;
        attempts.Should().BeLessThan((long) elements.Length * eligibleRules, "the rule index skips rules keyed to other names");
        var publications = record.ComputedPublications.ToArray();
        foreach (var element in elements) traversal.Of(element).GetPropertyValue("visibility");
        record.RuleAttempts.Should().Be(attempts);
        record.ComputedPublications.ToArray().Should().BeEquivalentTo(publications);
        var baseline = new NativeCssQueryDiagnostics();
        foreach (var element in elements)
            CssCascade.Traversal.For(document, diagnostics: baseline)!.Of(element).GetPropertyValue("display");
        var independentStates = baseline.Queries.Sum(query => query.StatePublications);
        independentStates.Should().Be(elements.Sum(element => 1 + Ancestors(element).Count()));
        independentStates.Should().BeGreaterThan(elements.Length * 3);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReusingRawParentDeclarationsPreservesComputedInheritance(bool inheritParentWidth)
    {
        using var fixture = Create(
            """
            <style>
              :root { --colour: red; --extent: 10px; --heading-colour: inherit; color: green }
              body { visibility: hidden; width: 40px }
              .outer { --colour: blue; color: var(--colour); width: var(--extent) }
              .inner { --extent: 20px; visibility: visible; color: inherit }
              .inner > span { width: inherit; display: inline !important }
              span { display: none; color: purple }
              @media (min-width: 1px) { button { display: block } }
            </style>
            <div class="outer"><div class="inner"><span style="color:orange">text</span><button>Save</button></div></div>
            <p style="visibility:visible">sibling</p>
            """);
        var document = fixture.Document;
        var inner = Select(document, ".inner").Single();
        if (inheritParentWidth) inner.SetAttribute("style", "width:inherit");
        var traversal = CssCascade.Traversal.For(document)!;
        // CSS Cascade: inherited computed widths are never re-substituted in the child's variable scope.
        foreach (var element in Select(document, ".outer, .inner, span, button, p").Reverse())
        {
            var actual = traversal.Of(element);
            var outer = ContentDom.ClassNames(element).Contains("outer");
            var span = element.LocalName == "span";
            var paragraph = element.LocalName == "p";
            actual.GetPropertyValue("width").Should().Be(outer || inheritParentWidth && (ReferenceEquals(element, inner) || span) ? "10px" : "auto");
            actual.GetPropertyValue("display").Should().Be(span ? "inline" : "block");
            actual.GetPropertyValue("visibility").Should().Be(outer ? "hidden" : "visible");
            actual.GetPropertyValue("color").Should().Be(span ? "orange" : paragraph ? "green" : "blue");
            actual.GetPropertyValue("--extent").Should().Be(outer || paragraph ? "10px" : "20px");
            actual.GetPropertyValue("--heading-colour").Should().Be("inherit");
            if (span) actual.GetPropertyPriority("display").Should().BeEmpty();
        }
        var leaf = ContentDom.First(document, "span")!;
        CssCascade.Of(leaf)!.GetPropertyValue("width").Should().Be(inheritParentWidth ? "10px" : "auto");
    }

    [TestCase(":root", "font-size", "16px", "16px")]
    [TestCase(":root", "text-align", "inherit", "start")]
    [TestCase(".outer", "font-size", "2em", "2em")]
    [TestCase(".inner", "font-size", "1.5em", "1.5em")]
    public void AuthoredTypographyResolvesAgainstItsComputedParent(string selector, string property, string value, string expected)
    {
        using var fixture = Create("<style>" + selector + " { " + property + ":" + value + " }</style><div class='outer'><div class='inner'></div></div>");
        CssCascade.Traversal.For(fixture.Document)!.Of(Select(fixture.Document, selector).Single()).GetPropertyValue(property)
            .Should().Be(expected);
    }

    [TestCase(10)]
    [TestCase(2000)]
    public void ResizeMeasurementsOnlyMatchObservedSubtreesAndTheirAncestors(int unrelatedRows)
    {
        using var fixture = Create(
            "<style>:root{"
            + string.Concat(Enumerable.Range(0, 128).Select(i => $"--theme-{i}:red;"))
            + "}</style><aside id='sidebar'><div id='target'><span id='leaf'>Files</span>"
            + "<div hidden><b id='hidden'>Hidden</b></div><script>ignored()</script></div></aside><main>"
            + string.Concat(Enumerable.Repeat("<section><a>Enable</a></section>", unrelatedRows))
            + "</main>");
        var document = fixture.Document;
        var visibility = new ElementVisibility(useComputedStyle: true);
        var diagnostics = new NativeCssQueryDiagnostics();
        var sizes = new FlatLayout.SizeQuery(document, visibility, 1280, CssCascade.Traversal.For(document, diagnostics: diagnostics)!);
        var leaf = ContentDom.ElementById(document, "leaf")!;
        var target = ContentDom.ElementById(document, "target")!;
        var sidebar = ContentDom.ElementById(document, "sidebar")!;

        sizes.Width(sidebar).Should().Be(1280);
        diagnostics.Queries.Single().StatePublications.Should().Be(3, "a width query needs only html, body and the sidebar, not its descendants");
        sizes.Measure(leaf).Should().Be(new FlatBox(0, 0, 1280, 16));
        sizes.Measure(sidebar).Should().Be(new FlatBox(0, 0, 1280, 48));
        sizes.Measure(target).Should().Be(new FlatBox(0, 0, 1280, 32));
        sizes.Measure(ContentDom.ElementById(document, "hidden")!).Should().Be(FlatBox.Empty);
        sizes.Measure(document.CreateElement("div")).Should().Be(FlatBox.Empty);
        sizes.Measure(sidebar).Height.Should().Be(48);
        diagnostics.Queries.Single().StatePublications.Should().Be(5, "only html, body, the sidebar and its two rendered descendants need the cascade");

        var layout = FlatLayout.Of(document, visibility, 1280, 720, 96);
        foreach (var element in new[] { leaf, target, sidebar })
        {
            var expected = layout.ClientBoxOf(element)!.Value;
            sizes.Measure(element).Should().Be(new FlatBox(0, 0, expected.Width, expected.Height));
        }
    }

    [Test]
    public void VisibilityQueriesKeepNativeCascadeAndVariablesWithoutComputingPaint()
    {
        using var fixture = Create(
            """
            <style>
              :root { --shown: block; --hidden: none; --bad: var(--bad) }
              .parent { display: var(--shown); visibility: hidden; width: 20ch }
              .parent > span { display: inherit; visibility: var(--bad); color: red }
              #visible { display: var(--hidden); visibility: visible }
              .parent > #visible { display: var(--shown) !important }
              .paint-only { color: red; width: 20ch }
            </style>
            <div class="parent"><span id="inherited"></span><span id="visible" class="paint-only"></span></div>
            """);
        var document = fixture.Document;
        var diagnostics = new NativeCssQueryDiagnostics();
        var traversal = CssCascade.Traversal.For(document, CssCascade.StyleScope.Visibility, diagnostics)!;
        var inherited = traversal.Of(ContentDom.ElementById(document, "inherited")!);
        inherited.GetPropertyValue("display").Should().Be("block");
        inherited.GetPropertyValue("visibility").Should().Be("hidden");
        var visible = traversal.Of(ContentDom.ElementById(document, "visible")!);
        visible.GetPropertyValue("display").Should().Be("block");
        visible.GetPropertyValue("visibility").Should().Be("visible");
        var record = diagnostics.Queries.Single();
        record.ComputedPublications.GetValueOrDefault("width").Should().Be(0);
        record.ComputedPublications.GetValueOrDefault("color").Should().Be(0);
        var attempts = record.RuleAttempts;
        inherited.GetPropertyValue("width").Should().Be("auto");
        inherited.GetPropertyValue("color").Should().Be("red");
        visible.GetPropertyValue("color").Should().Be("red");
        visible.GetPropertyValue("width").Should().Be("20ch");
        record.RuleAttempts.Should().Be(attempts, "demanding paint must reuse the same matched states");
    }

    [Test]
    public void InvalidInheritedConsumersUseTheParentRatherThanTheNativeInitialFallback()
    {
        using var fixture = Create(
            """
            <style>
              #parent { --a:var(--a); color:green; visibility:hidden }
              #child { color:var(--a) !important; visibility:var(--a) }
            </style>
            <div id="parent"><span id="child">text</span></div>
            """);
        var document = fixture.Document;
        var element = ContentDom.ElementById(document, "child")!;
        var computed = CssCascade.Traversal.For(document)!.Of(element);

        computed.Should().NotBeNull();
        computed!.GetPropertyValue("color").Should().Be("green");
        computed.GetPropertyValue("visibility").Should().Be("hidden");
        computed.GetPropertyPriority("color").Should().BeEmpty();
        var sheet = NativeCssStyleSheets.Get(document, new CssValueWork(default)).Single().Sheet;
        sheet.ApplicableStyleRules(new CssMediaEnvironment(), new CssValueWork(default))
            .Last().Style.GetPropertyPriority("color").Should().Be("important");
    }

    [Test]
    public void AccessibilitySharesVisibilityWithinOneSnapshotOnly()
    {
        using var fixture = Create("<style>:root{--show:block} button{display:var(--show)}</style><main><button><span>Save</span></button></main>");
        var document = fixture.Document;
        var diagnostics = new NativeCssQueryDiagnostics();
        var first = AccessibilityTree.Build(document, AccessibilityOptions.Snapshot, diagnostics: diagnostics);
        diagnostics.Queries.Count.Should().Be(1);
        AccessibilitySnapshot.Render(first).Should().Contain("Save");
        document.DocumentElement!.SetAttribute("style", "--show:none");
        var second = AccessibilityTree.Build(document, AccessibilityOptions.Snapshot, diagnostics: diagnostics);
        diagnostics.Queries.Count.Should().Be(2);
        diagnostics.Queries[1].Should().NotBeSameAs(diagnostics.Queries[0]);
        AccessibilitySnapshot.Render(second).Should().NotContain("Save");
    }

    [Test]
    public void LayoutScopePreservesFlexValuesAndVariableInheritance()
    {
        using var fixture = Create(
            """
            <style>
              :root { --basis: 24px; --grow: 2; --flow: row-reverse; --align: center }
              main { display: flex; flex-direction: var(--flow); align-items: var(--align); direction: rtl }
              div { flex: var(--grow) 1 var(--basis); width: 48px; visibility: inherit }
              .override { --basis: 32px; align-self: flex-end; flex-grow: 3 !important }
            </style>
            <main><div></div><div class="override"><span></span></div></main>
            """);
        var document = fixture.Document;
        var complete = CssCascade.Traversal.For(document)!;
        var layout = CssCascade.Traversal.For(document, CssCascade.StyleScope.Layout)!;
        foreach (var element in ContentDom.Descendants(document).Reverse())
        {
            var expected = complete.Of(element)!;
            var actual = layout.Of(element)!;
            actual.Should().NotBeNull();
            foreach (var name in new[] { "display", "visibility", "flex-direction", "flex-wrap", "direction",
                         "align-self", "align-items", "flex-basis", "width", "flex-grow", "flex-shrink" })
            {
                actual.GetPropertyValue(name).Should().Be(expected.GetPropertyValue(name),
                    "{0} on {1} must retain the complete cascade's answer", name, element.LocalName);
                var isMain = element.LocalName == "main";
                var isDiv = element.LocalName == "div";
                var isOverride = ContentDom.ClassNames(element).Contains("override");
                var literal = name switch
                {
                    "display" => isMain ? "flex" : element.LocalName is "head" or "style" ? "none" : element.LocalName == "span" ? "inline" : "block",
                    "visibility" => "visible",
                    "flex-direction" => isMain ? "row-reverse" : "row",
                    "flex-wrap" => "nowrap",
                    "direction" => isMain || isDiv || element.LocalName == "span" ? "rtl" : "ltr",
                    "align-self" => isOverride ? "flex-end" : "auto",
                    "align-items" => isMain ? "center" : "normal",
                    "flex-basis" => isDiv ? isOverride ? "32px" : "24px" : "auto",
                    "width" => isDiv ? "48px" : "auto",
                    "flex-grow" => isDiv ? isOverride ? "3" : "2" : "0",
                    "flex-shrink" => "1",
                    _ => throw new InvalidOperationException("Unasserted layout property: " + name)
                };
                actual.GetPropertyValue(name).Should().Be(literal, "{0} on {1}", name, element.LocalName);
            }
        }
    }

    private static DomTestFixture Create(string html) => DomTestFixture.Create(html);

    private static IReadOnlyList<Element> Select(Document document, string selector) =>
        DomSelectors.QuerySelectorAll(NativeCssStyleSheets.RealmOf(document)!, document, selector);

    private static IEnumerable<Element> Ancestors(Element element)
    {
        for (var parent = element.ParentNode; parent is not null; parent = parent.ParentNode)
            if (parent is Element ancestor) yield return ancestor;
    }
}
