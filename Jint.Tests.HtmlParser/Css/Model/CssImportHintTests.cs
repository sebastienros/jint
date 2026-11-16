#nullable enable
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssImportHintTests
{
    private static bool Hint(string source) => NativeCssParsing.HasImportRule(source, new CssValueWork(default));

    [TestCase("/* @import 'comment'; */ a { border-color:red; }", false)]
    [TestCase("a { content:'@import'; background:url(@import); }", false)]
    [TestCase("@media all { @import 'nested'; }", false)]
    [TestCase("a { b { @import 'nested'; } }", false)]
    [TestCase("@import 'a';", true)]
    [TestCase("@\\69mport 'a';", true)]
    [TestCase("@IMPORT 'a';", true)]
    [TestCase("??? {} @import 'legal-after-invalid-selector';", true)]
    [TestCase("a {} @import 'invalid-after-style';", true)]
    [TestCase("@import 'a' layer;", true)]
    public void HintReadsOnlyTopLevelRulesAndDoesNotValidateOrMaterialize(string source, bool expected)
    {
        Hint(source).Should().Be(expected);
    }

    [Test]
    public void HintToleratesUnsupportedSelectorsAndDeclarations()
    {
        Hint("a:unsupported-selector { border-color:red; @container x {} } b { unknown-property:x; }").Should().BeFalse();
        Hint(new string('(', 12000) + "@import 'nested';" + new string(')', 12000)).Should().BeFalse();
        // A candidate sheet is semantically built once; raw declaration grammar stays deferred.
        var sheet = CssStyleSheet.Parse("@import 'a'; b { box-shadow:none; }");
        sheet.Rules.Count.Should().Be(2);
        sheet.ImportedStyleSheets(new CssValueWork(default)).Should().Equal(sheet);
        var rule = (CssStyleRule) sheet.Rules[1];
        rule.Style.Count.Should().Be(1);
    }

    [Test]
    public void CancellationAndSharedWorkCheckpointsBoundHintsAndNativeTraversals()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => NativeCssParsing.HasImportRule("", new CssValueWork(cancellation.Token)));

        var sheet = CssStyleSheet.Parse("@import 'a';");
        var child = CssStyleSheet.Parse("a {}");
        var stamp = sheet.Stamp;
        Assert.Throws<OperationCanceledException>(() => ((CssImportRule) sheet.Rules[0]).SetStyleSheet(child, null, null,
            new CssValueWork(cancellation.Token)));
        ((CssImportRule) sheet.Rules[0]).StyleSheet.Should().BeNull();
        child.Attachment.ImportOwner.Should().BeNull();
        sheet.Stamp.Should().Be(stamp);
        Assert.Throws<OperationCanceledException>(() => sheet.ImportedStyleSheets(new CssValueWork(cancellation.Token)));
    }
}
