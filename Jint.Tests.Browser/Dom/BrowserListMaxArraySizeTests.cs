#nullable enable

using Jint.Browser;
using Jint.Runtime;

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The hand-written <c>Jint.Browser</c> members that hand script a list as long as what it configured build that
/// array under <c>Options.Constraints.MaxArraySize</c> (issue #4505), like the lists #4493 bounded through
/// <c>ConstructFast</c>: a canvas context's dash list
/// (https://html.spec.whatwg.org/multipage/canvas.html#dom-context-2d-getlinedash), which <c>setLineDash</c> doubles for
/// an odd count, and the lists a <c>Sanitizer</c> hands back from <c>get()</c>
/// (https://wicg.github.io/sanitizer-api/#dom-sanitizer-get). Each is pinned past the limit and at exactly it.
/// </summary>
public sealed class BrowserListMaxArraySizeTests
{
    private const uint Limit = 4;

    private static Browser LimitedBrowser()
        => new(new BrowserOptions().ConfigureEngine(options => options.Constraints.MaxArraySize = Limit));

    private static async Task ShouldBeRefused(Func<Task> action, int reportedSize)
    {
        var thrown = await Caught.ExceptionAsync(action);

        thrown.Should().BeOfType<MemoryLimitExceededException>();
        thrown!.Message.Should().Be($"The array size {reportedSize} is larger than maximum allowed ({Limit})");
    }

    [TestCase("[1, 2, 3]", 6)]
    [TestCase("[1, 2, 3, 4, 5]", 10)]
    [TestCase("[1, 2, 3, 4]", 4)]
    [TestCase("[1, 2]", 2)]
    public async Task GetLineDashIsBoundedByTheLimit(string segments, int stored)
    {
        await using var browser = LimitedBrowser();
        var page = await browser.NewPageAsync();
        await page.EvaluateAsync($"globalThis.ctx = document.createElement('canvas').getContext('2d'); ctx.setLineDash({segments});");

        if (stored > Limit)
        {
            await ShouldBeRefused(() => page.EvaluateAsync("ctx.getLineDash()"), stored);
        }
        else
        {
            (await page.EvaluateAsync<double>("ctx.getLineDash().length")).Should().Be(stored);
        }
    }

    [TestCase("elements", "allowElement", 5)]
    [TestCase("removeElements", "removeElement", 5)]
    [TestCase("attributes", "allowAttribute", 5)]
    [TestCase("elements", "allowElement", 4)]
    [TestCase("removeElements", "removeElement", 4)]
    [TestCase("attributes", "allowAttribute", 4)]
    public async Task SanitizerGetIsBoundedByTheLimit(string list, string method, int count)
    {
        await using var browser = LimitedBrowser();
        var page = await browser.NewPageAsync();
        await page.EvaluateAsync($$"""
            globalThis.s = new Sanitizer({ {{list}}: [] });
            const names = { 0: 'p', 1: 'b', 2: 'i', 3: 'em', 4: 'a' };
            for (let i = 0; i < {{count}}; i++) s.{{method}}(names[i]);
            """);

        if (count > Limit)
        {
            await ShouldBeRefused(() => page.EvaluateAsync("s.get()"), count);
        }
        else
        {
            (await page.EvaluateAsync<double>($"s.get().{list}.length")).Should().Be(count);
        }
    }
}
