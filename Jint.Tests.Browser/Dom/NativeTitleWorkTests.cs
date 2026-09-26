using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeTitleWorkTests
{
    [Test]
    public void TitleReadCancelsWhileDiscoveringTheDocumentElementAfterACommentPrefix()
    {
        var document = Document.CreateHtml();
        for (var index = 0; index < 1024; index++) document.AppendChild(document.CreateComment("prefix"));
        var root = document.CreateElement("html");
        document.AppendChild(root);
        var title = document.CreateElement("title");
        root.AppendChild(title);
        title.AppendChild(document.CreateTextNode("title"));
        using var cancellation = new CancellationTokenSource();
        var cancellationUnits = 0;
        Assert.Throws<OperationCanceledException>(() => DomTitleMembers.Get(document,
            units => { if (units == 256) { cancellationUnits = units; cancellation.Cancel(); } }, cancellation.Token));
        cancellationUnits.Should().Be(256);
    }

    [Test]
    public void TitleSetterChecksCancellationAfterTheAtomicReplacement()
    {
        var title = Document.CreateHtml().CreateElement("title");
        title.AppendChild(title.OwnerDocument!.CreateTextNode("before"));
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => DomTitleMembers.SetText(title, "after",
            _ => { if (title.FirstChild is Text { Data: "after" }) cancellation.Cancel(); }, cancellation.Token));
        title.ChildCount.Should().Be(1);
        ((Text) title.FirstChild!).Data.Should().Be("after");
    }
}
