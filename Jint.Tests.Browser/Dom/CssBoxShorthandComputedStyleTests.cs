#nullable enable

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

/// <summary>The background and border shorthands set their longhands (#4592).</summary>
/// <remarks>https://drafts.csswg.org/css-backgrounds-3/#background and #border-shorthands</remarks>
public sealed class CssBoxShorthandComputedStyleTests
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
    public async Task ShorthandsReachTheirLonghandsLikeChromium()
    {
        // The issue's repro, with Chromium's answer.
        (await Read("""
            #a { background: rgb(81, 43, 212) }
            #b { border: 1px solid rgb(81, 43, 212) }
            #c { margin: 7px }
            #d { background-color: rgb(81, 43, 212) }
            """, "<p id=a>a</p><p id=b>b</p><p id=c>c</p><p id=d>d</p>", """
            (() => {
                const g = (id) => getComputedStyle(document.getElementById(id));
                return [g('a').backgroundColor, g('a').getPropertyValue('background-color'),
                    g('b').borderTopColor, g('b').borderTopWidth, g('b').borderTopStyle, g('c').marginTop, g('d').backgroundColor].join(' | ');
            })()
            """)).Should().Be("rgb(81, 43, 212) | rgb(81, 43, 212) | rgb(81, 43, 212) | 1px | solid | 7px | rgb(81, 43, 212)");
    }

    [Test]
    public async Task ImportantAndVarValuesExpandAfterSubstitution()
    {
        (await Read("""
            :root { --c: rgb(81, 43, 212); --line: 2px dashed red }
            #a { background: var(--c) !important }
            #a { background: blue }
            #b { border: var(--line) }
            #c { border-left: var(--missing) }
            #d { margin: var(--m, 1px 2px) }
            """, "<p id=a>a</p><p id=b>b</p><p id=c>c</p><p id=d>d</p>", """
            (() => {
                const g = (id) => getComputedStyle(document.getElementById(id));
                return [g('a').backgroundColor, g('a').backgroundImage, g('b').borderRightWidth, g('b').borderRightStyle,
                    g('b').borderBottomColor, g('c').borderLeftStyle, g('d').marginTop, g('d').marginRight].join(' | ');
            })()
            """)).Should().Be("rgb(81, 43, 212) | none | 2px | dashed | rgb(255, 0, 0) | none | 1px | 2px");
    }

    [Test]
    public async Task ComponentsAreOrderFreeAndOmittedOnesReset()
    {
        (await Read("""
            #a { border-color: red; border-top: dotted 3px }
            #b { background: url(x.png) no-repeat right top / cover fixed content-box #00ff00 }
            #c { background-color: red; background: none }
            #d { border: 1px solid red blue }
            #e { border: 2px dashed green; border: solid solid; border: -1px solid; border-color: 1px solid }
            #f { background: lime; background: none none; background: repeat repeat repeat }
            """, "<p id=a>a</p><p id=b>b</p><p id=c>c</p><p id=d>d</p><p id=e>e</p><p id=f>f</p>", """
            (() => {
                const g = (id) => getComputedStyle(document.getElementById(id));
                const b = g('b');
                return [g('a').borderTopStyle, g('a').borderTopWidth, g('a').borderTopColor, g('a').borderLeftColor,
                    b.backgroundImage, b.backgroundRepeat, b.backgroundPositionX, b.backgroundPositionY, b.backgroundSize,
                    b.backgroundAttachment, b.backgroundOrigin, b.backgroundClip, b.backgroundColor,
                    g('c').backgroundColor, g('d').borderTopStyle,
                    g('e').borderTopWidth, g('e').borderTopStyle, g('e').borderTopColor, g('f').backgroundColor].join(' | ');
            })()
            """)).Should().Be("dotted | 3px | rgb(0, 0, 0) | rgb(255, 0, 0) | url(x.png) | no-repeat | right | top | cover | "
            + "fixed | content-box | content-box | rgb(0, 255, 0) | rgba(0, 0, 0, 0) | none | "
            + "2px | dashed | rgb(0, 128, 0) | rgb(0, 255, 0)");
    }

    [Test]
    public async Task InlineShorthandsRoundTripThroughTheStyleAttribute()
    {
        (await Read("", "<p id=a>a</p>", """
            (() => {
                const a = document.getElementById('a');
                a.style.border = '1px solid red';
                a.style.background = 'var(--c, blue)';
                const first = [a.getAttribute('style'), a.style.borderTopWidth, a.style.border, a.style.backgroundColor,
                    a.style.background, getComputedStyle(a).backgroundColor].join(' | ');
                a.style.borderLeftColor = 'green';
                a.style.background = 'url(a.png) center';
                return first + ' # ' + [a.style.border, a.style.borderLeft, a.style.backgroundPosition, a.style.background].join(' | ');
            })()
            """)).Should().Be("border: 1px solid red; background: var(--c, blue); | 1px | 1px solid red |  | var(--c, blue) | rgb(0, 0, 255)"
            + " #  | 1px solid green | center center | url(a.png) center center");
    }
}
