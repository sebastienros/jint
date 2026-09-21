using Jint.Browser.Dom;

namespace Jint.Tests.Browser;

/// <summary>https://drafts.csswg.org/css-syntax/#consume-string-token: continuations add no value.</summary>
public sealed class SelectorStringContinuationTests
{
    [TestCase("\n", "'")]
    [TestCase("\r", "'")]
    [TestCase("\r\n", "'")]
    [TestCase("\f", "'")]
    [TestCase("\n", "\"")]
    [TestCase("\r", "\"")]
    [TestCase("\r\n", "\"")]
    [TestCase("\f", "\"")]
    public void ContinuationsPreserveValuesAcrossDomSelectorEntryPoints(string newline, string quote)
    {
        using var fixture = DomTestFixture.Create("<main><p id='target'></p><i></i></main>");
        var continuation = "\\" + newline;
        var values = new (string Source, string Value)[]
        {
            ("a" + continuation + "b", "ab"),
            (continuation + continuation, ""),
            ("a" + continuation + continuation + "b", "ab"),
            ("\\6" + continuation + "b", "\u0006b"),
            ("\\61" + continuation + "b", "ab"),
            ("\\061" + continuation + "b", "ab"),
            ("\\0061" + continuation + "b", "ab"),
            ("\\00061" + continuation + "b", "ab"),
            ("\\000061" + continuation + "b", "ab"),
            ("\\61" + continuation + " b", "a b"),
            ("\\000061" + continuation + " b", "a b"),
            ("\\61 " + continuation + "b", "ab"),
            ("\\61\t" + continuation + " b", "a b"),
            ("\\61" + continuation + "\\62", "ab"),
            ("a\\" + quote + continuation + "b", "a" + quote + "b"),
            ("a\\\\" + continuation + "b", "a\\b"),
            ("a/*" + continuation + "*/b", "a/**/b"),
            ("a" + continuation + ":is(p),)([]", "a:is(p),)([]"),
        };
        foreach (var (source, value) in values)
        {
            fixture.Engine.SetValue("value", value);
            fixture.Execute("document.getElementById('target').setAttribute('title', value);");
            var attribute = "[title=" + quote + source + quote + "]";
            foreach (var selector in new[] { attribute, ":is(:unknown," + attribute + ")", ":not(i)" + attribute })
            {
                fixture.Engine.SetValue("selector", selector);
                fixture.Text("""
                    (() => {
                        const root = document.querySelector('main'), target = document.getElementById('target');
                        const fragment = document.createDocumentFragment();
                        fragment.append(target.cloneNode(true));
                        try { return [root.querySelector(selector) === target,
                          document.querySelector(selector) === target,
                          root.querySelectorAll(selector).length === 1,
                          fragment.querySelectorAll(selector).length === 1,
                          target.matches(selector), target.webkitMatchesSelector(selector),
                          target.closest(selector) === target].join('/'); }
                        catch (error) { return error.name + ': ' + JSON.stringify(selector); }
                    })()
                    """).Should().Be("true/true/true/true/true/true/true", $"selector {selector}");
            }
        }
    }

    [TestCase("p")]
    [TestCase("[title='a\\62']")]
    [TestCase("/* 'a\\\nb' */ p")]
    [TestCase("[title='a\\\\\nb']")]
    [TestCase("p\\\n[title='ab']")]
    [TestCase("[title=a\\\nb]")]
    [TestCase("[title='a\\\nb\nb']")]
    public void OtherTokensAndBadStringsAreNotRewritten(string selector)
        => DomSelectorText.NormalizeStringContinuations(selector).Should().BeSameAs(selector);

    [TestCase("\n")]
    [TestCase("\r")]
    [TestCase("\r\n")]
    [TestCase("\f")]
    public void EofRecoveryStillClosesTheStringAfterAContinuation(string newline)
    {
        using var fixture = DomTestFixture.Create("<p title='ab'></p>");
        fixture.Engine.SetValue("selector", "[title='ab\\" + newline);
        fixture.Bool("document.querySelector(selector) !== null").Should().BeTrue();
    }
}
