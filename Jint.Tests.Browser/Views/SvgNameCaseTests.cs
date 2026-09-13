namespace Jint.Tests.Browser.Views;

public sealed class SvgNameCaseTests
{
    [TestCase("SVG")]
    [TestCase("CIRCLE")]
    [TestCase("ForeignObject")]
    public async Task FactoryCloneAndImportKeepCaseAndUseTheGenericInterface(string name)
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='host'></div>");
        await page.EvaluateAsync("globalThis.svgName = " + System.Text.Json.JsonSerializer.Serialize(name));
        (await page.EvaluateAsync<bool>("""
            const ns = 'http://www.w3.org/2000/svg';
            const e = document.createElementNS(ns, 'p:' + svgName);
            e.append(document.createTextNode('text')); host.append(e);
            const clone = e.cloneNode(true);
            const xml = document.implementation.createDocument(null, 'root');
            const imported = xml.importNode(e, true);
            const made = xml.createElementNS(ns, 'p:' + svgName);
            [e, clone, imported, made].every(n => n.localName === svgName && n.prefix === 'p' &&
              n.namespaceURI === ns && Object.getPrototypeOf(n) === SVGElement.prototype) &&
              e === host.firstChild && clone.textContent === 'text' && imported.textContent === 'text'
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task KnownSvgNamesAndHtmlParserAdjustmentsKeepTheirNativeInterfaces()
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<svg id='root'><foreignobject id='foreign'></foreignobject></svg>");
        (await page.EvaluateAsync<bool>("""
            const ns = 'http://www.w3.org/2000/svg';
            const svg = document.createElementNS(ns, 'svg');
            const foreignObject = document.createElementNS(ns, 'foreignObject');
            root.localName === 'svg' && root instanceof SVGSVGElement && svg instanceof SVGSVGElement &&
              foreign.localName === 'foreignObject' && foreignObject.localName === 'foreignObject' &&
              Object.getPrototypeOf(foreignObject) === Object.getPrototypeOf(foreign)
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task DomParserDocumentsUseTheSameFactoryForCreationAndCloning()
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>test</p>");
        (await page.EvaluateAsync<bool>("""
            const parser = new DOMParser();
            const html = parser.parseFromString('<p>test</p>', 'text/html');
            const xml = parser.parseFromString('<root/>', 'text/xml');
            [html, xml].every(doc => {
              const e = doc.createElementNS('http://www.w3.org/2000/svg', 'SVG');
              return e.localName === 'SVG' && e.cloneNode().localName === 'SVG' &&
                Object.getPrototypeOf(e) === SVGElement.prototype;
            });
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }
}
