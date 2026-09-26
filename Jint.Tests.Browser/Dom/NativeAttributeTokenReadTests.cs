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
    public void IndexedHitStopsBeforeUnneededSuffixAndStillChecksBeforeReturn()
    {
        var element = Document.CreateHtml().CreateElement("div");
        element.SetAttributeNS(null, "class", "first " + new string('x', 8192));
        var list = DomAttributeTokenList.Of(element, "class");
        var charged = 0;
        list.ReadItem(0, units => charged += units, default).Should().Be("first");
        charged.Should().BeLessThan(256);
        var checks = 0;
        Caught.Exception(() => list.ReadItem(0, _ => { if (++checks == 2) throw new OperationCanceledException(); }, default))
            .Should().BeOfType<OperationCanceledException>();
        checks.Should().Be(2);
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
        checks.Should().Be(1);
        enumerator.Dispose();
        checks.Should().Be(2);
        list.Read(null, default).Should().Equal("changed");
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
