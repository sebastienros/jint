using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssImportSourceTests
{
    private readonly CssValueWork _work = new(default);

    [Test]
    public void CaptureDoesNotAssociateAnUnloadedOwnerOrMaterializeImportFreeSource()
    {
        var (document, owner) = Owner();
        var resource = NativeCssStyleSheets.PrepareOwner(document, owner, _work)!;
        NativeCssStyleSheets.CaptureImportSource(document, owner, _work).Should().BeNull();
        resource.Associated.Should().BeFalse();
        NativeCssStyleSheets.Install(document, owner, "p{color:red}", "https://css.test/a.css", "https://css.test/", _work);
        var source = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
        NativeCssStyleSheets.MayContainImport(source, _work).Should().BeFalse();
        NativeCssStyleSheets.MayContainImport(source, _work).Should().BeFalse();
        source.Sheet.Should().BeNull();
        resource.Sheet.Should().BeNull();
    }

    [Test]
    public void EveryInstallInvalidatesEvenEqualTextAndUrlsAndRefreshesTheHint()
    {
        var (document, owner) = Owner();
        NativeCssStyleSheets.Install(document, owner, "p{color:red}", "https://css.test/a.css", "https://css.test/", _work);
        var before = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
        NativeCssStyleSheets.MayContainImport(before, _work).Should().BeFalse();
        NativeCssStyleSheets.Install(document, owner, before.Source, "https://css.test/a.css", "https://css.test/", _work);
        var equal = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
        NativeCssStyleSheets.IsCurrent(before).Should().BeFalse();
        equal.SourceGeneration.Should().NotBeSameAs(before.SourceGeneration);
        NativeCssStyleSheets.Install(document, owner, "@import 'b.css';", "https://css.test/a.css", "https://css.test/", _work);
        var after = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
        NativeCssStyleSheets.IsCurrent(equal).Should().BeFalse();
        NativeCssStyleSheets.MayContainImport(after, _work).Should().BeTrue();
        after.Sheet.Should().BeNull();
    }

    [Test]
    public void ImportsAndCssomDemandShareOneModelAndUnrelatedMutationsKeepTheSourceCurrent()
    {
        var (document, owner) = Owner();
        NativeCssStyleSheets.Install(document, owner, "@import 'b.css'; p{color:red}", "https://css.test/a.css", "https://css.test/", _work);
        var source = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
        var sheet = NativeCssStyleSheets.EnsureSheet(source, _work);
        NativeCssStyleSheets.Get(document, _work).Single().Sheet.Should().BeSameAs(sheet);
        owner.SetAttribute("media", "print");
        owner.ParentNode!.AppendChild(document.CreateElement("aside"));
        ((CssStyleRule) sheet.Rules[1]).Style.SetProperty("color", "blue");
        NativeCssStyleSheets.IsCurrent(source).Should().BeTrue();
        NativeCssStyleSheets.EnsureSheet(source, _work).Should().BeSameAs(sheet);
        sheet.Media.MediaText.Should().Be("print");
        sheet.Attachment.BaseUrl!.AbsoluteUri.Should().Be("https://css.test/");
    }

    [Test]
    public void ReentrantMaterializationCannotOverwriteThePublishedSheet()
    {
        var (document, owner) = Owner();
        NativeCssStyleSheets.Install(document, owner, "@import 'b.css';", "", "", _work);
        var source = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
        CssStyleSheet? published = null;
        var reentrant = new CssValueWork(default, () =>
        {
            if (published is not null) return;
            published = NativeCssStyleSheets.EnsureSheet(NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!, _work);
        });
        Assert.Throws<CssImportSourceStaleException>(() => NativeCssStyleSheets.EnsureSheet(source, reentrant));
        published.Should().NotBeNull();
        NativeCssStyleSheets.Get(document, _work).Single().Sheet.Should().BeSameAs(published);
    }

    [Test]
    public void AReplacementFromAParsingCheckpointCannotPublishTheOldSource()
    {
        var (document, owner) = Owner();
        NativeCssStyleSheets.Install(document, owner, "@import 'old.css';", "", "", _work);
        var source = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
        var replaced = false;
        var reentrant = new CssValueWork(default, () =>
        {
            if (replaced) return;
            replaced = true;
            NativeCssStyleSheets.Install(document, owner, "@import 'new.css';", "", "", _work);
        });
        Assert.Throws<CssImportSourceStaleException>(() => NativeCssStyleSheets.EnsureSheet(source, reentrant));
        source.Resource.Sheet.Should().BeNull();
        ((CssImportRule) NativeCssStyleSheets.Get(document, _work).Single().Sheet.Rules[0]).Href.Should().Be("new.css");
    }

    [Test]
    public void DisassociationAndAdoptionInvalidateTheCapturedSource()
    {
        var (document, owner) = Owner();
        NativeCssStyleSheets.Install(document, owner, "@import 'b.css';", "", "", _work);
        var source = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
        NativeCssStyleSheets.DisassociateOwner(document, owner, _work);
        NativeCssStyleSheets.IsCurrent(source).Should().BeFalse();
        NativeCssStyleSheets.CaptureImportSource(document, owner, _work).Should().BeNull();
        NativeCssStyleSheets.Install(document, owner, source.Source, "", "", _work);
        var reinstalled = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
        Document.CreateHtml().AdoptNode(owner);
        NativeCssStyleSheets.IsCurrent(reinstalled).Should().BeFalse();
    }

    private static (Document Document, Element Owner) Owner()
    {
        var document = Document.CreateHtml();
        var owner = document.CreateElement("style");
        var root = document.CreateElement("div");
        document.AppendChild(root);
        root.AppendChild(owner);
        return (document, owner);
    }
}
