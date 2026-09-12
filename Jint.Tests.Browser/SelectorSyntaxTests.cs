namespace Jint.Tests.Browser;

public sealed class SelectorSyntaxTests
{
    [TestCase(">*")]
    [TestCase(" + div")]
    [TestCase("/* comment */ ~ div")]
    [TestCase("body, > div")]
    [TestCase(":is(body, div), /* comment */ > div")]
    [TestCase("[title=','], + div")]
    [TestCase(@".escaped\,, > div")]
    public void DomSelectorOperationsRejectRelativeSelectors(string selector)
    {
        using var fixture = DomTestFixture.Create("<body><div></div></body>");
        fixture.Engine.SetValue("selector", selector);
        fixture.Text("""
            (() => {
                const contexts = [document, document.body, document.createElement('div'), document.createDocumentFragment()];
                const failures = [];
                for (const root of contexts) {
                    const methods = root instanceof Element
                        ? ['querySelector', 'querySelectorAll', 'matches', 'webkitMatchesSelector', 'closest']
                        : ['querySelector', 'querySelectorAll'];
                    for (const method of methods) {
                        try { root[method](selector); failures.push(method + ': no throw'); }
                        catch (e) { if (!(e instanceof DOMException) || e.name !== 'SyntaxError' || e.code !== 12) failures.push(method + ': ' + e); }
                    }
                }
                return failures.join(';');
            })()
            """).Should().BeEmpty();
    }

    [Test]
    public void NestedCombinatorsAndEscapedPunctuationKeepTheirMeaning()
    {
        using var fixture = DomTestFixture.Create("<body><div id='parent' title=', > ]'><span></span></div><p class='a,b'></p></body>");
        fixture.Text("""
            (() => {
                const parent = document.getElementById('parent');
                return [document.querySelector('div:has(> span)') === parent,
                    parent.matches(':has(> span)'), parent.closest(':has(> span)') === parent,
                    document.querySelector('[title=", > ]"]') === parent,
                    document.querySelector('.a\\,b').localName === 'p',
                    document.querySelector('/* > , */ #parent') === parent,
                    document.querySelector(':is(p, div):has(> span)') === parent].join('/');
            })()
            """).Should().Be("true/true/true/true/true/true/true");
    }

    [Test]
    public void ConversionHappensOnceAfterTheReceiverCheck()
    {
        using var fixture = DomTestFixture.Create("<body></body>");
        fixture.Text("""
            (() => {
                let conversions = 0;
                const selector = { toString() { conversions++; return '>*'; } };
                try { Element.prototype.matches.call({}, selector); } catch (e) { if (!(e instanceof TypeError)) return 'wrong receiver error'; }
                const before = conversions;
                try { document.body.matches(selector); } catch (e) { if (!(e instanceof DOMException)) return 'wrong selector error'; }
                return [before, conversions].join('/');
            })()
            """).Should().Be("0/1");
    }
}
