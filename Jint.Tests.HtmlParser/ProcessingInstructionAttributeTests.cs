#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class ProcessingInstructionAttributeTests
{
    [Test]
    public void ConstructionAndDataReplacementKeepAttributeStateLazy()
    {
        var document = Document.CreateHtml();
        var pi = document.CreateProcessingInstruction("marker", "name='part'");
        pi.HasAttributeState.Should().BeFalse();
        pi.Data = "name='changed'";
        pi.HasAttributeState.Should().BeFalse();
        var clone = (ProcessingInstruction) pi.CloneNode();
        clone.HasAttributeState.Should().BeFalse();
        pi.GetAttribute("name").Should().Be("changed");
        pi.HasAttributeState.Should().BeTrue();
        pi.Data = pi.Data;
        pi.HasAttributeState.Should().BeFalse();
    }

    [Test]
    public void XmlGrammarDecodesOnlyXmlReferencesAndPreservesCaseOrderAndWhitespace()
    {
        var pi = Document.CreateHtml().CreateProcessingInstruction("marker",
            " \tName = 'first' name=\"&amp;&lt;&gt;&quot;&apos;&#65;&#x1F642;\r\n\t\" ");
        pi.HasAttributes().Should().BeTrue();
        pi.GetAttributeNames().Should().Equal("Name", "name");
        pi.GetAttribute("Name").Should().Be("first");
        pi.GetAttribute("name").Should().Be("&<>\"'A🙂\r\n\t");
        pi.GetAttribute("NAME").Should().BeNull();
        pi.HasAttribute("NAME").Should().BeFalse();
        var names = pi.GetAttributeNames();
        names[0] = "changed";
        pi.GetAttributeNames().Should().Equal("Name", "name");
    }

    [TestCase("a='1' a='2'")]
    [TestCase("a='1' bad")]
    [TestCase("a='1'b='2'")]
    [TestCase("a=unquoted")]
    [TestCase("a='&nbsp;'")]
    [TestCase("a='&amp'")]
    [TestCase("a='&#0;'")]
    [TestCase("a='&#xD800;'")]
    [TestCase("a='&#x110000;'")]
    [TestCase("a='&#xFFFE;'")]
    [TestCase("a='&#X41;'")]
    [TestCase("a='&#;'")]
    [TestCase("a='&#x;'")]
    [TestCase("a='<'")]
    [TestCase("1a='1'")]
    [TestCase("a='1'\fb='2'")]
    [TestCase("a='1'\0")]
    [TestCase("a='unterminated")]
    public void AnyGrammarFailureDiscardsTheWholeMap(string data)
    {
        var pi = Document.CreateXml().CreateProcessingInstruction("start", data);
        pi.HasAttributes().Should().BeFalse();
        pi.GetAttributeNames().Should().BeEmpty();
        pi.GetAttribute("a").Should().BeNull();
    }

    [Test]
    public void SetRemoveAndTogglePreserveMapOrderAndSerializeThroughData()
    {
        var pi = Document.CreateXml().CreateProcessingInstruction("marker", "a='1' b='2'");
        pi.SetAttribute("a", "&<>\"'");
        pi.Data.Should().Be("a=\"&amp;&lt;&gt;&quot;'\" b=\"2\"");
        pi.GetAttribute("a").Should().Be("&<>\"'");
        pi.ToggleAttribute("b", true).Should().BeTrue();
        pi.GetAttribute("b").Should().Be("2");
        pi.ToggleAttribute("c", false).Should().BeFalse();
        pi.ToggleAttribute("c").Should().BeTrue();
        pi.GetAttributeNames().Should().Equal("a", "b", "c");
        pi.ToggleAttribute("b").Should().BeFalse();
        pi.SetAttribute("b", "last");
        pi.GetAttributeNames().Should().Equal("a", "c", "b");
        pi.RemoveAttribute("missing");
        pi.GetAttributeNames().Should().Equal("a", "c", "b");
        pi.Data.Should().Be("a=\"&amp;&lt;&gt;&quot;'\" c=\"\" b=\"last\"");
    }

    [Test]
    public void EqualValueReplaceDataInvalidatesAnAuthoritativeNonXmlMap()
    {
        var pi = Document.CreateXml().CreateProcessingInstruction("marker", "");
        pi.SetAttribute("<name", "kept"); // Valid DOM local name, not an XML Name.
        pi.GetAttribute("<name").Should().Be("kept");
        pi.Data = pi.Data;
        pi.HasAttributes().Should().BeFalse();
        pi.SetAttribute("1name", "kept");
        pi.GetAttribute("1name").Should().Be("kept");
        pi.ReplaceData(0, 0, "");
        pi.HasAttributes().Should().BeFalse();
    }

    [TestCase("")]
    [TestCase("a b")]
    [TestCase("a/b")]
    [TestCase("a=b")]
    [TestCase("a>b")]
    [TestCase("a\0b")]
    public void AttributeMutationsValidateDomLocalNamesBeforeChangingData(string name)
    {
        var pi = Document.CreateXml().CreateProcessingInstruction("marker", "a='1'");
        var error = Assert.Throws<DomException>(() => pi.SetAttribute(name, "value"));
        error.Should().BeOfType<DomException>();
        ((DomException) error!).Name.Should().Be("InvalidCharacterError");
        Assert.Throws<DomException>(() => pi.ToggleAttribute(name)).Should().BeOfType<DomException>();
        pi.Data.Should().Be("a='1'");
    }

    [Test]
    public void NativeAttributePreparationResumesWithoutRescanningAndRestartsAfterDataMutation()
    {
        foreach (var length in new[] { 1000, 10000 })
        {
            var pi = Document.CreateHtml().CreateProcessingInstruction("marker", "name='" + new string('x', length) + "'");
            var work = 0;
            bool done;
            do
            {
                done = pi.PrepareAttributes(1, CancellationToken.None, out var used);
                used.Should().BeLessThanOrEqualTo(2);
                work += used;
            } while (!done);
            work.Should().BeInRange(length, length + 20);
            pi.GetAttribute("name").Should().HaveLength(length);
            pi.PrepareAttributes(1, CancellationToken.None, out var cached).Should().BeTrue();
            cached.Should().Be(0);
            pi.Data = "name='new'";
            pi.GetAttribute("name").Should().Be("new");
        }
        var pending = Document.CreateHtml().CreateProcessingInstruction("marker", "name='" + new string('x', 10000) + "'");
        pending.PrepareAttributes(1, CancellationToken.None, out _).Should().BeFalse();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => pending.PrepareAttributes(4096, cancellation.Token, out _)).Should().BeOfType<OperationCanceledException>();
        pending.Data = "name='replacement'";
        pending.GetAttribute("name").Should().Be("replacement");
    }

    [Test]
    public void CloneImportAndAdoptionKeepNativeDataAndIndependentAttributeMaps()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateXml();
        var pi = source.CreateProcessingInstruction("marker", "name='one'");
        pi.GetAttribute("name").Should().Be("one");
        var clone = (ProcessingInstruction) pi.CloneNode();
        var imported = (ProcessingInstruction) destination.ImportNode(pi);
        clone.HasAttributes().Should().BeFalse();
        imported.HasAttributes().Should().BeFalse();
        ((ProcessingInstruction) clone.CloneNode()).HasAttributes().Should().BeFalse();
        clone.Data = clone.Data;
        clone.GetAttribute("name").Should().Be("one");
        clone.SetAttribute("name", "two");
        imported.SetAttribute("name", "three");
        pi.GetAttribute("name").Should().Be("one");
        clone.GetAttribute("name").Should().Be("two");
        imported.GetAttribute("name").Should().Be("three");
        destination.AdoptNode(pi);
        pi.GetAttribute("name").Should().Be("one");
        pi.OwnerDocument.Should().BeSameAs(destination);
    }

    [Test]
    public void AttributeWritesRepairLiveRangesAndQueueCharacterDataIncludingEqualWrites()
    {
        var document = Document.CreateHtml();
        var pi = document.CreateProcessingInstruction("marker", "name='one'");
        var range = document.CreateRange();
        range.SetStart(new(pi), 2);
        range.SetEnd(new(pi), (uint) pi.Data.Length);
        using var subscription = document.ObserveMutations(pi,
            new MutationObserverOptions { CharacterData = true, CharacterDataOldValue = true });
        pi.SetAttribute("name", "two");
        range.Start.Offset.Should().Be(0);
        range.End.Offset.Should().Be(0);
        pi.SetAttribute("name", "two");
        var records = subscription.TakeRecords();
        records.Should().HaveCount(2);
        records[0].Kind.Should().Be(MutationRecordKind.CharacterData);
        records[0].OldValue.Should().Be("name='one'");
        records[1].OldValue.Should().Be("name=\"two\"");
        pi.GetAttribute("name").Should().Be("two");

        range.SetStart(new(pi), 0);
        range.SetEnd(new(pi), 1);
        range.DeleteContents();
        pi.GetAttribute("name").Should().BeNull();
        pi.GetAttribute("ame").Should().Be("two");
    }

    [Test]
    public void HostWorkCountsLongReadsAndCancellationBeforeWriteLeavesDataAndMapUnchanged()
    {
        var document = Document.CreateHtml();
        var pi = document.CreateProcessingInstruction("marker", "name='" + new string('x', 10000) + "'");
        var count = 0;
        pi.GetAttribute("name", new(units => count += units, default)).Should().HaveLength(10000);
        count.Should().BeInRange(10000, 10060);
        var original = pi.Data;
        using var cancellation = new CancellationTokenSource();
        count = 0;
        var work = new ProcessingInstructionAttributeWork(units =>
        {
            count += units;
            if (count > 500) cancellation.Cancel();
        }, cancellation.Token);
        Assert.Throws<OperationCanceledException>(() => pi.SetAttribute("name", new string('y', 10000), work));
        pi.Data.Should().Be(original);
        pi.GetAttribute("name").Should().Be(new string('x', 10000));
    }

    [Test]
    public void LongDuplicateNamesChargeComparisonAndPollCancellation()
    {
        var name = new string('n', 10000);
        var pi = Document.CreateHtml().CreateProcessingInstruction("marker", name + "='one' " + name + "='two'");
        var work = 0;
        bool done;
        do
        {
            done = pi.PrepareAttributes(1, default, out var used);
            work += used;
        } while (!done);
        pi.HasAttributes().Should().BeFalse();
        work.Should().BeInRange(30000, 30050);

        var cancelled = Document.CreateHtml().CreateProcessingInstruction("marker", name + "='one' " + name + "='two'");
        using var cancellation = new CancellationTokenSource();
        var count = 0;
        Assert.Throws<OperationCanceledException>(() => cancelled.HasAttributes(new(units =>
        {
            count += units;
            if (count > 21000) cancellation.Cancel();
        }, cancellation.Token)));
        count.Should().BeGreaterThan(21000);
    }

    [Test]
    public void ThrowingNativeRangeNotificationLeavesCommittedDataAndMapCoherent()
    {
        var document = Document.CreateHtml();
        var pi = document.CreateProcessingInstruction("marker", "name='before'");
        var range = document.CreateRange();
        range.SelectNodeContents(new(pi));
        using var subscription = range.ObserveChanges(document);
        var failure = new InvalidOperationException("notification");
        document.PendingRangeChanges = () => throw failure;
        Assert.Throws<InvalidOperationException>(() => pi.SetAttribute("name", "after")).Should().BeSameAs(failure);
        pi.Data.Should().Be("name=\"after\"");
        pi.GetAttribute("name").Should().Be("after");
        document.RangeOperationDepth.Should().Be(0);
    }

    [Test]
    public void CancellationFromNativeRangeNotificationIsObservedAfterTheCoherentAttributeCommit()
    {
        var document = Document.CreateHtml();
        var pi = document.CreateProcessingInstruction("marker", "name='before'");
        var range = document.CreateRange();
        range.SelectNodeContents(new(pi));
        using var subscription = range.ObserveChanges(document);
        using var cancellation = new CancellationTokenSource();
        document.PendingRangeChanges = cancellation.Cancel;
        Assert.Throws<OperationCanceledException>(() => pi.SetAttribute("name", "after", new(null, cancellation.Token)));
        pi.Data.Should().Be("name=\"after\"");
        pi.GetAttribute("name").Should().Be("after");
        document.RangeOperationDepth.Should().Be(0);
    }
}
