#nullable enable
using System.Reflection;
using Jint.HtmlParser;
using Jint.HtmlParser.Serialization;

namespace Jint.Tests.HtmlParser.Serialization;

// Authored fixtures derived from HTML Standard §13.3 (2026-09-25),
// https://html.spec.whatwg.org/multipage/parsing.html#serialising-html-fragments.
[TestFixture]
public sealed class HtmlShadowAndWorkTests
{
    [Test]
    public void TemplateContentReplacesOrdinaryChildrenAndChangesScriptingOnlyAcrossOwners()
    {
        var document = Document.CreateHtml();
        var template = document.CreateElement("template");
        template.AppendChild(document.CreateTextNode("ordinary"));
        var noscript = template.TemplateContent!.OwnerDocument!.CreateElement("noscript");
        noscript.AppendChild(noscript.OwnerDocument!.CreateTextNode("<&"));
        template.TemplateContent.AppendChild(noscript);
        var options = new HtmlSerializationOptions(scriptingEnabled: true);
        HtmlMarkupSerializer.Serialize(template, options).Should().Be("<template><noscript>&lt;&amp;</noscript></template>");
        HtmlMarkupSerializer.SerializeChildren(template, options).Should().Be("<noscript>&lt;&amp;</noscript>");
        HtmlMarkupSerializer.Serialize(template.TemplateContent, options).Should().Be("<noscript><&</noscript>");
        template.FirstChild!.ParentNode.Should().BeSameAs(template);
        template.TemplateContent.FirstChild.Should().BeSameAs(noscript);

        var nested = noscript.OwnerDocument!.CreateElement("template");
        nested.TemplateContent!.AppendChild(nested.OwnerDocument!.CreateTextNode("nested"));
        template.TemplateContent.AppendChild(nested);
        HtmlMarkupSerializer.Serialize(template, options).Should().EndWith("<template>nested</template></template>");

        var sameOwnerTemplate = document.CreateElement("template");
        var sameOwnerHost = document.CreateElement("div");
        var sameOwnerRoot = ShadowTree.Attach(sameOwnerHost, new ShadowRootInit(ShadowRootMode.Open), default);
        ShadowTree.SetDeclarativeTemplateContent(sameOwnerTemplate, sameOwnerRoot, false);
        var sameOwnerNoscript = document.CreateElement("noscript");
        sameOwnerNoscript.AppendChild(document.CreateTextNode("<&"));
        sameOwnerRoot.AppendChild(sameOwnerNoscript);
        HtmlMarkupSerializer.Serialize(sameOwnerTemplate, options)
            .Should().Be("<template><noscript><&</noscript></template>");
    }

