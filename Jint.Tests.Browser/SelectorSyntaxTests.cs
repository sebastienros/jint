namespace Jint.Tests.Browser;

public sealed class SelectorSyntaxTests
{
    /// <summary>
    /// One element per shape the walk in <c>DomSelectorText</c> has to tell apart: a subtree to scope an
    /// attribute selector to, a parent with an element child for <c>:has()</c>, punctuation inside an
    /// attribute value, an escaped comma in a class name, and a class whose name really contains a pipe.
    /// </summary>
    private const string Markup = """
        <body>
          <div id='parent' lang='en-GB' title=', > ]'><span></span></div>
          <p class='a,b'></p>
          <div id='attr-value'><div id='attr-value-div1' align='center'></div></div>
          <i class='ns|div'></i>
        </body>
        """;

    /// <summary>
    /// The five DOM entry points over the four contexts the Selectors-API corpus runs its table through, so
    /// a fix that reached only one of them cannot look like a fix. <c>selector</c> is set as a host value
    /// rather than spelled into the source, which keeps a backslash a backslash.
    /// </summary>
    private const string EveryEntryPoint = """
        (() => {
            const contexts = [document, document.body, document.createElement('div'), document.createDocumentFragment()];
            const outcomes = [];
            for (const root of contexts) {
                const methods = root instanceof Element
                    ? ['querySelector', 'querySelectorAll', 'matches', 'webkitMatchesSelector', 'closest']
                    : ['querySelector', 'querySelectorAll'];
                for (const method of methods) {
                    try { root[method](selector); outcomes.push(method + ': no throw'); }
                    catch (e) { outcomes.push(method + ': ' + (e instanceof DOMException && e.code === 12 ? e.name : e)); }
                }
            }
            return outcomes.join(';');
        })()
        """;

    [TestCase(">*")]
    [TestCase(" + div")]
    [TestCase("/* comment */ ~ div")]
    [TestCase("body, > div")]
    [TestCase(":is(body, div), /* comment */ > div")]
    [TestCase("[title=','], + div")]
    [TestCase(@".escaped\,, > div")]
    public void DomSelectorOperationsRejectRelativeSelectors(string selector) => EveryEntryPointRefuses(selector);

    /// <summary>
    /// https://dom.spec.whatwg.org/#scope-match-a-selectors-string parses "with the empty namespace-prefix
    /// map", so every prefix is undeclared and https://drafts.csswg.org/selectors/#type-nmsp makes the whole
    /// selector invalid — at any depth, which is what <c>:not(ns|div)</c> and <c>[ns|attr]</c> are here for.
    /// </summary>
    [TestCase("ns|div")]
    [TestCase("ns/**/|div")]
    [TestCase(":not(ns/**/|div)")]
    [TestCase("[ns/**/|attr]")]
    [TestCase(@"n\73 |div")]
    [TestCase(@"n\000073 |div")]
    [TestCase("n\\73\t|div")]
    [TestCase("n\\73\r\n|div")]
    [TestCase("n\\73\f|div")]
    [TestCase(@"\6e s|div")]
    [TestCase(":not(n\\73 |div)")]
    [TestCase("[n\\73 |attr]")]
    [TestCase("ns|*")]
    [TestCase(":not(ns|div)")]
    [TestCase(":has(> ns|div)")]
    [TestCase("div ns|p")]
    [TestCase("#parent, ns|div")]
    [TestCase("[ns|attr]")]
    [TestCase("[ns|attr=\"x\"]")]
    [TestCase(":not([ns|attr])")]
    [TestCase("ns|div, #parent")]
    public void DomSelectorOperationsRejectAnUndeclaredNamespacePrefix(string selector) => EveryEntryPointRefuses(selector);

