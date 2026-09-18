namespace Jint.Tests.Browser;

/// <summary>https://drafts.csswg.org/selectors/#type-nmsp: an empty prefix selects no namespace.</summary>
public sealed class EmptyNamespaceSelectorTests
{
    private const string Setup = """
        const root = document.getElementById('root');
        const plain = document.createElementNS(null, 'div'); plain.id = 'plain';
        const child = document.createElementNS(null, 'span'); child.id = 'child';
        plain.append(child); root.append(plain);
        const foreign = document.createElementNS('urn:foreign', 'div'); foreign.id = 'foreign'; root.append(foreign);
        """;

    [TestCase("|div", "plain")]
    [TestCase("|*", "plain,child")]
    [TestCase("|div > |span", "child")]
    [TestCase(":is(|div)", "plain")]
    [TestCase(":where(|div)", "plain")]
    [TestCase(":is(:-jint-empty-namespace-0), |span", "child")]
    [TestCase(@":is(:\2d jint-empty-namespace-0), |span", "child")]
    [TestCase(":not(|*)", "html,foreign")]
    [TestCase("|div:has(> |span)", "plain")]
    [TestCase("|/**/div", "plain")]
    [TestCase(@"|d\69 v", "plain")]
    [TestCase("*|div", "html,plain,foreign")]
    [TestCase("[title='|div']", "html")]
    [TestCase("/* |fake */ |div", "plain")]
    public void NativeParsingKeepsNamesNestingAndUnrelatedPipes(string selector, string expected)
    {
        using var fixture = DomTestFixture.Create("<div id='root'><div id='html' title='|div'></div></div>");
        fixture.Execute(Setup);
        fixture.Engine.SetValue("selector", selector);
        fixture.Text("Array.from(root.querySelectorAll(selector), e => e.id).join(',')").Should().Be(expected);
        fixture.Text("root.querySelector(selector)?.id ?? ''").Should().Be(expected.Split(',')[0]);
    }

    [Test]
    public void AllEntryPointsUseTheSamePredicateAndKeepTheirScope()
    {
        using var fixture = DomTestFixture.Create("<div id='root'><div id='html'></div></div>");
        fixture.Execute(Setup);
        fixture.Text("""
            [document.querySelector('|div') === plain,
             root.querySelector(':scope > |div') === plain,
             root.querySelectorAll(':scope > |div')[0] === plain,
             plain.matches('|div'), plain.webkitMatchesSelector('|div'),
             child.closest('|div') === plain,
             !foreign.matches('|div'), !document.getElementById('html').matches('|div'),
             plain.querySelector(':scope > |span') === child].join('/')
            """).Should().Be("true/true/true/true/true/true/true/true/true");
    }

    [Test]
    public void DetachedTreesAndSnapshotsKeepNodeIdentity()
    {
        using var fixture = DomTestFixture.Create("<div id='root'><div id='html'></div></div>");
        fixture.Execute(Setup);
        fixture.Text("""
            (() => {
                const list = root.querySelectorAll('|div');
                const fragment = document.createDocumentFragment(); fragment.append(plain);
                return [list.length === 1, list[0] === plain,
                    root.querySelector('|div') === null,
                    fragment.querySelector('|div') === plain,
                    fragment.querySelectorAll('|span')[0] === child,
                    plain.matches('|div'), child.closest('|div') === plain].join('/');
            })()
            """).Should().Be("true/true/true/true/true/true/true");
    }

    [TestCase("|-jint-empty-namespace-0")]
    [TestCase("|div:unknown")]
    [TestCase("|div, :-jint-empty-namespace-0")]
    [TestCase(@"|div, :\2d jint-empty-namespace-0")]
    public void PrivatePredicateNamesDoNotBecomeAnExtension(string selector)
    {
        using var fixture = DomTestFixture.Create("<div id='root'></div>");
        fixture.Engine.SetValue("selector", selector);
        if (selector[1] == '-')
        {
            fixture.Number("document.querySelectorAll(selector).length").Should().Be(0);
        }
        else
        {
            fixture.Text("try { document.querySelector(selector); 'accepted'; } catch(e) { e.name; }").Should().Be("SyntaxError");
        }
    }

    [Test]
    public async Task NoNamespaceTypeNamesAreOrdinalAndXmlDeclarationsUseTheSharedNamespaceAnswer()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='root'></div>");
        (await page.EvaluateAsync<string>("""
            (() => {
                const root = document.getElementById('root');
                const upper = document.createElementNS(null, 'DIV'); root.append(upper);
                const lower = document.createElementNS(null, 'div'); root.append(lower);
                const xml = new DOMParser().parseFromString('<root xmlns="urn:xml"><div/><clear xmlns=""><div/></clear></root>', 'text/xml');
                return [root.querySelector('|DIV') === upper,
                    root.querySelector('|div') === lower,
                    root.querySelector('|\\44 IV') === upper,
                    !upper.matches('|div'), !lower.matches('|DIV'),
                    xml.querySelectorAll('|div').length === 1,
                    xml.querySelector('|div').parentElement.localName === 'clear'].join('/');
            })()
            """)).Should().Be("true/true/true/true/true/true/true");
    }

    [TestCase("|div#")]
    [TestCase("|div[")]
    [TestCase("|div >")]
    [TestCase("|div:unknown()")]
    public void AdaptationDoesNotRescueInvalidOriginalGrammar(string selector)
    {
        using var fixture = DomTestFixture.Create("<div id='root'></div>");
        fixture.Engine.SetValue("selector", selector);
        fixture.Text("try { document.querySelectorAll(selector); 'accepted'; } catch(e) { e.name; }")
            .Should().Be("SyntaxError");
    }

    [Test]
    public async Task AdapterRetainsPagePredicates()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<details id='details' open></details>");
        (await page.EvaluateAsync<string>("""
            (() => {
                const plain = document.createElementNS(null, 'div'); document.body.append(plain);
                const selector = ':open, |div';
                const before = Array.from(document.querySelectorAll(selector), e => e.localName).join(',');
                document.getElementById('details').removeAttribute('open');
                const after = Array.from(document.querySelectorAll(selector), e => e.localName).join(',');
                return before + '/' + after;
            })()
            """)).Should().Be("details,div/div");
    }
}
