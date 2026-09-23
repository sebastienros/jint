#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Shadow;

public class ShadowOwnershipTests
{
    [Test]
    public void DirectRootCloneImportAndAdoptHaveDistinctErrors()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateHtml();
        var host = source.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open, Clonable: true), default);
        var child = source.CreateTextNode("child");
        root.AppendChild(child);
        Assert.That(Assert.Throws<DomException>(() => root.CloneNode())!.Name, Is.EqualTo("NotSupportedError"));
        Assert.That(Assert.Throws<DomException>(() => destination.ImportNode(root, true))!.Name,
            Is.EqualTo("NotSupportedError"));
        Assert.That(Assert.Throws<DomException>(() => destination.AdoptNode(root))!.Name,
            Is.EqualTo("HierarchyRequestError"));
        root.Host.Should().BeSameAs(host);
        root.OwnerDocument.Should().BeSameAs(source);
        root.FirstChild.Should().BeSameAs(child);
    }

    [Test]
    public void ShallowCloneAndImportCopyWholeClonableShadowTreeWithoutLightChildren()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateHtml();
        var host = source.CreateElement("x-host");
        host.SetAttribute("id", "source");
        var root = ShadowTree.Attach(host,
            new ShadowRootInit(ShadowRootMode.Closed, true, true, SlotAssignmentMode.Manual, true),
            new ShadowAttachmentContext(null, false, true));
        var shadowChild = source.CreateElement("div");
        shadowChild.SetAttribute("class", "inside");
        shadowChild.AppendChild(source.CreateTextNode("inside"));
        root.AppendChild(shadowChild);
        host.AppendChild(source.CreateTextNode("light"));

        foreach (var copy in new[] { (Element)host.CloneNode(), (Element)destination.ImportNode(host) })
        {
            copy.Should().NotBeSameAs(host);
            copy.ChildCount.Should().Be(0);
            copy.Attributes.Single().Should().NotBeSameAs(host.Attributes.Single());
            var shadow = copy.AttachedShadowRoot!;
            shadow.Should().NotBeSameAs(root);
            shadow.Host.Should().BeSameAs(copy);
            shadow.ParentNode.Should().BeNull();
            shadow.ChildCount.Should().Be(1);
            shadow.Mode.Should().Be(ShadowRootMode.Closed);
            shadow.DelegatesFocus.Should().BeTrue();
            shadow.Serializable.Should().BeTrue();
            shadow.SlotAssignment.Should().Be(SlotAssignmentMode.Manual);
            shadow.Clonable.Should().BeTrue();
            shadow.AvailableToElementInternals.Should().BeFalse();
            shadow.CustomElementRegistry.Should().BeNull();
            var copiedChild = (Element)shadow.FirstChild!;
            copiedChild.Should().NotBeSameAs(shadowChild);
            copiedChild.Attributes.Single().Should().NotBeSameAs(shadowChild.Attributes.Single());
            copiedChild.FirstChild.Should().NotBeSameAs(shadowChild.FirstChild);
            copiedChild.OwnerDocument.Should().BeSameAs(copy.OwnerDocument);
            shadow.OwnerDocument.Should().BeSameAs(copy.OwnerDocument);
        }

        var nonClonable = source.CreateElement("div");
        ShadowTree.Attach(nonClonable, new ShadowRootInit(ShadowRootMode.Open), default);
        ((Element)nonClonable.CloneNode(true)).AttachedShadowRoot.Should().BeNull();
    }

    [Test]
    public void DeepCloneRetainsNestedShadowAndTemplateOwners()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateHtml();
        var host = source.CreateElement("div");
        var outer = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open, Clonable: true), default);
        var innerHost = source.CreateElement("section");
        var inner = ShadowTree.Attach(innerHost, new ShadowRootInit(ShadowRootMode.Open, Clonable: true), default);
        var template = source.CreateElement("template");
        var inert = template.TemplateContent!.OwnerDocument!;
        template.TemplateContent.AppendChild(inert.CreateTextNode("inert"));
        inner.AppendChild(template);
        outer.AppendChild(innerHost);
        host.AppendChild(source.CreateTextNode("light"));

        var copy = (Element)destination.ImportNode(host, true);
        copy.FirstChild.Should().NotBeSameAs(host.FirstChild);
        var copiedOuter = copy.AttachedShadowRoot!;
        var copiedInnerHost = (Element)copiedOuter.FirstChild!;
        var copiedInner = copiedInnerHost.AttachedShadowRoot!;
        var copiedTemplate = (Element)copiedInner.FirstChild!;
        copiedOuter.OwnerDocument.Should().BeSameAs(destination);
        copiedInner.OwnerDocument.Should().BeSameAs(destination);
        copiedTemplate.OwnerDocument.Should().BeSameAs(destination);
        copiedTemplate.TemplateContent!.OwnerDocument.Should().BeSameAs(destination.GetTemplateContentsOwnerDocument());
        copiedTemplate.TemplateContent.FirstChild.Should().NotBeSameAs(template.TemplateContent.FirstChild);
        copiedTemplate.TemplateContent.FirstChild!.OwnerDocument.Should().BeSameAs(destination.GetTemplateContentsOwnerDocument());
    }

    [Test]
    public void DocumentCloneKeepsScopedRegistryOnlyAndPropagatesResultingAssociation()
    {
        var scoped = new CustomElementRegistryIdentity(true);
        var global = new CustomElementRegistryIdentity(false);
        foreach (var registry in new CustomElementRegistryIdentity?[] { null, global, scoped })
        {
            var document = Document.CreateHtml();
            document.SetCustomElementRegistry(registry);
            var host = document.CreateElement("div");
            host.SetCustomElementRegistry(registry);
            var root = ShadowTree.Attach(host,
                new ShadowRootInit(ShadowRootMode.Open, Clonable: true),
                new ShadowAttachmentContext(registry, false, false));
            var child = document.CreateElement("span");
            child.SetCustomElementRegistry(registry);
            root.AppendChild(child);
            document.AppendChild(host);

            foreach (var deep in new[] { false, true })
            {
                var copy = (Document)document.CloneNode(deep);
                copy.CustomElementRegistry.Should().BeSameAs(registry?.IsScoped == true ? registry : null);
                if (deep)
                {
                    var copiedHost = copy.DocumentElement!;
                    var copiedRoot = copiedHost.AttachedShadowRoot!;
                    copiedHost.CustomElementRegistry.Should().BeSameAs(copy.CustomElementRegistry);
                    copiedRoot.CustomElementRegistry.Should().BeSameAs(copy.CustomElementRegistry);
                    ((Element)copiedRoot.FirstChild!).CustomElementRegistry.Should().BeSameAs(copy.CustomElementRegistry);
                }
                else
                {
                    copy.ChildCount.Should().Be(0);
                }
            }
        }
    }

    [Test]
    public void DocumentCloneUsesCreationRealmDefaultWhenSourceIsNullOrGlobal()
    {
        var creationGlobal = new CustomElementRegistryIdentity(false);
        var otherGlobal = new CustomElementRegistryIdentity(false);
        var document = new Document(DocumentKind.Html, "text/html", creationGlobal);
        document.CustomElementRegistry.Should().BeSameAs(creationGlobal);
        var host = document.CreateElement("div");
        host.SetCustomElementRegistry(otherGlobal);
        var root = ShadowTree.Attach(host,
            new ShadowRootInit(ShadowRootMode.Open, Clonable: true),
            new ShadowAttachmentContext(otherGlobal, false, false));
        var child = document.CreateElement("span");
        child.SetCustomElementRegistry(otherGlobal);
        root.AppendChild(child);
        document.AppendChild(host);

        foreach (var association in new CustomElementRegistryIdentity?[] { null, otherGlobal })
        {
            document.SetCustomElementRegistry(association);
            foreach (var deep in new[] { false, true })
            {
                var copy = (Document)document.CloneNode(deep);
                copy.CustomElementRegistry.Should().BeSameAs(creationGlobal);
                if (deep)
                {
                    var copiedHost = copy.DocumentElement!;
                    copiedHost.CustomElementRegistry.Should().BeSameAs(creationGlobal);
                    copiedHost.AttachedShadowRoot!.CustomElementRegistry.Should().BeSameAs(creationGlobal);
                    ((Element)copiedHost.AttachedShadowRoot.FirstChild!).CustomElementRegistry
                        .Should().BeSameAs(creationGlobal);
                }
            }
        }

        Assert.Throws<ArgumentException>(() => new Document(DocumentKind.Html, "text/html",
            new CustomElementRegistryIdentity(true)));
    }

    [Test]
    public void ImportAndAdoptionHandleGlobalScopedAndKeepNullIndependently()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateHtml();
        var oldGlobal = new CustomElementRegistryIdentity(false);
        var newGlobal = new CustomElementRegistryIdentity(false);
        var scoped = new CustomElementRegistryIdentity(true);
        source.SetCustomElementRegistry(oldGlobal);
        destination.SetCustomElementRegistry(newGlobal);
        var host = source.CreateElement("div");
        host.SetCustomElementRegistry(oldGlobal);
        var root = ShadowTree.Attach(host,
            new ShadowRootInit(ShadowRootMode.Open, Clonable: true),
            new ShadowAttachmentContext(oldGlobal, false, false));
        var child = source.CreateElement("span");
        child.SetCustomElementRegistry(scoped);
        root.AppendChild(child);

        var imported = (Element)destination.ImportNode(host, true);
        imported.CustomElementRegistry.Should().BeSameAs(newGlobal);
        imported.AttachedShadowRoot!.CustomElementRegistry.Should().BeSameAs(newGlobal);
        ((Element)imported.AttachedShadowRoot.FirstChild!).CustomElementRegistry.Should().BeSameAs(scoped);
        host.CustomElementRegistry.Should().BeSameAs(oldGlobal);
        root.CustomElementRegistry.Should().BeSameAs(oldGlobal);

        destination.AdoptNode(host);
        host.OwnerDocument.Should().BeSameAs(destination);
        root.OwnerDocument.Should().BeSameAs(destination);
        child.OwnerDocument.Should().BeSameAs(destination);
        host.CustomElementRegistry.Should().BeSameAs(newGlobal);
        root.CustomElementRegistry.Should().BeSameAs(newGlobal);
        child.CustomElementRegistry.Should().BeSameAs(scoped);

        var nullHost = source.CreateElement("div");
        var nullRoot = ShadowTree.Attach(nullHost,
            new ShadowRootInit(ShadowRootMode.Open, Clonable: true), default);
        var template = source.CreateElement("template");
        ShadowTree.SetDeclarativeTemplateContent(template, nullRoot, true);
        var nullCopy = (Element)destination.ImportNode(nullHost);
        nullCopy.AttachedShadowRoot!.CustomElementRegistry.Should().BeNull();
        nullCopy.AttachedShadowRoot.KeepCustomElementRegistryNull.Should().BeTrue();
        destination.AdoptNode(nullHost);
        nullRoot.CustomElementRegistry.Should().BeNull();

        var fillHost = source.CreateElement("div");
        var fillRoot = ShadowTree.Attach(fillHost, new ShadowRootInit(ShadowRootMode.Open), default);
        destination.AdoptNode(fillHost);
        fillRoot.CustomElementRegistry.Should().BeSameAs(newGlobal);
    }

    [Test]
    public void FragmentOperationsDrainRootsWithoutChangingTheirHost()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateHtml();
        var target = destination.CreateElement("div");
        var host = source.CreateElement("section");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var appendChild = source.CreateTextNode("append");
        root.AppendChild(appendChild);
        target.AppendChild(root);
        root.OwnerDocument.Should().BeSameAs(source);
        root.ChildCount.Should().Be(0);
        appendChild.OwnerDocument.Should().BeSameAs(destination);
        root.Host.Should().BeSameAs(host);

        var replacement = source.CreateTextNode("replace");
        root.AppendChild(replacement);
        target.ReplaceChild(root, appendChild);
        root.OwnerDocument.Should().BeSameAs(destination);
        root.Host.Should().BeSameAs(host);
        replacement.OwnerDocument.Should().BeSameAs(destination);
        root.ChildCount.Should().Be(0);

        target.ReplaceChild(root, replacement);
        root.OwnerDocument.Should().BeSameAs(destination);
        root.Host.Should().BeSameAs(host);
        target.ReplaceChildren(root);
        root.ChildCount.Should().Be(0);
    }

    [Test]
    public void CloneAndImportDoNotCopyObserverRegistrationsOrStampAnExistingDestination()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateHtml();
        var host = source.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open, Clonable: true), default);
        var child = source.CreateElement("span");
        root.AppendChild(child);
        using var subscription = source.ObserveMutations(root,
            new MutationObserverOptions { ChildList = true, Subtree = true });
        var sourceStamp = source.MutationStamp;
        var destinationStamp = destination.MutationStamp;

        var clone = (Element)host.CloneNode(true);
        var imported = (Element)destination.ImportNode(host, true);
        source.MutationStamp.Should().Be(sourceStamp);
        destination.MutationStamp.Should().Be(destinationStamp);
        subscription.TakeRecords().Should().BeEmpty();

        clone.AttachedShadowRoot!.AppendChild(source.CreateTextNode("clone-only"));
        imported.AttachedShadowRoot!.AppendChild(destination.CreateTextNode("import-only"));
        subscription.TakeRecords().Should().BeEmpty();
        root.ChildCount.Should().Be(1);
        root.FirstChild.Should().BeSameAs(child);
    }

    [Test]
    public void AdoptionPreservesRootAndAttributeIdentitiesAndInvalidatesBothDocuments()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateHtml();
        var host = source.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var child = source.CreateElement("span");
        child.SetAttribute("data-id", "same");
        var attribute = child.Attributes.Single();
        root.AppendChild(child);
        source.AppendChild(host);
        var sourceStamp = source.MutationStamp;
        var destinationStamp = destination.MutationStamp;

        destination.AdoptNode(host);
        source.DocumentElement.Should().BeNull();
        host.AttachedShadowRoot.Should().BeSameAs(root);
        child.ParentNode.Should().BeSameAs(root);
        attribute.OwnerElement.Should().BeSameAs(child);
        host.OwnerDocument.Should().BeSameAs(destination);
        root.OwnerDocument.Should().BeSameAs(destination);
        child.OwnerDocument.Should().BeSameAs(destination);
        attribute.OwnerDocument.Should().BeSameAs(destination);
        source.MutationStamp.Should().BeGreaterThan(sourceStamp);
        destination.MutationStamp.Should().BeGreaterThan(destinationStamp);

        var sameDocumentStamp = destination.MutationStamp;
        destination.AdoptNode(host);
        destination.MutationStamp.Should().Be(sameDocumentStamp);
    }

    [Test]
    public void RegistriesAreNativeIdentitiesAndSettersInvalidateOnlyOnChange()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        document.CustomElementRegistry.Should().BeNull();
        element.CustomElementRegistry.Should().BeNull();
        var identity = new CustomElementRegistryIdentity(true);
        var start = document.MutationStamp;
        document.SetCustomElementRegistry(identity);
        document.MutationStamp.Should().BeGreaterThan(start);
        var afterDocument = document.MutationStamp;
        document.SetCustomElementRegistry(identity);
        document.MutationStamp.Should().Be(afterDocument);
        element.SetCustomElementRegistry(identity);
        document.MutationStamp.Should().BeGreaterThan(afterDocument);
        var afterElement = document.MutationStamp;
        element.SetCustomElementRegistry(identity);
        document.MutationStamp.Should().Be(afterElement);
        element.SetCustomElementRegistry(null);
        element.CustomElementRegistry.Should().BeNull();
        document.MutationStamp.Should().BeGreaterThan(afterElement);
    }

    [Test]
    public void HostObserversDoNotSeeRootMutationsButRootObserversDo()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        using var hostObserver = document.ObserveMutations(host,
            new MutationObserverOptions { ChildList = true, Subtree = true });
        using var rootObserver = document.ObserveMutations(root,
            new MutationObserverOptions { ChildList = true, Subtree = true });
        var child = document.CreateElement("span");
        root.AppendChild(child);
        hostObserver.TakeRecords().Should().BeEmpty();
        var rootRecords = rootObserver.TakeRecords();
        rootRecords.Should().HaveCount(1);
        rootRecords[0].Target.Should().BeSameAs(root);
    }

    [Test]
    public void DeclarativeRootMetadataSurvivesShallowHostClone()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host,
            new ShadowRootInit(ShadowRootMode.Open, true, true, SlotAssignmentMode.Manual, true), default);
        var template = document.CreateElement("template");
        ShadowTree.SetDeclarativeTemplateContent(template, root, true);
        root.AppendChild(document.CreateElement("span"));

        var copy = (Element)host.CloneNode();
        var copiedRoot = copy.AttachedShadowRoot!;
        copiedRoot.Declarative.Should().BeTrue();
        copiedRoot.KeepCustomElementRegistryNull.Should().BeTrue();
        copiedRoot.AvailableToElementInternals.Should().BeFalse();
        copiedRoot.Mode.Should().Be(root.Mode);
        copiedRoot.DelegatesFocus.Should().Be(root.DelegatesFocus);
        copiedRoot.Serializable.Should().Be(root.Serializable);
        copiedRoot.SlotAssignment.Should().Be(root.SlotAssignment);
        copiedRoot.Clonable.Should().Be(root.Clonable);
        copiedRoot.ChildCount.Should().Be(1);
        copiedRoot.FirstChild.Should().NotBeSameAs(root.FirstChild);
        root.Declarative.Should().BeTrue();
        root.FirstChild.Should().NotBeNull();
    }

    [Test]
    public void AdoptionOfNullElementRegistryUsesItsParentEffectiveGlobalRegistry()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateHtml();
        var destinationGlobal = new CustomElementRegistryIdentity(false);
        var scoped = new CustomElementRegistryIdentity(true);
        destination.SetCustomElementRegistry(destinationGlobal);
        var host = source.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open),
            new ShadowAttachmentContext(scoped, false, false));
        var parent = source.CreateElement("section");
        parent.SetCustomElementRegistry(scoped);
        var child = source.CreateElement("span");
        parent.AppendChild(child);
        root.AppendChild(parent);
        var rootChild = source.CreateElement("span");
        root.AppendChild(rootChild);
        destination.AdoptNode(host);

        root.CustomElementRegistry.Should().BeSameAs(scoped);
        parent.CustomElementRegistry.Should().BeSameAs(scoped);
        child.CustomElementRegistry.Should().BeNull();
        rootChild.CustomElementRegistry.Should().BeNull();

        var secondHost = source.CreateElement("div");
        var secondRoot = ShadowTree.Attach(secondHost, new ShadowRootInit(ShadowRootMode.Open),
            new ShadowAttachmentContext(scoped, false, false));
        var globalSourceChild = source.CreateElement("span");
        globalSourceChild.SetCustomElementRegistry(new CustomElementRegistryIdentity(false));
        secondRoot.AppendChild(globalSourceChild);
        destination.AdoptNode(secondHost);
        globalSourceChild.CustomElementRegistry.Should().BeSameAs(destinationGlobal);
    }
}
