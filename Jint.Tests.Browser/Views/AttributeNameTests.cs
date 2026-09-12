#nullable enable
namespace Jint.Tests.Browser.Views;

public sealed class AttributeNameTests
{
    [TestCase("0name")]
    [TestCase("-name")]
    [TestCase(".name")]
    [TestCase("a!b")]
    [TestCase("a@b")]
    [TestCase("\u0080")]
    [TestCase("😀")]
    public async Task ModernNamesProduceUsableNativeAttributes(string name)
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='target'></div>");
        await page.EvaluateAsync("globalThis.attributeName = " + System.Text.Json.JsonSerializer.Serialize(name));
        (await page.EvaluateAsync<bool>("""
            const attr = document.createAttribute(attributeName);
            attr.value = 'value'; target.setAttributeNode(attr);
            const native = target.getAttributeNode(attributeName);
            const nsAttr = document.createAttributeNS('urn:test', 'p:' + attributeName);
            nsAttr.value = 'namespaced'; target.setAttributeNodeNS(nsAttr);
            const copy = target.cloneNode(true);
            attr === native && attr.ownerElement === target && attr.name === attributeName &&
              target.getAttribute(attributeName) === 'value' &&
              nsAttr.prefix === 'p' && nsAttr.localName === attributeName && nsAttr.namespaceURI === 'urn:test' &&
              target.getAttributeNS('urn:test', attributeName) === 'namespaced' &&
              copy.getAttribute(attributeName) === 'value' && copy.getAttributeNS('urn:test', attributeName) === 'namespaced'
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task CaseFoldingIsAsciiOnlyAndOnlyForUnqualifiedHtmlCreation()
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>test</p>");
        (await page.EvaluateAsync<string>("""
            const xml = new DOMParser().parseFromString('<root/>', 'application/xml');
            [document.createAttribute('AÄ').name, xml.createAttribute('AÄ').name,
             document.createAttributeNS(null, 'AÄ').name, document.createAttributeNS('', 'B').namespaceURI === null].join('|')
            """)).Should().Be("aÄ|AÄ|AÄ|true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task InvalidNamesAndNamespacesKeepTheirDistinctExceptions()
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>test</p>");
        (await page.EvaluateAsync<string>("""
            const error = callback => { try { callback(); return 'none'; } catch (e) { return e.name; } };
            [error(() => document.createAttribute('a b')), error(() => document.createAttribute('a=b')),
             error(() => document.createAttributeNS(null, 'p:name')),
             error(() => document.createAttributeNS('urn:test', 'xml:name')),
             error(() => Document.prototype.createAttribute.call({}, 'a b'))].join('|')
            """)).Should().Be("InvalidCharacterError|InvalidCharacterError|NamespaceError|NamespaceError|TypeError");
        page.Errors.Should().BeEmpty();
    }
}
