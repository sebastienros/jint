#nullable enable
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Colors;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css;

[TestFixture]
public sealed class NativeCssQueryTests
{
    [Test]
    public void ImportanceOriginInlineSpecificityAndOrderSelectActualSource()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        target.SetAttribute("id", "target");
        target.SetAttribute("class", "target");
        var ua = CssStyleSheet.Parse("#target { opacity:.1 !important; }");
        var user = CssStyleSheet.Parse("#target { opacity:.2 !important; }");
        var author = CssStyleSheet.Parse(".target { opacity:.3; } #target { opacity:.4; } #target { opacity:.5; }");
        var inline = CssDeclarationBlock.Parse("opacity:.6 !important");
        var query = Query(document, [new(ua, NativeCssOrigin.UserAgent), new(user, NativeCssOrigin.User), new(author, NativeCssOrigin.Author)], [(target, inline)]);
        var matching = new SelectorMatchWork(document, default);
        var result = query.GetProperty(target, "opacity", ref matching);
        result.Text.Should().Be("0.1");
        result.Source!.Rule.Should().BeSameAs(ua.Rules[0]);
        query.MatchedRules(target, ref matching).Count.Should().Be(5);

        query = Query(document, [new(author, NativeCssOrigin.Author)]);
        result = query.GetProperty(target, "opacity", ref matching);
        result.Text.Should().Be("0.5");
        result.Source!.Rule.Should().BeSameAs(author.Rules[2]);
        query = Query(document, [new(author, NativeCssOrigin.Author)], [(target, inline)]);
        query.GetProperty(target, "opacity", ref matching).Source!.Inline.Should().BeTrue();
    }

    [Test]
    public void InheritanceWideKeywordsAndRollbackRetainDisposition()
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        var child = document.CreateElement("span");
        parent.AppendChild(child);
        var author = CssStyleSheet.Parse("div { visibility:hidden; opacity:.5; } span { opacity:inherit; display:revert; }");
        var ua = CssStyleSheet.Parse("span { display:block; }");
        var query = Query(document, [new(ua, NativeCssOrigin.UserAgent), new(author, NativeCssOrigin.Author)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "visibility", ref matching).Text.Should().Be("hidden");
        query.GetProperty(child, "opacity", ref matching).Disposition.Should().Be(NativeCssDisposition.Inherited);
        query.GetProperty(child, "opacity", ref matching).Text.Should().Be("0.5");
        query.GetProperty(child, "display", ref matching).Source!.Rule.Should().BeSameAs(ua.Rules[0]);
        query.GetProperty(child, "position", ref matching).Text.Should().Be("static");
    }

    [Test]
    public void VariableFallbackWideKeywordRollsBackTheOrigin()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var ua = CssStyleSheet.Parse("div { display:block; }");
        var author = CssStyleSheet.Parse("div { display:var(--missing,revert); }");
        var query = Query(document, [new(ua, NativeCssOrigin.UserAgent), new(author, NativeCssOrigin.Author)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "display", ref matching).Text.Should().Be("block");
    }

    [Test]
    public void CustomVariablesKeepDefiningScopeAndDoNotRetokenize()
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        var child = document.CreateElement("span");
        parent.AppendChild(child);
        var sheet = CssStyleSheet.Parse("div { --base:hidden; --alias:var(--base); } span { --base:visible; visibility:var(--alias); --n:12; width:var(--n)px; }");
        var query = Query(document, [new(sheet, NativeCssOrigin.Author)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "visibility", ref matching).Text.Should().Be("hidden");
        query.GetProperty(child, "--alias", ref matching).Text.Should().Be("hidden");
        var width = query.GetProperty(child, "width", ref matching);
        width.Text.Should().Be("auto");
        width.Disposition.Should().Be(NativeCssDisposition.InvalidAtComputedValue);
        query.GetProperty(child, "--n", ref matching).Text.Should().Be("12");
    }

    [Test]
    public void PendingShorthandIsSubstitutedAndInvalidValueNeverResurrectsEarlierCandidate()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var sheet = CssStyleSheet.Parse("div { width:20px; --flow:column wrap; flex-flow:var(--flow); } div { width:var(--missing); }");
        var query = Query(document, [new(sheet, NativeCssOrigin.Author)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "flex-direction", ref matching).Text.Should().Be("column");
        query.GetProperty(target, "flex-wrap", ref matching).Text.Should().Be("wrap");
        query.GetProperty(target, "flex-flow", ref matching).Text.Should().Be("column wrap");
        query.GetProperty(target, "width", ref matching).Text.Should().Be("auto");
    }

    [Test]
    public void ReadingOnePropertyLeavesUnrequestedSubstitutionAndPendingMetadataAlone()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var sheet = CssStyleSheet.Parse("div { display:block; width:var(--missing); }");
        var query = Query(document, [new(sheet, NativeCssOrigin.Author)]);
        var matching = new SelectorMatchWork(document, default);
        var display = query.GetProperty(target, "display", ref matching);
        display.Text.Should().Be("block");
        query.GetProperty(target, "display", ref matching).Should().BeSameAs(display);
        Assert.Throws<CssIncompleteGrammarException>(() => query.GetProperty(target, "background", ref matching));
        query.GetProperty(target, "display", ref matching).Should().BeSameAs(display);
    }

    [Test]
    public void InactiveMediaDoesNotMatchAndMutationInvalidatesCachedValues()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var sheet = CssStyleSheet.Parse("div { display:block; } @media print { div { display:none; } }");
        var query = Query(document, [new(sheet, NativeCssOrigin.Author)]);
        var matching = new SelectorMatchWork(document, default);
        query.MatchedRules(target, ref matching).Count.Should().Be(1);
        query.GetProperty(target, "display", ref matching).Text.Should().Be("block");
        ((CssStyleRule) sheet.Rules[0]).Style.SetProperty("display", "none");
        Assert.Throws<InvalidOperationException>(() => query.GetProperty(target, "display", ref matching))!
            .Message.Should().Be(NativeCssQuery.Invalidated);
    }

    [Test]
    public void SourceInstallationIsLazyAndReplacementPreservesSheetIdentity()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var owner = document.CreateElement("style");
        root.AppendChild(owner);
        var source = document.CreateTextNode("div { display:block; }");
        owner.AppendChild(source);
        var work = new CssValueWork(default);
        NativeCssStyleSheets.Install(document, owner, "div { display:block; }",
            "https://example.test/a.css", "https://example.test/base/", work);
        var sheets = NativeCssStyleSheets.Get(document, work);
        var sheet = sheets[0].Sheet;
        sheet.Attachment.OwnerNode.Should().BeSameAs(owner);
        sheet.Attachment.SourceUrl!.AbsoluteUri.Should().Be("https://example.test/a.css");
        sheet.Attachment.BaseUrl!.AbsoluteUri.Should().Be("https://example.test/base/");
        var query = Query(document, sheets.ToArray());
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(root, "display", ref matching).Text.Should().Be("block");
        source.Data = "div { display:none; }";
        NativeCssStyleSheets.Install(document, owner, "div { display:none; }",
            "https://example.test/a.css", "https://example.test/base/", work);
        Assert.Throws<InvalidOperationException>(() => query.GetProperty(root, "display", ref matching));
        NativeCssStyleSheets.Get(document, work)[0].Sheet.Should().BeSameAs(sheet);
        sheet.Rules[0].CssText.Should().Contain("display: none");

        // An installation never invokes a pending property validator during HTML parsing.
        source.Data = "div { background:red; }";
        NativeCssStyleSheets.Install(document, owner, "div { background:red; }", "", "", work);
        Assert.Throws<CssIncompleteGrammarException>(() => NativeCssStyleSheets.Get(document, work));
    }

    [TestCase("width:1in", "width", "96px")]
    [TestCase("height:25vh", "height", "192px")]
    [TestCase("width:calc(10vw + 4px)", "width", "106.4px")]
    [TestCase("width:calc(50% + 4px)", "width", "calc(50% + 4px)")]
    [TestCase("width:calc(-2px)", "width", "0px")]
    [TestCase("width:20%", "width", "20%")]
    [TestCase("opacity:150%", "opacity", "1")]
    [TestCase("opacity:calc(25% * 2)", "opacity", "0.5")]
    [TestCase("opacity:calc(-1)", "opacity", "0")]
    [TestCase("z-index:calc(2.5)", "z-index", "3")]
    public void ComputationUsesTypedNumbersAndKeepsPercentageBases(string declarations, string name, string expected)
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var block = CssDeclarationBlock.Parse(declarations);
        var query = Query(document, [], [(target, block)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, name, ref matching).Text.Should().Be(expected);
    }

    [Test]
    public void FontDependentUnitRequiresAnExplicitMetricAndDoesNotBlockOtherProperties()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var block = CssDeclarationBlock.Parse("width:2em;display:block");
        var query = Query(document, [], [(target, block)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "display", ref matching).Text.Should().Be("block");
        Assert.Throws<CssIncompleteGrammarException>(() => query.GetProperty(target, "width", ref matching))!
            .Blocker.Should().Be("C6:font-size");
        var work = new CssValueWork(default);
        query = new(document, [], [(target, block)], new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work, new NativeCssMetrics { FontSize = 20 });
        query.GetProperty(target, "width", ref matching).Text.Should().Be("40px");
    }

    [Test]
    public void ComputedViewDefersEnumerationAndRetainsSnapshotGuards()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var block = CssDeclarationBlock.Parse("display:block; width:2em");
        var view = new NativeCssComputedStyle(Query(document, [], [(target, block)]), target,
            new SelectorMatchWork(document, default));
        view.GetPropertyValue("display").Should().Be("block");
        view.CssText.Should().BeEmpty();
        Assert.Throws<CssIncompleteGrammarException>(() => _ = view.Length);
        block.SetProperty("display", "none");
        Assert.Throws<InvalidOperationException>(() => view.GetPropertyValue("display"));
    }

    [Test]
    public void DeepInheritanceDoesNotUseTheClrCallStack()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        var target = root;
        for (var i = 0; i < 10_000; i++)
        {
            var child = document.CreateElement("span");
            target.AppendParsedChild(child);
            target = child;
        }
        var query = Query(document, [], [(root, CssDeclarationBlock.Parse("visibility:hidden"))]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "visibility", ref matching).Text.Should().Be("hidden");
    }

    [Test]
    public void CurrentColorUsesInheritedColorAndTheElementsOwnComputedColor()
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        var child = document.CreateElement("span");
        parent.AppendChild(child);
        var sheet = CssStyleSheet.Parse("div { color:red; } span { color:currentColor; background-color:currentColor; }");
        var query = Query(document, [new(sheet, NativeCssOrigin.Author)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "color", ref matching).Text.Should().Be("rgb(255, 0, 0)");
        query.GetProperty(child, "background-color", ref matching).Text.Should().Be("rgb(255, 0, 0)");
        query.GetProperty(parent, "background-color", ref matching).Text.Should().Be("rgba(0, 0, 0, 0)");
    }

    [Test]
    public void SystemColorsUseOnlyAnExplicitImmutablePalette()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var matching = new SelectorMatchWork(document, default);
        var query = Query(document, []);
        Assert.Throws<CssIncompleteGrammarException>(() => query.GetProperty(target, "color", ref matching))!
            .Blocker.Should().Be("C6:system-color:canvastext");
        query.GetProperty(target, "display", ref matching).Text.Should().Be("inline");
        var work = new CssValueWork(default);
        var blue = CssColorParser.Parse(CssReferenceInput.Parse("blue", null, default).Components, 0, work).Value;
        var palette = NativeCssSystemColors.Create([("canvastext", blue)], work);
        query = new(document, [], [], new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work, systemColors: palette);
        query.GetProperty(target, "color", ref matching).Text.Should().Be("rgb(0, 0, 255)");
    }

    [Test]
    public void InlineSheetDemandReconcilesNativeTextWithoutReplacingFetchedLinkSource()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var owner = document.CreateElement("style");
        var source = document.CreateTextNode("div { display:block; }");
        owner.AppendChild(source);
        root.AppendChild(owner);
        var link = document.CreateElement("link");
        root.AppendChild(link);
        var work = new CssValueWork(default);
        NativeCssStyleSheets.Install(document, link, "div { opacity:.5; }", "https://example.test/a.css", "", work);
        var sheets = NativeCssStyleSheets.Get(document, work);
        var inline = sheets[0].Sheet;
        var fetched = sheets[1].Sheet;
        source.Data = "div { display:none; }";
        sheets = NativeCssStyleSheets.Get(document, work);
        sheets[0].Sheet.Should().BeSameAs(inline);
        ((CssStyleRule) inline.Rules[0]).Style.GetPropertyValue("display").Should().Be("none");
        sheets[1].Sheet.Should().BeSameAs(fetched);
        ((CssStyleRule) fetched.Rules[0]).Style.GetPropertyValue("opacity").Should().Be("0.5");
    }

    [Test]
    public void LazySheetLexingChecksTheHostBeforeFinishingOneLargeToken()
    {
        var options = new CssParseOptions { Limits = new ParseLimits { MaxTokenCharacters = 2048 } };
        var checks = 0;
        var work = new CssValueWork(default, () =>
        {
            if (++checks == 2) throw new OperationCanceledException();
        });
        Assert.Throws<OperationCanceledException>(() => CssStyleSheet.Parse(
            new string('a', 8192) + " { display:block; }", options, work, default));
        checks.Should().Be(2);
    }

    [Test]
    public void InlineAttributesParseOnlyWhenTheirElementIsDemandedAndWritesInvalidateViews()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var target = document.CreateElement("div");
        root.AppendChild(target);
        target.SetAttribute("style", "opacity:.5");
        var unrelated = document.CreateElement("div");
        root.AppendChild(unrelated);
        unrelated.SetAttribute("style", "border-image:pending");
        var work = new CssValueWork(default);
        var query = new NativeCssQuery(document, [], [], new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work, readInlineAttributes: true);
        var view = new NativeCssComputedStyle(query, target, new SelectorMatchWork(document, default));
        view.GetPropertyValue("opacity").Should().Be("0.5");
        target.SetAttribute("style", "opacity:.75");
        Assert.Throws<InvalidOperationException>(() => view.GetPropertyValue("opacity"))!
            .Message.Should().Be(NativeCssQuery.Invalidated);
        query = new(document, [], [], new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work, readInlineAttributes: true);
        view = new(query, target, new SelectorMatchWork(document, default));
        view.GetPropertyValue("opacity").Should().Be("0.75");
    }

    private static NativeCssQuery Query(Document document, NativeCssSheet[] sheets,
        (Element Element, CssDeclarationBlock Block)[]? inline = null)
    {
        var work = new CssValueWork(default);
        return new(document, sheets, inline ?? [], new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work);
    }
}
