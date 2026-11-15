#nullable enable

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>getComputedStyle</c> absolutizes font- and viewport-relative lengths, reduces <c>calc()</c> and resolves
/// <c>transform-origin</c> keywords (#4615). Expected values are Chromium's, at 1280 × 720 and 16px.
/// </summary>
/// <remarks>
/// https://drafts.csswg.org/cssom/#resolved-values, https://drafts.csswg.org/css-values-4/#relative-lengths,
/// https://drafts.csswg.org/css-values-4/#calc-computed-value
/// </remarks>
public sealed class ComputedLengthResolutionTests
{
    private static async Task<string> Read(string css, string body, string script)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<!doctype html><style>{css}</style>{body}");
        var result = await page.EvaluateAsync<string>(script);
        page.Errors.Should().BeEmpty();
        return result!;
    }

    [Test]
    public async Task TheIssueReproMatchesChromium()
    {
        (await Read("""
            html { --nav: 3.5rem; scroll-padding-top: calc(1.5rem + var(--nav)); }
            #flex { display: flex; gap: 0.4rem; row-gap: 1em; column-gap: 2vw; }
            #pad { padding-left: 1rem; }
            #u { position: relative; display: inline-block; }
            #u::after { content: ''; position: absolute; left: 0; right: 0; height: 2px; transform: scaleX(0); transform-origin: left; }
            #o { width: 100px; height: 20px; transform-origin: left top; }
            """, """
            <div id=flex><span>a</span><span>b</span></div>
            <div id=pad>p</div>
            <span id=u>Title</span>
            <div id=o>o</div>
            """, """
            (() => {
                const cs = (id, pseudo) => getComputedStyle(document.getElementById(id), pseudo);
                return [getComputedStyle(document.documentElement).scrollPaddingTop,
                    cs('flex').gap, cs('flex').rowGap, cs('flex').columnGap, cs('pad').paddingLeft,
                    cs('u', '::after').transformOrigin, cs('o').transformOrigin].join(' | ');
            })()
            """)).Should().Be("80px | 16px 25.6px | 16px | 25.6px | 16px | 0px 1px | 0px 0px");
    }

    [Test]
    public async Task AGapInRemAloneIsPixels()
    {
        (await Read("#g { display: flex; gap: 0.4rem }", "<div id=g></div>", """
            (() => { const s = getComputedStyle(document.getElementById('g')); return [s.gap, s.rowGap, parseFloat(s.columnGap)].join(' | '); })()
            """)).Should().Be("6.4px | 6.4px | 6.4");
    }

    [Test]
    public async Task FontRelativeUnitsFollowTheFontSizeCascade()
    {
        (await Read("""
            html { font-size: 20px }
            #outer { font-size: 1.5em; margin-top: 2em; padding-top: 1rem }
            #inner { font-size: 50%; letter-spacing: 0.5em }
            #kw { font-size: large; margin-bottom: 1em }
            #calc { font-size: calc(1rem + 2px); min-width: calc(10vw - 1em); max-height: min(10vh, 100px) }
            """, "<div id=outer><div id=inner></div></div><p id=kw></p><span id=calc></span>", """
            (() => {
                const g = (id) => getComputedStyle(document.getElementById(id));
                return [g('outer').fontSize, g('outer').marginTop, g('outer').paddingTop,
                    g('inner').fontSize, g('inner').letterSpacing,
                    g('kw').fontSize, g('kw').marginBottom,
                    g('calc').fontSize, g('calc').minWidth, g('calc').maxHeight].join(' | ');
            })()
            """)).Should().Be("30px | 60px | 20px | 15px | 7.5px | 18px | 18px | 22px | 106px | 72px");
    }

    [Test]
    public async Task AbsoluteUnitsBecomePixelsAndPercentagesStay()
    {
        (await Read("""
            #a { margin-left: 1in; margin-right: 12pt; text-indent: calc(50% + 1rem); top: 2.54cm }
            #a::before { content: "1rem 2em"; }
            """, "<div id=a></div>", """
            (() => {
                const s = getComputedStyle(document.getElementById('a'));
                return [s.marginLeft, s.marginRight, s.textIndent, s.top,
                    getComputedStyle(document.getElementById('a'), '::before').content].join(' | ');
            })()
            """)).Should().Be("96px | 16px | calc(50% + 16px) | 96px | \"1rem 2em\"");
    }

    [Test]
    public async Task InheritedLengthsAreAbsolutizedWhereTheyAreDeclared()
    {
        // CSS Cascade 5 §7.2: a child inherits its parent's computed (absolute) value, not its text.
        (await Read("""
            #p { font-size: 10px; line-height: 2em }
            #c { font-size: 40px }
            """, "<div id=p><span id=c></span></div>", """
            getComputedStyle(document.getElementById('c')).lineHeight
            """)).Should().Be("20px");
    }

    [Test]
    public async Task OriginsResolveKeywordsAndPercentagesAgainstTheBox()
    {
        (await Read("""
            #a::after { content: ''; display: block; width: 40px; height: 10px; transform-origin: right bottom 3px }
            #b::after { content: ''; display: block; width: 40px; height: 10px; transform-origin: 25% 1rem }
            #c::after { content: ''; display: block; width: 40px; height: 10px; perspective-origin: top }
            """, "<div id=a></div><div id=b></div><div id=c></div>", """
            (() => {
                const g = (id, name) => getComputedStyle(document.getElementById(id), '::after')[name];
                return [g('a', 'transformOrigin'), g('b', 'transformOrigin'), g('c', 'perspectiveOrigin')].join(' | ');
            })()
            """)).Should().Be("40px 10px 3px | 10px 16px | 20px 0px");
    }

    [Test]
    public async Task MathIsClampedForNonNegativePropertiesAndDeepNestingStaysBounded()
    {
        (await Read("#a { padding-left: calc(1rem - 2rem); margin-left: calc(1rem - 2rem); min-width: max(1rem, 2em, 10px) }",
            "<span id=a></span>", """
            (() => {
                const a = document.getElementById('a'), s = getComputedStyle(a);
                const deep = 'calc('.repeat(200) + '1rem + 50%' + ')'.repeat(200);
                a.style.marginRight = deep;
                const right = s.marginRight;
                return [s.paddingLeft, s.marginLeft, s.minWidth, typeof right === 'string' && right.length > 0].join(' | ');
            })()
            """)).Should().Be("0px | -16px | 32px | true");
    }

    [Test]
    public async Task ViewportUnitsFollowTheEmulatedViewport()
    {
        await using var browser = new Browser(new global::Jint.Browser.BrowserOptions { Viewport = new global::Jint.Browser.Viewport(400, 300) });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><style>#v { margin-left: 10vw; margin-top: 10vh; padding-left: 10vmax; padding-top: 10vmin }</style><div id=v></div>");
        (await page.EvaluateAsync<string>("""
            (() => { const s = getComputedStyle(document.getElementById('v')); return [s.marginLeft, s.marginTop, s.paddingLeft, s.paddingTop].join(' | '); })()
            """)).Should().Be("40px | 30px | 40px | 30px");
        page.Errors.Should().BeEmpty();
    }
}
