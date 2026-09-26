#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Css.Selectors;

[TestFixture]
public sealed class SelectorElementStateWorkTests
{
    private static CompiledSelector Parse(string text) => SelectorCompiler.Compile(text, null, default);

    [Test]
    public void ManyShortCheckableReadsChargeEveryProducerUnitToTheSharedSelectorCounter()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var probe = new HtmlCheckedWorkProbe();
        document.CheckedWorkProbe = probe;
        for (var i = 0; i < 64; i++)
        {
            var input = document.CreateElement("input");
            var attributes = Enumerable.Range(0, 128).Select(j => new ParserAttribute(null, "data-" + j, null, "x"))
                .Append(new(null, "type", null, "text")).ToArray();
            input.InitializeParsedAttributes(attributes, default);
            root.AppendChild(input);
        }
        var before = probe.Units;
        var checks = 0;
        var work = new SelectorMatchWork(root, default, () => checks++);
        SelectorMatcher.QuerySelectorAll(Parse(":checked"), root, default, ref work).Should().BeEmpty();
        (probe.Units - before).Should().BeGreaterThan(64 * 128);
        // Each short helper has a boundary callback. Attribute scans must also contribute
        // enough work to trigger shared-counter polls between those boundaries.
        checks.Should().BeGreaterThan(64 + 20);
    }

    [Test]
    public void RadioGroupPreparationIsCancellableThroughTheSelectorInvocation()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        for (var i = 0; i < 2000; i++)
        {
            var input = document.CreateElement("input");
            input.InitializeParsedAttributes(new ParserAttribute[]
            {
                new(null, "type", null, "radio"), new(null, "name", null, "group")
            }, default);
            root.AppendChild(input);
        }
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new SelectorMatchWork(root, cancellation.Token, () =>
        {
            if (++checks == 3) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.QuerySelectorAll(Parse(":indeterminate"), root, default, ref work));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LongSelectSizeAndSvgAncestorReadsUseSelectorCancellation(bool svg)
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("select");
        if (svg)
        {
            element = document.CreateElementNS(Namespaces.Svg, "a");
            element.SetAttribute("href", "");
            for (var i = 0; i < 3000; i++)
            {
                var parent = document.CreateElementNS(Namespaces.Svg, "g");
                parent.AppendChild(element);
                element = parent;
            }
            while (element.FirstChild is Element child) element = child;
        }
        else element.SetAttribute("size", new string('0', 10000) + "1");
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new SelectorMatchWork(element, cancellation.Token, () =>
        {
            if (++checks == 2) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.Matches(Parse(svg ? ":any-link" : ":closed"), element, null, default, ref work));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CheckableProducerCallbacksRejectMutationAndAdoptionBeforePublishing(bool adopt)
    {
        var document = Document.CreateHtml();
        var input = document.CreateElement("input");
        input.InitializeParsedAttributes(new ParserAttribute[]
        {
            new(null, "type", null, "checkbox"), new(null, "checked", null, "")
        }, default);
        var other = Document.CreateHtml();
        var work = new SelectorMatchWork(input, default, () =>
        {
            if (adopt) other.AdoptNode(input);
            else input.SetAttribute("data-mutated", "");
        });
        Assert.Throws<InvalidOperationException>(() =>
            SelectorMatcher.Matches(Parse(":checked"), input, null, default, ref work))!
            .Message.Should().Be(SelectorMatchWork.Invalidated);
        var fresh = new SelectorMatchWork(input, default);
        SelectorMatcher.Matches(Parse(":checked"), input, null, default, ref fresh).Should().BeTrue();
    }
}
