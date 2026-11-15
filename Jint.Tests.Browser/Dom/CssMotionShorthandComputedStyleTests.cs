#nullable enable

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

/// <summary>Computed style reports the animation and transition longhands and their initial values (#4617).</summary>
/// <remarks>https://drafts.csswg.org/css-animations-1/#animation and https://drafts.csswg.org/css-transitions-1/#transition-shorthand-property</remarks>
public sealed class CssMotionShorthandComputedStyleTests
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
    public async Task AnimationLonghandsAndShorthandMatchChromium()
    {
        // The issue's repro, with Chromium's answer.
        (await Read("""
            #a2 { animation: spin 2s linear infinite; }
            @keyframes spin { to { transform: rotate(1turn) } }
            """, "<div id=a1></div><div id=a2></div>", """
            ['a1', 'a2'].map(id => {
                const s = getComputedStyle(document.getElementById(id));
                return id + ' ' + JSON.stringify([s.animation, s.animationName, s.animationDuration, s.animationTimingFunction, s.animationIterationCount]);
            }).join('\n')
            """)).Should().Be("""
            a1 ["none","none","0s","ease","1"]
            a2 ["2s linear infinite spin","spin","2s","linear","infinite"]
            """);
    }

    [Test]
    public async Task UnsetAnimationLonghandsReportTheirInitialValues()
    {
        (await Read("", "<div id=a></div>", """
            (() => {
                const s = getComputedStyle(document.getElementById('a'));
                return [s.animationDelay, s.animationDirection, s.animationFillMode, s.animationPlayState,
                    s.getPropertyValue('animation-composition'), s.getPropertyValue('animation-timeline')].join(' | ');
            })()
            """)).Should().Be("0s | normal | none | running | replace | auto");
    }

    [Test]
    public async Task TransitionLonghandsAndShorthandMatchChromium()
    {
        (await Read("""
            #b { transition: opacity 300ms ease-in-out, transform .5s 100ms }
            #c { transition: color 1s, background-color 2s; transition-duration: 250ms }
            """, "<div id=a></div><div id=b></div><div id=c></div>", """
            ['a', 'b', 'c'].map(id => {
                const s = getComputedStyle(document.getElementById(id));
                return id + ' ' + JSON.stringify([s.transition, s.transitionProperty, s.transitionDuration,
                    s.transitionTimingFunction, s.transitionDelay, s.transitionBehavior]);
            }).join('\n')
            """)).Should().Be("""
            a ["all","all","0s","ease","0s","normal"]
            b ["opacity 0.3s ease-in-out, transform 0.5s 0.1s","opacity, transform","0.3s, 0.5s","ease-in-out, ease","0s, 0.1s","normal, normal"]
            c ["color 0.25s, background-color 0.25s","color, background-color","0.25s","ease, ease","0s, 0s","normal, normal"]
            """);
    }

    [Test]
    public async Task ComputedTimesAreSecondsWhileSpecifiedOnesKeepTheirText()
    {
        (await Read("#a { animation-duration: 1500ms; animation-delay: -0ms; transition-duration: 0.0001ms, 1e21s, 1234567ms }",
            "<div id=a style='transition-delay: 20ms'></div>", """
            (() => {
                const e = document.getElementById('a');
                const s = getComputedStyle(e);
                return [s.animationDuration, s.animationDelay, s.transitionDelay, e.style.transitionDelay, s.transitionDuration].join(' | ');
            })()
            """)).Should().Be("1.5s | 0s | 0.02s | 20ms | 1e-07s, 1e+21s, 1234.57s");
    }

    [Test]
    public async Task InheritAndVarReachTheLonghands()
    {
        (await Read("""
            :root { --motion: fade 4s ease-out }
            #p { animation: pulse 1s }
            #c { animation-name: inherit }
            #v { animation: var(--motion) }
            """, "<div id=p><div id=c></div></div><div id=v></div>", """
            (() => {
                const g = (id) => getComputedStyle(document.getElementById(id));
                return [g('c').animationName, g('c').animationDuration, g('v').animationName, g('v').animationDuration,
                    g('v').animationTimingFunction, g('v').animation].join(' | ');
            })()
            """)).Should().Be("pulse | 0s | fade | 4s | ease-out | 4s ease-out fade");
    }
}
