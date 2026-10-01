#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public class HtmlOptionCompletionTests
{
    [TestCase("</option></select>")]
    [TestCase("<option>other</select>")]
    [TestCase("")]
    public void ActualParserCompletionClonesSelectedOptionContents(string ending)
    {
        var document = MarkupParser.ParseHtml("<select><button><selectedcontent></selectedcontent></button><option selected><b>chosen</b> tail" + ending);
        var select = document.DocumentElement!.LastChild!.FirstChild.As<Element>();
        var content = select.FirstChild!.FirstChild.As<Element>();
        var option = select.FirstChild.NextSibling.As<Element>();
        content.ChildCount.Should().Be(2);
        content.FirstChild.As<Element>().LocalName.Should().Be("b");
        content.FirstChild!.FirstChild.As<Text>().Data.Should().Be("chosen");
        content.LastChild.As<Text>().Data.Should().Be(" tail");
        content.FirstChild.Should().NotBeSameAs(option.FirstChild);
        content.FirstChild!.OwnerDocument.Should().BeSameAs(document);
    }
    [Test]
    public void OrdinarySelectParsingKeepsEnhancedViewsCold()
    {
        var document = MarkupParser.ParseHtml("<select><option selected>chosen<option>other");
        var select = document.DocumentElement!.LastChild!.FirstChild.As<Element>();
        select.HasHtmlState.Should().BeFalse();
        foreach (var option in select.ChildNodes.Cast<Element>()) option.HasHtmlState.Should().BeFalse();
    }

    [TestCase(1)]
    [TestCase(3)]
    public void EveryInputSplitKeepsCompletionAndColdViews(int quota)
    {
        const string source = "<select><button><selectedcontent></selectedcontent></button><option selected><b>chosen</b> tail";
        for (var split = 0; split <= source.Length; split++)
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput(source[..split]);
            Drain(HtmlParseStepKind.NeedInput);
            session.AppendInput(source[split..], isFinal: true);
            Drain(HtmlParseStepKind.Complete);
            var select = document.DocumentElement!.LastChild!.FirstChild.As<Element>();
            select.HasHtmlState.Should().BeFalse();
            var option = select.FirstChild!.NextSibling.As<Element>();
            option.HasHtmlState.Should().BeFalse();
            select.FirstChild.FirstChild!.LastChild.As<Text>().Data.Should().Be(" tail");

            void Drain(HtmlParseStepKind expected)
            {
                HtmlParseStep step;
                var turns = 0;
                do
                {
                    if (++turns > 10_000) throw new InvalidOperationException("Option completion stalled.");
                    step = session.Drive(quota, default);
                } while (step.Kind == HtmlParseStepKind.Yielded);
                step.Kind.Should().Be(expected);
            }
        }
    }

    [Test]
    public void ThrowingNativeCompletionNotificationFaultsWithoutReplayingTheCommit()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<select><button><selectedcontent></selectedcontent></button><option selected>chosen");
        while (session.Drive(4096, default).Kind == HtmlParseStepKind.Yielded) { }
        var content = document.DocumentElement!.LastChild!.FirstChild!.FirstChild!.FirstChild.As<Element>();
        content.AppendChild(document.CreateTextNode("old"));
        var range = document.CreateRange();
        range.SelectNodeContents(new(content));
        using var subscription = range.ObserveChanges(document);
        var failure = new InvalidOperationException("completion notification");
        document.PendingRangeChanges = () => throw failure;
        session.AppendInput("</option>", isFinal: true);
        Assert.Throws<InvalidOperationException>(() => session.Drive(4096, default)).Should().BeSameAs(failure);
        content.FirstChild.As<Text>().Data.Should().Be("chosen");
        document.RangeOperationDepth.Should().Be(0);
        Assert.Throws<InvalidOperationException>(() => session.Drive(4096, default));
    }

}
