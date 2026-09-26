#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssMediaListTests
{
    [TestCase("", true)]
    [TestCase("screen", true)]
    [TestCase("print", false)]
    [TestCase("not unknown", true)]
    [TestCase("unknown", false)]
    [TestCase("only SCREEN and (min-width: 40em)", true)]
    [TestCase("(width >= 1024px)", true)]
    [TestCase("(1024px <= width < 1200px)", true)]
    [TestCase("(1200px > width >= 1024px)", true)]
    [TestCase("(width < 1024px)", false)]
    [TestCase("(height:768px) and (orientation:landscape)", true)]
    [TestCase("(aspect-ratio:4/3)", true)]
    [TestCase("(aspect-ratio > 1)", true)]
    [TestCase("(aspect-ratio < 1/0)", true)]
    [TestCase("(aspect-ratio:0/0)", false)]
    [TestCase("not layer", false)]
    [TestCase("(width < = 2000px)", false)]
    [TestCase("(width </* comment */= 2000px)", true)]
    [TestCase("(resolution:96dpi)", true)]
    [TestCase("(color)", true)]
    [TestCase("(hover:hover) and (pointer:fine)", true)]
    [TestCase("(prefers-color-scheme:dark)", false)]
    [TestCase("not (prefers-reduced-motion:reduce)", true)]
    [TestCase("((width:1px) or (height:768px)) and (color:8)", true)]
    [TestCase("(unknown-feature) or (color:8)", true)]
    [TestCase("(color) or ((color) and (color) or (color))", true)]
    [TestCase("(color) or (width < = 20px)", true)]
    [TestCase("(color) or ()", true)]
    [TestCase("not ((unknown-feature) and (color:0))", true)]
    [TestCase("not (unknown-feature)", false)]
    [TestCase("(width:1024px) or (height:1px) and (color)", false)]
    [TestCase("screen and (width:1px) or (color)", false)]
    [TestCase("or and (color), print", false)]
    [TestCase("&, screen", true)]
    public void EvaluatesConditionsAgainstImmutableDeviceSnapshot(string source, bool expected)
    {
        var list = CssMediaList.Parse(source);
        list.Matches(new CssMediaEnvironment()).Should().Be(expected);
        // Serialized query text must retain the same evaluation after reparsing.
        CssMediaList.Parse(list.MediaText).Matches(new CssMediaEnvironment()).Should().Be(expected);
    }

    [Test]
    public void MutationsCompareParsedQueriesAndKeepFailuresAtomic()
    {
        var list = CssMediaList.Parse("SCREEN and (width: 1024px), print");
        list.MediaText.Should().Be("screen and (width: 1024px), print");
        var before = list.Stamp;
        list.AppendMedium("screen AND (width:1024px)");
        list.Stamp.Should().Be(before);
        list.AppendMedium("screen, print");
        list.Stamp.Should().Be(before);
        list.DeleteMedium("PRINT");
        list.Count.Should().Be(1);
        Assert.Throws<DomException>(() => list.DeleteMedium("print"))!.Name.Should().Be("NotFoundError");
        before = list.Stamp;
        Assert.Throws<CssIncompleteRuleGrammarException>(() => list.SetMediaText("(color-gamut:srgb)"))!.Blocker.Should().Be("R2:media-feature:color-gamut");
        list.Stamp.Should().Be(before);
        list.SetMediaText("");
        list.Count.Should().Be(0);
        list.Matches(new CssMediaEnvironment()).Should().BeTrue();
    }

    [Test]
    public void KnownPendingValuesRemainNamedCompletionFailures()
    {
        Assert.Throws<CssIncompleteRuleGrammarException>(() => CssMediaList.Parse("(width:calc(1px + 1px))"))!.Blocker.Should().Be("R2:media-value-expression");
        Assert.Throws<CssIncompleteRuleGrammarException>(() => CssMediaList.Parse("(width:1vw)"))!.Blocker.Should().Be("R2:media-length-unit:vw");
        CssMediaList.Parse("(min-orientation:portrait), (width:10bananas), (color:1px)").MediaText.Should().Be("not all, not all, not all");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => CssMediaList.Parse("screen", cancellationToken: cancellation.Token));
    }

    [Test]
    public void NumericGrammarUsesLexicalSignBeforeFiniteProjection()
    {
        CssMediaList.Parse("(width:1e-9999)").MediaText.Should().Be("not all");
        CssMediaList.Parse("(width:-1e-9999)").MediaText.Should().Be("not all");
        CssMediaList.Parse("(width:0e-9999)").MediaText.Should().Be("(width: 0)");
        CssMediaList.Parse("(aspect-ratio:-1e-9999)").MediaText.Should().Be("not all");
        CssMediaList.Parse("(aspect-ratio:1/-1e-9999)").MediaText.Should().Be("not all");
    }

    [Test]
    public void NumericSerializationUsesTheSharedCssomDecimalPolicy()
    {
        var list = CssMediaList.Parse("(width:2.9802322387695312e-8px)");
        list.MediaText.Should().Be("(width: 0px)");
        CssMediaList.Parse("(width:1.23456789px)").MediaText.Should().Be("(width: 1.234568px)");
        CssMediaList.Parse("(resolution:1e2dpi)").MediaText.Should().Be("(resolution: 100dpi)");
        CssMediaList.Parse("(aspect-ratio:1.25/2)").MediaText.Should().Be("(aspect-ratio: 1.25 / 2)");
    }

    [Test]
    public void NumericComponentScansChargeSharedWorkAndObserveCancellation()
    {
        var source = "(width:" + new string('0', 100_000) + "1px)";
        var parser = new CssSyntaxParser(source, null, default);
        var values = parser.ParseComponentValues();
        var checkpoints = 0;
        var list = CssMediaList.FromComponents(source, values, parser, new CssValueWork(default, () => checkpoints++));
        list.MediaText.Should().Be("(width: 1px)");
        checkpoints.Should().BeGreaterThan(40);
        using var cancellation = new CancellationTokenSource();
        checkpoints = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++checkpoints == 20) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => CssMediaList.FromComponents(source, values, parser, work));
    }

    [Test]
    public void DeepConditionsUseAnIterativeProgram()
    {
        var source = new string('(', 2000) + "color" + new string(')', 2000);
        var list = CssMediaList.Parse(source);
        list.Matches(new CssMediaEnvironment()).Should().BeTrue();
        list.MediaText.Should().Be(source);
        Assert.Throws<ParseLimitException>(() => CssMediaList.Parse(source,
            new CssParseOptions { Limits = new ParseLimits { MaxNestingDepth = 10 } }));
    }
}
