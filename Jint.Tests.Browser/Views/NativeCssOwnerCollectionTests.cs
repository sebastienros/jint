#nullable enable
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.Browser.Views;

public sealed class NativeCssOwnerCollectionTests
{
    [TestCase("attribute")]
    [TestCase("text")]
    [TestCase("insert")]
    [TestCase("remove")]
    [TestCase("siblings")]
    public void UnrelatedMutateThenReadWorkDoesNotGrowWithDocumentSize(string mutation)
    {
        var small = MutateThenRead(64, mutation);
        var large = MutateThenRead(32_768, mutation);
        large.Should().BeLessThanOrEqualTo(small + 4,
            "stylesheet collection must visit registered owners and their ancestry, not unrelated descendants");
    }

    private static int MutateThenRead(int count, string mutation)
    {
        var document = Document.CreateHtml();
        var html = document.CreateElement("html");
        var head = document.CreateElement("head");
        var body = document.CreateElement("body");
        document.AppendChild(html);
        html.AppendChild(head);
        html.AppendChild(body);
        var style = document.CreateElement("style");
        style.SetAttribute("title", "test");
        head.AppendChild(style);
        NativeCssStyleSheets.Install(document, style, "p{color:red}", "https://example.test/", "https://example.test/", new(default));
        var text = document.CreateTextNode("before");
        body.AppendChild(text);
        var first = document.CreateElement("style");
        var last = document.CreateElement("style");
        if (mutation == "siblings") body.AppendChild(first);
        for (var i = 0; i < count; i++) body.AppendChild(document.CreateElement("div"));
        if (mutation == "siblings")
        {
            body.AppendChild(last);
            NativeCssStyleSheets.Install(document, first, "p{color:green}", "https://example.test/", "https://example.test/", new(default));
            NativeCssStyleSheets.Install(document, last, "p{color:blue}", "https://example.test/", "https://example.test/", new(default));
        }
        var expectedCount = mutation == "siblings" ? 3 : 1;
        NativeCssStyleSheets.Get(document, new(default)).Count.Should().Be(expectedCount);
        NativeCssStyleSheets.Get(document, new(default), includeShadow: true).Count.Should().Be(expectedCount);
        var checks = 0;
        var work = new CssValueWork(default, () => checks++);
        for (var i = 0; i < 16; i++)
        {
            switch (mutation)
            {
                case "attribute": body.SetAttribute("data-state", i.ToString()); break;
                case "text": text.Data = i.ToString(); break;
                case "insert": body.AppendChild(document.CreateElement("span")); break;
                case "remove": body.RemoveChild(body.LastChild!); break;
                case "siblings": body.InsertBefore(document.CreateElement("span"), last); break;
            }
            NativeCssStyleSheets.Get(document, work, includeShadow: true).Count.Should().Be(expectedCount);
            NativeCssStyleSheets.Get(document, work).Count.Should().Be(expectedCount);
            NativeCssStyleSheets.SetsOf(document).NamesOf(work).Should().Equal("test");
        }
        return checks;
    }

    [Test]
    public async Task LiveCollectionsAndComputedStylesFollowOwnerOrderAndScopeChanges()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style id=a>p{color:red}</style><style id=b>p{color:blue}</style><p id=p>text</p><div id=host></div><iframe id=frame srcdoc=''></iframe>");
        (await page.EvaluateAsync<string>("""
            (() => {
                const a=document.getElementById('a'), b=document.getElementById('b');
                const p=document.getElementById('p'), host=document.getElementById('host');
                const live=getComputedStyle(p), sheets=document.styleSheets;
                const color=expected=>live.color===expected;
                if (!color('rgb(0, 0, 255)')) return 'initial';
                document.head.append(a);
                if (!color('rgb(255, 0, 0)') || sheets[1].ownerNode!==a) return 'reorder';
                a.textContent='p{color:green}';
                if (!color('rgb(0, 128, 0)')) return 'source';
                const root=host.attachShadow({mode:'open'});
                root.innerHTML='<style id=c>p{color:purple}</style><p>shadow</p>';
                const shadowP=root.querySelector('p'), shadowLive=getComputedStyle(shadowP);
                root.append(a);
                if (sheets.length!==1 || root.styleSheets.length!==2 ||
                    !color('rgb(0, 0, 255)') || shadowLive.color!=='rgb(0, 128, 0)') return 'scope';
                a.remove();
                if (root.styleSheets.length!==1 || shadowLive.color!=='rgb(128, 0, 128)') return 'remove';
                const other=document.getElementById('frame').contentDocument;
                other.adoptNode(a);
                other.head.append(a);
                if (other.styleSheets.length!==1 || sheets.length!==1) return 'adopt out';
                document.head.append(a);
                if (other.styleSheets.length!==0 || sheets.length!==2 || !color('rgb(0, 128, 0)')) return 'adopt back';
                host.remove();
                if (root.styleSheets.length!==0) return 'disconnected shadow';
                document.body.append(host);
                if (root.styleSheets.length!==1 || shadowLive.color!=='rgb(128, 0, 128)') return 'reconnected shadow';
                b.type='text/plain';
                if (sheets.length!==1) return 'ineligible';
                b.type='text/css';
                if (sheets.length!==2) return 'eligible';
                return 'ok';
            })()
            """)).Should().Be("ok");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public void ReentrantMutationDoesNotPublishAStaleOwnerOrder()
    {
        var document = Document.CreateHtml();
        var style = document.CreateElement("style");
        document.AppendChild(style);
        NativeCssStyleSheets.Install(document, style, "p{color:red}", "https://example.test/", "https://example.test/", new(default));
        var checks = 0;
        var work = new CssValueWork(default, () =>
        {
            if (++checks != 3) return;
            document.RemoveChild(style);
        });
        Assert.Throws<InvalidOperationException>(() => NativeCssStyleSheets.Get(document, work));
        NativeCssStyleSheets.Get(document, new(default)).Should().BeEmpty();
        document.AppendChild(style);
        NativeCssStyleSheets.Get(document, new(default)).Count.Should().Be(1);
    }
}
