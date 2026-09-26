#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Serialization;

namespace Jint.Tests.HtmlParser.Serialization;

[TestFixture]
public sealed class HtmlSerializationOptionsTests
{
    [Test]
    public void RootsAreSnapshottedDeduplicatedByIdentityAndReadOnly()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Closed), default);
        var source = new List<ShadowRoot> { root, root };
        var options = new HtmlSerializationOptions(true, true, source);
        source.Clear();
        options.ScriptingEnabled.Should().BeTrue();
        options.SerializableShadowRoots.Should().BeTrue();
        options.ShadowRoots.Should().HaveCount(1);
        options.ShadowRoots[0].Should().BeSameAs(root);
        Assert.Throws<NotSupportedException>(() => ((IList<ShadowRoot>) options.ShadowRoots).Add(root));
        Assert.Throws<ArgumentException>(() => _ = new HtmlSerializationOptions(shadowRoots: new ShadowRoot[] { null! }));
    }
}
