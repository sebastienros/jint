#nullable enable

namespace Jint.Tests.Browser.Dom;

public sealed class NativeValidityBindingTests
{
    [Test]
    public void SavedValidityObjectTracksNativeControlAndCustomMessage()
    {
        using var dom = DomTestFixture.Create("<input id=i required>");
        dom.Execute("var i=document.getElementById('i'), v=i.validity;");
        dom.Bool("v===i.validity && v instanceof ValidityState && v.valueMissing && !v.valid").Should().BeTrue();
        dom.Execute("i.value='present';");
        dom.Bool("v.valid && !v.valueMissing").Should().BeTrue();
        dom.Execute("i.setCustomValidity('custom');");
        dom.Bool("v.customError && !v.valid").Should().BeTrue();
        dom.Text("i.validationMessage").Should().Be("custom");
        dom.Execute("i.setCustomValidity('');");
        dom.Bool("v.valid && !v.customError").Should().BeTrue();
    }

    [Test]
    public void CheckValidityUsesTheOwnersInvalidEventAlgorithm()
    {
        using var dom = DomTestFixture.Create("<input id=i required>");
        dom.Execute("var i=document.getElementById('i'), count=0; i.addEventListener('invalid',e=>{count++;e.preventDefault()});");
        dom.Bool("i.checkValidity()").Should().BeFalse();
        dom.Number("count").Should().Be(1);
    }
}
