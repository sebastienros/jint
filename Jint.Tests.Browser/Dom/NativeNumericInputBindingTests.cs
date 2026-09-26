namespace Jint.Tests.Browser.Dom;

public sealed class NativeNumericInputBindingTests
{
    [Test]
    public void NumberAndSteppingUseTheSameNativeCurrentValue()
    {
        using var dom = DomTestFixture.Create("<input id=i type=number min=0 max=10 step=2 value=2>");
        dom.Execute("var i=document.getElementById('i'); i.stepUp();");
        dom.Text("i.value").Should().Be("4");
        dom.Number("i.valueAsNumber").Should().Be(4);
        dom.Execute("i.valueAsNumber=6; i.stepDown(2);");
        dom.Text("i.value").Should().Be("2");
        dom.Execute("i.valueAsNumber=NaN;");
        dom.Bool("i.value==='' && Number.isNaN(i.valueAsNumber)").Should().BeTrue();
        dom.Text("(()=>{try{i.valueAsNumber=Infinity}catch(e){return e.name}})()").Should().Be("TypeError");
    }

    [Test]
    public void DateConversionReadsTheDateSlotWithoutCallingUserMethods()
    {
        using var dom = DomTestFixture.Create("<input id=i type=date value=2020-02-29>");
        dom.Execute("var i=document.getElementById('i'), d=i.valueAsDate; d.getTime=()=>{throw Error('must not call')}; i.valueAsDate=d;");
        dom.Text("i.value").Should().Be("2020-02-29");
        dom.Bool("d instanceof Date && i.valueAsDate!==d && i.valueAsDate.getUTCDate()===29").Should().BeTrue();
        dom.Text("(()=>{try{i.valueAsDate={valueOf(){throw Error('must not coerce')}}}catch(e){return e.name}})()").Should().Be("TypeError");
        dom.Execute("i.valueAsDate=new Date(NaN);");
        dom.Bool("i.value==='' && i.valueAsDate===null").Should().BeTrue();
        dom.Execute("i.type='text';");
        dom.Bool("Number.isNaN(i.valueAsNumber) && i.valueAsDate===null").Should().BeTrue();
        dom.Text("(()=>{try{i.stepUp()}catch(e){return e.name}})()").Should().Be("InvalidStateError");
    }

    [TestCase("text")]
    [TestCase("datetime-local")]
    public void DateSetterChecksObjectConversionThenApplicabilityThenDateBrand(string type)
    {
        using var dom = DomTestFixture.Create("<input id=i type='" + type + "'>");
        dom.Execute("var i=document.getElementById('i');");
        foreach (var value in new[] { "{}", "new Date(0)", "null", "undefined" })
            dom.Text("(()=>{try{i.valueAsDate=" + value + "}catch(e){return e.name}})()").Should().Be("InvalidStateError");
        foreach (var value in new[] { "1", "'x'", "true" })
            dom.Text("(()=>{try{i.valueAsDate=" + value + "}catch(e){return e.name}})()").Should().Be("TypeError");
    }
}