    /// <summary>
    /// The four spellings a <c>|</c> has that are <i>not</i> an undeclared prefix — the empty namespace, the
    /// any-namespace prefix, the column combinator and the hyphen-separated attribute operator — plus the
    /// three places a prefix-looking sequence is not a selector at all. None of them may start throwing.
    /// </summary>
    [TestCase("|div")]
    [TestCase("|*")]
    [TestCase("*|div")]
    [TestCase("*|*")]
    [TestCase("#parent [*|title]")]
    [TestCase("a || b")]
    [TestCase("a||b")]
    [TestCase("[lang|=\"en\"]")]
    [TestCase("[lang|=en]")]
    [TestCase("#parent, |div")]
    [TestCase(":not(|div)")]
    [TestCase("[title=\"ns|div\"]")]
    [TestCase("[title='ns|div']")]
    [TestCase("/* ns|div */ #parent")]
    [TestCase(@".ns\|div")]
    [TestCase(@".ns\7c div")]
    [TestCase("div /**/|div")]
    [TestCase(@"n\73  |div")]
    [TestCase("[lang/**/|=\"en\"]")]
    [TestCase(":has(> span)")]
    [TestCase("div:has(> span)")]
    public void SelectorsWhosePipeIsNotAnUndeclaredPrefixAreLeftToTheNativeParser(string selector)
    {
        using var fixture = DomTestFixture.Create(Markup);
        fixture.Engine.SetValue("selector", selector);
        var outcomes = fixture.Text(EveryEntryPoint)!;
        outcomes.Split(';').Should().AllSatisfy(outcome => outcome.Should().EndWith(": no throw"));
    }

    /// <summary>
    /// https://drafts.csswg.org/css-syntax/ §5.4's consume algorithms close every open block, function,
    /// string and comment at EOF, so each of these is a <b>valid</b> selector that matches — which is what
    /// <c>#attr-value [align="center"</c> asserts in wpt's own Selectors-API table.
    /// </summary>
    [TestCase("#attr-value [align=\"center\"", "attr-value-div1")]
    [TestCase("#attr-value [align='center'", "attr-value-div1")]
    [TestCase("#attr-value [align=center", "attr-value-div1")]
    [TestCase("#attr-value [align=\"center", "attr-value-div1")]
    [TestCase("[title=\", > ]\"", "parent")]
    [TestCase("[title=\", > ]", "parent")]
    [TestCase("div:has(> span", "parent")]
    [TestCase(":is(p, div):has(> span", "parent")]
    [TestCase("#parent /* unterminated", "parent")]
    [TestCase(":is(div:has(> span", "parent")]
    [TestCase("#attr-value [align=\"center\" /* unterminated", "attr-value-div1")]
    public void AnOpenConstructIsClosedAtEofAndTheSelectorMatches(string selector, string id)
    {
        using var fixture = DomTestFixture.Create(Markup);
        fixture.Engine.SetValue("selector", selector);
        fixture.Engine.SetValue("id", id);
        fixture.Text("""
            (() => {
                const target = document.getElementById(id);
                const roots = [document, document.body];
                const outcomes = [];
                for (const root of roots) {
                    outcomes.push('querySelector: ' + (root.querySelector(selector) === target));
                    const all = root.querySelectorAll(selector);
                    outcomes.push('querySelectorAll: ' + (all.length === 1 && all[0] === target));
                }
                outcomes.push('matches: ' + target.matches(selector));
                outcomes.push('webkitMatchesSelector: ' + target.webkitMatchesSelector(selector));
                outcomes.push('closest: ' + (target.closest(selector) === target));
                return outcomes.filter(o => !o.endsWith('true')).join(';');
            })()
            """).Should().BeEmpty();
    }

    /// <summary>
    /// The EOF close is the one thing this walk does that changes the text handed to the native matcher, so
    /// a selector that was already balanced must come back byte for byte — including the ones whose
    /// brackets and parentheses are inside a string, a comment or an escape.
    /// </summary>
    [Test]
    public void NestedCombinatorsAndEscapedPunctuationKeepTheirMeaning()
    {
        using var fixture = DomTestFixture.Create(Markup);
        fixture.Text("""
            (() => {
                const parent = document.getElementById('parent');
                return [document.querySelector('div:has(> span)') === parent,
                    parent.matches(':has(> span)'), parent.closest(':has(> span)') === parent,
                    document.querySelector('[title=", > ]"]') === parent,
                    document.querySelector('.a\\,b').localName === 'p',
                    document.querySelector('/* > , */ #parent') === parent,
                    document.querySelector('[title="/* ("]') === null,
                    document.querySelector(':is(p, div):has(> span)') === parent].join('/');
            })()
            """).Should().Be("true/true/true/true/true/true/true/true");
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

    private static void EveryEntryPointRefuses(string selector)
    {
        using var fixture = DomTestFixture.Create(Markup);
        fixture.Engine.SetValue("selector", selector);
        var outcomes = fixture.Text(EveryEntryPoint)!;
        outcomes.Split(';').Should().AllSatisfy(outcome => outcome.Should().EndWith(": SyntaxError"));
    }
}
