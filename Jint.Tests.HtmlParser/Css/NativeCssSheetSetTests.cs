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
    public void PreparedOpenStylesDoNotAssociateThroughDocumentOrShadowReads()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var style = document.CreateElement("style");
        style.SetAttribute("title", "completed");
        var text = document.CreateTextNode("div {display:block;");
        style.AppendChild(text);
        root.AppendChild(style);
        var shadow = ShadowTree.Attach(root, new(ShadowRootMode.Open), default);
        var shadowStyle = document.CreateElement("style");
        shadowStyle.AppendChild(document.CreateTextNode("span {display:block}"));
        shadow.AppendChild(shadowStyle);
        var work = new CssValueWork(default);
        var resource = NativeCssStyleSheets.PrepareOwner(document, style, work)!;
        NativeCssStyleSheets.PrepareOwner(document, shadowStyle, work);
        var sets = NativeCssStyleSheets.SetsOf(document);
        NativeCssStyleSheets.Get(document, work, includeShadow: true).Should().BeEmpty();
        NativeCssStyleSheets.Get(shadow, work).Should().BeEmpty();
        NativeCssStyleSheets.AssociatedOwner(style, work).Should().BeNull();
        sets.NamesOf(work).Should().BeEmpty();
        sets.Preferred(work).Should().BeEmpty();
        resource.Associated.Should().BeFalse();
        resource.Sheet.Should().BeNull();
        text.Data += "background:red}";
        NativeCssStyleSheets.Get(document, work).Should().BeEmpty();
        // The simulated parser completion installs source without parsing pending property values.
        NativeCssStyleSheets.Install(document, style, text.Data, "", "", work);
        resource.Sheet.Should().BeNull();
        sets.Preferred(work).Should().Be("completed");
        sets.NamesOf(work).Should().Equal("completed");
        NativeCssStyleSheets.Get(document, work).Count.Should().Be(1);
        NativeCssStyleSheets.Get(shadow, work).Should().BeEmpty();
        NativeCssStyleSheets.Install(document, shadowStyle, "span {display:block}", "", "", work);
        NativeCssStyleSheets.Get(shadow, work).Count.Should().Be(1);
        NativeCssStyleSheets.Get(document, work).Count.Should().Be(1);
    }

    [Test]
    public void CssomEditsRetainIdentityUntilExplicitStyleLifecycleReplacement()
    {
        var document = Document.CreateHtml();
        var style = document.CreateElement("style");
        document.AppendChild(style);
        var text = document.CreateTextNode("div {display:block}");
        style.AppendChild(text);
        var work = new CssValueWork(default);
        NativeCssStyleSheets.Install(document, style, text.Data, "", "", work);
        var retained = NativeCssStyleSheets.Get(document, work).Single().Sheet;
        retained.InsertRule("span {display:none}", 1);
        NativeCssStyleSheets.Get(document, work).Single().Sheet.Should().BeSameAs(retained);
        text.Data = "div {display:none}";
        NativeCssStyleSheets.Get(document, work).Single().Sheet.Should().BeSameAs(retained);
        retained.Rules.Count.Should().Be(2);
        NativeCssStyleSheets.DisassociateOwner(document, style, work);
        NativeCssStyleSheets.Get(document, work).Should().BeEmpty();
        retained.Attachment.OwnerNode.Should().BeNull();
        NativeCssStyleSheets.Install(document, style, text.Data, "", "", work);
        var replacement = NativeCssStyleSheets.Get(document, work).Single().Sheet;
        replacement.Should().NotBeSameAs(retained);
        replacement.Rules.Count.Should().Be(1);
        replacement.Rules[0].CssText.Should().Contain("display: none");
    }

    [Test]
    public void RootListsExcludeNestedShadowAndTemplateContentAndRecreateStyleAssociations()
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
        NativeCssStyleSheets.Install(document, style, "span {display:block}", "", "", work);
        var nestedHost = document.CreateElement("section");
        shadow.AppendChild(nestedHost);
        var nested = ShadowTree.Attach(nestedHost, new(ShadowRootMode.Open), default);
        var nestedStyle = document.CreateElement("style");
        nested.AppendChild(nestedStyle);
        NativeCssStyleSheets.Install(document, nestedStyle, "", "", "", work);
        var template = document.CreateElement("template");
        shadow.AppendChild(template);
        template.TemplateContent!.AppendChild(document.CreateElement("style"));
        NativeCssStyleSheets.Get(document, work).Should().BeEmpty();
        NativeCssStyleSheets.SetsOf(document).NamesOf(work).Should().BeEmpty();
        var retained = NativeCssStyleSheets.Get(shadow, work).Single().Sheet;
        NativeCssStyleSheets.DisassociateOwner(document, style, work);
        NativeCssStyleSheets.DisassociateOwner(document, nestedStyle, work);
        host.ParentNode!.RemoveChild(host);
        NativeCssStyleSheets.Get(shadow, work).Should().BeEmpty();
        retained.Attachment.OwnerNode.Should().BeNull();
        document.AppendChild(host);
        NativeCssStyleSheets.Install(document, style, "span {display:block}", "", "", work);
        NativeCssStyleSheets.Get(shadow, work).Single().Sheet.Should().NotBeSameAs(retained);
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

    [Test]
    public void SetNamesUseCurrentDomOrderEvenWhenFetchesAssociateInReverseOrder()
    {
        var document = Document.CreateHtml();
        var work = new CssValueWork(default);
        var root = document.CreateElement("html");
        document.AppendChild(root);
        var a = Link("a");
        var b = Link("b");
        root.AppendChild(a);
        root.AppendChild(b);
        NativeCssStyleSheets.PrepareOwner(document, a, work);
        NativeCssStyleSheets.PrepareOwner(document, b, work);
        NativeCssStyleSheets.Install(document, b, "", "", "", work);
        NativeCssStyleSheets.Install(document, a, "", "", "", work);
        var sets = NativeCssStyleSheets.SetsOf(document);
        sets.NamesOf(work).Should().Equal("a", "b");
        root.InsertBefore(b, a);
        sets.NamesOf(work).Should().Equal("b", "a");
        sets.Preferred(work).Should().Be("b");

        Element Link(string title)
        {
            var link = document.CreateElement("link");
            link.SetAttribute("rel", "stylesheet");
            link.SetAttribute("title", title);
            return link;
        }
    }

    [Test]
    public void ImmediateDisabledTransitionsCannotOverwriteANewerCssomAssignment()
    {
        var document = Document.CreateHtml();
        var link = document.CreateElement("link");
        link.SetAttribute("rel", "stylesheet");
        document.AppendChild(link);
        var work = new CssValueWork(default);
        NativeCssStyleSheets.Install(document, link, "", "", "", work);
        var sheet = NativeCssStyleSheets.Get(document, work).Single().Sheet;
        link.SetAttribute("disabled", "");
        sheet.Disabled.Should().BeTrue();
        sheet.Disabled = false;
        NativeCssStyleSheets.Get(document, work).Single().Sheet.Disabled.Should().BeFalse();
        link.RemoveAttribute("disabled");
        sheet.Disabled.Should().BeFalse();
        sheet.Disabled = true;
        NativeCssStyleSheets.Get(document, work).Single().Sheet.Disabled.Should().BeTrue();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReentrantDisabledTransitionsPreserveRemovalHistoryInEitherRegistrationOrder(bool cssFirst)
    {
        var document = Document.CreateHtml();
        var link = document.CreateElement("link");
        link.SetAttribute("rel", "stylesheet");
        document.AppendChild(link);
        var work = new CssValueWork(default);
        if (cssFirst) NativeCssStyleSheets.PrepareOwner(document, link, work);
        using var earlier = new MutationSubscription();
        earlier.Observe(link, new MutationObserverOptions { Attributes = true, AttributeFilter = ["disabled"] });
        var reentered = false;
        earlier.PendingRecord = pending =>
        {
            pending.TakeRecords();
            if (reentered) return;
            reentered = true;
            link.RemoveAttribute("disabled");
            link.SetAttribute("disabled", "");
        };
        var resource = NativeCssStyleSheets.PrepareOwner(document, link, work)!;
        NativeCssStyleSheets.Install(document, link, "", "", "", work);
        var sheet = NativeCssStyleSheets.Get(document, work).Single().Sheet;
        link.SetAttribute("disabled", "");
        sheet.Disabled.Should().BeTrue();
        resource.ExplicitlyEnabled.Should().BeTrue();
        // A later CSSOM assignment stays authoritative; no query replays the attribute.
        sheet.Disabled = false;
        NativeCssStyleSheets.Get(document, work).Single().Sheet.Disabled.Should().BeFalse();
    }

    [Test]
    public void LightweightLinkHistoryPrecedesEligibilityAndSuccessfulFetchAssociation()
    {
        var document = Document.CreateHtml();
        var link = document.CreateElement("link");
        document.AppendChild(link);
        var work = new CssValueWork(default);
        NativeCssStyleSheets.AssociateOwner(document, link, work);
        link.SetAttribute("disabled", "");
        link.RemoveAttribute("disabled");
        link.SetAttribute("rel", "alternate stylesheet");
        link.SetAttribute("title", "alternate");
        var sets = NativeCssStyleSheets.SetsOf(document);
        NativeCssStyleSheets.AssociateOwner(document, link, work);
        sets.NamesOf(work).Should().BeEmpty();
        NativeCssStyleSheets.Get(document, work).Should().BeEmpty();
        NativeCssStyleSheets.Install(document, link, "", "", "", work);
        sets.NamesOf(work).Should().Equal("alternate");
        sets.Preferred(work).Should().Be("alternate");
        NativeCssStyleSheets.Get(document, work).Single().Sheet.Disabled.Should().BeFalse();
    }

    [Test]
    public void AlternateLinksJoinAnAlreadySelectedOrPreferredSet()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        document.AppendChild(root);
        var work = new CssValueWork(default);
        var sets = NativeCssStyleSheets.SetsOf(document);
        sets.SetSelected("b", work);
        var selected = Link("b");
        NativeCssStyleSheets.Install(document, selected, "", "", "", work);
        NativeCssStyleSheets.Get(document, work).Single().Sheet.Disabled.Should().BeFalse();
        sets.Preferred(work).Should().BeEmpty();
        sets.Last(work).Should().Be("b");
        var fresh = Document.CreateHtml();
        var freshRoot = fresh.CreateElement("html");
        fresh.AppendChild(freshRoot);
        var preferred = fresh.CreateElement("link");
        preferred.SetAttribute("rel", "alternate stylesheet");
        preferred.SetAttribute("title", "a");
        freshRoot.AppendChild(preferred);
        NativeCssStyleSheets.SetDefaultStyle(fresh, "a", work);
        NativeCssStyleSheets.Install(fresh, preferred, "", "", "", work);
        NativeCssStyleSheets.Get(fresh, work).Single().Sheet.Disabled.Should().BeFalse();

        Element Link(string title)
        {
            var link = document.CreateElement("link");
            link.SetAttribute("rel", "alternate stylesheet");
            link.SetAttribute("title", title);
            root.AppendChild(link);
            return link;
        }
    }

    [Test]
    public void ExplicitlyEnabledLinkHistorySurvivesAdoptionAndReturnToItsOriginalDocument()
    {
        var original = Document.CreateHtml();
        var destination = Document.CreateHtml();
        var originalRoot = original.CreateElement("html");
        var destinationRoot = destination.CreateElement("html");
        original.AppendChild(originalRoot);
        destination.AppendChild(destinationRoot);
        var link = original.CreateElement("link");
        originalRoot.AppendChild(link);
        var work = new CssValueWork(default);
        NativeCssStyleSheets.PrepareOwner(original, link, work);
        link.SetAttribute("disabled", "");
        link.RemoveAttribute("disabled");
        NativeCssStyleSheets.DisassociateOwner(original, link, work);
        destination.AdoptNode(link);
        destinationRoot.AppendChild(link);
        link.SetAttribute("rel", "alternate stylesheet");
        link.SetAttribute("title", "enabled");
        NativeCssStyleSheets.Install(destination, link, "", "", "", work);
        NativeCssStyleSheets.SetsOf(destination).Preferred(work).Should().Be("enabled");
        NativeCssStyleSheets.Get(destination, work).Single().Sheet.Disabled.Should().BeFalse();
        NativeCssStyleSheets.DisassociateOwner(destination, link, work);
        original.AdoptNode(link);
        originalRoot.AppendChild(link);
        NativeCssStyleSheets.Install(original, link, "", "", "", work);
        NativeCssStyleSheets.SetsOf(original).Preferred(work).Should().Be("enabled");
        NativeCssStyleSheets.Get(original, work).Single().Sheet.Disabled.Should().BeFalse();
    }

    [Test]
    public void InterruptedAssociationLeavesHistoryUnpublishedAndCanRetry()
    {
        var baseline = Setup();
        var checks = 0;
        NativeCssStyleSheets.Install(baseline.Document, baseline.Style, "", "", "", new CssValueWork(default, () => checks++));
        var pending = Setup();
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        Assert.Throws<OperationCanceledException>(() => NativeCssStyleSheets.Install(pending.Document, pending.Style, "", "", "",
            new CssValueWork(cancellation.Token, () => { if (++calls == checks - 1) cancellation.Cancel(); })));
        var work = new CssValueWork(default);
        var sets = NativeCssStyleSheets.SetsOf(pending.Document);
        sets.Preferred(work).Should().BeEmpty();
        sets.NamesOf(work).Should().BeEmpty();
        NativeCssStyleSheets.Install(pending.Document, pending.Style, "", "", "", work);
        sets.Preferred(work).Should().Be("a");
        sets.NamesOf(work).Should().Equal("a");

        static (Document Document, Element Style) Setup()
        {
            var document = Document.CreateHtml();
            var style = document.CreateElement("style");
            style.SetAttribute("title", "a");
            document.AppendChild(style);
            NativeCssStyleSheets.PrepareOwner(document, style, new CssValueWork(default));
            return (document, style);
        }
    }

    [Test]
    public void PostcommitInstallationCancellationRetainsCoherentAssociationHistory()
    {
        var baseline = Setup();
        var checks = 0;
        NativeCssStyleSheets.Install(baseline.Document, baseline.Style, "", "", "", new CssValueWork(default, () => checks++));
        var pending = Setup();
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        Assert.Throws<OperationCanceledException>(() => NativeCssStyleSheets.Install(pending.Document, pending.Style, "", "", "",
            new CssValueWork(cancellation.Token, () => { if (++calls == checks) cancellation.Cancel(); })));
        var work = new CssValueWork(default);
        var sets = NativeCssStyleSheets.SetsOf(pending.Document);
        sets.Preferred(work).Should().Be("a");
        sets.NamesOf(work).Should().Equal("a");
        NativeCssStyleSheets.Get(pending.Document, work).Single().Sheet.Attachment.OwnerNode.Should().BeSameAs(pending.Style);

        static (Document Document, Element Style) Setup()
        {
            var document = Document.CreateHtml();
            var style = document.CreateElement("style");
            style.SetAttribute("title", "a");
            document.AppendChild(style);
            NativeCssStyleSheets.PrepareOwner(document, style, new CssValueWork(default));
            return (document, style);
        }
    }

    [Test]
    public void ReentrantHistoryAndDirectSheetFlagChangesVetoStaleSelection()
    {
        var document = Document.CreateHtml();
        var work = new CssValueWork(default);
        Style(document, "a", work);
        Style(document, "b", work);
        var sets = NativeCssStyleSheets.SetsOf(document);
        var changed = false;
        Assert.Throws<InvalidOperationException>(() => sets.SetSelected("a", new CssValueWork(default, () =>
        {
            if (changed) return;
            changed = true;
            sets.SetDefaultStyle("b", work);
        })));
        sets.Last(work).Should().BeNull();
        sets.Preferred(work).Should().Be("b");
        var sheet = NativeCssStyleSheets.Get(document, work)[0].Sheet;
        var checks = 0;
        sets.SetSelected("b", new CssValueWork(default, () => checks++));
        sets.SetSelected("a", work);
        var calls = 0;
        Assert.Throws<InvalidOperationException>(() => sets.SetSelected("b", new CssValueWork(default, () =>
        {
            if (++calls == checks) sheet.Disabled = true;
        })));
        sets.Last(work).Should().Be("a");
        sheet.Disabled.Should().BeTrue();
    }

    [Test]
    public void RemovedOwnerHistoryDoesNotAddToLiveReadWork()
    {
        var document = Document.CreateHtml();
        var work = new CssValueWork(default);
        Style(document, "survivor", work);
        var sets = NativeCssStyleSheets.SetsOf(document);
        var before = Checks();
        for (var i = 0; i < 1000; i++)
        {
            var removed = Style(document, "removed", work);
            NativeCssStyleSheets.DisassociateOwner(document, removed, work);
            removed.ParentNode!.RemoveChild(removed);
        }
        Checks().Should().Be(before);

        int Checks()
        {
            var calls = 0;
            sets.NamesOf(new CssValueWork(default, () => calls++)).Should().Equal("survivor");
            return calls;
        }
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
        NativeCssStyleSheets.Install(document, style, "div {display:block;background:red}", "", "", work);
        return style;
    }
}
