#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Mutations;

public class MutationSubscriptionTests
{
    [Test]
    public void QualifiedAttributeMetadataIsFrozenWithoutChangingDomLocalNameOrOldValue()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        using var subscription = document.ObserveMutations(root, new MutationObserverOptions
        {
            Attributes = true, AttributeOldValue = true
        });
        root.SetAttributeNS("urn:test", "first:local", "one");
        var original = root.GetAttributeNodeNS("urn:test", "local")!;
        original.Value = "two";
        root.RemoveAttributeNode(original);
        original.Prefix = "detached";
        var replacement = document.CreateAttributeNS("urn:test", "second:local");
        replacement.Value = "three";
        root.SetAttributeNode(replacement);
        var changedPrefix = document.CreateAttributeNS("urn:test", "third:local");
        changedPrefix.Value = "four";
        root.SetAttributeNode(changedPrefix);
        replacement.Prefix = "also-detached";
        root.SetAttribute("plain", "plain-value");
        var plain = root.GetAttributeNode("plain")!;
        var records = subscription.TakeRecords();
        records.Select(record => record.AttributeQualifiedName).Should().Equal(
            "first:local", "first:local", "first:local", "second:local", "third:local", "plain");
        records.Take(5).Select(record => record.AttributeName).Should().OnlyContain(name => name == "local");
        records.Take(5).Select(record => record.AttributeNamespace).Should().OnlyContain(uri => uri == "urn:test");
        records.Select(record => record.OldValue).Should().Equal(null, "one", "two", null, "three", null);
        records[5].AttributeQualifiedName.Should().BeSameAs(plain.LocalName, "unprefixed metadata reuses the existing local-name string");
        records[5].AttributeNamespace.Should().BeNull();
        original.Prefix.Should().Be("detached");
        replacement.Prefix.Should().Be("also-detached");
    }

    [Test]
    public void ObserveDefaultsValidationAndFilterSnapshot()
    {
        var document = Document.CreateXml();
        var element = document.CreateElement("root");
        var filter = new List<string> { "Case" };
        using var subscription = document.ObserveMutations(element, new MutationObserverOptions
        {
            AttributeOldValue = false,
            AttributeFilter = filter
        });
        filter[0] = "other";
        element.SetAttribute("Case", "one");
        element.SetAttribute("case", "two");
        element.SetAttributeNS("urn:example", "p:Case", "three");
        var records = subscription.TakeRecords();
        records.Should().HaveCount(1);
        records[0].AttributeName.Should().Be("Case");
        records[0].AttributeNamespace.Should().BeNull();
        records[0].OldValue.Should().BeNull();

        Assert.Throws<ArgumentException>(() => subscription.Observe(element,
            new MutationObserverOptions { ChildList = true, Attributes = false, AttributeFilter = ["x"] }));
        Assert.Throws<ArgumentException>(() => subscription.Observe(element,
            new MutationObserverOptions { ChildList = true, CharacterData = false, CharacterDataOldValue = true }));
        Assert.Throws<ArgumentException>(() => subscription.Observe(element,
            new MutationObserverOptions { AttributeFilter = [null!] }));
        Assert.Throws<ArgumentException>(() => subscription.Observe(element, new MutationObserverOptions()));
        element.SetAttribute("Case", "four");
        subscription.TakeRecords().Should().ContainSingle();

        subscription.Observe(element, new MutationObserverOptions { Attributes = false, AttributeOldValue = false,
            ChildList = true });
        element.SetAttribute("Case", "five");
        subscription.TakeRecords().Should().BeEmpty();
    }

    [Test]
    public void OverlappingRegistrationsDeduplicateAndProjectOldValuesPerSubscription()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        var child = document.CreateElement("child");
        root.AppendChild(child);
        using var first = document.ObserveMutations(root, new MutationObserverOptions
        {
            Attributes = true, Subtree = true
        });
        first.Observe(child, new MutationObserverOptions { AttributeOldValue = true });
        using var second = document.ObserveMutations(root, new MutationObserverOptions
        {
            Attributes = true, Subtree = true
        });
        child.SetAttribute("x", "before");
        first.TakeRecords();
        second.TakeRecords();

        child.SetAttribute("x", "after");
        first.TakeRecords().Select(record => record.OldValue).Should().Equal("before");
        second.TakeRecords().Select(record => record.OldValue).Should().Equal((string?) null);
    }

    [Test]
    public void AttributeAndCharacterDataSemanticPathsIncludeEqualWrites()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        var text = document.CreateTextNode("a");
        var comment = document.CreateComment("b");
        var cdata = document.CreateCDataSection("c");
        var instruction = document.CreateProcessingInstruction("go", "d");
        root.AppendChild(text);
        root.AppendChild(comment);
        root.AppendChild(cdata);
        root.AppendChild(instruction);
        using var subscription = document.ObserveMutations(root, new MutationObserverOptions
        {
            Attributes = true, AttributeOldValue = true, CharacterData = true,
            CharacterDataOldValue = true, Subtree = true
        });

        root.SetAttribute("x", "one");
        var original = root.GetAttributeNode("x")!;
        original.Value = "one";
        var replacement = document.CreateAttribute("x");
        replacement.Value = "two";
        root.SetAttributeNode(replacement).Should().BeSameAs(original);
        root.SetAttributeNS("urn:test", "p:local", "three");
        root.RemoveAttributeNS("urn:test", "local");
        root.RemoveAttribute("missing");
        root.RemoveAttributeNode(replacement);
        text.Data = "a";
        comment.Data = "b2";
        cdata.Data = "c2";
        instruction.Data = "d2";

        var records = subscription.TakeRecords();
        records.Select(record => record.Kind).Should().Equal(
            MutationRecordKind.Attributes, MutationRecordKind.Attributes, MutationRecordKind.Attributes,
            MutationRecordKind.Attributes, MutationRecordKind.Attributes, MutationRecordKind.Attributes,
            MutationRecordKind.CharacterData, MutationRecordKind.CharacterData,
            MutationRecordKind.CharacterData, MutationRecordKind.CharacterData);
        records.Take(3).Select(record => record.OldValue).Should().Equal(null, "one", "one");
        records[3].AttributeName.Should().Be("local");
        records[3].AttributeNamespace.Should().Be("urn:test");
        records[4].OldValue.Should().Be("three");
        records[6].OldValue.Should().Be("a");
        records[9].OldValue.Should().Be("d");
        original.OwnerElement.Should().BeNull();
        replacement.OwnerElement.Should().BeNull();
        Assert.Throws<NotSupportedException>(() => ((IList<Node>) records[0].AddedNodes).Add(text));
    }

    [Test]
    public void FragmentAndReplacementProduceAggregateSnapshotsInOrder()
    {
        var document = Document.CreateXml();
        var target = document.CreateElement("target");
        var old = document.CreateElement("old");
        target.AppendChild(old);
        var fragment = document.CreateDocumentFragment();
        var first = document.CreateElement("first");
        var second = document.CreateElement("second");
        fragment.AppendChild(first);
        fragment.AppendChild(second);
        using var subscription = document.ObserveMutations(target, new MutationObserverOptions { ChildList = true });
        subscription.Observe(fragment, new MutationObserverOptions { ChildList = true });
        target.ReplaceChild(fragment, old);

        var records = subscription.TakeRecords();
        records.Should().HaveCount(2);
        records[0].Target.Should().BeSameAs(fragment);
        records[0].RemovedNodes.Should().Equal(first, second);
        records[1].Target.Should().BeSameAs(target);
        records[1].AddedNodes.Should().Equal(first, second);
        records[1].RemovedNodes.Should().Equal(old);
        records[1].PreviousSibling.Should().BeNull();
        records[1].NextSibling.Should().BeNull();
        target.ChildNodes.Should().Equal(first, second);

        var descendant = document.CreateElement("descendant");
        first.AppendChild(descendant);
        subscription.Observe(target, new MutationObserverOptions { ChildList = true, Subtree = true });
        target.ReplaceChildren(descendant);
        records = subscription.TakeRecords();
        records.Should().HaveCount(2);
        records[0].Target.Should().BeSameAs(first);
        records[0].RemovedNodes.Should().Equal(descendant);
        records[1].Target.Should().BeSameAs(target);
        records[1].RemovedNodes.Should().Equal(first, second);
        records[1].AddedNodes.Should().Equal(descendant);
    }

    [Test]
    public void TransientsSurviveDrainAndAdoptionUntilDelivery()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateXml();
        var root = source.CreateElement("root");
        var detached = source.CreateElement("detached");
        root.AppendChild(detached);
        using var first = source.ObserveMutations(root, new MutationObserverOptions
        {
            Attributes = true, ChildList = true, Subtree = true
        });
        using var second = source.ObserveMutations(root, new MutationObserverOptions
        {
            Attributes = true, Subtree = true
        });
        root.RemoveChild(detached);
        first.TakeRecords().Should().ContainSingle();
        destination.AdoptNode(detached);
        detached.SetAttribute("x", "one");
        first.TakeRecords().Should().ContainSingle();
        second.TakeRecords().Should().ContainSingle();
        first.TakeRecordsForDelivery().Should().BeEmpty();

        detached.SetAttribute("x", "two");
        first.TakeRecords().Should().BeEmpty();
        second.TakeRecords().Should().ContainSingle();
        second.TakeRecordsForDelivery().Should().BeEmpty();
        detached.SetAttribute("x", "three");
        second.TakeRecords().Should().BeEmpty();
    }

    [Test]
    public void ReobserveDisconnectAndDisposeHaveDistinctLifetimes()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var child = document.CreateElement("child");
        root.AppendChild(child);
        var subscription = document.ObserveMutations(root, new MutationObserverOptions
        {
            Attributes = true, Subtree = true
        });
        root.RemoveChild(child);
        child.SetAttribute("x", "one");
        subscription.Observe(root, new MutationObserverOptions { ChildList = true, Subtree = true });
        subscription.TakeRecords().Should().ContainSingle();
        child.SetAttribute("x", "two");
        subscription.TakeRecords().Should().BeEmpty();
        subscription.Disconnect();
        subscription.Observe(child, new MutationObserverOptions { Attributes = true });
        child.SetAttribute("x", "three");
        subscription.TakeRecords().Should().ContainSingle();
        subscription.Dispose();
        subscription.Disconnect();
        subscription.Dispose();
        Assert.Throws<ObjectDisposedException>(() => subscription.Observe(child, new MutationObserverOptions { Attributes = true }));
        Assert.Throws<ObjectDisposedException>(() => subscription.TakeRecords());
        Assert.Throws<ObjectDisposedException>(() => subscription.TakeRecordsForDelivery());
    }

    [Test]
    public void TemplateContentIsAnIndependentObservationRoot()
    {
        var document = Document.CreateHtml();
        var template = document.CreateElement("template");
        var content = template.TemplateContent!;
        using var host = document.ObserveMutations(template, new MutationObserverOptions
        {
            ChildList = true, Subtree = true
        });
        using var contents = document.ObserveMutations(content, new MutationObserverOptions
        {
            ChildList = true, Subtree = true
        });
        content.AppendChild(content.OwnerDocument!.CreateElement("inside"));
        host.TakeRecords().Should().BeEmpty();
        contents.TakeRecords().Should().ContainSingle();
    }

    [Test]
    public void OrdinaryMovesAndSameNodeOperationsKeepAlgorithmRecords()
    {
        var document = Document.CreateXml();
        var source = document.CreateElement("source");
        var destination = document.CreateElement("destination");
        var node = document.CreateTextNode("value");
        source.AppendChild(node);
        using var subscription = document.ObserveMutations(source, new MutationObserverOptions { ChildList = true });
        subscription.Observe(destination, new MutationObserverOptions { ChildList = true });

        destination.AppendChild(node);
        var records = subscription.TakeRecords();
        records.Select(record => record.Target).Should().Equal(source, destination);
        records[0].RemovedNodes.Should().Equal(node);
        records[1].AddedNodes.Should().Equal(node);

        destination.InsertBefore(node, node);
        records = subscription.TakeRecords();
        records.Should().HaveCount(2);
        records[0].RemovedNodes.Should().Equal(node);
        records[1].AddedNodes.Should().Equal(node);

        destination.ReplaceChild(node, node);
        records = subscription.TakeRecords();
        records.Should().HaveCount(2);
        records[0].RemovedNodes.Should().Equal(node);
        records[1].AddedNodes.Should().Equal(node);
        records[1].RemovedNodes.Should().BeEmpty();
        destination.ChildNodes.Should().Equal(node);

        var empty = document.CreateDocumentFragment();
        destination.AppendChild(empty);
        subscription.TakeRecords().Should().BeEmpty();
    }

    [Test]
    public void FirstChildInsertionAndReplacementPreserveOriginalSiblingBoundaries()
    {
        var document = Document.CreateXml();
        var parent = document.CreateElement("parent");
        var a = document.CreateElement("a");
        var b = document.CreateElement("b");
        var c = document.CreateElement("c");
        parent.AppendChild(a);
        parent.AppendChild(b);
        parent.AppendChild(c);
        using var subscription = document.ObserveMutations(parent, new MutationObserverOptions { ChildList = true });

        var front = document.CreateElement("front");
        parent.InsertBefore(front, a);
        var record = subscription.TakeRecords().Single();
        record.PreviousSibling.Should().BeNull();
        record.NextSibling.Should().BeSameAs(a);

        var fragment = document.CreateDocumentFragment();
        var f1 = document.CreateElement("f1");
        var f2 = document.CreateElement("f2");
        fragment.AppendChild(f1);
        fragment.AppendChild(f2);
        parent.InsertBefore(fragment, front);
        record = subscription.TakeRecords().Single();
        record.PreviousSibling.Should().BeNull();
        record.NextSibling.Should().BeSameAs(front);
        record.AddedNodes.Should().Equal(f1, f2);

        parent.ReplaceChild(a, b);
        var records = subscription.TakeRecords();
        records.Should().HaveCount(2);
        records[0].RemovedNodes.Should().Equal(a);
        records[0].NextSibling.Should().BeSameAs(b);
        records[1].AddedNodes.Should().Equal(a);
        records[1].RemovedNodes.Should().Equal(b);
        records[1].PreviousSibling.Should().BeSameAs(a);
        records[1].NextSibling.Should().BeSameAs(c);
        parent.ChildNodes.Should().Equal(f1, f2, front, a, c);
    }

    [Test]
    public void FailedInsertionLeavesOwnershipLinksAndQueuesUntouched()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateXml();
        var sourceRoot = source.CreateElement("source");
        var destinationRoot = destination.CreateElement("destination");
        source.AppendChild(sourceRoot);
        destination.AppendChild(destinationRoot);
        var incoming = source.CreateElement("incoming");
        sourceRoot.AppendChild(incoming);
        using var subscription = source.ObserveMutations(sourceRoot,
            new MutationObserverOptions { ChildList = true, Subtree = true });
        subscription.Observe(destinationRoot, new MutationObserverOptions { ChildList = true });

        Assert.Throws<DomException>(() => destinationRoot.InsertBefore(incoming, sourceRoot));
        incoming.ParentNode.Should().BeSameAs(sourceRoot);
        incoming.OwnerDocument.Should().BeSameAs(source);
        subscription.TakeRecords().Should().BeEmpty();
    }

    [Test]
    public void ParsedTextCommitAndNativeStampAreAtomicAcrossCancellation()
    {
        var document = Document.CreateHtml();
        var text = document.CreateTextNode("old");
        using var subscription = document.ObserveMutations(text,
            new MutationObserverOptions { CharacterDataOldValue = true });
        var before = document.MutationStamp;
        using var canceled = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => text.AppendParsedData("-cancel".AsSpan(),
            checkpoint =>
            {
                if (checkpoint == TextAppendCheckpoint.AfterPreparation) canceled.Cancel();
            }, canceled.Token));
        text.Data.Should().Be("old");
        document.MutationStamp.Should().Be(before);
        subscription.TakeRecords().Should().BeEmpty();

        using var canceledAfterCommit = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => text.AppendParsedData("-committed".AsSpan(),
            checkpoint =>
            {
                if (checkpoint == TextAppendCheckpoint.AfterCommit) canceledAfterCommit.Cancel();
            }, canceledAfterCommit.Token));
        text.Data.Should().Be("old-committed");
        document.MutationStamp.Should().BeGreaterThan(before);
        subscription.TakeRecords().Select(record => record.OldValue).Should().Equal("old");
    }

    [Test]
    public void StampsSaturateAndAdoptionInvalidatesBothDocuments()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateXml();
        var detached = source.CreateElement("detached");
        var sourceBefore = source.MutationStamp;
        var destinationBefore = destination.MutationStamp;
        destination.AdoptNode(detached);
        source.MutationStamp.Should().BeGreaterThan(sourceBefore);
        destination.MutationStamp.Should().BeGreaterThan(destinationBefore);

        var stamp = typeof(Document).GetField("_mutationStamp",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        stamp.SetValue(destination, ulong.MaxValue - 1);
        detached.SetAttribute("x", "one");
        destination.MutationStamp.Should().Be(ulong.MaxValue);
        detached.SetAttribute("x", "two");
        destination.MutationStamp.Should().Be(ulong.MaxValue);
    }

    [Test]
    public void ObserverPresenceIsLocalAndFollowsAdoptedRegistrations()
    {
        var source = Document.CreateXml();
        var unrelated = Document.CreateXml();
        var destination = Document.CreateXml();
        var observed = source.CreateElement("observed");
        source.MayHaveMutationRegistrations.Should().BeFalse();
        unrelated.MayHaveMutationRegistrations.Should().BeFalse();
        using var subscription = source.ObserveMutations(observed,
            new MutationObserverOptions { Attributes = true });
        source.MayHaveMutationRegistrations.Should().BeTrue();
        unrelated.MayHaveMutationRegistrations.Should().BeFalse();
        destination.MayHaveMutationRegistrations.Should().BeFalse();

        destination.AdoptNode(observed);
        destination.MayHaveMutationRegistrations.Should().BeTrue();
        observed.SetAttribute("x", "after adoption");
        subscription.TakeRecords().Should().ContainSingle();
        unrelated.MayHaveMutationRegistrations.Should().BeFalse();
    }

    [Test]
    public void TrustedPendingSignalPreservesSubscriptionEncounterOrder()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        var child = document.CreateElement("child");
        root.AppendChild(child);
        using var ancestor = document.ObserveMutations(root,
            new MutationObserverOptions { Attributes = true, Subtree = true });
        using var direct = document.ObserveMutations(child,
            new MutationObserverOptions { Attributes = true });
        var pending = new List<MutationSubscription>();
        ancestor.PendingRecord = pending.Add;
        direct.PendingRecord = pending.Add;
        child.SetAttribute("x", "value");
        pending.Should().Equal(direct, ancestor);
    }

    [Test]
    public void PublishedParserSeamsEmitRecordsWhileCloneAndImportStaySilent()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateXml();
        var root = source.CreateElement("root");
        source.AppendChild(root);
        using var subscription = source.ObserveMutations(root, new MutationObserverOptions
        {
            ChildList = true, Attributes = true, CharacterData = true, Subtree = true
        });
        var child = source.CreateParsedElement(null, "child", null);
        child.InitializeParsedAttributes([new ParserAttribute(null, "initial", null, "value")],
            CancellationToken.None);
        root.AppendParsedChild(child);
        child.AddMissingParsedAttributes([new ParserAttribute(null, "later", null, "next")],
            CancellationToken.None);
        var text = source.CreateTextNode("old");
        child.AppendParsedChild(text);
        text.AppendParsedData("new".AsSpan(), CancellationToken.None);
        subscription.TakeRecords().Select(record => record.Kind).Should().Equal(
            MutationRecordKind.ChildList, MutationRecordKind.Attributes,
            MutationRecordKind.ChildList, MutationRecordKind.CharacterData);

        var sourceBefore = source.MutationStamp;
        var destinationBefore = destination.MutationStamp;
        var clone = root.CloneNode(deep: true);
        var imported = destination.ImportNode(root, deep: true);
        clone.ChildCount.Should().Be(1);
        imported.ChildCount.Should().Be(1);
        source.MutationStamp.Should().Be(sourceBefore);
        destination.MutationStamp.Should().Be(destinationBefore);
        subscription.TakeRecords().Should().BeEmpty();
    }

    [Test]
    public void ParserModeInvalidatesOnlyWhenItsValueChanges()
    {
        var document = Document.CreateHtml();
        var before = document.MutationStamp;
        document.SetParserMode(DocumentMode.Quirks);
        document.MutationStamp.Should().BeGreaterThan(before);
        var after = document.MutationStamp;
        document.SetParserMode(DocumentMode.Quirks);
        document.MutationStamp.Should().Be(after);
    }
}
