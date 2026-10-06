namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeGeneratedHistoryTests
{
    [Test]
    public async Task GeneratedHistoryUsesInstalledIdentityAndPreservesConversionOrder()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("", "https://history.test/path");
        (await page.EvaluateAsync<bool>("""
            var order=[];
            var title={toString(){order.push('title');return ''}};
            var url={toString(){order.push('url');return '#retained'}};
            History.prototype.pushState.call(history,{answer:42},title,url);
            var first=Object.getOwnPropertyDescriptor(History.prototype,'state').get.call(history);
            var second=history.state;
            var conversions=0,rejected=0;
            var argument={toString(){conversions++;return ''},valueOf(){conversions++;return 0}};
            for(var receiver of [{},document,Object.create(history),Object.create(History.prototype)]) {
                try { History.prototype.pushState.call(receiver,{},argument,argument); }
                catch(error) { if(error instanceof TypeError && /Illegal invocation/.test(error.message)) rejected++; }
            }
            Object.getPrototypeOf(history)===Object.prototype && history.hasOwnProperty('length') &&
                History.prototype.pushState.length===2 && History.prototype.go.length===0 &&
                order.join(',')==='title,url' && first.answer===42 && second.answer===42 && first!==second &&
                location.hash==='#retained' && rejected===4 && conversions===0
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }
}
