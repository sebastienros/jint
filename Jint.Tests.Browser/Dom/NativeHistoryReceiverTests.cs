using Jint.Browser;

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeHistoryReceiverTests
{
    [Test]
    public async Task InstalledHistoryKeepsOwnMembersAndRefusesUnregisteredReceiversBeforeConversion()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("", "https://history.test/path");
        (await page.EvaluateAsync<bool>("""
            var own = Object.getPrototypeOf(history) === Object.prototype && history.hasOwnProperty('length');
            var converted = 0, rejected = 0;
            var value = { valueOf() { converted++; return 0; }, toString() { converted++; return 'manual'; } };
            for (var operation of [
                () => history.go.call({}, value),
                () => Object.getOwnPropertyDescriptor(history, 'scrollRestoration').set.call({}, value),
                () => history.pushState.call({}, {}, '', value),
                () => Object.getOwnPropertyDescriptor(history, 'length').get.call({})
            ]) { try { operation(); } catch (e) { if (e instanceof TypeError) rejected++; } }
            history.pushState({ answer: 42 }, '', '#state');
            history.scrollRestoration = 'manual';
            own && rejected === 4 && converted === 0 && history.state.answer === 42 &&
                history.scrollRestoration === 'manual' && location.hash === '#state'
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }
}
