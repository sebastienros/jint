#nullable enable

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class CssSupportsTests
{
    [Test]
    public async Task NamespaceDescriptorsAndDetachedCallsArePreserved()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        (await page.EvaluateAsync<string>("""
            (() => {
              const descriptor = Object.getOwnPropertyDescriptor(CSS, 'supports');
              const supports = CSS.supports;
              return [Object.prototype.toString.call(CSS), supports.name, supports.length,
                descriptor.enumerable, descriptor.writable, descriptor.configurable,
                supports('color', 'red'), supports.call(null, 'color:red'),
                supports.call({}, 'selector(div)'), CSS.escape('a b')].join('|');
            })()
            """)).Should().Be("[object CSS]|supports|1|true|true|true|true|true|true|a\\ b");
    }

    [Test]
    public async Task ArgumentsConvertInOrderAndExceptionsEscape()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        (await page.EvaluateAsync<string>("""
            (() => {
              const log = [];
              const first = { toString() { log.push('property'); return 'color'; } };
              const second = { toString() { log.push('value'); return 'red'; } };
              const extra = { toString() { throw 'extra'; } };
              log.push(CSS.supports(first, second, extra));
              const failure = {};
              try { CSS.supports({ toString() { throw failure; } }, second); }
              catch (e) { log.push(e === failure); }
              try { CSS.supports(first, { toString() { throw failure; } }); }
              catch (e) { log.push(e === failure); }
              return log.join('|');
            })()
            """)).Should().Be("property|value|true|true|property|true");
    }

    [Test]
    public async Task NativeCapabilityAnswersCoverBothOverloads()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        (await page.EvaluateAsync<string>("""
            [CSS.supports('color', 'red'), CSS.supports(' color', 'red'),
             CSS.supports('color', 'red!important'), CSS.supports('color:red!important'),
             CSS.supports('--', ''), CSS.supports('border-color', 'red'),
             CSS.supports('selector(:is(div, :unknown))'),
             CSS.supports('(color:red) or (display:block) trailing'),
             CSS.supports('color', 'red) or (display:block')].join('|')
            """)).Should().Be("true|false|false|true|true|false|false|false|false");
    }
}
