#nullable enable
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.Descriptors;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssFontFaceRuleTests
{
    [Test]
    public void MonacoStaticAndDynamicDescriptorsRemainPassiveAndCanonical()
    {
        var sheet = CssStyleSheet.Parse("@font-face {font-family:codicon;src:url(codicon.ttf) format('truetype');font-display:block}"
            + "@font-face {font-family:monaco-icons;src:url(data:font/woff;base64,AA==);font-weight:normal;font-style:normal}");
        sheet.Rules.Count.Should().Be(2);
        var rule = (CssFontFaceRule) sheet.Rules[0];
        ((int) rule.Type).Should().Be(5);
        var before = sheet.Stamp;
        rule.Style.GetPropertyValue("font-family").Should().Be("codicon");
        rule.Style.GetPropertyValue("src").Should().Be("url(\"codicon.ttf\") format(\"truetype\")");
        rule.CssText.Should().Contain("font-display: block;");
        sheet.Stamp.Should().Be(before);
        var source = rule.Style.ResolveProperty("src", new CssValueWork(default))!.Value.DescriptorValue.Sources[0];
        source.Kind.Should().Be(CssFontSourceKind.Url);
        source.Name.Should().Be("codicon.ttf");
        source.Format.Should().Be("truetype");
        var dynamicRule = (CssFontFaceRule) sheet.Rules[1];
        dynamicRule.Style.GetPropertyValue("font-weight").Should().Be("normal");
        dynamicRule.Style.GetPropertyValue("font-style").Should().Be("normal");
    }

    [Test]
    public void EmptyIncompleteAndMalformedRulesHaveIndependentRetentionAndIdentity()
    {
        var sheet = CssStyleSheet.Parse("@font-face{} @font-face {font-family:Only} @font-face wrong{} @font-face;"
            + "@media all {@font-face {src:url(font.woff)}}");
        sheet.Rules.Count.Should().Be(3);
        var empty = (CssFontFaceRule) sheet.Rules[0];
        empty.Style.GetPropertyValue("font-weight").Should().BeEmpty();
        empty.Style.Count.Should().Be(0);
        var nested = (CssFontFaceRule) sheet.Rules[2].Rules[0];
        nested.ParentRule.Should().BeSameAs(sheet.Rules[2]);
        nested.ParentStyleSheet.Should().BeSameAs(sheet);
        var style = empty.Style;
        var span = empty.SourceSpan;
        var stamp = sheet.Stamp;
        style.SetProperty("font-family", "MiXeD");
        sheet.Stamp.Should().NotBe(stamp);
        empty.Style.Should().BeSameAs(style);
        empty.SourceSpan.Should().Be(span);
        sheet.DeleteRule(0);
        empty.ParentRule.Should().BeNull();
        empty.ParentStyleSheet.Should().BeNull();
        var detachedStamp = sheet.Stamp;
        style.SetProperty("font-display", "swap");
        sheet.Stamp.Should().Be(detachedStamp);
        sheet.InsertRule("@font-face {font-family:New}", 0).Should().Be(0);
        sheet.Rules[0].Should().NotBeSameAs(empty);
    }

    [Test]
    public void DescriptorContextRejectsOrdinaryEffectsAndSourceImportanceButRetainsApiPriority()
    {
        var block = CssDeclarationBlock.Parse("font-family:First; font-family:Second; font-family:A,B;"
            + "font-family:Third!important;font:italic;all:initial;color:red;width:1px;--x:10;word-wrap:normal;unknown:yes",
            CssDeclarationContext.FontFace);
        block.Count.Should().Be(1);
        block.GetPropertyValue("FONT-FAMILY").Should().Be("Second");
        foreach (var name in new[] { "font", "all", "color", "width", "--x", "word-wrap", "overflow-wrap" })
            block.GetPropertyValue(name).Should().BeEmpty();
        block.SetProperty("font-family", "Final", "important");
        block.GetPropertyPriority("font-family").Should().Be("important");
        block.CssText.Should().Be("font-family: Final !important;");
        block.SetProperty("font-family", "Other", "invalid");
        block.GetPropertyValue("font-family").Should().Be("Final");
        block.SetProperty("font", "normal");
        block.SetProperty("all", "initial");
        block.SetProperty("--x", "1");
        block.Count.Should().Be(1);
    }


    [Test]
    public void DescriptorLeafIgnoresNestedRuleSyntaxWithoutWalkingIt()
    {
        var sheet = CssStyleSheet.Parse("@font-face{font-family:Known;@font-feature-values Unfinished{unknown:x}src:url(font.woff)}");
        var rule = (CssFontFaceRule) sheet.Rules[0];
        rule.Rules.Count.Should().Be(0);
        rule.Style.GetPropertyValue("font-family").Should().Be("Known");
        var nested = (CssStyleRule) CssStyleSheet.Parse("a {@font-face {font-family:Invalid}}").Rules[0];
        nested.Rules.Count.Should().Be(0);
    }

    [Test]
    public void TargetReadAndWriteDoNotDemandUnrelatedPendingDescriptors()
    {
        var sheet = CssStyleSheet.Parse("@font-face{font-family:Known;font-width:condensed}");
        var rule = (CssFontFaceRule) sheet.Rules[0];
        var block = rule.Style;
        var stamp = sheet.Stamp;
        block.GetPropertyValue("font-family").Should().Be("Known");
        sheet.Stamp.Should().Be(stamp);
        block.SetProperty("font-family", "Changed");
        block.GetPropertyValue("font-family").Should().Be("Changed");
        var pending = Assert.Throws<CssIncompleteGrammarException>(() => block.GetPropertyValue("font-width"))!;
        pending.Blocker.Should().Be("R4:font-face:font-width");
        Assert.Throws<CssIncompleteGrammarException>(() => _ = block.CssText);
    }

    [TestCase("url(bad) format(unknown),local(Valid Name),url(good) format(woff2) tech(variations)",
        "local(\"Valid Name\"), url(\"good\") format(\"woff2\") tech(variations)")]
    [TestCase("url(bad) tech(unknown), url(good)", "url(\"good\")")]
    [TestCase("url(good) format('woff2-variations')", "url(\"good\") format(\"woff2\") tech(variations)")]
    [TestCase("local('MiXeD'), rubbish, url(next)", "local(\"MiXeD\"), url(\"next\")")]
    public void SourceListRecoversEntriesAndPreservesOrder(string value, string expected)
    {
        var block = CssDeclarationBlock.Parse("src:" + value, CssDeclarationContext.FontFace);
        block.GetPropertyValue("src").Should().Be(expected);
        block.SetProperty("src", "url(bad) format(unknown)");
        block.GetPropertyValue("src").Should().Be(expected);
    }

    [TestCase("font-weight", "900 100", "900 100")]
    [TestCase("font-weight", "calc(200 + 300) 700", "calc(500) 700")]
    [TestCase("font-weight", "auto", "auto")]
    [TestCase("font-style", "oblique -45deg 30deg", "oblique -45deg 30deg")]
    [TestCase("font-style", "oblique .25turn", "oblique 0.25turn")]
    [TestCase("font-style", "left", "left")]
    public void DescriptorRangesUseNumericMathAndAngleGrammars(string name, string value, string expected)
    {
        var block = CssDeclarationBlock.Parse(name + ":" + value, CssDeclarationContext.FontFace);
        block.GetPropertyValue(name).Should().Be(expected);
    }

    [TestCase("font-weight", "bolder")]
    [TestCase("font-weight", "1001")]
    [TestCase("font-weight", "0.99999999999999999999")]
    [TestCase("font-weight", "100 200 300")]
    [TestCase("font-style", "oblique 91deg")]
    [TestCase("font-style", "oblique 90.00000000000000000001deg")]
    [TestCase("font-style", "oblique 1turn")]
    [TestCase("font-style", "inherit")]
    [TestCase("font-family", "serif")]
    [TestCase("font-family", "One,Two")]
    public void InvalidDescriptorsDoNotReplaceAValidDeclaration(string name, string value)
    {
        var block = CssDeclarationBlock.Parse(name + ":" + value, CssDeclarationContext.FontFace);
        block.Count.Should().Be(0);
    }

    [Test]
    public void CancelledAndReentrantWritesPublishNoPartialReplacement()
    {
        var block = CssDeclarationBlock.Parse("font-family:Before;src:url(before)", CssDeclarationContext.FontFace);
        var text = block.CssText;
        var stamp = block.Stamp;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => block.SetProperty("font-family", "After", cancellationToken: cancellation.Token));
        block.CssText.Should().Be(text);
        block.Stamp.Should().Be(stamp);
        var entered = false;
        var work = new CssValueWork(default, () =>
        {
            if (entered) return;
            entered = true;
            block.SetProperty("font-display", "swap");
        });
        Assert.Throws<InvalidOperationException>(() => block.SetProperty("font-family", "After", null, null, work));
        block.GetPropertyValue("font-family").Should().Be("Before");
        block.GetPropertyValue("font-display").Should().Be("swap");
    }
}
