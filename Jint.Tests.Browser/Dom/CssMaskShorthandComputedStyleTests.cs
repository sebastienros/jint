#nullable enable

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

/// <summary>The mask and -webkit-mask shorthands set their longhands (#4616).</summary>
/// <remarks>
/// https://drafts.csswg.org/css-masking-1/#the-mask and https://compat.spec.whatwg.org/#css-properties
/// </remarks>
public sealed class CssMaskShorthandComputedStyleTests
{
    private const string Svg = "url(\"data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg'/>\")";

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
    public async Task IssueReproMatchesChromium()
    {
        // The issue's repro, with Chromium's answer: UnoCSS presetIcons emits `mask: var(--un-icon) no-repeat`.
        (await Read($$"""
            #m1 { --icon: {{Svg}}; mask: var(--icon) no-repeat; mask-size: 100% 100%; }
            #m2 { mask: {{Svg}} no-repeat center / contain; }
            #m3 { -webkit-mask: {{Svg}} no-repeat; }
            #m4 { mask-image: url("data:image/svg+xml;utf8,x"); }
            """, "<span id=m1></span><span id=m2></span><span id=m3></span><span id=m4></span>", """
            ['m1', 'm2', 'm3', 'm4'].map(id => {
                const s = getComputedStyle(document.getElementById(id));
                return id + ' ' + JSON.stringify([s.maskImage.slice(0, 30), s.maskRepeat, s.maskPosition, s.maskSize, s.webkitMaskImage?.slice(0, 30)]);
            }).join('\n')
            """)).Should().Be(string.Join("\n",
            """m1 ["url(\"data:image/svg+xml;utf8,<","no-repeat","0% 0%","100% 100%","url(\"data:image/svg+xml;utf8,<"]""",
            """m2 ["url(\"data:image/svg+xml;utf8,<","no-repeat","50% 50%","contain","url(\"data:image/svg+xml;utf8,<"]""",
            """m3 ["url(\"data:image/svg+xml;utf8,<","no-repeat","0% 0%","auto","url(\"data:image/svg+xml;utf8,<"]""",
            """m4 ["url(\"data:image/svg+xml;utf8,x","repeat","0% 0%","auto","url(\"data:image/svg+xml;utf8,x"]"""));
    }

    [Test]
    public async Task EveryLonghandReportsItsInitialValueOrTheComponentThatSetIt()
    {
        (await Read("""
            #a { mask: url(a.svg) right 10px bottom 20% / 10px auto repeat-x content-box no-clip subtract luminance }
            #b { mask: url(a.svg) padding-box, linear-gradient(red, blue) top left }
            #c { mask: url(a.svg) border-box content-box }
            #d { mask-repeat: space; mask: none }
            """, "<p id=a></p><p id=b></p><p id=c></p><p id=d></p><p id=e></p>", """
            ['a', 'b', 'c', 'd', 'e'].map(id => {
                const s = getComputedStyle(document.getElementById(id));
                return [s.maskImage, s.maskPosition, s.maskSize, s.maskRepeat, s.maskOrigin, s.maskClip, s.maskComposite, s.maskMode].join(' | ');
            }).join('\n')
            """)).Should().Be(string.Join("\n",
            "url(a.svg) | calc(100% - 10px) 80% | 10px auto | repeat-x | content-box | no-clip | subtract | luminance",
            "url(a.svg), linear-gradient(rgb(255, 0, 0), rgb(0, 0, 255)) | 0% 0%, 0% 0% | auto, auto | repeat, repeat | "
                + "padding-box, border-box | padding-box, border-box | add, add | match-source, match-source",
            "url(a.svg) | 0% 0% | auto | repeat | border-box | content-box | add | match-source",
            "none | 0% 0% | auto | repeat | border-box | border-box | add | match-source",
            "none | 0% 0% | auto | repeat | border-box | border-box | add | match-source"));
    }

    [Test]
    public async Task InvalidMasksAreDroppedAndMissingVariablesResetTheLonghands()
    {
        (await Read("""
            #a { mask: url(a.svg) no-repeat; mask: url(b.svg) url(c.svg); mask: red; mask: center / ; mask: alpha luminance }
            #b { mask: url(a.svg) no-repeat; mask: var(--missing) }
            #c { --m: url(c.svg) center; -webkit-mask: var(--m) }
            """, "<p id=a></p><p id=b></p><p id=c></p>", """
            ['a', 'b', 'c'].map(id => {
                const s = getComputedStyle(document.getElementById(id));
                return [s.maskImage, s.maskRepeat, s.maskPosition].join(' | ');
            }).join('\n')
            """)).Should().Be(string.Join("\n",
            "url(a.svg) | no-repeat | 0% 0%",
            "none | repeat | 0% 0%",
            "url(c.svg) | repeat | 50% 50%"));
    }

    [Test]
    public async Task WebkitAliasesReadAndWriteTheUnprefixedProperties()
    {
        (await Read("", "<p id=a></p>", """
            (() => {
                const a = document.getElementById('a');
                a.style.webkitMask = 'url(a.svg) no-repeat center';
                const first = [a.getAttribute('style'), a.style.maskImage, a.style.webkitMaskRepeat, a.style.WebkitMaskPosition,
                    a.style.mask, a.style.webkitMask, a.style.getPropertyValue('-webkit-mask-image')].join(' | ');
                a.style.WebkitMaskSize = 'cover';
                a.style.setProperty('-webkit-mask-clip', 'no-clip');
                const s = getComputedStyle(a);
                return first + ' # ' + [a.style.maskSize, a.style.mask, s.webkitMaskSize, s.WebkitMaskClip, s.webkitMaskPosition,
                    s.webkitMaskOrigin].join(' | ');
            })()
            """)).Should().Be("mask: url(a.svg) center center no-repeat; | url(a.svg) | no-repeat | center center"
            + " | url(a.svg) center center no-repeat | url(a.svg) center center no-repeat | url(a.svg)"
            + " # cover | url(a.svg) center center / cover no-repeat no-clip | cover | no-clip | 50% 50% | border-box");
    }
}
