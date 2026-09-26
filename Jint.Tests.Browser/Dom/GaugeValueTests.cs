#nullable enable

using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class GaugeValueTests
{
    [Test]
    public void MeterDerivesClampedValuesWithoutChangingTheAttributes()
    {
        using var dom = DomTestFixture.Create("<meter id=m min=5 max=2 value=100 low=-4 high=-3 optimum=999></meter>");
        dom.Execute("var m=document.getElementById('m');");
        dom.Text("JSON.stringify([m.min,m.max,m.value,m.low,m.high,m.optimum])").Should().Be("[5,5,5,5,5,5]");
        dom.Text("m.getAttribute('max')").Should().Be("2");
        dom.Execute("m.max=20; m.low=8; m.high=6; m.value=7;");
        dom.Text("JSON.stringify([m.min,m.max,m.value,m.low,m.high,m.optimum])").Should().Be("[5,20,7,8,8,20]");
    }

    [Test]
    public void ProgressDistinguishesMissingValueAndUsesTheHtmlNumberPrefix()
    {
        using var dom = DomTestFixture.Create("<progress id=p max=' +10 trailing'></progress>");
        dom.Execute("var p=document.getElementById('p');");
        dom.Number("p.position").Should().Be(-1);
        dom.Number("p.value").Should().Be(0);
        dom.Execute("p.setAttribute('value',' 5 trailing');");
        dom.Number("p.value").Should().Be(5);
        dom.Number("p.position").Should().Be(0.5);
        dom.Execute("p.value=100;");
        dom.Number("p.value").Should().Be(10);
        dom.Text("p.getAttribute('value')").Should().Be("100");
        dom.Execute("p.removeAttribute('value'); p.value=p.value;");
        dom.Number("p.position").Should().Be(0);
    }

    [Test]
    public void GaugeSemanticAttributesIgnoreOtherNamespaces()
    {
        using var dom = DomTestFixture.Create("<meter id=m></meter><progress id=p></progress>");
        dom.Execute("var m=document.getElementById('m'), p=document.getElementById('p');");
        dom.Execute("m.setAttributeNS('urn:test','min','9'); p.setAttributeNS('urn:test','value','1');");
        dom.Number("m.min").Should().Be(0);
        dom.Number("p.position").Should().Be(-1);
    }

    [Test]
    public void MeterDefaultMidpointRemainsFiniteAtBinary64Extremes()
    {
        using var dom = DomTestFixture.Create("<meter id=m min='1e308' max='1.7e308'></meter>");
        dom.Execute("var m=document.getElementById('m');");
        dom.Bool("Number.isFinite(m.optimum) && m.optimum >= m.min && m.optimum <= m.max").Should().BeTrue();
        dom.Execute("m.min=-1.7e308;");
        dom.Number("m.optimum").Should().Be(0);
    }
    [Test]
    public void ProgressMaximumAndPositionShareTheBoundedNumberConversion()
    {
        using var dom = DomTestFixture.Create("<progress id=p value=1></progress>");
        dom.Execute("var p=document.getElementById('p'); p.setAttribute('max', '2'+'0'.repeat(400)+'e-400');");
        dom.Number("p.max").Should().Be(2);
        dom.Number("p.position").Should().Be(0.5);
    }

    [Test]
    public void AColdMissingValueReadCannotSkipCancellationDuringAttributeScanning()
    {
        var element = Document.CreateHtml().CreateElementNS(Namespaces.Html, "progress");
        for (var i = 0; i < 1000; i++) element.SetAttribute("data-a" + i, "unused");
        using var cancellation = new CancellationTokenSource();
        Action read = () => DomGaugeMembers.Progress(element, true, units =>
        {
            if (units > 0) cancellation.Cancel();
        }, cancellation.Token);
        read.Should().Throw<OperationCanceledException>();
        DomGaugeMembers.Progress(element, true, null, default).Should().Be(-1);
    }

    [Test]
    public void LongNumericConversionObservesCancellation()
    {
        var element = Document.CreateHtml().CreateElementNS(Namespaces.Html, "progress");
        element.SetAttribute("max", "2" + new string('0', 100000) + "e-100000");
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        Action read = () => DomGaugeMembers.ProgressMaximum(element, _ =>
        {
            if (++calls == 2) cancellation.Cancel();
        }, cancellation.Token);
        read.Should().Throw<OperationCanceledException>();
        DomGaugeMembers.ProgressMaximum(element, null, default).Should().Be(2);
    }
}
