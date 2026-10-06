#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public sealed class HtmlStyleCompletionTests
{
    private static HtmlParseStep DriveToBoundary(HtmlParserSession session)
    {
        for (var i = 0; i < 10000; i++)
        {
            var step = session.Drive(4096, default);
            if (step.Kind != HtmlParseStepKind.Yielded) return step;
            session.TryTakeCompletedStyle(out _).Should().BeFalse("this continuation expects no undrained style completion");
        }
        throw new InvalidOperationException("Parser failed to reach a boundary.");
    }

    [TestCase("<style id=s>text</style><link id=after>", Namespaces.Html)]
    [TestCase("<svg><style id=s>text</style></svg><div id=after>", Namespaces.Svg)]
    [TestCase("<svg><style id=s>text<div id=after>", Namespaces.Svg)]
    public void StyleClosureYieldsBeforeFollowingTokenAndDrainChargesWork(string source, string expectedNamespace)
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document, enableScriptRequests: true);
        session.AppendInput(source, isFinal: true);
        session.Drive(65536, default).Kind.Should().Be(HtmlParseStepKind.Yielded);
        var style = document.GetElementById("s")!;
        style.NamespaceUri.Should().Be(expectedNamespace);
        session.IsStyleOpen(style).Should().BeFalse();
        document.GetElementById("after").Should().BeNull();
        var before = session.WorkCount;
        session.Drive(65536, default).Kind.Should().Be(HtmlParseStepKind.Yielded);
        session.WorkCount.Should().Be(before, "the host must drain completion before parsing resumes");
        session.TryTakeCompletedStyle(out var completed).Should().BeTrue();
        completed.Should().BeSameAs(style);
        session.WorkCount.Should().Be(before + 1);
        session.TryTakeCompletedStyle(out completed).Should().BeFalse();
        completed.Should().BeNull();
        DriveToBoundary(session).Kind.Should().Be(HtmlParseStepKind.Complete);
        document.GetElementById("after").Should().NotBeNull();
    }

    [TestCase("<style id=s>text", Namespaces.Html)]
    [TestCase("<svg><style id=s>text", Namespaces.Svg)]
    [TestCase("<svg><style id=s />", Namespaces.Svg)]
    public void EofAndSelfClosingStylesProduceOneCompletion(string source, string expectedNamespace)
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document, enableScriptRequests: true);
        session.AppendInput(source, isFinal: true);
        session.Drive(65536, default).Kind.Should().Be(HtmlParseStepKind.Yielded);
        session.TryTakeCompletedStyle(out var style).Should().BeTrue();
        style!.NamespaceUri.Should().Be(expectedNamespace);
        session.IsStyleOpen(style).Should().BeFalse();
        session.TryTakeCompletedStyle(out _).Should().BeFalse();
        DriveToBoundary(session).Kind.Should().Be(HtmlParseStepKind.Complete);
        session.TryTakeCompletedStyle(out _).Should().BeFalse();
    }

    [Test]
    public void OpenStyleRemainsOpenAcrossNeedInputEvenWithScriptingDisabled()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document, new HtmlParseOptions { ScriptingEnabled = false },
            enableScriptRequests: true);
        session.AppendInput("<style id=s>first");
        DriveToBoundary(session).Kind.Should().Be(HtmlParseStepKind.NeedInput);
        var style = document.GetElementById("s")!;
        session.IsStyleOpen(style).Should().BeTrue();
        session.TryTakeCompletedStyle(out _).Should().BeFalse();
        session.AppendInput(" second</style><meta id=after>", isFinal: true);
        session.Drive(65536, default).Kind.Should().Be(HtmlParseStepKind.Yielded);
        session.IsStyleOpen(style).Should().BeFalse();
        session.TryTakeCompletedStyle(out var completed).Should().BeTrue();
        completed.Should().BeSameAs(style);
        document.GetElementById("after").Should().BeNull();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AbortOrCanceledDrainClearsCompletionAndTerminalOpenFacts(bool cancel)
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document, enableScriptRequests: true);
        session.AppendInput("<style id=s>text</style><style id=later>", isFinal: true);
        session.Drive(65536, default).Kind.Should().Be(HtmlParseStepKind.Yielded);
        var style = document.GetElementById("s")!;
        if (cancel)
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => session.TryTakeCompletedStyle(out _, cancellation.Token));
        }
        else session.Abort();
        session.TryTakeCompletedStyle(out _).Should().BeFalse();
        session.IsStyleOpen(style).Should().BeFalse();
        document.GetElementById("later").Should().BeNull();
        Assert.Throws<InvalidOperationException>(() => session.Drive(4096, default));
    }

    [Test]
    public void MultipleStylesPauseInSourceOrderBeforeLaterMetadataTokens()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document, enableScriptRequests: true);
        session.AppendInput("<style id=first></style><style id=second></style><meta id=after>", isFinal: true);
        foreach (var id in new[] { "first", "second" })
        {
            session.Drive(65536, default).Kind.Should().Be(HtmlParseStepKind.Yielded);
            session.TryTakeCompletedStyle(out var style).Should().BeTrue();
            style!.GetAttribute("id").Should().Be(id);
            document.GetElementById("after").Should().BeNull();
        }
        DriveToBoundary(session).Kind.Should().Be(HtmlParseStepKind.Complete);
        document.GetElementById("after").Should().NotBeNull();
    }

    [Test]
    public void StandaloneAndFragmentParsingNeverCreateCompletionQueues()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<style>text</style><svg><style>svg text</style></svg>", isFinal: true);
        DriveToBoundary(session).Kind.Should().Be(HtmlParseStepKind.Complete);
        session.TryTakeCompletedStyle(out _).Should().BeFalse();
        var fragment = HtmlParserSession.CreateFragment(document.CreateElement("div"));
        fragment.AppendInput("<style>fragment text</style>", isFinal: true);
        DriveToBoundary(fragment).Kind.Should().Be(HtmlParseStepKind.Complete);
        fragment.TryTakeCompletedStyle(out _).Should().BeFalse();
        var hostWithoutStyles = new HtmlParserSession(Document.CreateHtml(), enableScriptRequests: true);
        hostWithoutStyles.AppendInput("<div>text</div><math><style>math text</style></math>", isFinal: true);
        DriveToBoundary(hostWithoutStyles).Kind.Should().Be(HtmlParseStepKind.Complete);
        hostWithoutStyles.TryTakeCompletedStyle(out _).Should().BeFalse();
        // Inspect only the cold allocation invariant, not parser semantic internals.
        var builderField = typeof(HtmlParserSession).GetField("_builder",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var queueField = typeof(HtmlParserSession).Assembly.GetType("Jint.HtmlParser.Html.HtmlTreeBuilder")!
            .GetField("_completedStyles", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        queueField.GetValue(builderField.GetValue(session)).Should().BeNull();
        queueField.GetValue(builderField.GetValue(fragment)).Should().BeNull();
        queueField.GetValue(builderField.GetValue(hostWithoutStyles)).Should().BeNull();
    }
}
