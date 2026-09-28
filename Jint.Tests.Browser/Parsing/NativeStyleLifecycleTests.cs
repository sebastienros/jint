using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Parsing;

public sealed class NativeStyleLifecycleTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task CompletedStyleAndLinkAssociationsPreserveSourceOrder(bool linkFirst, bool acrossParserQuota)
    {
        var style = "<style title=a>" + (acrossParserQuota ? new string(' ', 8192) : "") + "p{color:red}</style>";
        const string link = "<link title=b rel=stylesheet href=/b>";
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/b", _ => LoopbackResponse.Css("p{color:blue}"))
            .MapHtml("/", (linkFirst ? link + style : style + link) + """
                <script>window.firstPreferred=document.preferredStyleSheetSet;</script>
                <p>text</p>
                """));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        (await fixture.Page.EvaluateAsync<string>("firstPreferred")).Should().Be(linkFirst ? "b" : "a");
        (await fixture.Page.EvaluateAsync<string>("Array.from(document.styleSheetSets).join(',')")).Should().Be(linkFirst ? "b,a" : "a,b");
        (await fixture.Page.EvaluateAsync<int>("document.styleSheets.length")).Should().Be(2);
        fixture.Page.Errors.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ShadowStyleSourceChangesAndHostReconnectionReplaceTheAssociatedSheet(bool declarative)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(declarative
            ? "<div id=host><template shadowrootmode=open><style id=s>p{color:red}</style><p>text</p></template></div>"
            : "<div id=host></div>");
        (await page.EvaluateAsync<bool>("""
            (() => {
                const host=document.getElementById('host');
                const root=host.shadowRoot ?? host.attachShadow({mode:'open'});
                if (!root.firstChild) root.innerHTML='<style id=s>p{color:red}</style><p>text</p>';
                const style=root.querySelector('#s');
                const first=style.sheet;
                if (first === null || root.styleSheets.length !== 1) return false;
                first.insertRule('p{display:none}', 0);
                if (style.sheet !== first) return false;
                style.textContent='p{color:blue}';
                const second=style.sheet;
                if (second === null || second === first || first.ownerNode !== null) return false;
                host.remove();
                if (second.ownerNode !== null || style.sheet !== null) return false;
                style.textContent='p{color:green}';
                const other=document.implementation.createHTMLDocument();
                other.adoptNode(host);
                document.body.appendChild(host);
                const third=style.sheet;
                return third !== null && third !== second && root.styleSheets.length === 1 &&
                    getComputedStyle(root.querySelector('p')).color === 'green';
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }
    [Test]
    public async Task BatchedNativeAdoptionDoesNotRetireTheReplacementRootWatchOrRequest()
    {
        var requests = 0;
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/sheet", _ => { Interlocked.Increment(ref requests); return LoopbackResponse.Css("p{color:red}"); })
            .MapHtml("/", "<iframe id=f srcdoc=''></iframe><div id=host></div>"));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        await fixture.Page.EvaluateAsync("""
            window.transferHost=document.getElementById('host');
            transferHost.attachShadow({mode:'open'}).innerHTML=
                '<link id=l rel=stylesheet href=/sheet><style id=s>p{visibility:visible}</style><p>text</p>';
            """);
        await fixture.Page.RunOnLoopAsync(engine =>
        {
            var host = DomBindings.Bind<Element>(engine.GetValue("transferHost"), "adoption regression").Target;
            var destination = DomBindings.Bind<Document>(engine.Evaluate("document.getElementById('f').contentDocument"), "adoption regression").Target;
            // Native host operations deliberately batch the old removal and new addition before delivery.
            destination.AdoptNode(host);
            destination.DocumentElement!.LastChild!.AppendChild(host);
            engine.Execute("""
                transferHost.shadowRoot.querySelector('#s').textContent='p{visibility:hidden}';
                window.afterAdoptionSheet=transferHost.shadowRoot.querySelector('#l').sheet;
                """);
            requests.Should().Be(2, "the replacement request must survive the old document's removal delivery");
            var style = host.AttachedShadowRoot!.ChildNodes.OfType<Element>().Single(element => element.LocalName == "style");
            // A native write reaches the existing observer without a wrapper that could silently re-register it.
            ((Text) style.FirstChild!).Data = "p{visibility:visible}";
            return true;
        });
        (await fixture.Page.WaitForIdleAsync(Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<bool>("""
            transferHost.shadowRoot.querySelector('#l').sheet === afterAdoptionSheet &&
            transferHost.shadowRoot.querySelector('#s').sheet.cssRules[0].style.visibility === 'visible' &&
            transferHost.shadowRoot.styleSheets.length === 2
            """)).Should().BeTrue();
        requests.Should().Be(2);
        fixture.Page.Errors.Should().BeEmpty();
    }

}
