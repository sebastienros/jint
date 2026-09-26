#nullable enable

using Jint.Browser.Dom;
using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeAttributeTokenReadTests
{
    [Test]
    public void OrderedSetReadsStayLiveAndPreserveWeakListIdentity()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("a");
        var list = DomAttributeTokenList.Rel(element);
        list.Should().BeSameAs(DomAttributeTokenList.Of(element, "rel"));
        list.Element.Should().BeSameAs(element);
        list.Attribute.Should().Be("rel");
        element.SetAttributeNS(null, "rel", " b\ta b\nc\r\fb A\u00a0B");
        list.ReadLength(null, default).Should().Be(4);
        list.Read(null, default).Should().Equal("b", "a", "c", "A\u00a0B");
        list.ReadItem(0, null, default).Should().Be("b");
        list.ReadItem(3, null, default).Should().Be("A\u00a0B");
        list.ReadItem(4, null, default).Should().BeNull();
        list.ReadItem(uint.MaxValue, null, default).Should().BeNull();
        list.ReadContains("A\u00a0B", null, default).Should().BeTrue();
        list.ReadContains("B", null, default).Should().BeFalse();
        list.ReadValue(null, default).Should().Be(" b\ta b\nc\r\fb A\u00a0B");
        list.Value = "new new";
        list.ReadSnapshot(null, default).Should().Equal("new");
        element.SetAttributeNS("urn:test", "rel", "ignored");
        element.RemoveAttributeNS(null, "rel");
        list.ReadLength(null, default).Should().Be(0);
        Document.CreateHtml().AdoptNode(element);
        DomAttributeTokenList.Rel(element).Should().BeSameAs(list);
        DomAttributeTokenList.Rel((Element) element.CloneNode()).Should().NotBeSameAs(list);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TokenReadsCheckActualLongTokenAndDelimiterCharacters(bool delimiters)
    {
        var element = Document.CreateHtml().CreateElement("div");
        element.SetAttributeNS(null, "class", new string(delimiters ? ' ' : 'x', 8192) + "end");
        var list = DomAttributeTokenList.Of(element, "class");
        using var cancellation = new CancellationTokenSource();
        var charged = 0;
        void Check(int units)
        {
            charged += units;
            if (charged >= 256) cancellation.Cancel();
        }
        Caught.Exception(() => list.ReadItem(0, Check, cancellation.Token)).Should().BeOfType<OperationCanceledException>();
        charged.Should().Be(256);
        list.ReadLength(null, default).Should().Be(1);
    }

    [Test]
    public void DuplicateEqualityChargesActualCharacters()
    {
        var element = Document.CreateHtml().CreateElement("div");
        var token = new string('x', 1024);
        element.SetAttributeNS(null, "class", token + " " + token);
        var list = DomAttributeTokenList.Of(element, "class");
        using var cancellation = new CancellationTokenSource();
        var charged = 0;
        void Check(int units)
        {
            charged += units;
            // Attribute lookup plus both token scans fit below this boundary; equality crosses it.
            if (charged >= 2304) cancellation.Cancel();
        }
        Caught.Exception(() => list.ReadLength(Check, cancellation.Token)).Should().BeOfType<OperationCanceledException>();
        charged.Should().Be(2304);
        list.ReadLength(null, default).Should().Be(1);
    }

    [Test]
    public void WarmIndexedHitUsesCompletedIndexAndStillChecksBeforeReturn()
    {
        var element = Document.CreateHtml().CreateElement("div");
        element.SetAttributeNS(null, "class", "first " + new string('x', 8192));
        var list = DomAttributeTokenList.Of(element, "class");
        list.ReadLength(null, default).Should().Be(2);
        var charged = 0;
        list.ReadItem(0, units => charged += units, default).Should().Be("first");
        charged.Should().BeLessThan(256);
        var checks = 0;
        Caught.Exception(() => list.ReadItem(0, _ => { if (++checks == 2) throw new OperationCanceledException(); }, default))
            .Should().BeOfType<OperationCanceledException>();
        checks.Should().Be(2);
    }

    [Test]
    public void RepeatedLengthAndItemIterationScalesWithTokenCount()
    {
        static int IterationWork(int count)
        {
            var element = Document.CreateHtml().CreateElement("div");
            element.SetAttributeNS(null, "class", string.Join(' ', Enumerable.Range(0, count).Select(index => "t" + index)));
            var list = DomAttributeTokenList.Of(element, "class");
            var charged = 0;
            void Check(int units) => charged += units;
            for (uint index = 0; index < (uint) list.ReadLength(Check, default); index++)
                list.ReadItem(index, Check, default).Should().Be("t" + index);
            return charged;
        }
        var small = IterationWork(128);
        var large = IterationWork(256);
        large.Should().BeGreaterThan(small);
        large.Should().BeLessThan(3 * small);
    }

    [Test]
    public void ViewAndRawValueStayColdAndDirectWritesInvalidateWarmIndex()
    {
        var element = Document.CreateHtml().CreateElement("div");
        var raw = new string('x', 8192) + " second";
        element.SetAttributeNS(null, "class", raw);
        var list = DomAttributeTokenList.Of(element, "class");
        var charged = 0;
        void Check(int units) => charged += units;
        list.ReadValue(Check, default).Should().BeSameAs(raw);
        charged.Should().BeLessThan(256);
        charged = 0;
        list.ReadLength(Check, default).Should().Be(2);
        charged.Should().BeGreaterThan(8192);
        charged = 0;
        list.ReadLength(Check, default).Should().Be(2);
        list.ReadItem(1, Check, default).Should().Be("second");
        charged.Should().BeLessThan(256);
        element.SetAttributeNS(null, "class", "changed");
        list.ReadItem(0, null, default).Should().Be("changed");
        element.RemoveAttributeNS(null, "class");
        list.ReadLength(null, default).Should().Be(0);
        list.Value.Should().BeEmpty();
        element.SetAttributeNS(null, "class", "");
        list.ReadLength(null, default).Should().Be(0);
        element.GetAttributeNS(null, "class").Should().BeEmpty();
        list.Value = "restored";
        list.Read(null, default).Should().Equal("restored");
    }

    [Test]
    public void InterruptedIndexBuildPublishesNothingAndNextDemandCompletesFreshBuild()
    {
        var element = Document.CreateHtml().CreateElement("div");
        element.SetAttributeNS(null, "class", new string('x', 8192) + " second");
        var list = DomAttributeTokenList.Of(element, "class");
        using var cancellation = new CancellationTokenSource();
        Caught.Exception(() => list.ReadLength(units => { if (units >= 256) cancellation.Cancel(); }, cancellation.Token))
            .Should().BeOfType<OperationCanceledException>();
        var charged = 0;
        list.ReadLength(units => charged += units, default).Should().Be(2);
        charged.Should().BeGreaterThan(8192);
        charged = 0;
        list.ReadLength(units => charged += units, default).Should().Be(2);
        charged.Should().BeLessThan(256);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SourceChangedByCheckpointRetriesWithoutPublishingStaleIndex(bool absent)
    {
        var element = Document.CreateHtml().CreateElement("div");
        if (!absent) element.SetAttributeNS(null, "class", new string('x', 8192));
        var list = DomAttributeTokenList.Of(element, "class");
        var changed = false;
        var checks = 0;
        void Check(int units)
        {
            checks++;
            if (changed || (absent ? checks != 2 : units < 256)) return;
            changed = true;
            element.SetAttributeNS(null, "class", "fresh current");
        }
        list.ReadLength(Check, default).Should().Be(2);
        changed.Should().BeTrue();
        list.Read(null, default).Should().Equal("fresh", "current");
        var charged = 0;
        list.ReadLength(units => charged += units, default).Should().Be(2);
        charged.Should().BeLessThan(256);
    }

    [Test]
    public void EnumerationDisposalRunsFinalCheckAndUsesOneInvocationSnapshot()
    {
        var element = Document.CreateHtml().CreateElement("div");
        element.SetAttributeNS(null, "class", "first second");
        var list = DomAttributeTokenList.Of(element, "class");
        var checks = 0;
        var enumerator = list.Read(_ => checks++, default).GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be("first");
        element.SetAttributeNS(null, "class", "changed");
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be("second");
        var beforeDisposal = checks;
        beforeDisposal.Should().BeGreaterThan(0);
        enumerator.Dispose();
        checks.Should().Be(beforeDisposal + 1);
        list.Read(null, default).Should().Equal("changed");
    }

    [Test]
    public void NativeAttributeReplacementAndRemovalInvalidateCapturedSourceProof()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        element.SetAttributeNS(null, "class", "one two");
        var list = DomAttributeTokenList.Of(element, "class");
        var work = new DomReadWork(null, default);
        list.ReadSnapshot(work, out var proof).Should().Equal("one", "two");
        proof.IsCurrent.Should().BeTrue();
        var old = element.GetAttributeNodeNS(null, "class")!;
        var replacement = document.CreateAttribute("class"); replacement.Value = old.Value;
        element.SetAttributeNode(replacement);
        proof.IsCurrent.Should().BeFalse();
        list.ReadSnapshot(work, out proof).Should().Equal("one", "two");
        proof.IsCurrent.Should().BeTrue();
        replacement.Value = "changed";
        proof.IsCurrent.Should().BeFalse();
        list.ReadSnapshot(work, out proof).Should().Equal("changed");
        element.RemoveAttributeNode(replacement);
        proof.IsCurrent.Should().BeFalse();
        list.ReadSnapshot(work, out proof).Should().BeEmpty();
        proof.Value.Should().BeNull();
        proof.IsCurrent.Should().BeTrue();
        element.SetAttributeNS(null, "class", "");
        proof.IsCurrent.Should().BeFalse();
        list.ReadSnapshot(work, out proof).Should().BeEmpty();
        proof.Value.Should().BeEmpty();
        proof.IsCurrent.Should().BeTrue();
    }

    [Test]
    public void CheckpointRemovalOfCurrentAttributeSlotRestartsLookupSafely()
    {
        var element = Document.CreateHtml().CreateElement("div");
        for (var i = 0; i < 128; i++) element.SetAttributeNS(null, "x" + i, "value");
        element.SetAttributeNS(null, "class", "current");
        var list = DomAttributeTokenList.Of(element, "class");
        var removed = false;
        void Check(int units)
        {
            if (removed || units < 256) return;
            removed = true;
            foreach (var attribute in element.Attributes.ToArray()) element.RemoveAttributeNode(attribute);
            element.SetAttributeNS(null, "class", "fresh");
        }
        list.ReadLength(Check, default).Should().Be(1);
        removed.Should().BeTrue();
        list.ReadItem(0, null, default).Should().Be("fresh");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FinalCheckpointReplacementOrRemovalRetriesBeforeIndexPublication(bool remove)
    {
        static Element Create()
        {
            var element = Document.CreateHtml().CreateElement("div");
            element.SetAttributeNS(null, "class", "old source");
            return element;
        }
        var baselineChecks = 0;
        DomAttributeTokenList.Of(Create(), "class").ReadLength(_ => baselineChecks++, default).Should().Be(2);
        var element = Create();
        var list = DomAttributeTokenList.Of(element, "class");
        var checks = 0;
        var changed = false;
        void Check(int units)
        {
            if (++checks != baselineChecks || changed) return;
            changed = true;
            var attribute = element.GetAttributeNodeNS(null, "class")!;
            if (remove) element.RemoveAttributeNode(attribute);
            else
            {
                var replacement = element.OwnerDocument!.CreateAttribute("class");
                replacement.Value = "fresh";
                element.SetAttributeNode(replacement);
            }
        }
        list.ReadLength(Check, default).Should().Be(remove ? 0 : 1);
        changed.Should().BeTrue();
        list.ReadItem(0, null, default).Should().Be(remove ? null : "fresh");
    }

    [Test]
    public void ExposedIndexedStringIsReusedAndDirectMutationInvalidatesIt()
    {
        var element = Document.CreateHtml().CreateElement("div");
        var raw = "prefix " + new string('x', 8192);
        element.SetAttributeNS(null, "class", raw);
        var list = DomAttributeTokenList.Of(element, "class");
        list.ReadLength(null, default).Should().Be(2);
        var charged = 0;
        var first = list.ReadItem(1, units => charged += units, default);
        charged.Should().BeGreaterThan(8192);
        charged = 0;
        list.ReadItem(1, units => charged += units, default).Should().BeSameAs(first);
        charged.Should().BeLessThan(256);
        element.GetAttributeNodeNS(null, "class")!.Value = "prefix fresh";
        list.ReadItem(1, null, default).Should().Be("fresh");
        element.GetAttributeNodeNS(null, "class")!.Value = raw;
        list.ReadItem(1, null, default).Should().NotBeSameAs(first);
    }

    [Test]
    public void WarmExposedStringRechecksSourceAfterFinalCheckpoint()
    {
        static DomAttributeTokenList Create()
        {
            var element = Document.CreateHtml().CreateElement("div");
            element.SetAttributeNS(null, "class", "prefix old");
            var list = DomAttributeTokenList.Of(element, "class");
            list.ReadItem(1, null, default).Should().Be("old");
            return list;
        }
        var baselineChecks = 0;
        Create().ReadItem(1, _ => baselineChecks++, default).Should().Be("old");
        var list = Create();
        var checks = 0;
        var changed = false;
        void Check(int units)
        {
            if (++checks != baselineChecks || changed) return;
            changed = true;
            list.Element.SetAttributeNS(null, "class", "prefix fresh");
        }
        list.ReadItem(1, Check, default).Should().Be("fresh");
        changed.Should().BeTrue();
        list.ReadItem(1, null, default).Should().Be("fresh");
    }

    [Test]
    public void WarmLongIndexedTokenCopyChecksActualCharacterWork()
    {
        var element = Document.CreateHtml().CreateElement("div");
        element.SetAttributeNS(null, "class", "first " + new string('x', 8192));
        var list = DomAttributeTokenList.Of(element, "class");
        list.ReadLength(null, default).Should().Be(2);
        using var cancellation = new CancellationTokenSource();
        Caught.Exception(() => list.ReadItem(1,
            units => { if (units >= 256) cancellation.Cancel(); }, cancellation.Token))
            .Should().BeOfType<OperationCanceledException>();
        var charged = 0;
        var first = list.ReadItem(1, units => charged += units, default);
        first.Should().Be(new string('x', 8192));
        charged.Should().BeGreaterThan(8192);
        charged = 0;
        list.ReadItem(1, units => charged += units, default).Should().BeSameAs(first);
        charged.Should().BeLessThan(256);
    }

    [Test]
    public void RawValueLookupChecksWideAttributeInventoryAndUsesCurrentValue()
    {
        var element = Document.CreateHtml().CreateElement("div");
        for (var i = 0; i < 1024; i++) element.SetAttributeNS(null, "data-" + i, "x");
        element.SetAttributeNS(null, "class", "value");
        var list = DomAttributeTokenList.Of(element, "class");
        using var cancellation = new CancellationTokenSource();
        Caught.Exception(() => list.ReadValue(units => { if (units >= 256) cancellation.Cancel(); }, cancellation.Token))
            .Should().BeOfType<OperationCanceledException>();
        list.ReadValue(null, default).Should().Be("value");
        var work = new DomReadWork(null, default);
        list.ReadLength(work).Should().Be(1);
        list.ReadItem(0, work).Should().Be("value");
        list.ReadContains("value", work).Should().BeTrue();
        list.ReadSnapshot(work).Should().Equal("value");
        list.ReadValue(work).Should().Be("value");
    }
}
