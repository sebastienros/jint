namespace Jint.Tests.Browser.Dom;

public sealed class NativeHtmlSemanticBindingTests
{
    [Test]
    public void AnchorTextReadsActualDescendantsAndReplacesActualChildren()
    {
        using var dom = DomTestFixture.Create("<a id=a>before<span>inside</span><!-- omitted -->after</a>");
        dom.Execute("var a=document.getElementById('a'), span=a.firstElementChild;");
        dom.Text("a.text").Should().Be("beforeinsideafter");
        dom.Execute("a.text='replacement';");
        dom.Bool("a.text==='replacement' && a.childNodes.length===1 && a.firstChild.nodeType===Node.TEXT_NODE && span.parentNode===null").Should().BeTrue();
        dom.Execute("a.text='';");
        dom.Bool("a.childNodes.length===0 && a.text===''").Should().BeTrue();
    }

    [Test]
    public void AutofillGetterAndLiveMapViewUseNativeAlgorithms()
    {
        using var dom = DomTestFixture.Create("<input id=i><map id=m name=region></map><img id=a usemap=#region>");
        dom.Execute("var i=document.getElementById('i'), m=document.getElementById('m'), a=document.getElementById('a'); i.autocomplete='SECTION-test shipping street-address'; var images=m.images;");
        dom.Text("i.getAttribute('autocomplete')").Should().Be("SECTION-test shipping street-address");
        dom.Text("i.autocomplete").Should().Be("section-test shipping street-address");
        dom.Bool("images===m.images && images instanceof HTMLCollection && images.length===1 && images[0]===a").Should().BeTrue();
        dom.Execute("a.setAttribute('usemap','#other');");
        dom.Bool("images.length===0 && images.item(0)===null").Should().BeTrue();
        dom.Execute("m.name='other';");
        dom.Bool("images.length===1 && images[0]===a").Should().BeTrue();
    }

    [Test]
    public void EditingAndInheritedAttributesUseActualNativeAncestors()
    {
        using var dom = DomTestFixture.Create("<div id=p contenteditable=plaintext-only translate=no><span id=c></span></div>");
        dom.Execute("var p=document.getElementById('p'), c=document.getElementById('c');");
        dom.Bool("p.contentEditable==='plaintext-only' && c.contentEditable==='inherit' && c.isContentEditable && !c.translate").Should().BeTrue();
        dom.Execute("c.contentEditable='FALSE'; c.translate=true; c.spellcheck=true; document.designMode='ON';");
        dom.Bool("c.contentEditable==='false' && c.translate && c.spellcheck && document.designMode==='on'").Should().BeTrue();
        dom.Execute("c.contentEditable='inherit'; document.designMode='OFF'; p.contentEditable='false';");
        dom.Bool("!c.isContentEditable && document.designMode==='off'").Should().BeTrue();
    }

    [Test]
    public void ContextMenuAssignmentCanClearAndResumeTheRawIdLookup()
    {
        using var dom = DomTestFixture.Create("<div id=e contextmenu=m></div><menu id=m></menu><menu id=n></menu>");
        dom.Execute("var e=document.getElementById('e'), m=document.getElementById('m'), n=document.getElementById('n');");
        dom.Bool("e.contextMenu===m").Should().BeTrue();
        dom.Execute("e.contextMenu=n;");
        dom.Bool("e.contextMenu===n").Should().BeTrue();
        dom.Execute("e.contextMenu=null;");
        dom.Bool("e.contextMenu===m").Should().BeTrue();
    }

    [Test]
    public void UnavailableCommandsStillConvertArgumentsInOrderAndCheckTheReceiverFirst()
    {
        using var dom = DomTestFixture.Create("<form id=f></form><link id=l rel=import href=x>");
        dom.Execute("var order=[]; var answer=document.execCommand({toString(){order.push('command');return 'bold'}},false,{toString(){order.push('value');return 'v'}});");
        dom.Bool("answer===false && order.join(',')==='command,value' && document.queryCommandEnabled('bold')===false && document.queryCommandValue('bold')===''").Should().BeTrue();
        dom.Bool("(()=>{let converted=false;try{Document.prototype.execCommand.call({}, {toString(){converted=true;return 'bold'}})}catch(e){return e instanceof TypeError && !converted}})()").Should().BeTrue();
        dom.Bool("document.getElementById('l').import===null").Should().BeTrue();
        dom.Text("(()=>{try{document.getElementById('f').requestAutocomplete()}catch(e){return e.name}})()").Should().Be("NotSupportedError");
    }

    [Test]
    public void BodySetterUsesActualReplacementAndRejectsNullWithTheHtmlError()
    {
        using var dom = DomTestFixture.Create("<body><p>old</p></body>");
        dom.Execute("var old=document.body, next=document.createElement('body'); document.body=next; document.body=next;");
        dom.Bool("document.body===next && old.parentNode===null && next.parentNode===document.documentElement").Should().BeTrue();
        dom.Text("(()=>{try{document.body=null}catch(e){return e.name}})()").Should().Be("HierarchyRequestError");
        dom.Text("(()=>{try{document.body=document.createElement('div')}catch(e){return e.name}})()").Should().Be("HierarchyRequestError");
        dom.Bool("document.body===next").Should().BeTrue();
        dom.Execute("var empty=new Document(), foreign=empty.createElementNS('urn:foreign','root'), body=empty.createElementNS('http://www.w3.org/1999/xhtml','body'); empty.appendChild(foreign); empty.body=body;");
        dom.Bool("body.parentNode===foreign && foreign.firstChild===body").Should().BeTrue();
        dom.Execute("var later=empty.createElementNS('http://www.w3.org/1999/xhtml','body'); empty.body=later;");
        dom.Bool("body.parentNode===foreign && later.parentNode===foreign && body.nextSibling===later").Should().BeTrue();
    }
}
