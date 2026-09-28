#nullable enable
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

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
        rule.Style.GetPropertyValue("src").Should().Be("url(codicon.ttf) format('truetype')");
        rule.CssText.Should().Contain("font-display: block;");
        sheet.Stamp.Should().Be(before);
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
        block.GetPropertyValue("font-width").Should().Be("condensed");
        block.CssText.Should().Contain("font-width: condensed;");
    }




    [Test]
    public void LongDescriptorNamesPollCancellationDuringTheAllocatedCopy()
    {
        // Warm the string.Create callback before using its output allocation as the copy boundary.
        CssPropertyRegistry.NormalizeName("FONT-FAMILY", new CssValueWork(default)).Should().Be("font-family");
        var name = new string('A', 16384);
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var allocatedCopyPolls = 0;
        long allocationBeforeCall = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            polls++;
            // Precharging name.Length cannot cancel here: the destination string does not yet exist.
            if (GC.GetAllocatedBytesForCurrentThread() - allocationBeforeCall >= name.Length * sizeof(char)
                && ++allocatedCopyPolls == 2)
                cancellation.Cancel();
        });
        OperationCanceledException? failure = null;
        allocationBeforeCall = GC.GetAllocatedBytesForCurrentThread();
        // Keep assertion-framework allocations outside the boundary being observed.
        try { CssPropertyRegistry.NormalizeName(name, work); }
        catch (OperationCanceledException exception) { failure = exception; }
        failure.Should().NotBeNull();
        // Entry plus two charged copy chunks. An exit-only check sees the allocation just once.
        allocatedCopyPolls.Should().Be(2);
        polls.Should().Be(3);
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
