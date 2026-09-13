#nullable enable
namespace Jint.Tests.Browser.Views;

public sealed class AttributeNameTests
{
    [TestCase("null", "null")]
    [TestCase("undefined", "undefined")]
    [TestCase("0", "0")]
    [TestCase("true", "true")]
    public void PrimitiveNamesAreConvertedByBothFactories(string source, string expected)
    {
        using var fixture = DomTestFixture.Create("<div></div>");
        fixture.Text($"document.createAttribute({source}).name").Should().Be(expected);
        fixture.Text($"document.createAttributeNS(null, {source}).name").Should().Be(expected);
    }

    [Test]
    public async Task FactoryArgumentsAreConvertedOnceInParameterOrder()
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>test</p>");
        (await page.EvaluateAsync<string>("""
            const log = [];
            const ns = {toString() { log.push('namespace'); return 'urn:test'; }};
            const name = {toString() { log.push('name'); return log.length === 2 ? 'p:0name' : 'bad name'; }};
            const attr = document.createAttributeNS(ns, name);
            let count = 0;
            const plain = document.createAttribute({toString() { count++; return count === 1 ? '0name' : 'bad name'; }});
            [log.join(','), attr.name, plain.name, count].join('|')
            """)).Should().Be("namespace,name|p:0name|0name|1");
        page.Errors.Should().BeEmpty();
    }

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
            target.setAttribute(attributeName, 'updated');
            target.setAttributeNS('urn:test', 'q:' + attributeName, 'updated namespace');
            const copy = target.cloneNode(true);
            attr === native && attr.ownerElement === target && attr.name === attributeName &&
              target.getAttribute(attributeName) === 'updated' && attr === target.getAttributeNode(attributeName) &&
              nsAttr.prefix === 'p' && nsAttr.localName === attributeName && nsAttr.namespaceURI === 'urn:test' &&
              target.getAttributeNS('urn:test', attributeName) === 'updated namespace' &&
              nsAttr === target.getAttributeNodeNS('urn:test', attributeName) &&
              copy.getAttribute(attributeName) === 'updated' && copy.getAttributeNS('urn:test', attributeName) === 'updated namespace'
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task MutationsConvertEveryArgumentOnceBeforeValidatingNames()
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='target'></div>");
        (await page.EvaluateAsync<string>("""
            const log = [];
            const text = (label, value) => ({toString() { log.push(label); return value; }});
            target.setAttributeNS(text('namespace', 'urn:test'), text('name', 'p:0name'), text('value', 'v'));
            target.setAttribute(text('plain name', '0name'), text('plain value', null));
            let error;
            try { target.setAttribute('bad name', {toString() { throw 'value conversion'; }}); }
            catch (caught) { error = caught; }
            [log.join(','), target.getAttributeNS('urn:test', '0name'), target.getAttribute('0name'), error].join('|');
            """)).Should().Be("namespace,name,value,plain name,plain value|v|null|value conversion");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task NamespacedWritesMatchNamespaceAndLocalNameAndPlainWritesMatchQualifiedName()
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div></div>");
        (await page.EvaluateAsync<string>("""
            const target = document.querySelector('div');
            target.setAttributeNS('urn:first', 'attr', 'first');
            target.setAttributeNS('urn:second', 'attr', 'second');
            const first = target.attributes[0]; const second = target.attributes[1];
            target.setAttribute('attr', 'changed');
            target.setAttributeNS('urn:second', 'p:attr', 'last');
            [target.attributes.length, first === target.attributes[0], second === target.attributes[1],
             first.value, second.value, second.prefix].join('|');
            """)).Should().Be("2|true|true|changed|last|");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task NewAttributesUseAsciiCaseRulesAndPreserveNativeMutationNotifications()
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div></div>");
        (await page.EvaluateAsync<string>("""
            const target = document.querySelector('div');
            const observer = new MutationObserver(() => {});
            observer.observe(target, {attributes: true, attributeOldValue: true});
            target.setAttribute('ÄFOO', 'first'); target.setAttribute('ÄFOO', 'second');
            target.setAttributeNS('urn:test', '0:ÄFOO', 'third');
            const xml = document.implementation.createDocument(null, 'root');
            const xhtml = xml.createElementNS('http://www.w3.org/1999/xhtml', 'div');
            xhtml.setAttribute('ÄFOO', 'xml');
            const records = observer.takeRecords().map(r => [r.attributeName, r.attributeNamespace, r.oldValue]);
            JSON.stringify([target.getAttributeNames(), xhtml.getAttributeNames(), records]);
            """)).Should().Be("[[\"Äfoo\",\"0:ÄFOO\"],[\"ÄFOO\"],[[\"Äfoo\",null,null],[\"Äfoo\",null,\"first\"],[\"ÄFOO\",\"urn:test\",null]]]");
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
