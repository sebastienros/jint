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
    public void ADisabledTransitionAtAnyColdCheckpointIsPublishedFromTheCurrentAuthority()
    {
        var (baselineDocument, baselineOwner) = Owner("link");
        NativeCssStyleSheets.Install(baselineDocument, baselineOwner, "@import 'b.css';", "", "", _work);
        var checkpoints = 0;
        NativeCssStyleSheets.EnsureSheet(NativeCssStyleSheets.CaptureImportSource(baselineDocument, baselineOwner, _work)!,
            new CssValueWork(default, () => checkpoints++));
        for (var transition = 1; transition <= checkpoints; transition++)
        {
            var (document, owner) = Owner("link");
            NativeCssStyleSheets.Install(document, owner, "@import 'b.css';", "", "", _work);
            var source = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
            var checks = 0;
            var sheet = NativeCssStyleSheets.EnsureSheet(source, new CssValueWork(default, () =>
            {
                if (++checks == transition) owner.SetAttribute("disabled", "");
            }));
            NativeCssStyleSheets.IsCurrent(source).Should().BeTrue();
            sheet.Disabled.Should().BeTrue("a cold checkpoint must not publish the old disabled flag");
            source.Resource.Sheet.Should().BeSameAs(sheet);
        }
    }

    [Test]
    public void AReentrantMediaAttributeWriteIsRetriedBeforeColdPublication()
    {
        var (document, owner) = Owner();
        owner.SetAttribute("media", "screen");
        NativeCssStyleSheets.Install(document, owner, "@import 'b.css';", "", "", _work);
        var source = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
        var checks = 0;
        var sheet = NativeCssStyleSheets.EnsureSheet(source, new CssValueWork(default, () =>
        {
            // Keep changing through parsing and metadata reads; only the final stable read may publish.
            if (++checks <= 20) owner.SetAttribute("media", checks % 2 == 0 ? "print" : "screen");
        }));
        sheet.Media.MediaText.Should().Be(owner.GetAttribute("media"));
        source.Resource.MediaSource.Should().Be(owner.GetAttribute("media"));
        NativeCssStyleSheets.IsCurrent(source).Should().BeTrue();
    }

    [Test]
    public void AReentrantCssomMediaWriteWinsOverAnEarlierOwnerAttributeRead()
    {
        var (baselineDocument, baselineOwner) = Owner();
        NativeCssStyleSheets.Install(baselineDocument, baselineOwner, "@import 'b.css';", "", "", _work);
        var baseline = NativeCssStyleSheets.CaptureImportSource(baselineDocument, baselineOwner, _work)!;
        NativeCssStyleSheets.EnsureSheet(baseline, _work);
        baselineOwner.SetAttribute("media", "print");
        var checkpoints = 0;
        NativeCssStyleSheets.EnsureSheet(baseline, new CssValueWork(default, () => checkpoints++));

        var (document, owner) = Owner();
        NativeCssStyleSheets.Install(document, owner, "@import 'b.css';", "", "", _work);
        var source = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
        var retained = NativeCssStyleSheets.EnsureSheet(source, _work);
        owner.SetAttribute("media", "print");
        var checks = 0;
        NativeCssStyleSheets.EnsureSheet(source, new CssValueWork(default, () =>
        {
            // The producer's last callback precedes its media commit; the next checkpoint is
            // the enclosing operation's final proof. That intervening CSSOM write must survive.
            if (++checks == checkpoints - 1) retained.Media.SetMediaText("speech");
        })).Should().BeSameAs(retained);
        retained.Media.MediaText.Should().Be("speech");
        source.Resource.MediaSource.Should().Be("print");
        NativeCssStyleSheets.IsCurrent(source).Should().BeTrue();
    }

    [Test]
    public void MetadataInterruptionDoesNotReplayACompletedReplacementOverALaterCssomEdit()
    {
        var (document, owner) = Owner();
        NativeCssStyleSheets.Install(document, owner, "p{color:red}", "https://css.test/a.css", "https://css.test/", _work);
        var sheet = NativeCssStyleSheets.EnsureSheet(NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!, _work);
        var previous = sheet.Rules[0];
        NativeCssStyleSheets.Install(document, owner, "p{color:blue}", "https://css.test/b.css", "https://css.test/other/", _work);
        var source = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
        CssRule? edited = null;
        var failure = new InvalidOperationException("metadata interrupted after replacement committed");
        Assert.Throws<InvalidOperationException>(() => NativeCssStyleSheets.EnsureSheet(source, new CssValueWork(default, () =>
        {
            if (ReferenceEquals(sheet.Rules[0], previous)) return;
            sheet.InsertRule("span{color:green}", 1);
            edited = sheet.Rules[1];
            throw failure;
        })))!.Should().BeSameAs(failure);
        source.Resource.Replaced.Should().BeFalse();
        sheet.Attachment.SourceUrl!.AbsoluteUri.Should().Be("https://css.test/b.css");
        sheet.Attachment.BaseUrl!.AbsoluteUri.Should().Be("https://css.test/other/");
        NativeCssStyleSheets.Get(document, _work).Single().Sheet.Should().BeSameAs(sheet);
        sheet.Rules.Count.Should().Be(2);
        sheet.Rules[1].Should().BeSameAs(edited);
    }

    [Test]
    public void ConnectivityWalkChargesDeepOwnersAndObservesCancellationInsideTheWalk()
    {
        var (document, owner) = DeepOwner(8192);
        NativeCssStyleSheets.Install(document, owner, "@import 'b.css';", "", "", _work);
        var source = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        Assert.Throws<OperationCanceledException>(() => NativeCssStyleSheets.CaptureImportConnectivity(source,
            new CssValueWork(cancellation.Token, () =>
            {
                if (++checks == 3) cancellation.Cancel();
            })));
        checks.Should().Be(3, "entry and two 4096-node chunks must poll before the terminal callback");
        source.Resource.Sheet.Should().BeNull();
    }

    [Test]
    public void ConnectivityRetriesAReentrantRemovalAndKeepsItsFinalWitnessConstantTime()
    {
        var (document, owner) = DeepOwner(8192);
        NativeCssStyleSheets.Install(document, owner, "@import 'b.css';", "", "", _work);
        var source = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
        var checks = 0;
        var witness = NativeCssStyleSheets.CaptureImportConnectivity(source, new CssValueWork(default, () =>
        {
            if (++checks == 2) owner.ParentNode!.RemoveChild(owner);
        }));
        witness.Connected.Should().BeFalse();
        witness.IsCurrent.Should().BeTrue();
        var completedChecks = checks;
        var reusable = true;
        for (var i = 0; i < 20000; i++) reusable &= witness.IsCurrent;
        reusable.Should().BeTrue();
        checks.Should().Be(completedChecks);
        document.DocumentElement!.AppendChild(owner);
        witness.IsCurrent.Should().BeFalse();
        NativeCssStyleSheets.CaptureImportConnectivity(source, _work).Connected.Should().BeTrue();
    }

    [Test]
    public void InlineSourceArrivalsInvalidateEqualFinalTextWithoutInvalidatingOtherOwners()
    {
        var (document, owner) = Owner();
        var text = document.CreateTextNode("@import 'b.css';");
        owner.AppendChild(text);
        NativeCssStyleSheets.Install(document, owner, text.Data, "", "", _work);
        var source = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
        var other = document.CreateElement("style");
        owner.ParentNode!.AppendChild(other);
        NativeCssStyleSheets.Install(document, other, "p{color:red}", "", "", _work);
        NativeCssStyleSheets.InvalidateImportSourceAtArrival(document, other);
        NativeCssStyleSheets.IsCurrent(source).Should().BeTrue();
        text.Data = "p{color:blue}";
        NativeCssStyleSheets.InvalidateImportSourceAtArrival(document, owner);
        var intermediate = NativeCssStyleSheets.CaptureImportSource(document, owner, _work)!;
        text.Data = source.Source;
        NativeCssStyleSheets.InvalidateImportSourceAtArrival(document, owner);
        NativeCssStyleSheets.IsCurrent(source).Should().BeFalse();
        NativeCssStyleSheets.IsCurrent(intermediate).Should().BeFalse();
        source.Resource.Source.Should().Be(source.Source, "arrival invalidation must not pretend deferred installation already ran");
        source.Resource.Sheet.Should().BeNull();
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

    private static (Document Document, Element Owner) Owner(string tagName = "style")
    {
        var document = Document.CreateHtml();
        var owner = document.CreateElement(tagName);
        if (tagName == "link") owner.SetAttribute("rel", "stylesheet");
        var root = document.CreateElement("div");
        document.AppendChild(root);
        root.AppendChild(owner);
        return (document, owner);
    }

    private static (Document Document, Element Owner) DeepOwner(int depth)
    {
        var (document, owner) = Owner();
        var parent = document.DocumentElement!;
        for (var i = 0; i < depth; i++)
        {
            var child = document.CreateElement("div");
            parent.AppendChild(child);
            parent = child;
        }
        parent.AppendChild(owner);
        return (document, owner);
    }
}
