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
                    getComputedStyle(root.querySelector('p')).color === 'rgb(0, 128, 0)';
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }
}
