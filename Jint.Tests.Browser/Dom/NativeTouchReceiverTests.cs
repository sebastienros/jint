using Jint.Browser;

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeTouchReceiverTests
{
    [Test]
    public async Task HandlerAttributesAcceptNativeNodesAndRejectAnIllegalReceiver()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<button id='target'>touch</button>");
        await page.SetTouchEmulationAsync(enabled: true);
        (await page.EvaluateAsync<bool>("""
            var target = document.getElementById('target');
            var calls = 0;
            var handler = () => calls++;
            var descriptor = Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'ontouchstart');
            target.ontouchstart = handler;
            var same = descriptor.get.call(target) === handler;
            var rejected = false;
            try { descriptor.set.call({}, handler); } catch (e) { rejected = e instanceof TypeError; }
            target.dispatchEvent(new Event('touchstart'));
            same && rejected && calls === 1
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }
}
