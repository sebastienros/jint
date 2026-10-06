namespace Jint.Tests.Browser.Dom;

public sealed class NativeDocumentCssRealmTests
{
    [Test]
    public async Task ManufacturedDocumentsHaveTheirCreationRealmBeforeAnyCssRead()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>principal</p>");
        (await page.EvaluateAsync<bool>("""
            (() => {
                const markup='<div id="t" style="white-space:pre">  a  b  </div>';
                const html=document.implementation.createHTMLDocument();
                html.body.innerHTML=markup;
                const xml=document.implementation.createDocument('http://www.w3.org/1999/xhtml', 'div');
                xml.documentElement.id='t';
                xml.documentElement.setAttribute('style', 'white-space:pre');
                xml.documentElement.textContent='  a  b  ';
                const parsedHtml=new DOMParser().parseFromString(markup, 'text/html');
                const parsedXml=new DOMParser().parseFromString(
                    '<div xmlns="http://www.w3.org/1999/xhtml" id="t" style="white-space:pre">  a  b  </div>', 'text/xml');
                return [html, xml, parsedHtml, parsedXml].every(doc => doc.querySelector('#t').innerText === '  a  b  ');
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }
}
