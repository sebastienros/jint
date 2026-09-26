#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public partial class HtmlTreeConstructionTests
{
    [TestCase("<body><?marker name='part'?><template for=part><p>new</p></template>",
        "<html><head></head><body><p>new</p></body></html>")]
    [TestCase("<body><?start name='part'?><b>old</b><?end?><template for=part><p>new</p></template>tail",
        "<html><head></head><body><p>new</p>tail</body></html>")]
    [TestCase("<body><?start name='part'?><b>old</b><template for=part><p>new</p></template>",
        "<html><head></head><body><p>new</p></body></html>")]
    [TestCase("<body><?start name='part'?>old<?start name='other'?>nested<?end?>old<?end?><template for=part>new</template>",
        "<html><head></head><body>new</body></html>")]
    [TestCase("<head><?marker name='part'?></head><body><template for=part><meta name=x></template>",
        "<html><head><meta></meta></head><body></body></html>")]
    [TestCase("<body><div><?marker name='part'?></div><template for=part>new</template>",
        "<html><head></head><body><div>new</div></body></html>")]
    [TestCase("<body><?marker name='part'?><template for=part>new",
        "<html><head></head><body>new<?marker name='part'?></body></html>")]
    [TestCase("<body><?start name='part'?>old<?end?><template for=part>new",
        "<html><head></head><body><?start name='part'?>new<?end ?></body></html>")]
    public void ContentPatchingUsesCurrentMarkersScopeAndCloseVersusEof(string source, string expected)
    {
        foreach (var quota in new[] { 1, 3, 100000 })
        {
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            SerializeWithTemplateContents(parsed.Document).Should().Be(expected);
        }
    }

    [TestCase("<?Marker name='part'?>", "part")]
    [TestCase("<?marker Name='part'?>", "part")]
    [TestCase("<?marker name='Part'?>", "part")]
    [TestCase("<?marker name='part' name='part'?>", "part")]
    [TestCase("<?marker name='part'?>", "")]
    [TestCase("<?marker name='part' broken?>", "part")]
    public void MarkerMismatchOrInvalidPseudoAttributesUseOrdinaryFallback(string marker, string name)
    {
        var parsed = Parse("<body>" + marker + "<template for='" + name + "'>new</template>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var template = (Element) parsed.Document.DocumentElement!.LastChild!.LastChild!;
        template.LocalName.Should().Be("template");
        ((Text) template.TemplateContent!.FirstChild!).Data.Should().Be("new");
        template.TemplatePatchState.Should().BeNull();
    }

    [Test]
    public void PatchTraversalDoesNotEnterUnrelatedTemplateContentsOrShadowTrees()
    {
        var parsed = Parse("<body><template><?marker name='part'?></template><template for=part>new</template>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var body = (Element) parsed.Document.DocumentElement!.LastChild!;
        var first = (Element) body.FirstChild!;
        first.TemplateContent!.FirstChild.Should().BeOfType<ProcessingInstruction>();
        ((Element) body.LastChild!).TemplateContent!.FirstChild.Should().BeOfType<Text>().Which.Data.Should().Be("new");
    }

    [Test]
    public void TemplateContentScopePatchesTheRealInertFragment()
    {
        var parsed = Parse("<template><?start name='part'?>old<?end?><template for=part><b>new</b></template></template>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var template = (Element) parsed.Document.DocumentElement!.FirstChild!.FirstChild!;
        var content = template.TemplateContent!;
        content.ChildCount.Should().Be(1);
        var inserted = (Element) content.FirstChild!;
        inserted.LocalName.Should().Be("b");
        inserted.OwnerDocument.Should().BeSameAs(content.OwnerDocument);
        inserted.FirstChild!.OwnerDocument.Should().BeSameAs(content.OwnerDocument);
    }

    [Test]
    public void FragmentScopeAndEveryShortInputSplitUseTheReturnedFragment()
    {
        const string source = "<?start name='part'?>old<?end?><template for=part><b>new</b></template>";
        for (var split = 0; split <= source.Length; split++)
        {
            foreach (var quota in new[] { 1, 3, 100000 })
            {
                var owner = Document.CreateHtml();
                var context = owner.CreateElement("div");
                var session = HtmlParserSession.CreateFragment(context);
                session.AppendInput(source[..split]);
                DrainPatchToNeedInput(session, quota);
                session.AppendInput(source[split..], isFinal: true);
                DrainPatchToComplete(session, quota);
                var fragment = session.Fragment!;
                fragment.ChildCount.Should().Be(1);
                var inserted = (Element) fragment.FirstChild!;
                inserted.LocalName.Should().Be("b");
                inserted.OwnerDocument.Should().BeSameAs(owner);
                ((Text) inserted.FirstChild!).Data.Should().Be("new");
                context.ChildCount.Should().Be(0);
            }
        }
    }

    [Test]
    public void MovedEndMarkerChangesInsertionToAppendAndClosureRemovesItsLiveParent()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<body><?start name='part'?>old<?end?><template for=part><b>first</b>");
        DrainPatchToNeedInput(session, 1);
        var body = (Element) document.DocumentElement!.LastChild!;
        var start = (ProcessingInstruction) body.FirstChild!;
        var end = (ProcessingInstruction) body.LastChild!;
        var moved = document.CreateElement("div");
        moved.AppendChild(end);
        session.AppendInput("<i>second</i></template>", isFinal: true);
        DrainPatchToComplete(session, 1);
        start.ParentNode.Should().BeNull();
        end.ParentNode.Should().BeNull();
        moved.ChildCount.Should().Be(0);
        body.ChildCount.Should().Be(2);
        ((Element) body.FirstChild!).LocalName.Should().Be("b");
        ((Element) body.LastChild!).LocalName.Should().Be("i");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeRemovalNotificationFailureOrCancellationLeavesCoherentCommitAndTerminalSession(bool cancel)
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<body><?start name='part'?><span>old</span><?end?>");
        DrainPatchToNeedInput(session, 1);
        var body = (Element) document.DocumentElement!.LastChild!;
        var removed = (Element) body.FirstChild!.NextSibling!;
        var range = document.CreateRange();
        range.SelectNodeContents(new(removed.FirstChild!));
        using var subscription = range.ObserveChanges(document);
        using var cancellation = new CancellationTokenSource();
        var failure = new InvalidOperationException("native notification");
        document.PendingRangeChanges = () =>
        {
            if (cancel) cancellation.Cancel();
            else throw failure;
        };
        session.AppendInput("<template for=part>new</template>", isFinal: true);
        Exception? caught = null;
        for (var turn = 0; turn < 10000 && caught is null; turn++)
        {
            try { session.Drive(1, cancellation.Token); }
            catch (Exception exception) { caught = exception; }
        }
        if (cancel) caught.Should().BeOfType<OperationCanceledException>();
        else caught.Should().BeSameAs(failure);
        removed.ParentNode.Should().BeNull();
        body.ChildCount.Should().Be(2);
        range.Start.Container.Node.Should().BeSameAs(body);
        document.RangeOperationDepth.Should().Be(0);
        Assert.Throws<InvalidOperationException>(() => session.Drive(100000, default));
        Assert.Throws<InvalidOperationException>(() => session.AppendInput("again"));
        body.ChildCount.Should().Be(2);
    }

    [Test]
    public void CancellationBeforeRemovalCommitPreservesThatSubtree()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<body><?start name='part'?><span>old</span><?end?>");
        DrainPatchToNeedInput(session, 1);
        var body = (Element) document.DocumentElement!.LastChild!;
        var old = (Element) body.FirstChild!.NextSibling!;
        session.AppendInput("<template for=part>new</template>", isFinal: true);
        var reachedRemoval = false;
        for (var turn = 0; turn < 10000; turn++)
        {
            session.Drive(1, default);
            var builder = BuilderOf(session);
            var operation = typeof(HtmlTreeBuilder).GetField("_templateOperation",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(builder);
            if (operation?.GetType().GetField("Stage",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(operation)?.ToString() != "Remove") continue;
            reachedRemoval = true;
            break;
        }
        reachedRemoval.Should().BeTrue();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.Drive(100000, cancellation.Token));
        old.ParentNode.Should().BeSameAs(body);
        body.ChildCount.Should().Be(3);
    }

    [Test]
    public void CapturedSiblingMovedAfterFirstCommitIsRemovedFromItsLiveParent()
    {
        var document = Document.CreateHtml();
        var otherDocument = Document.CreateHtml();
        var otherParent = otherDocument.CreateElement("div");
        var session = new HtmlParserSession(document);
        session.AppendInput("<body><?start name='part'?><span>one</span><span>two</span><?end?>");
        DrainPatchToNeedInput(session, 1);
        var body = (Element) document.DocumentElement!.LastChild!;
        var first = (Element) body.FirstChild!.NextSibling!;
        var second = (Element) first.NextSibling!;
        var range = document.CreateRange();
        range.SelectNodeContents(new(first.FirstChild!));
        using var subscription = range.ObserveChanges(document);
        document.PendingRangeChanges = () =>
        {
            document.PendingRangeChanges = null;
            otherParent.AppendChild(second);
        };
        session.AppendInput("<template for=part>new</template>", isFinal: true);
        DrainPatchToComplete(session, 1);
        first.ParentNode.Should().BeNull();
        second.ParentNode.Should().BeNull();
        second.OwnerDocument.Should().BeSameAs(otherDocument);
        otherParent.ChildCount.Should().Be(0);
        body.ChildCount.Should().Be(1);
        ((Text) body.FirstChild!).Data.Should().Be("new");
    }

    [TestCase("open")]
    [TestCase("closed")]
    public void AllowedDeclarativeShadowUsesRealRootFlagsAndPrecedesPatching(string mode)
    {
        foreach (var quota in new[] { 1, 3, 100000 })
        {
            var parsed = Parse("<body><div><template shadowrootmode=" + mode +
                " for=part shadowrootdelegatesfocus shadowrootserializable shadowrootclonable shadowrootslotassignment=manual shadowrootcustomelementregistry><p>shadow</p></template><b>light</b></div>", quota,
                context: new HtmlDocumentContext(AllowDeclarativeShadowRoots: true));
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            var host = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!;
            var root = host.AttachedShadowRoot!;
            root.Should().NotBeNull();
            root.Host.Should().BeSameAs(host);
            root.Mode.Should().Be(mode == "open" ? ShadowRootMode.Open : ShadowRootMode.Closed);
            root.DelegatesFocus.Should().BeTrue();
            root.Serializable.Should().BeTrue();
            root.Clonable.Should().BeTrue();
            root.SlotAssignment.Should().Be(SlotAssignmentMode.Manual);
            root.Declarative.Should().BeTrue();
            root.AvailableToElementInternals.Should().BeTrue();
            root.KeepCustomElementRegistryNull.Should().BeTrue();
            root.CustomElementRegistry.Should().BeNull();
            root.OwnerDocument.Should().BeSameAs(parsed.Document);
            var shadow = (Element) root.FirstChild!;
            shadow.LocalName.Should().Be("p");
            shadow.OwnerDocument.Should().BeSameAs(parsed.Document);
            host.ChildCount.Should().Be(1);
            ((Element) host.FirstChild!).LocalName.Should().Be("b");
        }
    }

    [TestCase("<body><div><template shadowrootmode=open><p>x</template></div>", false, "div")]
    [TestCase("<template shadowrootmode=open><p>x</template>", true, "head")]
    [TestCase("<body><a><template shadowrootmode=open><b>x</template></a>", true, "a")]
    public void DisallowedOrInvalidDeclarativeHostsKeepOrdinaryTemplate(string source, bool allowed, string hostName)
    {
        var parsed = Parse(source, 1, context: new HtmlDocumentContext(AllowDeclarativeShadowRoots: allowed));
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var host = hostName == "head" ? (Element) parsed.Document.DocumentElement!.FirstChild! : (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!;
        host.LocalName.Should().Be(hostName);
        host.AttachedShadowRoot.Should().BeNull();
        ((Element) host.FirstChild!).TemplateContent!.FirstChild.Should().BeOfType<Element>();
    }

    [Test]
    public void ExistingDeclarativeRootIsNotClearedByASecondTemplate()
    {
        var parsed = Parse("<body><div><template shadowrootmode=open><b>first</b></template><template shadowrootmode=open><i>second</i></template></div>", 1,
            context: new HtmlDocumentContext(AllowDeclarativeShadowRoots: true));
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var host = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!;
        ((Element) host.AttachedShadowRoot!.FirstChild!).LocalName.Should().Be("b");
        var ordinary = (Element) host.FirstChild!;
        ordinary.LocalName.Should().Be("template");
        ((Element) ordinary.TemplateContent!.FirstChild!).LocalName.Should().Be("i");
    }

    [Test]
    public void EnabledFragmentDeclarativeShadowAttachesToActualContextAndKeepsRootOwnedChildren()
    {
        var owner = Document.CreateHtml();
        var host = owner.CreateElement("div");
        var session = HtmlParserSession.CreateFragment(host, allowDeclarativeShadowRoots: true);
        session.AppendInput("<template shadowrootmode=closed><span a=1>shadow</span></template>light", isFinal: true);
        DrainPatchToComplete(session, 1);
        var root = host.AttachedShadowRoot!;
        root.Should().NotBeNull();
        root.Host.Should().BeSameAs(host);
        root.Mode.Should().Be(ShadowRootMode.Closed);
        root.OwnerDocument.Should().BeSameAs(owner);
        var child = (Element) root.FirstChild!;
        child.OwnerDocument.Should().BeSameAs(owner);
        child.GetAttributeNode("a")!.OwnerDocument.Should().BeSameAs(owner);
        session.Fragment!.ChildCount.Should().Be(1);
        ((Text) session.Fragment.FirstChild!).Data.Should().Be("light");
        host.ChildCount.Should().Be(0);
    }

    [Test]
    public void DeclarativeRegistryIdentityUsesHostDocumentAndNullRegistryAttribute()
    {
        foreach (var keepNull in new[] { false, true })
        {
            var document = Document.CreateHtml();
            var registry = new CustomElementRegistryIdentity(isScoped: false);
            document.SetCustomElementRegistry(registry);
            var session = new HtmlParserSession(document, context: new HtmlDocumentContext(AllowDeclarativeShadowRoots: true));
            session.AppendInput("<body><div><template shadowrootmode=open" +
                (keepNull ? " shadowrootcustomelementregistry" : "") + "><span>x</span></template></div>", isFinal: true);
            DrainPatchToComplete(session, 1);
            var host = (Element) document.DocumentElement!.LastChild!.FirstChild!;
            if (keepNull) host.AttachedShadowRoot!.CustomElementRegistry.Should().BeNull();
            else host.AttachedShadowRoot!.CustomElementRegistry.Should().BeSameAs(registry);
            host.AttachedShadowRoot!.KeepCustomElementRegistryNull.Should().Be(keepNull);
        }
    }

    [Test]
    public void PatchSearchAndSnapshotWorkScaleLinearlyAtQuotaOne()
    {
        static string Source(int count) => "<body>" + string.Concat(Enumerable.Repeat("<div><?marker name='other'?></div>", count)) +
            "<?start name='part'?>" + string.Concat(Enumerable.Repeat("<i>old</i>", count)) + "<?end?><template for=part>new</template>";
        var smaller = Parse(Source(100), 1);
        var larger = Parse(Source(1000), 1);
        var repeated = Parse(Source(1000), 1);
        larger.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        larger.Session.WorkCount.Should().Be(repeated.Session.WorkCount);
        larger.Session.WorkCount.Should().BeLessThan(smaller.Session.WorkCount * 11);
        var body = (Element) larger.Document.DocumentElement!.LastChild!;
        body.ChildCount.Should().Be(1001);
        ((Text) body.LastChild!).Data.Should().Be("new");
    }

    private sealed class ShadowHostFacts(ShadowAttachmentContext context, Exception? failure = null) : IHtmlShadowHostContextProvider
    {
        internal int Calls;
        internal Element? LastHost;
        public ShadowAttachmentContext GetShadowAttachmentContext(Element host)
        {
            Calls++;
            LastHost = host;
            if (failure is not null) throw failure;
            return context;
        }
    }

    [Test]
    public void DeclarativeHostFactsUseCachedDisableShadowAndPermissionGates()
    {
        var disabled = new ShadowHostFacts(new ShadowAttachmentContext(null, true, true));
        var parsed = Parse("<body><custom-host><template shadowrootmode=open>x</template></custom-host>", 1,
            context: new HtmlDocumentContext(AllowDeclarativeShadowRoots: true, ShadowHostContextProvider: disabled));
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var host = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!;
        disabled.Calls.Should().Be(1);
        disabled.LastHost.Should().BeSameAs(host);
        host.AttachedShadowRoot.Should().BeNull();
        ((Element) host.FirstChild!).TemplateContent!.FirstChild.Should().BeOfType<Text>();

        var unused = new ShadowHostFacts(default, new InvalidOperationException("must not be called"));
        Parse("<body><div><template shadowrootmode=open>x</template></div>", 1,
            context: new HtmlDocumentContext(ShadowHostContextProvider: unused)).Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        unused.Calls.Should().Be(0);
        var once = new ShadowHostFacts(default);
        Parse("<body><div><template shadowrootmode=open>x</template><template shadowrootmode=open>y</template></div>", 1,
            context: new HtmlDocumentContext(AllowDeclarativeShadowRoots: true, ShadowHostContextProvider: once)).Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        once.Calls.Should().Be(1);
    }

    [Test]
    public void HostFactsFailureIsNotCaughtAsANormativeAttachmentFallback()
    {
        var document = Document.CreateHtml();
        var failure = DomException.NotSupported();
        var provider = new ShadowHostFacts(default, failure);
        var session = new HtmlParserSession(document,
            context: new HtmlDocumentContext(AllowDeclarativeShadowRoots: true, ShadowHostContextProvider: provider));
        session.AppendInput("<body><div><template shadowrootmode=open>x</template></div>", isFinal: true);
        Assert.Throws<DomException>(() => DrainPatchToComplete(session, 1)).Should().BeSameAs(failure);
        var host = (Element) document.DocumentElement!.LastChild!.FirstChild!;
        host.AttachedShadowRoot.Should().BeNull();
        host.ChildCount.Should().Be(0);
        Assert.Throws<InvalidOperationException>(() => session.Drive(100000, default));
    }

    private static void DrainPatchToNeedInput(HtmlParserSession session, int quota)
    {
        for (var turn = 0; turn < 100000; turn++)
        {
            var step = session.Drive(quota, default);
            if (step.Kind == HtmlParseStepKind.NeedInput) return;
            step.Kind.Should().Be(HtmlParseStepKind.Yielded);
        }
        Assert.Fail("Fragment did not reach its input boundary.");
    }

    private static void DrainPatchToComplete(HtmlParserSession session, int quota)
    {
        for (var turn = 0; turn < 100000; turn++)
        {
            var step = session.Drive(quota, default);
            if (step.Kind == HtmlParseStepKind.Complete) return;
            step.Kind.Should().Be(HtmlParseStepKind.Yielded);
        }
        Assert.Fail("Template parser did not complete.");
    }
}
