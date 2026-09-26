using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class CreationRealmWorkTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void SubtreeRecordingChecksWideNodesAndAttributesBeforeAnyAdoption(bool attributes)
    {
        using var fixture = DomTestFixture.Create("");
        var document = fixture.Document;
        var root = document.CreateElement("div");
        for (var i = 0; i < 4096; i++)
        {
            if (attributes) root.SetAttribute("a" + i, "value");
            else root.AppendChild(document.CreateTextNode("value"));
        }
        var revision = document.MutationStamp;
        var checks = 0;
        var realm = DomRealm.Of(fixture.Engine);
        Assert.Throws<OperationCanceledException>(() => realm.RecordSubtree(root, _ =>
        {
            if (++checks == 3) throw new OperationCanceledException();
        }, CancellationToken.None));
        checks.Should().Be(3);
        root.OwnerDocument.Should().BeSameAs(document);
        document.MutationStamp.Should().Be(revision);
        realm.RecordSubtree(root);
    }

    [Test]
    public void RecordingHonorsCancellationRaisedByItsCheckpoint()
    {
        using var fixture = DomTestFixture.Create("");
        using var cancellation = new CancellationTokenSource();
        var root = fixture.Document.CreateElement("div");
        Assert.Throws<OperationCanceledException>(() => DomRealm.Of(fixture.Engine)
            .RecordSubtree(root, _ => cancellation.Cancel(), cancellation.Token));
        root.OwnerDocument.Should().BeSameAs(fixture.Document);
    }
}
