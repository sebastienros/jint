#nullable enable

using Jint.Browser.Accessibility;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeReflectionBindingTests
{
    [Test]
    public void InputTypeAndDefaultValueReadsKeepCurrentValueStateCold()
    {
        using var dom = DomTestFixture.Create("<input id=i type=NuMbEr value='2 invalid'>");
        var input = ContentDom.ElementById(dom.Document, "i")!;
        input.ExistingInputValueState.Should().BeNull();
        dom.Text("document.getElementById('i').type").Should().Be("number");
        dom.Text("document.getElementById('i').defaultValue").Should().Be("2 invalid");
        input.ExistingInputValueState.Should().BeNull();
        dom.Execute("document.getElementById('i').defaultValue='3 invalid';");
        input.ExistingInputValueState.Should().BeNull();
        dom.Text("document.getElementById('i').getAttribute('value')").Should().Be("3 invalid");
    }

    [Test]
    public void DefaultValueReflectionPreservesCleanAndDirtyNativeValueBehavior()
    {
        using var dom = DomTestFixture.Create("<input id=i value=initial>");
        dom.Execute("var i=document.getElementById('i'); i.defaultValue='clean';");
        dom.Text("i.value").Should().Be("clean");
        dom.Execute("i.value='dirty'; i.defaultValue='next';");
        dom.Text("i.value").Should().Be("dirty");
        dom.Text("i.defaultValue").Should().Be("next");
    }

    [Test]
    public void FormSubmissionAttributesNormalizeKnownValuesAndPreserveAliases()
    {
        using var dom = DomTestFixture.Create("<form id=f method=POST enctype=TEXT/PLAIN><button id=b formenctype=MULTIPART/FORM-DATA></button><input id=i formenctype=TEXT/PLAIN></form>");
        dom.Execute("var f=document.getElementById('f'), b=document.getElementById('b'), i=document.getElementById('i');");
        dom.Text("f.method").Should().Be("post");
        dom.Text("f.encoding").Should().Be("text/plain");
        dom.Text("b.formEncType").Should().Be("multipart/form-data");
        dom.Text("i.formEncType").Should().Be("text/plain");
        dom.Execute("f.encoding='invalid'; f.method='invalid'; b.formEncType='text/plain';");
        dom.Text("f.enctype").Should().Be("application/x-www-form-urlencoded");
        dom.Text("f.method").Should().Be("get");
        dom.Text("b.formEnctype").Should().Be("text/plain");
        dom.Text("f.getAttribute('enctype')").Should().Be("invalid");
    }
}
