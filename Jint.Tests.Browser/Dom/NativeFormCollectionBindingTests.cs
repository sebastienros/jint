namespace Jint.Tests.Browser.Dom;

public sealed class NativeFormCollectionBindingTests
{
    [Test]
    public void PastNamesSurviveRenameButNotAnOwnerTransitionBetweenReads()
    {
        using var dom = DomTestFixture.Create("<form id=f><input id=a name=old></form><form id=other></form>");
        dom.Execute("var f=document.getElementById('f'), a=document.getElementById('a'), other=document.getElementById('other'); var remembered=f.old; a.name='new';");
        dom.Bool("remembered===a && f.old===a && f.elements.namedItem('old')===null").Should().BeTrue();
        dom.Execute("other.appendChild(a); f.appendChild(a);");
        dom.Bool("f.old===undefined && f.new===a").Should().BeTrue();
    }

    [Test]
    public void FormNamedGetterFallsBackToImagesAndEnumeratesTreeAndSourceOrder()
    {
        using var dom = DomTestFixture.Create("<form id=f><img id=picture name=pictureName><input id=first name=old><input id=second name=current><input type=image name=excluded></form>");
        dom.Execute("var f=document.getElementById('f'), first=document.getElementById('first'); var remembered=f.old; first.name='renamed';");
        dom.Bool("f.picture===f.pictureName && f.picture instanceof HTMLImageElement && f.elements.namedItem('picture')===null && f.excluded===undefined").Should().BeTrue();
        dom.Text("Object.getOwnPropertyNames(f).filter(x=>!/^\\d+$/.test(x)).join(',')").Should().Be("picture,pictureName,first,renamed,old,second,current");
    }

    [Test]
    public void CheckedAndIndeterminateUseNativeFlagsAndIdlDirtyCheckedness()
    {
        using var dom = DomTestFixture.Create("<form><input id=a type=checkbox checked><input id=b type=radio name=g checked><input id=c type=radio name=g></form>");
        dom.Execute("var a=document.getElementById('a'), b=document.getElementById('b'), c=document.getElementById('c'); var events=0; for(const n of [a,b,c]) n.onchange=()=>events++; a.checked=false; a.defaultChecked=false; a.defaultChecked=true; a.indeterminate=true; c.checked=true;");
        dom.Bool("!a.checked && a.defaultChecked && a.indeterminate && c.checked && !b.checked && events===0").Should().BeTrue();
    }

    [Test]
    public void ControlsUseNativeOwnersAndExcludeImageInputsFromElements()
    {
        using var dom = DomTestFixture.Create("<input id=external form=f><form id=f><input id=a name=one><input type=image name=image><img name=picture></form>");
        dom.Execute("var f=document.getElementById('f'), external=document.getElementById('external'), a=document.getElementById('a'), controls=f.elements;");
        dom.Bool("controls===f.elements && controls instanceof HTMLFormControlsCollection && controls.length===2 && controls[0]===external && controls[1]===a && f.length===2").Should().BeTrue();
        dom.Execute("external.removeAttribute('form');");
        dom.Bool("controls.length===1 && controls[0]===a && f[0]===a").Should().BeTrue();
    }

    [Test]
    public void DuplicateControlNamesProduceFreshLiveRadioListsWithNativeCheckedness()
    {
        using var dom = DomTestFixture.Create("<form id=f><input id=a name=g type=radio value=a checked><input id=b name=g type=radio value=b><input id=c name=g></form>");
        dom.Execute("var f=document.getElementById('f'), a=document.getElementById('a'), b=document.getElementById('b'), c=document.getElementById('c'), group=f.elements.namedItem('g');");
        dom.Bool("group instanceof RadioNodeList && group instanceof NodeList && group!==f.elements.namedItem('g') && group.length===3 && group[0]===a && group.value==='a'").Should().BeTrue();
        dom.Execute("group.value='b';");
        dom.Bool("b.checked && !a.checked && group.value==='b'").Should().BeTrue();
        dom.Execute("c.name='other'; a.remove();");
        dom.Bool("group.length===1 && group[0]===b && f.elements.namedItem('g')===b").Should().BeTrue();
    }

    [Test]
    public void RadioListValueUsesAbsentValueAsOnAndSkipsNonRadios()
    {
        using var dom = DomTestFixture.Create("<form id=f><input name=g value=on><input id=a type=radio name=g><input id=b type=radio name=g value=on></form>");
        dom.Execute("var f=document.getElementById('f'), a=document.getElementById('a'), b=document.getElementById('b'), group=f.elements.namedItem('g'); group.value='on';");
        dom.Bool("group.value==='on' && a.checked && !b.checked").Should().BeTrue();
        dom.Execute("group.value='missing';");
        dom.Bool("a.checked && group.value==='on'").Should().BeTrue();
    }
}
