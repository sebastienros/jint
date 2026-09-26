using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css;

[TestFixture]
public sealed class NativeCssSheetSetTests
{
    [Test]
    public void MetadataAssociationPreservesPreferredHistoryBeforeAnyCssDemand()
    {
        var document = Document.CreateHtml();
        var work = new CssValueWork(default);
        var sets = NativeCssStyleSheets.SetsOf(document);
        sets.Preferred(work).Should().BeEmpty();
        sets.Last(work).Should().BeNull();
        var first = Style(document, "first", work);
        first.ParentNode!.RemoveChild(first);
        Style(document, "second", work);
        sets.Preferred(work).Should().Be("first");
        sets.NamesOf(work).Should().Equal("second");
        sets.Selected(work).Should().BeEmpty();
    }

    [Test]
    public void SelectionSharesMaterializedFlagsAndDistinguishesPartialAndMultipleGroups()
    {
        var document = Document.CreateHtml();
        var work = new CssValueWork(default);
        Style(document, "a", work);
        Style(document, "a", work);
        Style(document, "b", work);
        Style(document, "", work);
        var sets = NativeCssStyleSheets.SetsOf(document);
        sets.NamesOf(work).Should().Equal("a", "b");
        sets.Selected(work).Should().Be("a");
        var sheets = NativeCssStyleSheets.Get(document, work);
        sheets[1].Sheet.Disabled = true;
        sets.Selected(work).Should().BeEmpty();
        sheets[2].Sheet.Disabled = false;
        sets.Selected(work).Should().BeNull();
        sets.SetSelected(null, work);
        sets.Last(work).Should().BeNull();
        sets.SetSelected("b", work);
        sheets[0].Sheet.Disabled.Should().BeTrue();
        sheets[1].Sheet.Disabled.Should().BeTrue();
        sheets[2].Sheet.Disabled.Should().BeFalse();
        sheets[3].Sheet.Disabled.Should().BeFalse();
        sets.Selected(work).Should().Be("b");
        sets.Last(work).Should().Be("b");
        sets.EnableForSet("a", work);
        sets.Selected(work).Should().Be("a");
        sets.Last(work).Should().Be("b");
        sets.Preferred(work).Should().Be("a");
        sets.SetSelected("unknown", work);
        sets.Selected(work).Should().BeEmpty();
        sets.Last(work).Should().Be("unknown");
        sheets[3].Sheet.Disabled.Should().BeFalse();
    }

    [Test]
    public void DefaultStyleAndLateAssociationFollowPreferredAndLastIndependently()
    {
        var document = Document.CreateHtml();
        var work = new CssValueWork(default);
        Style(document, "a", work);
        Style(document, "b", work);
        var sets = NativeCssStyleSheets.SetsOf(document);
        sets.SetDefaultStyle("b", work);
        sets.Selected(work).Should().Be("b");
        sets.Last(work).Should().BeNull();
        var sheets = NativeCssStyleSheets.Get(document, work);
        sheets[1].Sheet.Disabled = true;
        sets.SetDefaultStyle("b", work);
        sheets[1].Sheet.Disabled.Should().BeTrue();
        sets.SetSelected("a", work);
        sets.SetDefaultStyle("b", work);
        sets.Selected(work).Should().Be("a");
        sets.Preferred(work).Should().Be("b");
        Style(document, "a", work);
        Style(document, "b", work);
        var late = NativeCssStyleSheets.Get(document, work);
        late[2].Sheet.Disabled.Should().BeFalse();
        late[3].Sheet.Disabled.Should().BeTrue();
        sets.EnableForSet("", work);
        sets.Selected(work).Should().BeEmpty();
        sets.Last(work).Should().Be("a");
    }

    [Test]
    public void LiveTitlesEligibilityAndMediaDoNotCreateAnotherSheetGraph()
    {
        var document = Document.CreateHtml();
        var work = new CssValueWork(default);
        var style = Style(document, "a", work);
        style.SetAttribute("media", "print");
        var sets = NativeCssStyleSheets.SetsOf(document);
        sets.NamesOf(work).Should().Equal("a");
        style.SetAttribute("title", "renamed");
        sets.NamesOf(work).Should().Equal("renamed");
        style.SetAttribute("type", "text/plain");
        sets.NamesOf(work).Should().BeEmpty();
        style.RemoveAttribute("type");
        sets.NamesOf(work).Should().Equal("renamed");
        sets.SetSelected("renamed", work);
        NativeCssStyleSheets.Get(document, work)[0].Sheet.Disabled.Should().BeFalse();
    }

    [Test]
    public void RootListsExcludeNestedShadowAndTemplateContentAndKeepReconnectedIdentity()
    {
        var document = Document.CreateHtml();
        var work = new CssValueWork(default);
        var host = document.CreateElement("div");
        document.AppendChild(host);
        var shadow = ShadowTree.Attach(host, new(ShadowRootMode.Open), default);
        var style = document.CreateElement("style");
        style.SetAttribute("title", "shadow-only");
        style.AppendChild(document.CreateTextNode("span {display:block}"));
        shadow.AppendChild(style);
        NativeCssStyleSheets.AssociateOwner(document, style, work);
        var nestedHost = document.CreateElement("b");
        shadow.AppendChild(nestedHost);
        var nested = ShadowTree.Attach(nestedHost, new(ShadowRootMode.Open), default);
        var nestedStyle = document.CreateElement("style");
        nested.AppendChild(nestedStyle);
        var template = document.CreateElement("template");
        shadow.AppendChild(template);
        template.TemplateContent!.AppendChild(document.CreateElement("style"));
        NativeCssStyleSheets.Get(document, work).Should().BeEmpty();
        NativeCssStyleSheets.SetsOf(document).NamesOf(work).Should().BeEmpty();
        var retained = NativeCssStyleSheets.Get(shadow, work).Single().Sheet;
        host.ParentNode!.RemoveChild(host);
        NativeCssStyleSheets.Get(shadow, work).Should().BeEmpty();
        document.AppendChild(host);
        NativeCssStyleSheets.Get(shadow, work).Single().Sheet.Should().BeSameAs(retained);
    }

    [Test]
    public void LongNamesAndManyAssociationsPollAndCancellationDoesNotPublishLast()
    {
        var document = Document.CreateHtml();
        var work = new CssValueWork(default);
        for (var i = 0; i < 100; i++) Style(document, new string('x', 1000) + i, work);
        var sets = NativeCssStyleSheets.SetsOf(document);
        var before = sets.Last(work);
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var bounded = new CssValueWork(cancellation.Token, () => { if (++checks == 10) cancellation.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => sets.SetSelected(new string('x', 1000) + 99, bounded));
        sets.Last(work).Should().Be(before);
        var renamed = false;
        var first = (Element) document.DocumentElement!.FirstChild!;
        var reentrant = new CssValueWork(default, () =>
        {
            if (renamed) return;
            renamed = true;
            first.SetAttribute("title", "changed");
        });
        Assert.Throws<InvalidOperationException>(() => sets.NamesOf(reentrant));
    }

    private static Element Style(Document document, string title, CssValueWork work)
    {
        var style = document.CreateElement("style");
        style.SetAttribute("title", title);
        // Association and set metadata do not request pending background value validation.
        style.AppendChild(document.CreateTextNode("div {display:block;background:red}"));
        var container = document.DocumentElement;
        if (container is null) { container = document.CreateElement("html"); document.AppendChild(container); }
        container.AppendChild(style);
        NativeCssStyleSheets.AssociateOwner(document, style, work);
        return style;
    }
}