    [Test]
    public void SelectedClosedRootHasExactMetadataBeforeLightChildrenAndNoSlotFlattening()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var registry = new CustomElementRegistryIdentity(true);
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Closed, true, true,
            SlotAssignmentMode.Manual, true), new ShadowAttachmentContext(registry, false, false));
        var slot = document.CreateElement("slot");
        shadow.AppendChild(slot);
        host.AppendChild(document.CreateTextNode("light"));
        var stamp = document.MutationStamp;
        using var subscription = document.ObserveMutations(host,
            new MutationObserverOptions { ChildList = true, Attributes = true, Subtree = true });
        HtmlMarkupSerializer.Serialize(host).Should().Be("<div>light</div>");
        var selected = new HtmlSerializationOptions(shadowRoots: new[] { shadow });
        const string wrapper = "<template shadowrootmode=\"closed\" shadowrootdelegatesfocus=\"\"" +
            " shadowrootserializable=\"\" shadowrootslotassignment=\"manual\" shadowrootclonable=\"\"" +
            " shadowrootcustomelementregistry=\"\"><slot></slot></template>";
        HtmlMarkupSerializer.Serialize(host, selected).Should().Be("<div>" + wrapper + "light</div>");
        HtmlMarkupSerializer.SerializeChildren(host, selected).Should().Be(wrapper + "light");
        HtmlMarkupSerializer.Serialize(shadow, selected).Should().Be("<slot></slot>");
        HtmlMarkupSerializer.Serialize(host, new HtmlSerializationOptions(serializableShadowRoots: true))
            .Should().Be("<div>" + wrapper + "light</div>");
        shadow.Host.Should().BeSameAs(host);
        slot.ParentNode.Should().BeSameAs(shadow);
        document.MutationStamp.Should().Be(stamp);
        subscription.TakeRecords().Should().BeEmpty();
    }

    [Test]
    public void SelectionIsReachableOnlyAndDoesNotAutomaticallySelectNestedRoots()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var outer = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var innerHost = document.CreateElement("span");
        var inner = ShadowTree.Attach(innerHost, new ShadowRootInit(ShadowRootMode.Open), default);
        inner.AppendChild(document.CreateTextNode("deep"));
        outer.AppendChild(innerHost);
        var unrelatedHost = document.CreateElement("div");
        var unrelated = ShadowTree.Attach(unrelatedHost, new ShadowRootInit(ShadowRootMode.Open), default);
        HtmlMarkupSerializer.Serialize(host, new HtmlSerializationOptions(shadowRoots: new[] { inner, unrelated }))
            .Should().Be("<div></div>");
        HtmlMarkupSerializer.Serialize(host, new HtmlSerializationOptions(shadowRoots: new[] { outer, unrelated }))
            .Should().Be("<div><template shadowrootmode=\"open\"><span></span></template></div>");
        HtmlMarkupSerializer.Serialize(host, new HtmlSerializationOptions(shadowRoots: new[] { outer, inner }))
            .Should().Be("<div><template shadowrootmode=\"open\"><span><template shadowrootmode=\"open\">deep</template></span></template></div>");
    }

    [Test]
    public void RegistryMarkerUsesNullGlobalAndScopedKindsNotIdentityEquality()
    {
        foreach (var documentRegistry in new CustomElementRegistryIdentity?[]
            { null, new(false), new(true) })
        foreach (var shadowRegistry in new CustomElementRegistryIdentity?[]
            { null, new(false), new(true) })
        {
            var document = Document.CreateHtml();
            document.SetCustomElementRegistry(documentRegistry);
            var host = document.CreateElement("div");
            var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open),
                new ShadowAttachmentContext(shadowRegistry, false, false));
            var output = HtmlMarkupSerializer.Serialize(host,
                new HtmlSerializationOptions(shadowRoots: new[] { shadow }));
            var marker = !(documentRegistry is null && shadowRegistry is null) &&
                !(documentRegistry is { IsScoped: false } && shadowRegistry is { IsScoped: false });
            output.Contains("shadowrootcustomelementregistry", StringComparison.Ordinal).Should().Be(marker);
        }
    }

    [Test]
    public void ExactQuotaIncludesEscapesSyntheticIsAndShadowWrapper()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div", "<&");
        host.AppendChild(document.CreateTextNode("&"));
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var options = new HtmlSerializationOptions(shadowRoots: new[] { shadow });
        var expected = HtmlMarkupSerializer.Serialize(host, options);
        HtmlMarkupSerializer.Serialize(host, options,
            new SerializationLimits { MaxOutputCharacters = expected.Length }).Should().Be(expected);
        var failure = Assert.Throws<SerializationLimitException>(() => HtmlMarkupSerializer.Serialize(host,
            options, new SerializationLimits { MaxOutputCharacters = expected.Length - 1 }));
        failure!.Limit.Should().Be(expected.Length - 1);
        failure.Observed.Should().Be(expected.Length);
    }

    [Test]
    public void EachAuthoredWorkLoopHasItsOwnCancellationCheckpoint()
    {
        var document = Document.CreateHtml();
        var name = "x-" + new string('a', 2048);
        var longName = document.CreateParsedElement("urn:foreign", name, "p");
        CancelAt(longName, SerializationStage.HtmlName, occurrence: 3);
        var text = document.CreateTextNode(new string('a', 2048));
        CancelAt(text, SerializationStage.HtmlEscape);
        var synthetic = document.CreateElement("button", new string('a', 2048));
        CancelAt(synthetic, SerializationStage.HtmlEscape);
        var root = document.CreateElement("div");
        for (var i = 0; i < 1024; i++) root.AppendChild(document.CreateElement("span"));
        CancelAt(root, SerializationStage.HtmlTraversal, occurrence: 12);
        CancelAt(root, SerializationStage.Materialize, occurrence: 3);

        var hosts = new List<ShadowRoot>();
        for (var i = 0; i < 300; i++)
            hosts.Add(ShadowTree.Attach(document.CreateElement("div"), new ShadowRootInit(ShadowRootMode.Open), default));
        CancelAt(root, SerializationStage.HtmlOptions, new HtmlSerializationOptions(shadowRoots: hosts), 4);
        var shadow = ShadowTree.Attach(document.CreateElement("div"), new ShadowRootInit(ShadowRootMode.Open), default);
        CancelAt(shadow.Host, SerializationStage.HtmlShadow, new HtmlSerializationOptions(shadowRoots: new[] { shadow }));
    }

    [Test]
    public void BufferedParsedTextStreamsWithoutMaterializingDataAndCancelsInsideScan()
    {
        var document = Document.CreateHtml();
        var text = document.CreateTextNode(string.Empty);
        var source = new string('x', 2048) + "<&";
        text.AppendParsedData(source.AsSpan(), default);
        var cache = typeof(Text).GetField("_cachedParsedData", BindingFlags.Instance | BindingFlags.NonPublic)!;
        cache.GetValue(text).Should().BeNull();
        CancelAt(text, SerializationStage.HtmlEscape, occurrence: 3);
        cache.GetValue(text).Should().BeNull();
        HtmlMarkupSerializer.Serialize(text).Should().Be(new string('x', 2048) + "&lt;&amp;");
        cache.GetValue(text).Should().BeNull();
    }

    [Test]
    public void MutationAdoptionAndSaturatedStampInvalidateUnpublishedOutput()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        root.AppendChild(document.CreateTextNode(new string('x', 1024)));
        var stamp = document.MutationStamp;
        HtmlMarkupSerializer.Serialize(root).Should().StartWith("<div>");
        document.MutationStamp.Should().Be(stamp);
        Assert.Throws<InvalidOperationException>(() => HtmlMarkupSerializer.Serialize(root,
            checkpoint: stage => { if (stage == SerializationStage.Materialize) root.SetAttribute("changed", "yes"); }));

        var moving = Document.CreateHtml();
        var adopted = moving.CreateElement("span");
        Assert.Throws<InvalidOperationException>(() => HtmlMarkupSerializer.Serialize(adopted,
            checkpoint: stage => { if (stage == SerializationStage.Materialize) document.AdoptNode(adopted); }));

        var saturated = Document.CreateHtml();
        var field = typeof(Document).GetField("_mutationStamp", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(saturated, ulong.MaxValue);
        Assert.Throws<InvalidOperationException>(() => HtmlMarkupSerializer.Serialize(saturated));
    }

    [Test]
    public void ShadowRegistryAndTemplatePointerChangesInvalidateOutput()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var options = new HtmlSerializationOptions(shadowRoots: new[] { shadow });
        Assert.Throws<InvalidOperationException>(() => HtmlMarkupSerializer.Serialize(host, options,
            checkpoint: stage =>
            {
                if (stage == SerializationStage.Materialize)
                    shadow.SetCustomElementRegistry(new CustomElementRegistryIdentity(true));
            }));

        var template = document.CreateElement("template");
        var replacementHost = document.CreateElement("div");
        var replacement = ShadowTree.Attach(replacementHost, new ShadowRootInit(ShadowRootMode.Open), default);
        Assert.Throws<InvalidOperationException>(() => HtmlMarkupSerializer.Serialize(template,
            checkpoint: stage =>
            {
                if (stage == SerializationStage.Materialize)
                    ShadowTree.SetDeclarativeTemplateContent(template, replacement, false);
            }));
    }

    [Test]
    public void InactiveTemplateOwnerIsVerifiedBeforeResultPublication()
    {
        var document = Document.CreateHtml();
        var fragment = document.CreateDocumentFragment();
        var template = document.CreateElement("template");
        var inertOwner = template.TemplateContent!.OwnerDocument!;
        template.TemplateContent.AppendChild(inertOwner.CreateTextNode("first"));
        fragment.AppendChild(template);
        fragment.AppendChild(document.CreateElement("span"));
        Assert.Throws<InvalidOperationException>(() => HtmlMarkupSerializer.Serialize(fragment,
            checkpoint: stage =>
            {
                if (stage == SerializationStage.Final) template.TemplateContent.AppendChild(inertOwner.CreateTextNode("late"));
            }));
    }

    [Test]
    public void DeepAndWideNativeTreesHaveLinearChargedWorkWithoutDepthCap()
    {
        var document = Document.CreateHtml();
        var top = document.CreateElement("div");
        var cursor = top;
        for (var i = 0; i < 2048; i++)
        {
            var child = document.CreateElement("div");
            cursor.AppendChild(child);
            cursor = child;
        }
        HtmlMarkupSerializer.Serialize(top).Length.Should().Be(2049 * "<div></div>".Length);

        static int Count(int size)
        {
            var owner = Document.CreateHtml();
            var parent = owner.CreateElement("div");
            for (var i = 0; i < size; i++) parent.AppendChild(owner.CreateElement("span"));
            var polls = 0;
            HtmlMarkupSerializer.Serialize(parent, checkpoint: stage =>
            {
                if (stage == SerializationStage.HtmlTraversal) polls++;
            });
            return polls;
        }
        var small = Count(512);
        var large = Count(1024);
        large.Should().BeLessThan(small * 3);
    }

    private static void CancelAt(Node node, SerializationStage target, HtmlSerializationOptions? options = null,
        int occurrence = 1)
    {
        using var cancellation = new CancellationTokenSource();
        var reached = false;
        var calls = 0;
        Assert.Throws<OperationCanceledException>(() => HtmlMarkupSerializer.Serialize(node, options,
            checkpoint: stage =>
            {
                if (stage != target || reached || ++calls != occurrence) return;
                reached = true;
                cancellation.Cancel();
            }, cancellationToken: cancellation.Token));
        reached.Should().BeTrue();
        calls.Should().Be(occurrence);
    }
}
