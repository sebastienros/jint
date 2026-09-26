#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Mutations;

public sealed class AttributeTransitionValueTests
{
    [Test]
    public void QueuedAttributeValuesPreserveEachTransitionAndReuseImmutableStrings()
    {
        var document = Document.CreateXml();
        var element = document.CreateElement("root");
        using var subscription = document.ObserveMutations(element,
            new MutationObserverOptions { Attributes = true, AttributeOldValue = true });
        var initial = new string('a', 33);
        var changed = new string('b', 33);
        var replaced = new string('c', 33);
        element.SetAttribute("x", initial);
        var original = element.GetAttributeNode("x")!;
        original.Value = changed;
        original.Value = changed;
        var replacement = document.CreateAttribute("x");
        replacement.Value = replaced;
        element.SetAttributeNode(replacement);
        element.RemoveAttributeNode(replacement);
        // Detached attributes can change without rewriting previously queued values.
        original.Value = "detached original";
        replacement.Value = "detached replacement";
        var records = subscription.TakeRecords();
        records.Select(record => record.AttributeNewValue).Should().Equal(initial, changed, changed, replaced, null);
        records.Select(record => record.OldValue).Should().Equal(null, initial, changed, changed, replaced);
        records[0].AttributeNewValue.Should().BeSameAs(initial);
        records[1].AttributeNewValue.Should().BeSameAs(changed);
        records[2].AttributeNewValue.Should().BeSameAs(changed);
        records[3].AttributeNewValue.Should().BeSameAs(replaced);
        records.Select(record => record.AttributeName).Should().OnlyContain(name => name == "x");
        records.Select(record => record.AttributeNamespace).Should().OnlyContain(uri => uri == null);
    }

    [Test]
    public void EmptyAttributeValueRemainsDistinctFromRemoval()
    {
        var document = Document.CreateXml();
        var element = document.CreateElement("root");
        using var subscription = document.ObserveMutations(element,
            new MutationObserverOptions { Attributes = true });
        element.SetAttribute("x", "");
        element.RemoveAttribute("x");
        var records = subscription.TakeRecords();
        records.Select(record => record.AttributeNewValue).Should().Equal("", null);
        records.Select(record => record.OldValue).Should().OnlyContain(value => value == null);
    }

    [Test]
    public void NamespaceAndPrefixReplacementPreserveBothNamesAndTransitionValues()
    {
        var document = Document.CreateXml();
        var element = document.CreateElement("root");
        using var subscription = document.ObserveMutations(element,
            new MutationObserverOptions { Attributes = true, AttributeOldValue = true });
        element.SetAttributeNS("urn:test", "first:local", "one");
        var original = element.GetAttributeNodeNS("urn:test", "local")!;
        element.SetAttributeNS("urn:test", "ignored:local", "two");
        var replacement = document.CreateAttributeNS("urn:test", "second:local");
        replacement.Value = "three";
        element.SetAttributeNode(replacement);
        element.RemoveAttributeNS("urn:test", "local");
        original.Prefix = "detached";
        replacement.Prefix = "also-detached";
        replacement.Value = "final detached value";
        var records = subscription.TakeRecords();
        records.Select(record => record.AttributeNewValue).Should().Equal("one", "two", "three", null);
        records.Select(record => record.OldValue).Should().Equal(null, "one", "two", "three");
        records.Select(record => record.AttributeName).Should().OnlyContain(name => name == "local");
        records.Select(record => record.AttributeNamespace).Should().OnlyContain(uri => uri == "urn:test");
        records.Select(record => record.AttributeQualifiedName).Should().Equal(
            "first:local", "first:local", "second:local", "second:local");
        records.Select(record => record.AttributePreviousQualifiedName).Should().Equal(null, null, "first:local", null);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void FirstPendingCallbackCannotChangeLaterSubscriptionsTransitionSnapshot(bool remove, bool secondOldValue)
    {
        var document = Document.CreateXml();
        var element = document.CreateElement("root");
        element.SetAttribute("x", "before");
        using var first = document.ObserveMutations(element,
            new MutationObserverOptions { Attributes = true, AttributeOldValue = true });
        using var second = document.ObserveMutations(element,
            new MutationObserverOptions { Attributes = true, AttributeOldValue = secondOldValue });
        var reentered = false;
        first.PendingRecord = _ =>
        {
            if (reentered) return;
            reentered = true;
            if (remove) element.RemoveAttribute("x");
            else element.SetAttribute("x", "inner");
        };
        element.SetAttribute("x", "outer");
        reentered.Should().BeTrue();
        var firstRecords = first.TakeRecords();
        var secondRecords = second.TakeRecords();
        firstRecords.Select(record => record.AttributeNewValue).Should().Equal("outer", remove ? null : "inner");
        secondRecords.Select(record => record.AttributeNewValue).Should().Equal("outer", remove ? null : "inner");
        firstRecords.Select(record => record.OldValue).Should().Equal("before", "outer");
        secondRecords.Select(record => record.OldValue).Should().Equal(
            secondOldValue ? "before" : null, secondOldValue ? "outer" : null);
        element.GetAttribute("x").Should().Be(remove ? null : "inner");
    }

    [Test]
    public void NonAttributeRecordsHaveNoAttributeValueMetadata()
    {
        var document = Document.CreateXml();
        var element = document.CreateElement("root");
        using var subscription = document.ObserveMutations(element,
            new MutationObserverOptions { ChildList = true, CharacterData = true, Subtree = true });
        var text = document.CreateTextNode("before");
        element.AppendChild(text);
        text.Data = "after";
        var records = subscription.TakeRecords();
        records.Select(record => record.Kind).Should().Equal(MutationRecordKind.ChildList, MutationRecordKind.CharacterData);
        records.Select(record => record.AttributeNewValue).Should().OnlyContain(value => value == null);
    }
}
