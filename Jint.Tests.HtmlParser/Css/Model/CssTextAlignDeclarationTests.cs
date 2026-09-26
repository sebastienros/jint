#nullable enable

using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssTextAlignDeclarationTests
{
    [TestCase("start", "start", "auto")]
    [TestCase("end", "end", "auto")]
    [TestCase("left", "left", "auto")]
    [TestCase("right", "right", "auto")]
    [TestCase("center", "center", "auto")]
    [TestCase("justify", "justify", "auto")]
    [TestCase("justify-all", "justify", "justify")]
    [TestCase("match-parent", "match-parent", "match-parent")]
    public void ShorthandExpandsAndReconstructsBothLonghands(string source, string all, string last)
    {
        var block = CssDeclarationBlock.Parse("text-align:" + source);
        block.GetPropertyValue("text-align-all").Should().Be(all);
        block.GetPropertyValue("text-align-last").Should().Be(last);
        block.GetPropertyValue("text-align").Should().Be(source);
        CssDeclarationBlock.Parse(block.CssText).GetPropertyValue("text-align").Should().Be(source);
        foreach (var context in new[] { CssDeclarationContext.Style, CssDeclarationContext.Keyframe })
            CssPropertyParser.Parse("text-align", source, context).Status.Should().Be(CssPropertyStatus.Valid);
    }

    [TestCase("text-align", "auto")]
    [TestCase("text-align", "center right")]
    [TestCase("text-align-all", "auto")]
    [TestCase("text-align-all", "justify-all")]
    [TestCase("text-align-last", "justify-all")]
    [TestCase("text-align-last", "\".\"")]
    public void InvalidValuesAreRejected(string name, string text) =>
        CssPropertyParser.Parse(name, text).Status.Should().Be(CssPropertyStatus.Invalid);

    [Test]
    public void ResetImportancePendingValuesAndPartialOverridesUseTheSharedShorthandRules()
    {
        var block = CssDeclarationBlock.Parse("text-align-last:center;text-align:left !important");
        block.GetPropertyValue("text-align-last").Should().Be("auto");
        block.GetPropertyPriority("text-align").Should().Be("important");
        block.SetProperty("text-align-last", "right");
        block.GetPropertyValue("text-align").Should().Be("");
        block.GetPropertyPriority("text-align").Should().Be("");
        block.SetProperty("text-align", "var(--alignment)", "important");
        block.GetDeclaration(0).PendingShorthand.Should().BeSameAs(block.GetDeclaration(1).PendingShorthand);
        block.GetPropertyValue("text-align").Should().Be("var(--alignment)");
        block.SetProperty("text-align-last", "end", "important");
        block.GetPropertyValue("text-align").Should().Be("");
        block.SetProperty("text-align", "inherit");
        block.GetPropertyValue("text-align-all").Should().Be("inherit");
        block.GetPropertyValue("text-align-last").Should().Be("inherit");
        block.GetPropertyValue("text-align").Should().Be("inherit");
    }

    [TestCase("left", "center", "")]
    [TestCase("justify", "justify", "justify-all")]
    [TestCase("match-parent", "match-parent", "match-parent")]
    [TestCase("center", "auto", "center")]
    public void InverseSerializationRejectsNonrepresentablePairs(string all, string last, string expected)
    {
        var block = CssDeclarationBlock.Parse("text-align-all:" + all + ";text-align-last:" + last);
        block.GetPropertyValue("text-align").Should().Be(expected);
    }

    [Test]
    public void AlignmentStringsStayNamedPendingGrammar()
    {
        var result = CssPropertyParser.Parse("text-align", "\".\"");
        result.Status.Should().Be(CssPropertyStatus.UnimplementedGrammar);
        result.Blocker.Should().Be("text-align:alignment-string");
        var work = new CssValueWork(default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action serialize = () => CssTextAlignPropertyParser.Serialize("left", "auto", new CssValueWork(cancellation.Token));
        serialize.Should().Throw<OperationCanceledException>();
        CssTextAlignPropertyParser.Serialize("left", "auto", work).Should().Be("left");
    }
}
