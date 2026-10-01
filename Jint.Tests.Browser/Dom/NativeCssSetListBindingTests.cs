namespace Jint.Tests.Browser.Dom;

public sealed class NativeCssSetListBindingTests
{
    [Test]
    public void StringListItemKeepsNullableBoundsAndChecksItsReceiverBeforeIndexConversion()
    {
        using var dom = DomTestFixture.Create("<style title='a'>a{color:red}</style>");
        dom.Execute("var names=document.styleSheetSets;");
        dom.Bool("names.item(0)==='a' && names.item(-1)===null && names.item(1)===null && names.item(2147483647)===null")
            .Should().BeTrue();
        dom.Bool("names[0]==='a' && names[1]===undefined && !Object.prototype.hasOwnProperty.call(names,'1')")
            .Should().BeTrue();
        dom.Bool("""
            (() => {
                let conversions=0;
                try {
                    DOMStringList.prototype.item.call({}, {valueOf(){conversions++; return 0}});
                    return false;
                } catch (error) { return error instanceof TypeError && conversions===0; }
            })()
            """).Should().BeTrue();
        dom.Execute("document.querySelector('style').remove()");
        dom.Bool("names===document.styleSheetSets && names.length===0 && names.item(0)===null && names[0]===undefined")
            .Should().BeTrue();
    }

    [Test]
    public void StylesheetSetNamesAreLiveStableAndShareTheActualDisabledAuthority()
    {
        using var dom = DomTestFixture.Create("<style title='a'>a{color:red}</style><style title='a'>b{color:blue}</style><style title='b'>c{color:green}</style>");
        dom.Execute("var sheets=document.styleSheets; sheets.length; var names=document.styleSheetSets;");
        dom.Bool("names instanceof DOMStringList && names===document.styleSheetSets && names.length===2 && names[0]==='a' && names.item(1)==='b'").Should().BeTrue();
        dom.Bool("names.item(-1)===null && names.item(2)===null && names[2]===undefined && names.contains('a') && !names.contains('A')").Should().BeTrue();
        dom.Execute("document.selectedStyleSheetSet='b';");
        dom.Bool("document.selectedStyleSheetSet==='b' && document.lastStyleSheetSet==='b' && document.preferredStyleSheetSet==='a' && sheets[0].disabled && sheets[1].disabled && !sheets[2].disabled").Should().BeTrue();
        dom.Execute("document.enableStyleSheetsForSet('a'); document.querySelector('style').title='c';");
        dom.Bool("document.lastStyleSheetSet==='b' && names===document.styleSheetSets && names.length===3 && names[0]==='c' && names[1]==='a'").Should().BeTrue();
        dom.Bool("(()=>{let converted=false;try{DOMStringList.prototype.contains.call({}, {toString(){converted=true;return 'a'}})}catch(error){return error instanceof TypeError && !converted}})()").Should().BeTrue();
    }

    [Test]
    public async Task ShadowStylesheetListsRetainIdentityAndEnumerateOnlyTheirOwnTree()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>a{color:red}</style><div id='host'></div>");
        await page.EvaluateAsync("var host=document.getElementById('host'), shadow=host.attachShadow({mode:'open'}); shadow.innerHTML='<style>b{color:blue}</style>'; var list=shadow.styleSheets;");
        (await page.EvaluateAsync<bool>("list instanceof StyleSheetList && list===shadow.styleSheets && list.length===1 && document.styleSheets.length===1")).Should().BeTrue();
        await page.EvaluateAsync("shadow.appendChild(document.createElement('style')); host.remove();");
        (await page.EvaluateAsync<bool>("list===shadow.styleSheets && list.length===0 && list.item(0)===null && list[0]===undefined")).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }
}
