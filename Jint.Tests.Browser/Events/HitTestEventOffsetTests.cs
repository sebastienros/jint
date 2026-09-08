#nullable enable

using Jint.Browser.Events;
using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>CSSOM View's event position uses the box hit before the first listener runs.</summary>
public sealed class HitTestEventOffsetTests
{
    [TestCase("pointermove")]
    [TestCase("pointerdown")]
    [TestCase("pointerup")]
    [TestCase("wheel")]
    public async Task TheFirstEventKeepsTheHitTestOffsetAndRedispatchDoesNotReuseIt(string type)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='before'>before</div><div id='target'>target</div>");
        await page.EvaluateAsync<object>($$"""
            globalThis.seen = [];
            const target = document.getElementById('target');
            target.addEventListener('{{type}}', e => {
              if (e.isTrusted) {
                globalThis.saved = e;
                document.getElementById('before').remove();
              }
              seen.push(e.offsetX, e.offsetY);
            });
            globalThis.following = [];
            target.addEventListener('{{type.Replace("pointer", "mouse", StringComparison.Ordinal)}}', e => {
              if (e.type !== 'wheel') {
                const box = target.getBoundingClientRect();
                following.push(e.offsetY === e.clientY - box.y);
              }
            });
            """);

        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var target = runtime.Document!.GetElementById("target")!;
            var box = runtime.Layout.Current().ClientBoxOf(target)!.Value;
            var kind = type switch
            {
                "pointermove" => MouseInputKind.Moved,
                "pointerdown" => MouseInputKind.Pressed,
                "pointerup" => MouseInputKind.Released,
                _ => MouseInputKind.Wheel,
            };
            InputDispatcher.DispatchMouse(runtime, new MouseInput(
                kind, box.X + 7, box.Y + 3, 0, 0, 1, EventModifiers.None, 0, 0));
            return true;
        });

        (await page.EvaluateAsync<string>("seen.join('|')")).Should().Be("7|3");
        (await page.EvaluateAsync<string>("following.join('|')"))
            .Should().Be(type == "wheel" ? "" : "true");
        (await page.EvaluateAsync<string>("""
            (() => {
              const box = target.getBoundingClientRect();
              const expected = [saved.clientX - box.x, saved.clientY - box.y];
              target.dispatchEvent(saved);
              return seen.slice(2).join('|') + '=' + expected.join('|');
            })()
            """)).Should().Be("7|19=7|19");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task PublicHoverKeepsTheOffsetBeforeTheListenerRemovesTheTarget()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='before'>before</div><div id='target'>target</div>");
        await page.EvaluateAsync<object>("""
            globalThis.seen = '';
            const target = document.getElementById('target');
            const box = target.getBoundingClientRect();
            globalThis.expected = [box.width / 2, box.height / 2].join('|');
            target.addEventListener('pointermove', e => {
              target.remove();
              seen = [e.offsetX, e.offsetY].join('|');
            });
            """);

        (await page.HoverAsync("#target")).Should().BeTrue();

        (await page.EvaluateAsync<bool>("seen === expected")).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }
}
