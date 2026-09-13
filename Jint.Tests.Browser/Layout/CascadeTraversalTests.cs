using System.Collections;
using AngleSharp;
using AngleSharp.Css;
using AngleSharp.Css.Dom;
using AngleSharp.Css.RenderTree;
using AngleSharp.Css.Values;
using AngleSharp.Dom;
using Jint.Browser.Accessibility;
using Jint.Browser.Dom.Views;
using Jint.Browser.Layout;
using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.Layout;

public sealed class CascadeTraversalTests
{
    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(false, 192)]
    [TestCase(true, 192)]
    public async Task EquivalentSiblingsComputeTheirLiteralDeclarationsOncePerQuery(bool layout, int precedingStyles)
    {
        using var context = BrowsingContext.New(Configuration.Default.WithCss());
        using var document = await context.OpenAsync(response => response.Content(
            "<style>.item { display:block }</style><main>"
            + string.Concat(Enumerable.Range(1, precedingStyles)
                .Select(width => $"<i style='display:block;width:{width}px' data-width='{width}'></i>"))
            + string.Concat(Enumerable.Repeat("<div class='item'></div>", 64)) + "</main>"));
        var sheet = (ICssStyleSheet) document.StyleSheets.Single();
        var rule = new CountingRule((ICssStyleRule) sheet.Rules[0]);
        var styles = new RuleStyles(new DefaultRenderDevice(), rule);
        var scope = layout ? CssCascade.StyleScope.Layout : CssCascade.StyleScope.Visibility;

        for (var query = 1; query <= 2; query++)
        {
            var traversal = new CssCascade.Traversal(styles, scope);
            foreach (var element in document.QuerySelectorAll("main > *"))
            {
                var computed = traversal.Of(element)!;
                computed.GetPropertyValue("display").Should().Be("block");
                if (layout && element.GetAttribute("data-width") is { } width)
                {
                    computed.GetPropertyValue("width").Should().Be(width + "px");
                }
            }
            rule.Computations.Should().Be(query,
                "equivalent siblings must not repeat native declaration computation, but a new query must recompute");
            rule.Matches.Should().Be(query * 64, "each element must still run the native selector matcher");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SharedDeclarationsPreserveSpecificityInlineStylesAndInheritance(bool layout)
    {
        using var context = BrowsingContext.New(Configuration.Default.WithCss());
        using var document = await context.OpenAsync(response => response.Content(
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
            """));
        var styles = document.DefaultView!.GetStyleCollection(new DefaultRenderDevice());
        var scope = layout ? CssCascade.StyleScope.Layout : CssCascade.StyleScope.Visibility;
        var traversal = new CssCascade.Traversal(styles, scope);
        var complete = new CssCascade.Traversal(styles);
        foreach (var element in document.All)
        {
            var actual = traversal.Of(element)!;
            var expected = complete.Of(element)!;
            foreach (var property in new[] { "display", "visibility" })
            {
                actual.GetPropertyValue(property).Should().Be(expected.GetPropertyValue(property), element.OuterHtml);
                actual.GetPropertyPriority(property).Should().Be(expected.GetPropertyPriority(property));
            }
        }
        document.QuerySelectorAll(".item").Select(element => traversal.Of(element)!.GetPropertyValue("display"))
            .Should().Equal("none", "block", "inline", "flex");
        document.QuerySelectorAll(".variable").Select(element => traversal.Of(element)!.GetPropertyValue("display"))
            .Should().Equal("block", "block", "none", "none");
        document.QuerySelectorAll(".inherited").Select(element => traversal.Of(element)!.GetPropertyValue("display"))
            .Should().Equal("flex", "none");
    }

    [Test]
    public async Task SharedClassCandidatesStillMatchEachElementsAttributesAndAncestors()
    {
        using var context = BrowsingContext.New(Configuration.Default.WithCss());
        using var document = await context.OpenAsync(response => response.Content(
            "<style>.item { display:block } .item[data-hide], .hidden > .item { display:none }</style>"
            + "<main><div class='item'></div><div class='item' data-hide></div></main>"
            + "<main class='hidden'><div class='item'></div></main>"));
        var styles = document.DefaultView!.GetStyleCollection(new DefaultRenderDevice());
        var traversal = new CssCascade.Traversal(styles, CssCascade.StyleScope.Visibility);

        document.QuerySelectorAll(".item").Select(element => traversal.Of(element)!.GetPropertyValue("display"))
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
    public async Task ClassCandidatesPreserveNativeSelectorMatching(string selector)
    {
        using var context = BrowsingContext.New(Configuration.Default.WithCss());
        using var document = await context.OpenAsync(response => response.Content(
            $"<style>{selector} {{ display:none }} .b {{ display:flex }} {selector} {{ visibility:hidden }}</style>"
            + "<main class='ancestor'><div class='a' data-x><span class='b'></span></div>"
            + "<div class='a b'></div><div class='123'></div><div class='b'></div></main>"));
        var styles = document.DefaultView!.GetStyleCollection(new DefaultRenderDevice());
        var complete = new CssCascade.Traversal(styles);
        var scoped = new CssCascade.Traversal(styles, CssCascade.StyleScope.Visibility);
        foreach (var element in document.All)
        {
            var expected = complete.Of(element)!;
            var actual = scoped.Of(element)!;
            actual.GetPropertyValue("display").Should().Be(expected.GetPropertyValue("display"));
            actual.GetPropertyValue("visibility").Should().Be(expected.GetPropertyValue("visibility"));
            var native = element.ComputeCurrentStyle();
            CssCascade.Of(element, resolveInheritance: false)!.Select(property => (property.Name, property.Value))
                .Should().Equal(native.Select(property => (property.Name, property.Value)));
        }
    }

    [Test]
    public async Task ScopedCascadeKeepsNestedRulesUnderEmptyParents()
    {
        using var context = BrowsingContext.New(Configuration.Default.WithCss());
        using var document = await context.OpenAsync(response => response.Content(
            "<style>main { & > button { display:none; color:red } }</style>"
            + "<main><button>hidden</button></main>"));
        var styles = document.DefaultView!.GetStyleCollection(new DefaultRenderDevice());
        var button = document.QuerySelector("button")!;
        new CssCascade.Traversal(styles).Of(button)!.GetPropertyValue("display").Should().Be("none");
        foreach (var scope in new[] { CssCascade.StyleScope.Visibility, CssCascade.StyleScope.Layout })
        {
            new CssCascade.Traversal(styles, scope).Of(button)!.GetPropertyValue("display").Should().Be("none");
        }
    }

    [TestCase("block")]
    [TestCase("flex")]
    public async Task BoundedHeightPreservesExactMeasurements(string display)
    {
        using var context = BrowsingContext.New(Configuration.Default.WithCss());
        using var document = await context.OpenAsync(response => response.Content(
            $"<main style='display:{display};flex-direction:row-reverse'>"
            + "<div><button>one</button><button>two</button></div><div hidden>hidden</div>"
            + "<div><div><button>three</button></div></div></main>"));
        var visibility = new ElementVisibility(useComputedStyle: true);
        var root = document.DocumentElement!;
        var full = FlatLayout.Of(document, visibility, 1280, 32, 0);
        var expected = full.DocumentBoxOf(root)!.Value.Height;
        foreach (var bound in new[] { 1d, 16, 33, expected - 1, expected, expected + 16 })
        {
            var sizes = new FlatLayout.SizeQuery(document, visibility, 1280, visibility.CreateTraversal(document));
            sizes.HeightUpTo(root, bound).Should().Be(Math.Min(expected, Math.Ceiling(bound / 16) * 16));
            foreach (var element in document.All)
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
            var styles = new CountingStyles(document.DefaultView!.GetStyleCollection(document.Context.GetService<IRenderDevice>()!));
            var traversal = new CssCascade.Traversal(styles);
            var elements = document.All.ToArray();
            foreach (var element in elements)
            {
                traversal.Of(element).Should().NotBeNull("the cascade for {0} must answer", element.LocalName);
            }

            var scopedMatches = styles.Matches;
            styles.Matches = 0;
            foreach (var element in elements)
            {
                styles.ComputeDeclarations(element);
            }

            return (Elements: elements.Length, Scoped: scopedMatches, Legacy: styles.Matches);
        });
        counts.Scoped.Should().Be(counts.Elements);
        counts.Legacy.Should().BeGreaterThan(counts.Scoped * 8);
        TestContext.Out.WriteLine($"Admin form: {counts.Elements} elements; {counts.Legacy} legacy selector passes, {counts.Scoped} scoped passes.");
    }

    [Test]
    public async Task EachLayoutBuildsOneStyleCollectionAndDoesNotKeepItForTheNextQuery()
    {
        using var defaults = BrowsingContext.New(Configuration.Default.WithCss());
        var provider = new CountingProvider(defaults.GetService<ICssDefaultStyleSheetProvider>()!);
        using var context = BrowsingContext.New(Configuration.Default.WithCss().With(provider));
        using var document = await context.OpenAsync(response => response.Content(
            "<div><div><div><button>Save</button></div></div></div>"));
        var visibility = new ElementVisibility(useComputedStyle: true);
        provider.Reads = 0;

        FlatLayout.Of(document, visibility, 1280, 720, 0).Count.Should().Be(6);
        provider.Reads.Should().Be(1, "all elements in a layout share its style collection");
        document.QuerySelector("button")!.SetAttribute("hidden", "");
        FlatLayout.Of(document, visibility, 1280, 720, 0).Count.Should().Be(5);
        provider.Reads.Should().Be(2, "the next query must read the sheets again");
    }

    [TestCase(8)]
    [TestCase(32)]
    public async Task SelectorsAreMatchedOncePerElementRatherThanOncePerAncestorPerElement(int depth)
    {
        using var context = BrowsingContext.New(Configuration.Default.WithCss());
        // Bootstrap declares this inherited custom-property token at the root; it must not send every
        // descendant back through the explicit-inherit compatibility fallback.
        using var document = await context.OpenAsync(response => response.Content(
            "<style>:root{--colour:red;--bs-heading-color:inherit} div{color:var(--colour)}</style>"
            + string.Concat(Enumerable.Repeat("<div>", depth)) + "<button>Save</button>"
            + string.Concat(Enumerable.Repeat("</div>", depth))));
        var styles = new CountingStyles(document.DefaultView!.GetStyleCollection(new DefaultRenderDevice()));
        var traversal = new CssCascade.Traversal(styles);
        var elements = document.All.ToArray();

        foreach (var element in elements)
        {
            traversal.Of(element).Should().NotBeNull();
        }

        styles.Matches.Should().Be(elements.Length);

        // The old per-element API rematches every ancestor. Count work, not elapsed time.
        styles.Matches = 0;
        foreach (var element in elements)
        {
            styles.ComputeDeclarations(element);
        }

        styles.Matches.Should().Be(elements.Sum(element => 1 + element.GetAncestors().OfType<IElement>().Count()));
        styles.Matches.Should().BeGreaterThan(elements.Length * 3);
    }

    [Test]
    public async Task ReusingRawParentDeclarationsPreservesTheExistingComputedCascade()
    {
        using var context = BrowsingContext.New(Configuration.Default.WithCss());
        using var document = await context.OpenAsync(response => response.Content(
            """
            <style>
              :root { --colour: red; --extent: 10px; --heading-colour: inherit; color: green; font-size: 16px; text-align: inherit }
              body { visibility: hidden; width: 40px }
              .outer { --colour: blue; color: var(--colour); width: var(--extent); font-size: 2em }
              .inner { --extent: 20px; visibility: visible; color: inherit; font-size: 1.5em }
              .inner > span { width: inherit; display: inline !important }
              span { display: none; color: purple }
              @media (min-width: 1px) { button { display: block } }
            </style>
            <div class="outer"><div class="inner"><span style="color:orange">text</span><button>Save</button></div></div>
            <p style="visibility:visible">sibling</p>
            """));
        var styles = document.DefaultView!.GetStyleCollection(new DefaultRenderDevice());
        var traversal = new CssCascade.Traversal(styles);

        // Start with a leaf too: callers need not have visited every ancestor first.
        foreach (var element in document.All.Reverse())
        {
            var expected = styles.ComputeDeclarations(element);
            var actual = traversal.Of(element);
            actual.Should().NotBeNull();
            var explicitInherit = element.LocalName == "span";
            if (explicitInherit)
            {
                // 1.1.0 leaves this width unresolved instead of walking past the undeclared parent.
                // Keep the existing compatibility answer, including its child-relative var() value.
                expected.GetPropertyValue("width").Should().Be("inherit");
                actual!.GetPropertyValue("width").Should().Be("20px");
                CssCascade.Of(element)!.GetPropertyValue("width").Should().Be("20px");
            }

            actual!.Where(property => !property.Name.StartsWith("--", StringComparison.Ordinal)
                    && !(explicitInherit && property.Name == "width"))
                .Select(property => (property.Name, property.Value, property.IsImportant))
                .Should().BeEquivalentTo(expected.Where(property => !property.Name.StartsWith("--", StringComparison.Ordinal)
                        && !(explicitInherit && property.Name == "width"))
                    .Select(property => (property.Name, property.Value, property.IsImportant)));
        }
    }

    [TestCase(10)]
    [TestCase(2000)]
    public async Task ResizeMeasurementsOnlyMatchObservedSubtreesAndTheirAncestors(int unrelatedRows)
    {
        using var context = BrowsingContext.New(Configuration.Default.WithCss());
        using var document = await context.OpenAsync(response => response.Content(
            "<style>:root{"
            + string.Concat(Enumerable.Range(0, 128).Select(i => $"--theme-{i}:red;"))
            + "}</style><aside id='sidebar'><div id='target'><span id='leaf'>Files</span>"
            + "<div hidden><b id='hidden'>Hidden</b></div><script>ignored()</script></div></aside><main>"
            + string.Concat(Enumerable.Repeat("<section><a>Enable</a></section>", unrelatedRows))
            + "</main>"));
        var visibility = new ElementVisibility(useComputedStyle: true);
        var styles = new CountingStyles(document.DefaultView!.GetStyleCollection(new DefaultRenderDevice()));
        var sizes = new FlatLayout.SizeQuery(document, visibility, 1280, new CssCascade.Traversal(styles));
        var leaf = document.GetElementById("leaf")!;
        var target = document.GetElementById("target")!;
        var sidebar = document.GetElementById("sidebar")!;

        sizes.Width(sidebar).Should().Be(1280);
        styles.Matches.Should().Be(3, "a width query needs only html, body and the sidebar, not its descendants");
        sizes.Measure(leaf).Should().Be(new FlatBox(0, 0, 1280, 16));
        sizes.Measure(sidebar).Should().Be(new FlatBox(0, 0, 1280, 48));
        sizes.Measure(target).Should().Be(new FlatBox(0, 0, 1280, 32));
        sizes.Measure(document.GetElementById("hidden")!).Should().Be(FlatBox.Empty);
        sizes.Measure(document.CreateElement("div")).Should().Be(FlatBox.Empty);
        sizes.Measure(sidebar).Height.Should().Be(48);
        styles.Matches.Should().Be(5, "only html, body, the sidebar and its two rendered descendants need the cascade");

        var layout = FlatLayout.Of(document, visibility, 1280, 720, 96);
        foreach (var element in new[] { leaf, target, sidebar })
        {
            var expected = layout.ClientBoxOf(element)!.Value;
            sizes.Measure(element).Should().Be(new FlatBox(0, 0, expected.Width, expected.Height));
        }
    }

    [Test]
    public async Task VisibilityQueriesKeepNativeCascadeAndVariablesWithoutComputingPaint()
    {
        using var context = BrowsingContext.New(Configuration.Default.WithCss());
        using var document = await context.OpenAsync(response => response.Content(
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
            """));
        var styles = new CountingStyles(document.DefaultView!.GetStyleCollection(new DefaultRenderDevice()));
        var traversal = new CssCascade.Traversal(styles, scope: CssCascade.StyleScope.Visibility);

        var inherited = traversal.Of(document.GetElementById("inherited")!)!;
        inherited.GetPropertyValue("display").Should().Be("block");
        inherited.GetPropertyValue("visibility").Should().Be("hidden");
        inherited.GetPropertyValue("width").Should().BeEmpty();
        inherited.GetPropertyValue("color").Should().BeEmpty();
        var visible = traversal.Of(document.GetElementById("visible")!)!;
        visible.GetPropertyValue("display").Should().Be("block");
        visible.GetPropertyValue("visibility").Should().Be("visible");
        styles.Matches.Should().Be(2, "the literal and variable-dependent cascades each filter active rules once, not once per element");
    }

    [Test]
    public async Task InvalidInheritedConsumersUseTheParentRatherThanTheNativeInitialFallback()
    {
        using var context = BrowsingContext.New(Configuration.Default.WithCss());
        using var document = await context.OpenAsync(response => response.Content(
            """
            <style>
              #parent { --a:var(--a); color:green; visibility:hidden }
              #child { color:var(--a) !important; visibility:var(--a) }
            </style>
            <div id="parent"><span id="child">text</span></div>
            """));
        var element = document.GetElementById("child")!;
        var styles = document.DefaultView!.GetStyleCollection(new DefaultRenderDevice());
        var native = styles.ComputeDeclarations(element);
        var computed = new CssCascade.Traversal(styles).Of(element);

        computed.Should().NotBeNull();
        computed!.GetPropertyValue("color").Should().Be("rgba(0, 128, 0, 1)");
        computed.GetPropertyValue("visibility").Should().Be("hidden");
        computed.GetPropertyPriority("color").Should().Be("important");
        computed.GetPropertyValue("color").Should().Be(native.GetPropertyValue("color"));
        computed.GetPropertyValue("visibility").Should().Be(native.GetPropertyValue("visibility"));
    }

    [Test]
    public async Task AccessibilitySharesVisibilityWithinOneSnapshotOnly()
    {
        using var defaults = BrowsingContext.New(Configuration.Default.WithCss());
        var provider = new CountingProvider(defaults.GetService<ICssDefaultStyleSheetProvider>()!);
        using var context = BrowsingContext.New(Configuration.Default.WithCss().With(provider));
        using var document = await context.OpenAsync(response => response.Content(
            "<style>:root{--show:block} button{display:var(--show)}</style>"
            + "<main><button><span>Save</span></button></main>"));
        provider.Reads = 0;

        var first = AccessibilityTree.Build(document, AccessibilityOptions.Snapshot);
        provider.Reads.Should().Be(1, "visibility and accessible names share the snapshot's style collection");
        AccessibilitySnapshot.Render(first).Should().Contain("Save");
        document.DocumentElement!.SetAttribute("style", "--show:none");
        var second = AccessibilityTree.Build(document, AccessibilityOptions.Snapshot);
        provider.Reads.Should().Be(2, "a later snapshot must observe same-turn style changes");
        AccessibilitySnapshot.Render(second).Should().NotContain("Save");
    }

    [Test]
    public async Task LayoutScopePreservesFlexValuesAndVariableInheritance()
    {
        using var context = BrowsingContext.New(Configuration.Default.WithCss());
        using var document = await context.OpenAsync(response => response.Content(
            """
            <style>
              :root { --basis: 24px; --grow: 2; --flow: row-reverse; --align: center }
              main { display: flex; flex-direction: var(--flow); align-items: var(--align); direction: rtl }
              div { flex: var(--grow) 1 var(--basis); width: 48px; visibility: inherit }
              .override { --basis: 32px; align-self: flex-end; flex-grow: 3 !important }
            </style>
            <main><div></div><div class="override"><span></span></div></main>
            """));
        var styles = document.DefaultView!.GetStyleCollection(new DefaultRenderDevice());
        var complete = new CssCascade.Traversal(styles);
        var layout = new CssCascade.Traversal(styles, CssCascade.StyleScope.Layout);
        foreach (var element in document.All.Reverse())
        {
            var expected = complete.Of(element)!;
            var actual = layout.Of(element)!;
            actual.Should().NotBeNull();
            foreach (var name in new[] { "display", "visibility", "flex-direction", "flex-wrap", "direction",
                         "align-self", "align-items", "flex-basis", "width", "flex-grow", "flex-shrink" })
            {
                actual.GetPropertyValue(name).Should().Be(expected.GetPropertyValue(name),
                    "{0} on {1} must retain the complete cascade's answer", name, element.LocalName);
            }
        }
    }

    private sealed class RuleStyles(IRenderDevice device, params ICssStyleRule[] rules) : IStyleCollection
    {
        public IRenderDevice Device => device;
        public IEnumerator<ICssStyleRule> GetEnumerator() => ((IEnumerable<ICssStyleRule>) rules).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CountingRule : ICssStyleRule
    {
        private readonly ICssStyleRule _source;
        internal int Matches { get; private set; }
        internal int Computations { get; private set; }

        internal CountingRule(ICssStyleRule source)
        {
            _source = source;
            Style = new CountingDeclaration(source.Style,
                source.Style.Select(property => new CountingProperty(property, () => Computations++)).ToArray());
        }

        public ICssStyleDeclaration Style { get; }
        public bool TryMatch(IElement element, IElement? scope, out Priority specificity)
        {
            var matched = _source.TryMatch(element, scope, out specificity);
            if (matched)
            {
                Matches++;
            }
            return matched;
        }
        public string SelectorText { get => _source.SelectorText; set => throw new NotSupportedException(); }
        public ISelector Selector => _source.Selector;
        public ICssRuleList Rules => _source.Rules;
        public CssRuleType Type => _source.Type;
        public string CssText { get => _source.CssText; set => throw new NotSupportedException(); }
        public ICssRule Parent => _source.Parent;
        public ICssStyleSheet Owner => _source.Owner;
        public void SetParent(ICssRule rule) => throw new NotSupportedException();
        public void SetOwner(ICssStyleSheet sheet) => throw new NotSupportedException();
        public void ToCss(TextWriter writer, IStyleFormatter formatter) => _source.ToCss(writer, formatter);
    }

    private sealed class CountingProperty(ICssProperty source, Action compute) : ICssProperty
    {
        public bool CanBeInherited => source.CanBeInherited;
        public bool IsAnimatable => source.IsAnimatable;
        public bool IsImportant { get => source.IsImportant; set => source.IsImportant = value; }
        public bool IsInherited => source.IsInherited;
        public bool IsInitial => source.IsInitial;
        public bool IsShorthand => source.IsShorthand;
        public string Name => source.Name;
        public ICssValue? RawValue => source.RawValue;
        public string Value { get => source.Value; set => source.Value = value; }
        public ICssProperty Compute(ICssComputeContext context)
        {
            compute();
            return source.Compute(context);
        }
        public void ToCss(TextWriter writer, IStyleFormatter formatter) => source.ToCss(writer, formatter);
    }

    private sealed class CountingDeclaration(ICssStyleDeclaration source, ICssProperty[] properties) : ICssStyleDeclaration
    {
        public string this[int index] => source[index];
        public string this[string name] => source[name];
        public int Length => source.Length;
        public ICssRule? Parent => source.Parent;
        public event Action<string>? Changed { add { } remove { } }
        public string CssText { get => source.CssText; set => throw new NotSupportedException(); }
        public ICssProperty GetProperty(string name) => properties.FirstOrDefault(property => property.Name == name)!;
        public string GetPropertyValue(string name) => source.GetPropertyValue(name);
        public string GetPropertyPriority(string name) => source.GetPropertyPriority(name);
        public void SetParent(ICssRule? rule) => throw new NotSupportedException();
        public void SetProperty(string name, string value, string? priority = null) => throw new NotSupportedException();
        public string RemoveProperty(string name) => throw new NotSupportedException();
        public void SetPropertyPriority(string name, string priority) => throw new NotSupportedException();
        public void SetDefaultProperty(string name, string value) => throw new NotSupportedException();
        public void Update(string value) => throw new NotSupportedException();
        public void ToCss(TextWriter writer, IStyleFormatter formatter) => source.ToCss(writer, formatter);
        public IEnumerator<ICssProperty> GetEnumerator() => ((IEnumerable<ICssProperty>) properties).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CountingStyles(IStyleCollection inner) : IStyleCollection
    {
        public IRenderDevice Device => inner.Device;
        internal int Matches { get; set; }

        public IEnumerator<ICssStyleRule> GetEnumerator()
        {
            Matches++;
            return inner.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CountingProvider(ICssDefaultStyleSheetProvider inner) : ICssDefaultStyleSheetProvider
    {
        internal int Reads { get; set; }

        public ICssStyleSheet Default
        {
            get
            {
                Reads++;
                return inner.Default;
            }
        }

        public void SetDefault(ICssStyleSheet? sheet) => inner.SetDefault(sheet);
        public void SetDefault(string source) => inner.SetDefault(source);
        public void AppendDefault(string source) => inner.AppendDefault(source);
    }
}
