namespace Jint.Tests.Browser.Dom;

public sealed class NativeInputSelectionBindingTests
{
    [Test]
    public void SelectionUsesNativeUtf16OffsetsDirectionAndUnsignedConversion()
    {
        using var dom = DomTestFixture.Create("<input id=i value='a😀b'>");
        dom.Execute("var i=document.getElementById('i'); i.setSelectionRange(1,3,'backward');");
        dom.Bool("i.selectionStart===1 && i.selectionEnd===3 && i.selectionDirection==='backward'").Should().BeTrue();
        dom.Execute("i.selectionStart=-1;");
        dom.Bool("i.selectionStart===4 && i.selectionEnd===4").Should().BeTrue();
        dom.Execute("i.selectionEnd=null;");
        dom.Bool("i.selectionStart===0 && i.selectionEnd===0").Should().BeTrue();
        dom.Execute("i.select();");
        dom.Bool("i.selectionStart===0 && i.selectionEnd===4 && i.selectionDirection==='none'").Should().BeTrue();
        dom.Bool("Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'selectionDirection').set===undefined").Should().BeTrue();
    }

    [Test]
    public void UnsupportedSelectionGettersReturnNullAndSettersUseNativeInvalidState()
    {
        using var dom = DomTestFixture.Create("<input id=i type=number value=12>");
        dom.Execute("var i=document.getElementById('i');");
        dom.Bool("i.selectionStart===null && i.selectionEnd===null && i.selectionDirection===null").Should().BeTrue();
        dom.Text("(() => {try {i.selectionStart=1;} catch(e) {return e.name;}})()").Should().Be("InvalidStateError");
        dom.Text("(() => {try {i.setSelectionRange(0,1);} catch(e) {return e.name;}})()").Should().Be("InvalidStateError");
    }

    [Test]
    public void ChangedRangesQueueSelectAndCoalesceSelectionchange()
    {
        using var dom = DomTestFixture.Create("<input id=i value=abcd>");
        dom.Execute("var i=document.getElementById('i'), selects=0, changes=0; i.addEventListener('select',()=>selects++); i.addEventListener('selectionchange',()=>changes++); i.setSelectionRange(1,2); i.setSelectionRange(2,3); i.setSelectionRange(2,3);");
        dom.Bool("selects===0 && changes===0").Should().BeTrue();
        dom.Engine.Tasks.ProcessTasks();
        dom.Bool("selects===2 && changes===1").Should().BeTrue();
    }
}
