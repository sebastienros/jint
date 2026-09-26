using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeTitleWorkTests
{
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
