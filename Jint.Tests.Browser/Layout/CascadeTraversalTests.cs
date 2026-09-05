using System.Collections;
using AngleSharp;
using AngleSharp.Css;
using AngleSharp.Css.Dom;
using AngleSharp.Css.RenderTree;
using AngleSharp.Dom;
using Jint.Browser.Accessibility;
using Jint.Browser.Dom.Views;
using Jint.Browser.Layout;
using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.Layout;

public sealed class CascadeTraversalTests
{
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
            // Custom properties now retain their resolved tokens instead of AngleSharp's empty
            // computed values. Ordinary properties must still match its existing cascade.
            actual!.Where(property => !property.Name.StartsWith("--", StringComparison.Ordinal))
                .Select(property => (property.Name, property.Value, property.IsImportant))
                .Should().BeEquivalentTo(expected.Where(property => !property.Name.StartsWith("--", StringComparison.Ordinal))
                    .Select(property => (property.Name, property.Value, property.IsImportant)));
        }
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
