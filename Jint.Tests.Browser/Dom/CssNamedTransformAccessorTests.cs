#nullable enable

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

/// <summary>CSSOM's supported-property accessors use the native declaration's generic property methods.</summary>
/// <remarks>https://drafts.csswg.org/cssom/#the-cssstyleproperties-interface</remarks>
public sealed class CssNamedTransformAccessorTests
{
    [TestCase("translate", "10px 20%", "30px 40%")]
    [TestCase("rotate", "x 45deg", "y 90deg")]
    [TestCase("scale", "2", "3")]
    public async Task NamedAccessorsShareTheNativeInlineRuleAndComputedDeclarations(string name, string first, string second)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#box { }</style><div id=box>text</div>");
        await page.EvaluateAsync($$"""
            var name = '{{name}}', first = '{{first}}', second = '{{second}}';
            var box = document.getElementById('box'), inline = box.style;
            var rule = document.styleSheets[0].cssRules[0].style;
            var computed = getComputedStyle(box);
            var prototype = CSSStyleDeclaration.prototype;
            var descriptor = Object.getOwnPropertyDescriptor(prototype, name);
            """);
        (await page.EvaluateAsync<bool>("""
            descriptor.enumerable && descriptor.configurable &&
            typeof descriptor.get === 'function' && typeof descriptor.set === 'function' &&
            descriptor.get.length === 0 && descriptor.set.length === 1 &&
            [inline, rule, computed].every(style =>
                style instanceof CSSStyleDeclaration && Object.getPrototypeOf(style) === prototype)
            """)).Should().BeTrue();

        await page.EvaluateAsync("rule[name] = first");
        (await page.EvaluateAsync<string>("rule.getPropertyValue(name) + '|' + computed[name]"))
            .Should().Be(first + "|" + first);
        await page.EvaluateAsync("rule.setProperty(name, second)");
        (await page.EvaluateAsync<string>("rule[name] + '|' + computed[name]"))
            .Should().Be(second + "|" + second);
        await page.EvaluateAsync("inline[name] = first");
        (await page.EvaluateAsync<string>("inline.getPropertyValue(name) + '|' + computed[name]"))
            .Should().Be(first + "|" + first);
        await page.EvaluateAsync("inline.setProperty(name, second)");
        (await page.EvaluateAsync<string>("inline[name] + '|' + computed.getPropertyValue(name)"))
            .Should().Be(second + "|" + second);

        // These writes reach the inherited accessor rather than adding receiver expandos. The same
        // computed receiver remains live across native rule and inline declaration changes.
        (await page.EvaluateAsync<bool>("""
            [inline, rule, computed].every(style =>
                !Object.prototype.hasOwnProperty.call(style, name) &&
                Object.getPrototypeOf(style) === prototype) &&
            box.style === inline && document.styleSheets[0].cssRules[0].style === rule &&
            Object.getOwnPropertyDescriptor(prototype, name).get === descriptor.get &&
            Object.getOwnPropertyDescriptor(prototype, name).set === descriptor.set
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [TestCase("translate", "10px 20%")]
    [TestCase("rotate", "x 45deg")]
    [TestCase("scale", "2")]
    public async Task NamedSettersConvertNullRemoveAndRetainUnvalidatedText(string name, string value)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#box { }</style><div id=box>text</div>");
        (await page.EvaluateAsync<bool>($$"""
            (() => {
                const name = '{{name}}', value = '{{value}}';
                const box = document.getElementById('box');
                const styles = [box.style, document.styleSheets[0].cssRules[0].style];
                return styles.every(style => {
                    style[name] = value;
                    style[name] = undefined;
                    if (style[name] !== 'undefined' || style.getPropertyValue(name) !== 'undefined') return false;
                    style[name] = 'bogus';
                    if (style[name] !== 'bogus') return false;
                    style[name] = null;
                    return style[name] === '' && style.getPropertyValue(name) === '' && style.length === 0;
                });
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [TestCase("translate", "10px 20%")]
    [TestCase("rotate", "x 45deg")]
    [TestCase("scale", "2")]
    public async Task ComputedNamedSettersRefuseAndWrongReceiversFailBeforeConversion(string name, string value)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=box>text</div>");
        (await page.EvaluateAsync<bool>($$"""
            (() => {
                const name = '{{name}}', value = '{{value}}';
                const box = document.getElementById('box');
                box.style[name] = value;
                const computed = getComputedStyle(box);
                const descriptor = Object.getOwnPropertyDescriptor(CSSStyleDeclaration.prototype, name);
                for (const input of [value, null, undefined, 'bogus']) {
                    let error;
                    try { computed[name] = input; } catch (caught) { error = caught; }
                    if (!(error instanceof DOMException) || error.name !== 'NoModificationAllowedError') return false;
                    if (computed[name] !== value || computed.getPropertyValue(name) !== value) return false;
                }
                return [{}, box, document].every(receiver => {
                    let conversions = 0, getterError, setterError;
                    try { descriptor.get.call(receiver); } catch (caught) { getterError = caught; }
                    try {
                        descriptor.set.call(receiver, {toString() { conversions++; return value; } });
                    } catch (caught) { setterError = caught; }
                    return getterError instanceof TypeError && setterError instanceof TypeError && conversions === 0;
                });
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }
}
