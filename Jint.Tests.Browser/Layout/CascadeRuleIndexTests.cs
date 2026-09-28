namespace Jint.Tests.Browser.Layout;

/// <summary>
/// The cascade tries each element only against rules whose selector subject could carry one of its names.
/// These cases are the ones where a name comparison or the subject compound is not what it first seems;
/// a missing match here means the index dropped a rule the matcher would have accepted.
/// </summary>
public class CascadeRuleIndexTests
{
    private static async Task<string> ComputedAsync(string markup, string script)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(markup);
        return (await page.EvaluateAsync<string>(script))!;
    }

    private const string Hidden = "[...document.querySelectorAll('[data-t]')].map(e => e.getAttribute('data-t') + ':' + getComputedStyle(e).display).join(' ')";

    [Test]
    public async Task QuirksModeIdsAndClassesMatchAcrossAsciiCase()
    {
        var result = await ComputedAsync(
            "<style>.Foo{display:none} #Main{display:none}</style><p class='foo' data-t=c></p><p id='main' data-t=i></p>",
            Hidden);
        result.Should().Be("c:none i:none");
    }

    [Test]
    public async Task HtmlTypeSelectorsIgnoreCaseAndForeignOnesKeepTheirCamelCase()
    {
        var result = await ComputedAsync(
            "<!doctype html><style>SECTION{display:none} foreignObject{display:none}</style>"
            + "<section data-t=h></section><svg><foreignObject data-t=f></foreignObject></svg>",
            Hidden);
        result.Should().Be("h:none f:none");
    }

    [Test]
    public async Task SelectorListsMixingKeyedAndUnkeyedBranchesReachEveryElement()
    {
        var result = await ComputedAsync(
            "<!doctype html><style>.a, :is(i){display:none} #x.y{display:none} span:first-child{display:none}</style>"
            + "<div><b class='a' data-t=a></b><i data-t=is></i><u id='x' class='z y' data-t=idc></u></div><div><span data-t=s></span></div>",
            Hidden);
        result.Should().Be("a:none is:none idc:none s:none");
    }

    [Test]
    public async Task ClassesSeparatedByAnyAsciiWhitespaceAreIndexed()
    {
        var result = await ComputedAsync(
            "<!doctype html><style>.z{display:none}</style><p class='x\ty\n z\f' data-t=w></p><p class='zz' data-t=n></p>",
            Hidden);
        result.Should().Be("w:none n:block");
    }

    [Test]
    public async Task SourceOrderAcrossBucketsIsPreserved()
    {
        var result = await ComputedAsync(
            "<!doctype html><style>.b{color:red} p{color:blue} .a{color:green}</style><p class='b a'>x</p>",
            "getComputedStyle(document.querySelector('p')).color");
        result.Should().Be("green");
    }

    [Test]
    public async Task HostAndShadowTreeRulesStillMatchTheirSubjects()
    {
        var result = await ComputedAsync(
            "<!doctype html><div id='host'></div>"
            + "<script>const root = host.attachShadow({mode:'open'});"
            + "root.innerHTML = '<style>:host{visibility:hidden} .inner{display:none}</style><p class=inner></p>';</script>",
            "getComputedStyle(host).visibility + ' ' + getComputedStyle(host.shadowRoot.querySelector('p')).display");
        result.Should().Be("hidden none");
    }

    // Separate getComputedStyle reads share one traversal per document; each input it captured must retire it.
    [Test]
    public async Task ASharedTraversalIsRetiredByEveryInputItCaptured()
    {
        var result = await ComputedAsync(
            "<!doctype html><style>.off{display:none} :focus{display:inline}</style>"
            + "<button id=b data-t=b>x</button><p id=p data-t=p></p>",
            """
            (() => {
              const b = document.getElementById('b'), p = document.getElementById('p');
              const live = getComputedStyle(p), out = [];
              const read = () => out.push(getComputedStyle(b).display + '/' + live.display);
              read();
              p.className = 'off'; read();
              document.styleSheets[0].insertRule('p{visibility:hidden}', 0); out.push(live.visibility);
              document.styleSheets[0].cssRules[1].style.display = 'contents'; read();
              b.focus(); read();
              p.style.display = 'flex'; read();
              return out.join(' ');
            })()
            """);
        result.Should().Be("inline-block/block inline-block/none hidden inline-block/contents inline/contents inline/flex");
    }

    // A document with no browsing context has no page witness; the query's own stamps must retire its traversal.
    [Test]
    public async Task ADocumentWithoutABrowsingContextRetiresItsTraversalOnMutation()
    {
        var result = await ComputedAsync(
            "<!doctype html><p></p>",
            """
            (() => {
              const doc = new DOMParser().parseFromString('<!doctype html><p>x</p>', 'text/html');
              const p = doc.querySelector('p'), out = [getComputedStyle(p).display];
              p.setAttribute('style', 'display:none'); out.push(getComputedStyle(p).display);
              p.style.display = 'flex'; out.push(getComputedStyle(p).display);
              return out.join(' ');
            })()
            """);
        result.Should().Be("block none flex");
    }
}
