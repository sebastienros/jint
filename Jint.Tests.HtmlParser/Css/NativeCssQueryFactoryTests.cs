#nullable enable
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssQueryFactoryTests
{
    [Test]
    public void RealmFreeFactorySharesActualSourcesAndUaWithoutComputingUnrequestedValues()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("section");
        document.AppendChild(root);
        root.SetAttribute("class", "gone");
        root.SetAttribute("style", "white-space:pre;color:red");
        var owner = document.CreateElement("style");
        root.AppendChild(owner);
        var work = new CssValueWork(default);
        NativeCssStyleSheets.Install(document, owner, ".gone {display:none;background:red}", "", "", work);
        var resource = NativeCssStyleSheets.PrepareOwner(document, owner, work)!;
        resource.Sheet.Should().BeNull();
        var diagnostics = new NativeCssQueryDiagnostics();
        var input = NativeCssStyleSheets.CreateQuery(document, new CssMediaEnvironment { Scripting = "none" },
            new SelectorEnvironment(document, null, null, null), work, diagnostics: diagnostics);
        var record = diagnostics.Queries.Single();
        record.StatePublications.Should().Be(0);
        record.ComputedPublications.Should().BeEmpty();
        input.Query.GetProperty(root, "display", ref input.Matching).Text.Should().Be("none");
        input.Query.GetProperty(root, "white-space", ref input.Matching).Text.Should().Be("pre");
        input.Query.GetProperty(root, "color", ref input.Matching).Text.Should().Be("red");
        record.ComputedPublications.Should().NotContainKey("background");
        record.ComputedPublications.Should().NotContainKey("background-color");
    }

    [Test]
    public void RealmFreeSelectorWorkUsesTheOperationTokenAndCheckpoint()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        document.AppendChild(target);
        var name = new string('x', 20000);
        target.SetAttribute("class", name);
        var owner = document.CreateElement("style");
        target.AppendChild(owner);
        NativeCssStyleSheets.Install(document, owner, "." + name + " {display:none}", "", "", new CssValueWork(default));
        using var cancellation = new CancellationTokenSource();
        var input = NativeCssStyleSheets.CreateQuery(document, new CssMediaEnvironment { Scripting = "none" },
            new SelectorEnvironment(document, null, null, null), new CssValueWork(cancellation.Token), cancellation.Cancel);
        Assert.Throws<OperationCanceledException>(() => input.Query.GetProperty(target, "display", ref input.Matching));
        cancellation.IsCancellationRequested.Should().BeTrue();
    }
}
