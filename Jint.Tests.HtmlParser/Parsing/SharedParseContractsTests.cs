#nullable enable
using System.Collections;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Parsing;

public class SharedParseContractsTests
{
    [Test]
    public void DefaultOptionsAndLimitsAreUnboundedAndReusable()
    {
        var first = new CssParseOptions();
        var second = new CssParseOptions();
        first.Limits.Should().BeSameAs(ParseLimits.Unbounded);
        second.Limits.Should().BeSameAs(ParseLimits.Unbounded);
        first.Diagnostics.Should().BeNull();
        ParseLimits.Unbounded.MaxInputCharacters.Should().Be(0);
        ParseLimits.Unbounded.MaxTokenCharacters.Should().Be(0);
        ParseLimits.Unbounded.MaxNestingDepth.Should().Be(0);

        var bounded = new ParseLimits
        {
            MaxInputCharacters = 5,
            MaxTokenCharacters = 3,
            MaxNestingDepth = 2
        };
        var options = new CssParseOptions { Limits = bounded };
        options.Limits.Should().BeSameAs(bounded);
        bounded.MaxInputCharacters.Should().Be(5);
        bounded.MaxTokenCharacters.Should().Be(3);
        bounded.MaxNestingDepth.Should().Be(2);
        ParseLimits.Unbounded.MaxInputCharacters.Should().Be(0);
    }

    [Test]
    public void LimitsAndCapacityRejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParseLimits { MaxInputCharacters = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParseLimits { MaxTokenCharacters = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParseLimits { MaxNestingDepth = -1 });
        Assert.Throws<ArgumentNullException>(() => new CssParseOptions { Limits = null! });
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParseDiagnosticCollector(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParseDiagnosticCollector(-1));
        new ParseLimits { MaxInputCharacters = long.MaxValue, MaxTokenCharacters = int.MaxValue,
            MaxNestingDepth = int.MaxValue }.MaxInputCharacters.Should().Be(long.MaxValue);
    }

    [Test]
    public void CollectorKeepsOrderedRecordsUntilCapacityAndResetsLiveView()
    {
        var collector = new ParseDiagnosticCollector(2);
        var view = collector.Items;
        view.Should().BeSameAs(collector.Items);
        collector.Capacity.Should().Be(2);
        collector.IsTruncated.Should().BeFalse();
        collector.Add("css/first", 0);
        collector.Add("html/second", 12);
        collector.Add("css/dropped", 13);
        collector.Add("css/dropped-again", 14);
        view.Select(item => item.Code).Should().Equal("css/first", "html/second");
        view.Select(item => item.Offset).Should().Equal(0, 12);
        collector.IsTruncated.Should().BeTrue();

        collector.Clear();
        view.Should().BeEmpty();
        collector.IsTruncated.Should().BeFalse();
        collector.Add("css/next", 4);
        view.Should().ContainSingle();
        view[0].Code.Should().Be("css/next");
    }

    [Test]
    public void CollectorViewCannotBeCastToWritableBackingCollection()
    {
        var collector = new ParseDiagnosticCollector();
        collector.Capacity.Should().Be(100);
        collector.Add("css/unexpected-eof", 7);
        var view = collector.Items;
        view.Should().NotBeOfType<List<ParseDiagnostic>>();
        Assert.Throws<NotSupportedException>(() => ((IList)view).Add(default(ParseDiagnostic)));
        Assert.Throws<NotSupportedException>(() => ((IList<ParseDiagnostic>)view).Clear());
        view.Should().ContainSingle();
    }

    [Test]
    public void DiagnosticsAndExceptionsExposeStableFields()
    {
        var empty = default(ParseDiagnostic);
        empty.Code.Should().BeEmpty();
        empty.Offset.Should().Be(0);
        var diagnostic = new ParseDiagnostic("html/unexpected-null-character", 9);
        diagnostic.Code.Should().Be("html/unexpected-null-character");
        diagnostic.Offset.Should().Be(9);

        var limit = new ParseLimitException(ParseLimitKind.TokenCharacters, 4, 5);
        limit.Kind.Should().Be(ParseLimitKind.TokenCharacters);
        limit.Limit.Should().Be(4);
        limit.Observed.Should().Be(5);
        limit.Message.Should().Contain("TokenCharacters").And.Contain("4").And.Contain("5");

        var error = new CssParseException("css/extra-input", 11);
        error.Code.Should().Be("css/extra-input");
        error.Offset.Should().Be(11);
        error.Message.Should().Contain("css/extra-input").And.Contain("11");
    }

}
