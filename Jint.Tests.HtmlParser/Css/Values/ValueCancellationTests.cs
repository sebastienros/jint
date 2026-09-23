using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Values;

[TestFixture]
public sealed class ValueCancellationTests
{
    [Test]
    public void PreCanceledWorkCannotProduceAnAtom()
    {
        var values = MarkupParser.ParseCssComponentValues("1px");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            CssPrimitiveParser.ParseNumericAtom(values, new CssValueWork(cancellation.Token)));
    }

    [Test]
    public void NumericComparisonPollsAfterC1ScanningHasFinished()
    {
        var exponent = new string('9', 12000);
        var left = Number("1e" + exponent);
        var right = Number("1e" + exponent[..^1] + "8");
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++checks == 3) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => left.CompareTo(right, work));
        checks.Should().Be(3);
    }

    [Test]
    public void ParsingLongDecodedTextPollsDuringTheValueOperation()
    {
        var values = MarkupParser.ParseCssComponentValues(new string('a', 12000));
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++checks == 2) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => CssPrimitiveParser.ParseIdentifier(values, work));
        checks.Should().Be(2);
    }

    [Test]
    public void ChildOperationsShareOneCadence()
    {
        var values = MarkupParser.ParseCssComponentValues(new string('a', 3000));
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++checks == 5) cancellation.Cancel();
        });
        CssPrimitiveParser.ParseIdentifier(values, work).IsMatch.Should().BeTrue();
        Assert.Throws<OperationCanceledException>(() => CssPrimitiveParser.ParseIdentifier(values, work));
        checks.Should().Be(5);
    }

    private static CssNumber Number(string source) =>
        CssPrimitiveParser.ParseNumericAtom(MarkupParser.ParseCssComponentValues(source), new CssValueWork(default)).Value.Number;
}
