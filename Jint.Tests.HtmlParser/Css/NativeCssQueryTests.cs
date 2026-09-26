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
    public void MatchedAndUnmatchedPendingDeclarationsCannotBlockAnUnrelatedDisplayRead()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var sheet = CssStyleSheet.Parse("span { border-color:red; } div { display:block; background:red; text-wrap:balance; }");
        var query = Query(document, [new(sheet, NativeCssOrigin.Author)]);
        var matching = new SelectorMatchWork(document, default);
        var stamp = sheet.Stamp;
        var result = query.GetProperty(target, "display", ref matching);
        result.Text.Should().Be("block");
        sheet.Stamp.Should().Be(stamp);
        Assert.Throws<CssIncompleteGrammarException>(() => query.GetProperty(target, "background-color", ref matching))!
            .PropertyName.Should().Be("background");
        Assert.Throws<CssIncompleteGrammarException>(() => query.GetProperty(target, "text-wrap-mode", ref matching))!
            .PropertyName.Should().Be("text-wrap");
        query.GetProperty(target, "display", ref matching).Should().BeSameAs(result);
        ((CssStyleRule) sheet.Rules[1]).Style.SetProperty("display", "none");
        Assert.Throws<InvalidOperationException>(() => query.GetProperty(target, "display", ref matching));
        Query(document, [new(sheet, NativeCssOrigin.Author)]).GetProperty(target, "display", ref matching).Text.Should().Be("none");
    }

    [Test]
    public void InlineSourceParsesAreReusedAndUnrelatedPendingSyntaxStaysLazy()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        target.SetAttribute("style", "border:solid; display:block;--x:hidden; visibility:var(--x)");
        var work = new CssValueWork(default);
        var block = NativeCssStyleSheets.InlineOf(target, work);
        NativeCssStyleSheets.InlineOf(target, work).Should().BeSameAs(block);
        var query = new NativeCssQuery(document, [], [], new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work, readInlineAttributes: true);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "display", ref matching).Text.Should().Be("block");
        query.GetProperty(target, "visibility", ref matching).Text.Should().Be("hidden");
        target.SetAttribute("style", "display:none");
        NativeCssStyleSheets.InlineOf(target, work).Should().NotBeSameAs(block);
        Assert.Throws<InvalidOperationException>(() => query.GetProperty(target, "display", ref matching));
    }

    [Test]
    public void InlinePublicationRetainsPendingExpansionUntilAnActualStyleAttributeWrite()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        target.SetAttribute("style", "--o:hidden scroll;overflow:var(--o)");
        var work = new CssValueWork(default);
        var edited = NativeCssStyleSheets.InlineOf(target, work).Copy(work);
        edited.SetProperty("overflow-x", "visible", null, null, work);
        var text = edited.SerializeSource(work);
        text.Should().NotContain("overflow-y:");
        edited.Serialize(work).Should().NotContain("overflow-y:");
        var version = NativeCssStyleSheets.InlineVersion(target);
        target.SetAttribute("style", text);
        NativeCssStyleSheets.RetainInline(target, text, edited, version, work);
        target.SetAttribute("id", "unrelated");
        NativeCssStyleSheets.InlineOf(target, work).Should().BeSameAs(edited);
        var query = new NativeCssQuery(document, [], [], new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work, readInlineAttributes: true);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "overflow-y", ref matching).Text.Should().Be("scroll");
        target.SetAttribute("style", text);
        var reparsed = NativeCssStyleSheets.InlineOf(target, work);
        reparsed.Should().NotBeSameAs(edited);
        reparsed.GetPropertyValue("overflow-y", work).Should().BeEmpty();
    }

    [Test]
    public void PendingCustomBindingsRefuseOnlyWhenReachedByTheRequestedProperty()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var sheet = CssStyleSheet.Parse("div { --good:block; --bad:attr(data-x); display:var(--good); visibility:var(--bad,hidden); }");
        var query = Query(document, [new(sheet, NativeCssOrigin.Author)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "display", ref matching).Text.Should().Be("block");
        Assert.Throws<CssIncompleteGrammarException>(() => query.GetProperty(target, "--bad", ref matching));
        Assert.Throws<CssIncompleteGrammarException>(() => query.GetProperty(target, "visibility", ref matching));
    }

    [Test]
    public void DeferredWhiteSpaceRejectsAnInterleavedTrimComponent()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var sheet = CssStyleSheet.Parse("div { --w:discard-before nowrap discard-after; white-space:var(--w); }");
        var query = Query(document, [new(sheet, NativeCssOrigin.Author)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "white-space-collapse", ref matching).Disposition.Should().Be(NativeCssDisposition.InvalidAtComputedValue);
    }

    [Test]
    public void CancellationAfterInlineSourceCommitKeepsTheAuthoritativePendingExpansion()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        target.SetAttribute("style", "--o:hidden scroll;overflow:var(--o)");
        var work = new CssValueWork(default);
        var edited = NativeCssStyleSheets.InlineOf(target, work).Copy(work);
        edited.SetProperty("overflow-x", "visible", null, null, work);
        var source = edited.SerializeSource(work);
        var version = NativeCssStyleSheets.InlineVersion(target);
        target.SetAttribute("style", source);
        using var cancellation = new CancellationTokenSource();
        var postCommit = new CssValueWork(cancellation.Token, cancellation.Cancel);
        Assert.Throws<OperationCanceledException>(() => NativeCssStyleSheets.RetainInline(target, source, edited, version, postCommit));
        NativeCssStyleSheets.InlineOf(target, work).Should().BeSameAs(edited);
        var query = new NativeCssQuery(document, [], [], new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work, readInlineAttributes: true);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "overflow-y", ref matching).Text.Should().Be("scroll");
    }

    [Test]
    public void SaturatedInlineMutationCountersStillInvalidateIdenticalSourceWrites()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        target.SetAttribute("style", "display:block");
        var work = new CssValueWork(default);
        var factory = typeof(NativeCssStyleSheets).GetMethod("InlineResourceOf", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var resource = factory.Invoke(null, [target])!;
        resource.GetType().GetField("Version", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(resource, ulong.MaxValue);
        var old = NativeCssStyleSheets.InlineOf(target, work);
        target.SetAttribute("style", "display:block");
        NativeCssStyleSheets.InlineOf(target, work).Should().NotBeSameAs(old);
    }

    [Test]
    public void SaturatedInlinePublicationIsRejectedBeforeTheNativeSourceCommit()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        const string source = "--o:hidden scroll;overflow:var(--o)";
        target.SetAttribute("style", source);
        var work = new CssValueWork(default);
        var factory = typeof(NativeCssStyleSheets).GetMethod("InlineResourceOf", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var resource = factory.Invoke(null, [target])!;
        resource.GetType().GetField("Version", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(resource, ulong.MaxValue);
        var retained = NativeCssStyleSheets.InlineOf(target, work);
        var edited = retained.Copy(work);
        edited.SetProperty("overflow-x", "visible", null, null, work);
        Assert.Throws<InvalidOperationException>(() => NativeCssStyleSheets.InlineVersion(target));
        target.GetAttribute("style").Should().Be(source);
        NativeCssStyleSheets.InlineOf(target, work).Should().BeSameAs(retained);
    }

    [Test]
    public void UnsupportedCustomRollbackRefusesOnlyWhenThatBindingIsDemanded()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var sheet = CssStyleSheet.Parse("div { --good:block; --unused:revert-layer; display:var(--good); visibility:var(--unused,hidden); }");
        var query = Query(document, [new(sheet, NativeCssOrigin.Author)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "display", ref matching).Text.Should().Be("block");
        Assert.Throws<CssIncompleteGrammarException>(() => query.GetProperty(target, "--unused", ref matching))!.Message.Should().Contain("revert-layer");
        Assert.Throws<CssIncompleteGrammarException>(() => query.GetProperty(target, "visibility", ref matching));
    }

    [Test]
    public void LazyShorthandSubstitutionRetainsIacvtAndCustomCycles()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var sheet = CssStyleSheet.Parse("div { border:solid; display:block; display:var(--missing); --a:var(--b); --b:var(--a); "
            + "--mode:preserve nowrap; white-space:var(--mode); visibility:var(--a,hidden); }");
        var query = Query(document, [new(sheet, NativeCssOrigin.Author)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "display", ref matching).Text.Should().Be("inline");
        query.GetProperty(target, "display", ref matching).Disposition.Should().Be(NativeCssDisposition.InvalidAtComputedValue);
        query.GetProperty(target, "white-space-collapse", ref matching).Text.Should().Be("preserve");
        query.GetProperty(target, "white-space", ref matching).Text.Should().Be("pre");
        query.GetProperty(target, "visibility", ref matching).Text.Should().Be("hidden");
    }

    [Test]
    public void WhiteSpaceLonghandsInheritIndividuallyAndAChildNormalOverridesPre()
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("pre");
        var inherited = document.CreateElement("span");
        var normal = document.CreateElement("b");
        parent.AppendChild(inherited);
        parent.AppendChild(normal);
        var work = new CssValueWork(default);
        var author = CssStyleSheet.Parse("pre { white-space-trim:discard-inner; } b { white-space:normal; }");
        var query = Query(document, [NativeCssBrowserDefaults.Sheet(document, work), new(author, NativeCssOrigin.Author)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(inherited, "white-space-collapse", ref matching).Text.Should().Be("preserve");
        query.GetProperty(inherited, "text-wrap-mode", ref matching).Text.Should().Be("nowrap");
        query.GetProperty(inherited, "white-space-trim", ref matching).Text.Should().Be("none");
        query.GetProperty(normal, "white-space-collapse", ref matching).Text.Should().Be("collapse");
        query.GetProperty(normal, "white-space", ref matching).Text.Should().Be("normal");
    }

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
    public void SourceInstallationIsLazyAndStyleLifecycleReplacementRecreatesSheetIdentity()
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
        NativeCssStyleSheets.DisassociateOwner(document, owner, work);
        NativeCssStyleSheets.Install(document, owner, "div { display:none; }",
            "https://example.test/a.css", "https://example.test/base/", work);
        Assert.Throws<InvalidOperationException>(() => query.GetProperty(root, "display", ref matching));
        sheet.Attachment.OwnerNode.Should().BeNull();
        var replacement = NativeCssStyleSheets.Get(document, work)[0].Sheet;
        replacement.Should().NotBeSameAs(sheet);
        replacement.Rules[0].CssText.Should().Contain("display: none");

        // An installation never invokes a pending property validator during HTML parsing.
        source.Data = "div { background:red; }";
        NativeCssStyleSheets.DisassociateOwner(document, owner, work);
        NativeCssStyleSheets.Install(document, owner, "div { background:red; }", "", "", work);
        var pendingSheet = NativeCssStyleSheets.Get(document, work)[0].Sheet;
        Assert.Throws<CssIncompleteGrammarException>(() => _ = pendingSheet.Rules[0].CssText);
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
    public void GlyphDependentUnitRequiresAnExplicitMetricAndDoesNotBlockOtherProperties()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var block = CssDeclarationBlock.Parse("width:2ch;display:block");
        var query = Query(document, [], [(target, block)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "display", ref matching).Text.Should().Be("block");
        Assert.Throws<CssIncompleteGrammarException>(() => query.GetProperty(target, "width", ref matching))!
            .Blocker.Should().Be("C6:zero-advance");
        var work = new CssValueWork(default);
        query = new(document, [], [(target, block)], new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work, new NativeCssMetrics { ZeroAdvance = 20 });
        query.GetProperty(target, "width", ref matching).Text.Should().Be("40px");
    }

    [Test]
    public void ComputedViewDefersEnumerationAndRetainsSnapshotGuards()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var block = CssDeclarationBlock.Parse("display:block; width:2ch");
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
    public void StyleLifecycleUpdatesNativeTextWithoutReplacingFetchedLinkSource()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var owner = document.CreateElement("style");
        var source = document.CreateTextNode("div { display:block; }");
        owner.AppendChild(source);
        root.AppendChild(owner);
        var link = document.CreateElement("link");
        link.SetAttribute("rel", "stylesheet");
        root.AppendChild(link);
        var work = new CssValueWork(default);
        NativeCssStyleSheets.Install(document, owner, source.Data, "", "", work);
        NativeCssStyleSheets.Install(document, link, "div { opacity:.5; }", "https://example.test/a.css", "", work);
        var sheets = NativeCssStyleSheets.Get(document, work);
        var inline = sheets[0].Sheet;
        var fetched = sheets[1].Sheet;
        source.Data = "div { display:none; }";
        // Reads consume the last explicit association; they do not advance parser/lifecycle state.
        NativeCssStyleSheets.Get(document, work)[0].Sheet.Should().BeSameAs(inline);
        ((CssStyleRule) inline.Rules[0]).Style.GetPropertyValue("display").Should().Be("block");
        NativeCssStyleSheets.DisassociateOwner(document, owner, work);
        NativeCssStyleSheets.Install(document, owner, source.Data, "", "", work);
        sheets = NativeCssStyleSheets.Get(document, work);
        sheets[0].Sheet.Should().NotBeSameAs(inline);
        inline.Attachment.OwnerNode.Should().BeNull();
        ((CssStyleRule) sheets[0].Sheet.Rules[0]).Style.GetPropertyValue("display").Should().Be("none");
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

    [Test]
    public void InheritedCurrentColorResolvesAgainstTheChildsOwnColor()
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        var child = document.CreateElement("span");
        parent.AppendChild(child);
        var sheet = CssStyleSheet.Parse("div { color:red; background-color:currentColor; } "
            + "span { color:blue; background-color:inherit; }");
        var query = Query(document, [new(sheet, NativeCssOrigin.Author)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(parent, "background-color", ref matching).Text.Should().Be("rgb(255, 0, 0)");
        var inherited = query.GetProperty(child, "background-color", ref matching);
        inherited.Text.Should().Be("rgb(0, 0, 255)");
        inherited.Value!.Color.Kind.Should().Be(CssColorKind.CurrentColor);
    }

    [Test]
    public void OwnerTypeAndMediaAttributesControlDemandedSheetEligibility()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        document.AppendChild(target);
        var owner = document.CreateElement("style");
        owner.AppendChild(document.CreateTextNode("div { opacity:.25; }"));
        target.AppendChild(owner);
        var work = new CssValueWork(default);
        owner.SetAttribute("type", "text/plain");
        NativeCssStyleSheets.Get(document, work).Should().BeEmpty();
        owner.SetAttribute("type", "text/css");
        owner.SetAttribute("media", "print");
        NativeCssStyleSheets.Install(document, owner, "div { opacity:.25; }", "", "", work);
        var sheets = NativeCssStyleSheets.Get(document, work);
        var query = Query(document, sheets.ToArray());
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "opacity", ref matching).Text.Should().Be("1");
        owner.SetAttribute("media", "screen");
        var updated = NativeCssStyleSheets.Get(document, work);
        updated[0].Sheet.Should().BeSameAs(sheets[0].Sheet);
        query = Query(document, updated.ToArray());
        matching = new(document, default);
        query.GetProperty(target, "opacity", ref matching).Text.Should().Be("0.25");
    }

    [Test]
    public void RetainedLinkSheetUsesTheOwnersCurrentRelationTokens()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        document.AppendChild(target);
        var link = document.CreateElement("link");
        target.AppendChild(link);
        link.SetAttribute("rel", "alternate\tStyleSheet\n");
        var work = new CssValueWork(default);
        NativeCssStyleSheets.Install(document, link, "div { opacity:.25; }", "https://example.test/a.css", "", work);
        var retained = NativeCssStyleSheets.Get(document, work)[0].Sheet;
        NativeCssStyleSheets.DisassociateOwner(document, link, work);
        link.SetAttribute("rel", "not-stylesheet stylesheetx");
        NativeCssStyleSheets.Get(document, work).Should().BeEmpty();
        link.RemoveAttribute("rel");
        NativeCssStyleSheets.Get(document, work).Should().BeEmpty();
        link.SetAttribute("rel", "stylesheet");
        NativeCssStyleSheets.Install(document, link, "div { opacity:.25; }", "https://example.test/a.css", "", work);
        var sheets = NativeCssStyleSheets.Get(document, work);
        retained.Attachment.OwnerNode.Should().BeNull();
        sheets[0].Sheet.Should().NotBeSameAs(retained);
        var query = Query(document, sheets.ToArray());
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "opacity", ref matching).Text.Should().Be("0.25");
    }

    [Test]
    public void HostMutationDuringReplacementParsingCannotPublishTheNewSheet()
    {
        var document = Document.CreateHtml();
        var link = document.CreateElement("link");
        document.AppendChild(link);
        link.SetAttribute("rel", "stylesheet");
        var work = new CssValueWork(default);
        NativeCssStyleSheets.Install(document, link, "div { opacity:.25; }", "", "", work);
        var retained = NativeCssStyleSheets.Get(document, work)[0].Sheet;
        NativeCssStyleSheets.Install(document, link, "/*" + new string('x', 20000) + "*/ div { opacity:.75; }", "", "", work);
        var checks = 0;
        var guarded = new CssValueWork(default, () =>
        {
            if (++checks == 10) link.SetAttribute("title", "changed during parsing");
        });
        Assert.Throws<InvalidOperationException>(() => NativeCssStyleSheets.Get(document, guarded))!
            .Message.Should().Be(NativeCssQuery.Invalidated);
        ((CssStyleRule) retained.Rules[0]).Style.GetPropertyValue("opacity").Should().Be("0.25");
        NativeCssStyleSheets.Get(document, work)[0].Sheet.Should().BeSameAs(retained);
        ((CssStyleRule) retained.Rules[0]).Style.GetPropertyValue("opacity").Should().Be("0.75");
    }

    [Test]
    public void SvgStyleSourcesParticipateAndLinkDisabledAttributesStayLive()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        document.AppendChild(target);
        var style = document.CreateElementNS(Namespaces.Svg, "style");
        style.AppendChild(document.CreateTextNode("div { opacity:.25; }"));
        target.AppendChild(style);
        var link = document.CreateElement("link");
        link.SetAttribute("rel", "stylesheet");
        link.SetAttribute("disabled", "");
        target.AppendChild(link);
        var work = new CssValueWork(default);
        NativeCssStyleSheets.Install(document, style, "div { opacity:.25; }", "", "", work);
        NativeCssStyleSheets.Install(document, link, "div { opacity:.75; }", "", "", work);
        var sheets = NativeCssStyleSheets.Get(document, work);
        sheets.Count.Should().Be(2);
        var retained = sheets[1].Sheet;
        retained.Disabled.Should().BeTrue();
        var matching = new SelectorMatchWork(document, default);
        Query(document, sheets.ToArray()).GetProperty(target, "opacity", ref matching).Text.Should().Be("0.25");
        link.RemoveAttribute("disabled");
        sheets = NativeCssStyleSheets.Get(document, work);
        sheets[1].Sheet.Should().BeSameAs(retained);
        retained.Disabled.Should().BeFalse();
        matching = new(document, default);
        Query(document, sheets.ToArray()).GetProperty(target, "opacity", ref matching).Text.Should().Be("0.75");
        retained.Disabled = true;
        NativeCssStyleSheets.Get(document, work)[1].Sheet.Disabled.Should().BeTrue();
        link.SetAttribute("disabled", "");
        link.RemoveAttribute("disabled");
        NativeCssStyleSheets.Get(document, work)[1].Sheet.Disabled.Should().BeFalse();
        style.SetAttribute("type", "text/plain");
        NativeCssStyleSheets.Get(document, work).Count.Should().Be(1);
    }

    [Test]
    public void ShadowInheritanceUsesHostAndSlotWhileAuthorSheetsKeepTheirTreeScope()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        host.SetAttribute("id", "host");
        document.AppendChild(host);
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var target = document.CreateElement("span");
        shadow.AppendChild(target);
        var shadowOwner = document.CreateElement("style");
        shadowOwner.AppendChild(document.CreateTextNode("span { opacity:var(--x); }"));
        shadow.AppendChild(shadowOwner);
        var owner = document.CreateElement("style");
        owner.AppendChild(document.CreateTextNode("#host { visibility:hidden; --x:.25; } "
            + "span { visibility:visible; } button { opacity:var(--x); }"));
        host.AppendChild(owner);
        var slot = document.CreateElement("slot");
        slot.SetAttribute("style", "visibility:collapse; --x:.75");
        shadow.AppendChild(slot);
        var assigned = document.CreateElement("button");
        host.AppendChild(assigned);
        var work = new CssValueWork(default);
        NativeCssStyleSheets.Install(document, shadowOwner, "span { opacity:var(--x); }", "", "", work);
        NativeCssStyleSheets.Install(document, owner, "#host { visibility:hidden; --x:.25; } "
            + "span { visibility:visible; } button { opacity:var(--x); }", "", "", work);
        var sheets = NativeCssStyleSheets.Get(document, work, includeShadow: true);
        NativeCssStyleSheets.Get(document, work).Count.Should().Be(1);
        var query = new NativeCssQuery(document, sheets, [], new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work, readInlineAttributes: true);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "visibility", ref matching).Text.Should().Be("hidden");
        query.GetProperty(target, "opacity", ref matching).Text.Should().Be("0.25");
        query.GetProperty(assigned, "visibility", ref matching).Text.Should().Be("collapse");
        query.GetProperty(assigned, "opacity", ref matching).Text.Should().Be("0.75");
    }

    [TestCase("visible", "scroll", "auto", "scroll")]
    [TestCase("clip", "scroll", "clip", "scroll")]
    [TestCase("visible", "clip", "visible", "clip")]
    [TestCase("scroll", "clip", "scroll", "clip")]
    public void OverflowAxesComputeJointly(string x, string y, string computedX, string computedY)
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        var inline = CssDeclarationBlock.Parse($"overflow-x:{x}; overflow-y:{y}");
        var query = Query(document, [], [(element, inline)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(element, "overflow-y", ref matching).Text.Should().Be(computedY);
        query.GetProperty(element, "overflow-x", ref matching).Text.Should().Be(computedX);
    }

    [Test]
    public void ExplicitOverflowInheritanceReadsTheParentsAdjustedValue()
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        var child = document.CreateElement("span");
        parent.AppendChild(child);
        var query = Query(document, [], [(parent, CssDeclarationBlock.Parse("overflow-x:visible; overflow-y:scroll")),
            (child, CssDeclarationBlock.Parse("overflow-x:inherit; overflow-y:visible"))]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "overflow-x", ref matching).Text.Should().Be("auto");
        query.GetProperty(child, "overflow-y", ref matching).Text.Should().Be("auto");
    }

    [TestCase("inline", "absolute", "block")]
    [TestCase("inline-flex", "fixed", "flex")]
    [TestCase("inline-block", "absolute", "block")]
    [TestCase("ruby", "absolute", "block ruby")]
    [TestCase("run-in flex", "absolute", "flex")]
    [TestCase("none", "absolute", "none")]
    [TestCase("contents", "absolute", "contents")]
    public void PositionedDisplayTypesBlockify(string display, string position, string expected)
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        var query = Query(document, [], [(element, CssDeclarationBlock.Parse($"display:{display};position:{position}"))]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(element, "display", ref matching).Text.Should().Be(expected);
    }

    [TestCase("inline-flex")]
    [TestCase("inline-grid")]
    public void InlineFlexAndGridParentsBlockifyTheirItems(string display)
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        var child = document.CreateElement("span");
        parent.AppendChild(child);
        var query = Query(document, [], [(parent, CssDeclarationBlock.Parse($"display:{display}"))]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "display", ref matching).Text.Should().Be("block");
    }

    [TestCase("inline")]
    [TestCase("run-in")]
    public void RubyInlinifiesBlockChildrenAndDescendantsOfInlineChildren(string childDisplay)
    {
        var document = Document.CreateHtml();
        var ruby = document.CreateElement("ruby");
        var block = document.CreateElement("div");
        var inline = document.CreateElement("span");
        var nested = document.CreateElement("div");
        ruby.AppendChild(block);
        ruby.AppendChild(inline);
        inline.AppendChild(nested);
        var query = Query(document, [], [(ruby, CssDeclarationBlock.Parse("display:ruby")),
            (block, CssDeclarationBlock.Parse("display:block")),
            (inline, CssDeclarationBlock.Parse($"display:{childDisplay}")),
            (nested, CssDeclarationBlock.Parse("display:block"))]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(block, "display", ref matching).Text.Should().Be("inline-block");
        query.GetProperty(inline, "display", ref matching).Text.Should().Be("inline");
        query.GetProperty(nested, "display", ref matching).Text.Should().Be("inline-block");
    }

    [Test]
    public void RubyInlinifiesRunInFlowRootToInlineBlock()
    {
        var document = Document.CreateHtml();
        var ruby = document.CreateElement("ruby");
        var child = document.CreateElement("div");
        ruby.AppendChild(child);
        var query = Query(document, [], [(ruby, CssDeclarationBlock.Parse("display:ruby")),
            (child, CssDeclarationBlock.Parse("display:run-in flow-root"))]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "display", ref matching).Text.Should().Be("inline-block");
    }

    [Test]
    public void ConsecutiveContentsParentsDoNotRescanTheirEntireAncestorChain()
    {
        int ReadAtDepth(int depth)
        {
            var document = Document.CreateHtml();
            var parent = document.CreateElement("div");
            document.AppendChild(parent);
            for (var i = 0; i < depth; i++)
            {
                var child = document.CreateElement("section");
                parent.AppendChild(child);
                parent = child;
            }
            var target = document.CreateElement("span");
            parent.AppendChild(target);
            var checks = 0;
            var work = new CssValueWork(default, () => checks++);
            var sheet = CssStyleSheet.Parse("div { display:flex; } section { display:contents; }");
            var query = new NativeCssQuery(document, [new(sheet, NativeCssOrigin.Author)], [], new CssMediaEnvironment(),
                new(document, null, null, null), CssEnvironmentSnapshot.Create([], work), work);
            var matching = new SelectorMatchWork(document, default);
            query.GetProperty(target, "display", ref matching).Text.Should().Be("block");
            return checks;
        }
        var shallow = ReadAtDepth(256);
        ReadAtDepth(512).Should().BeLessThan(shallow * 3);
    }

    [Test]
    public void FlexItemDisplayBlockifiesAndDeepParentDependenciesStayIterative()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var parent = root;
        for (var i = 0; i < 10000; i++)
        {
            var child = document.CreateElement("div");
            parent.AppendChild(child);
            parent = child;
        }
        var item = document.CreateElement("span");
        parent.AppendChild(item);
        var query = Query(document, [], [(parent, CssDeclarationBlock.Parse("display:flex"))]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(item, "display", ref matching).Text.Should().Be("block");
        query.GetProperty(root, "display", ref matching).Text.Should().Be("block");
    }

    [Test]
    public void BrowserDefaultsAreHtmlScopedAndAuthorRulesOverrideTheirNormalOrigin()
    {
        var document = Document.CreateHtml();
        var div = document.CreateElement("div");
        var svgDiv = document.CreateElementNS(Namespaces.Svg, "div");
        var style = document.CreateElement("style");
        var work = new CssValueWork(default);
        var ua = NativeCssBrowserDefaults.Sheet(document, work);
        var query = Query(document, [ua]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(div, "display", ref matching).Text.Should().Be("block");
        query.GetProperty(style, "display", ref matching).Text.Should().Be("none");
        query.GetProperty(svgDiv, "display", ref matching).Text.Should().Be("inline");
        var author = CssStyleSheet.Parse("div { display:inline; }");
        query = Query(document, [ua, new(author, NativeCssOrigin.Author)]);
        query.GetProperty(div, "display", ref matching).Text.Should().Be("inline");
        query = new(document, [ua], [], new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work, systemColors: NativeCssBrowserDefaults.Palette(false, work));
        query.GetProperty(div, "color", ref matching).Text.Should().Be("rgb(0, 0, 0)");
    }

    private static NativeCssQuery Query(Document document, NativeCssSheet[] sheets,
        (Element Element, CssDeclarationBlock Block)[]? inline = null)
    {
        var work = new CssValueWork(default);
        return new(document, sheets, inline ?? [], new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work);
    }
}
