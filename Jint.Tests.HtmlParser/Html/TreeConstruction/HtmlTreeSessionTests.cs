#nullable enable
using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public class HtmlTreeSessionTests
{
    [Test]
    public void InvalidTargetsAndLifecycleAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new HtmlParserSession(null!));
        Assert.Throws<ArgumentException>(() => new HtmlParserSession(Document.CreateXml()));
        var nonempty = Document.CreateHtml();
        nonempty.AppendChild(nonempty.CreateComment("existing"));
        Assert.Throws<ArgumentException>(() => new HtmlParserSession(nonempty));
        nonempty.FirstChild.Should().BeOfType<Comment>();

        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Drive(0, CancellationToken.None));
        session.AppendInput(string.Empty);
        session.Drive(1, CancellationToken.None).Kind.Should().BeOneOf(HtmlParseStepKind.NeedInput, HtmlParseStepKind.Yielded);
        session.AppendInput(string.Empty, isFinal: true);
        Assert.Throws<InvalidOperationException>(() => session.AppendInput("later"));
        HtmlParseStep result;
        do { result = session.Drive(100, CancellationToken.None); } while (result.Kind == HtmlParseStepKind.Yielded);
        result.Kind.Should().Be(HtmlParseStepKind.Complete);
        session.Drive(1, CancellationToken.None).Kind.Should().Be(HtmlParseStepKind.Complete);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Drive(0, CancellationToken.None));
        Assert.Throws<InvalidOperationException>(() => session.AppendInput(string.Empty));
    }

    [Test]
    public void PreCancellationTerminatesWithoutTreeWork()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<p>text", isFinal: true);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.Drive(10, canceled.Token));
        document.FirstChild.Should().BeNull();
        Assert.Throws<InvalidOperationException>(() => session.Drive(10, CancellationToken.None));
        Assert.Throws<InvalidOperationException>(() => session.AppendInput(""));
    }

    [Test]
    public void InputAndTokenBoundsPropagateAndTerminate()
    {
        var inputSession = new HtmlParserSession(Document.CreateHtml(), new HtmlParseOptions
        {
            Limits = new ParseLimits { MaxInputCharacters = 3 }
        });
        inputSession.AppendInput("abc");
        Assert.That(Assert.Throws<ParseLimitException>(() => inputSession.AppendInput("d"))!.Kind,
            Is.EqualTo(ParseLimitKind.InputCharacters));
        Assert.Throws<InvalidOperationException>(() => inputSession.Drive(1, CancellationToken.None));

        var tokenSession = new HtmlParserSession(Document.CreateHtml(), new HtmlParseOptions
        {
            Limits = new ParseLimits { MaxTokenCharacters = 3 }
        });
        tokenSession.AppendInput("<long>", isFinal: true);
        Assert.That(Assert.Throws<ParseLimitException>(() =>
        {
            while (true) tokenSession.Drive(1, CancellationToken.None);
        })!.Kind, Is.EqualTo(ParseLimitKind.TokenCharacters));
        Assert.Throws<InvalidOperationException>(() => tokenSession.AppendInput(""));
    }

    [Test]
    public void CharacterRunCrossesInitialHeadAndBodyWithinOneToken()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput(" \tX", isFinal: true);
        HtmlParseStep step;
        do { step = session.Drive(1, CancellationToken.None); } while (step.Kind == HtmlParseStepKind.Yielded);
        step.Kind.Should().Be(HtmlParseStepKind.Complete);
        document.DocumentElement!.FirstChild!.ChildCount.Should().Be(0);
        ((Text) document.DocumentElement.LastChild!.FirstChild!).Data.Should().Be("X");
    }

    [Test]
    public void NewlineIgnoreConsumesOnlyTheFirstFollowingLf()
    {
        foreach (var tag in new[] { "pre", "listing", "textarea" })
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput("<" + tag + ">");
            for (var i = 0; i < 500; i++)
                if (session.Drive(1, CancellationToken.None).Kind == HtmlParseStepKind.NeedInput) break;
            session.AppendInput("\n\nX</" + tag + ">", isFinal: true);
            HtmlParseStep step;
            do { step = session.Drive(1, CancellationToken.None); } while (step.Kind == HtmlParseStepKind.Yielded);
            step.Kind.Should().Be(HtmlParseStepKind.Complete);
            var element = (Element) document.DocumentElement!.LastChild!.FirstChild!;
            ((Text) element.FirstChild!).Data.Should().Be("\nX");
        }
    }

    [Test]
    public void DeepPopContinuesAcrossQuotaOneDrives()
    {
        const int depth = 1024;
        var source = "<p>" + string.Concat(Enumerable.Repeat("<x>", depth)) + "<div>tail";
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput(source, isFinal: true);
        HtmlParseStep result;
        var turns = 0;
        long previousWork = -1;
        do
        {
            result = session.Drive(1, CancellationToken.None);
            session.WorkCount.Should().BeGreaterThan(previousWork);
            previousWork = session.WorkCount;
            if (++turns > source.Length * 20) throw new InvalidOperationException("Deep close stalled.");
        } while (result.Kind == HtmlParseStepKind.Yielded);
        result.Kind.Should().Be(HtmlParseStepKind.Complete);
        var body = document.DocumentElement!.LastChild!;
        ((Element) body.FirstChild!).LocalName.Should().Be("p");
        ((Element) body.LastChild!).LocalName.Should().Be("div");
        ((Text) body.LastChild!.FirstChild!).Data.Should().Be("tail");
    }

    [Test]
    public void UnobservedTextWorkGrowsLinearly()
    {
        static long Count(int length)
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput("<p>" + new string('x', length), isFinal: true);
            HtmlParseStep step;
            do { step = session.Drive(3, CancellationToken.None); } while (step.Kind == HtmlParseStepKind.Yielded);
            step.Kind.Should().Be(HtmlParseStepKind.Complete);
            var p = (Element) document.DocumentElement!.LastChild!.FirstChild!;
            p.ChildCount.Should().Be(1);
            ((Text) p.FirstChild!).Data.Length.Should().Be(length);
            return session.WorkCount;
        }
        var small = Count(16_384);
        var large = Count(32_768);
        large.Should().BeLessThan(small * 3);
    }

    [Test]
    public void DiagnosticsUseTokenOffsetsAndSelfClosingOnce()
    {
        var collector = new ParseDiagnosticCollector();
        collector.Add("old", 999);
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document, new HtmlParseOptions { Diagnostics = collector });
        collector.Items.Should().BeEmpty();
        session.AppendInput("<!doctype html><body><div/><br/>", isFinal: true);
        HtmlParseStep step;
        do { step = session.Drive(100, CancellationToken.None); } while (step.Kind == HtmlParseStepKind.Yielded);
        step.Kind.Should().Be(HtmlParseStepKind.Complete);
        collector.Items.Count(item => item.Code == "html/tree-unacknowledged-self-closing-flag").Should().Be(1);
        collector.Items.Single(item => item.Code == "html/tree-unacknowledged-self-closing-flag").Offset
            .Should().Be(21);
    }
}
