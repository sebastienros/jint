#nullable enable
namespace Jint.Tests.Browser.Dom;

public sealed class ToggleAttributeTests
{
    [TestCase("")]
    [TestCase("a b")]
    [TestCase("a\0b")]
    [TestCase("a/b")]
    [TestCase("a=b")]
    [TestCase("a>b")]
    public void InvalidNamesAreRejectedEvenWhenForcePreventsInsertion(string name)
    {
        using var fixture = DomTestFixture.Create("<div></div>");
        fixture.Text($$"""
            const e = document.querySelector('div');
            const name = {{System.Text.Json.JsonSerializer.Serialize(name)}};
            [undefined, true, false].map(force => {
              try { e.toggleAttribute(name, force); return 'accepted'; }
              catch (error) { return [error.name, error.code, error instanceof DOMException].join(':'); }
            }).join('|');
            """).Should().Be("InvalidCharacterError:5:true|InvalidCharacterError:5:true|InvalidCharacterError:5:true");
    }

    [TestCase("0name")]
    [TestCase("-name")]
    [TestCase("a!b")]
    [TestCase("a<b")]
    [TestCase("\u0080")]
    [TestCase("\uFFFF")]
    public void ModernNamesCreateAndRemoveNativeAttributes(string name)
    {
        using var fixture = DomTestFixture.Create("<div></div>");
        fixture.Text($$"""
            const e = document.querySelector('div');
            const name = {{System.Text.Json.JsonSerializer.Serialize(name)}};
            const added = e.toggleAttribute(name);
            const attr = e.getAttributeNode(name);
            const retained = e.toggleAttribute(name, true);
            const same = attr === e.getAttributeNode(name);
            const copy = e.cloneNode(true);
            const removed = e.toggleAttribute(name);
            [added, attr.name, retained, same, copy.getAttribute(name), removed, e.attributes.length].join('|');
            """).Should().Be($"true|{name}|true|true||false|0");
    }

    [Test]
    public void CaseFoldingIsAsciiOnlyAndRequiresAnHtmlDocumentAndNamespace()
    {
        using var fixture = DomTestFixture.Create("<div></div>");
        fixture.Text("""
            const html = document.createElement('div');
            const svg = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            const xml = document.implementation.createDocument(null, 'root');
            const xhtml = xml.createElementNS('http://www.w3.org/1999/xhtml', 'div');
            [html, svg, xhtml].map(e => {
              e.toggleAttribute('ÄFOO'); return e.getAttributeNames().join(',');
            }).join('|');
            """).Should().Be("Äfoo|ÄFOO|ÄFOO");
    }

    [Test]
    public void ConversionHappensOnceAndRemovalUsesTheFirstQualifiedNameMatch()
    {
        using var fixture = DomTestFixture.Create("<div></div>");
        fixture.Text("""
            const e = document.querySelector('div');
            const first = document.createAttributeNS('urn:first', 'attr'); first.value = 'first';
            const second = document.createAttributeNS('urn:second', 'attr'); second.value = 'second';
            e.setAttributeNodeNS(first); e.setAttributeNodeNS(second);
            let calls = 0;
            const removed = e.toggleAttribute({toString() { calls++; return calls === 1 ? 'attr' : 'bad name'; }});
            [removed, calls, e.attributes.length, e.attributes[0] === second, second.value].join('|');
            """).Should().Be("false|1|1|true|second");
    }

    [Test]
    public async Task NativeAttributeChangesStillQueueMutationRecords()
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='target'></div>");
        (await page.EvaluateAsync<string>("""
            const observer = new MutationObserver(() => {});
            observer.observe(target, {attributes: true, attributeOldValue: true});
            target.toggleAttribute('0name'); target.toggleAttribute('0name', true); target.toggleAttribute('0name');
            JSON.stringify(observer.takeRecords().map(r => [r.type, r.attributeName, r.oldValue]));
            """)).Should().Be("[[\"attributes\",\"0name\",null],[\"attributes\",\"0name\",\"\"]]");
        page.Errors.Should().BeEmpty();
    }
}
