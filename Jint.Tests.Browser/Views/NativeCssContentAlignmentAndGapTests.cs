#nullable enable

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssContentAlignmentAndGapTests
{
    [TestCase("alignContent", "align-content", "first baseline", "first baseline", "first baseline")]
    [TestCase("justifyContent", "justify-content", "space-around", "space-around", "space-around")]
    [TestCase("placeContent", "place-content", "last baseline start", "last baseline start", "last baseline start")]
    [TestCase("rowGap", "row-gap", "2em", "2em", "2em")]
    [TestCase("columnGap", "column-gap", "thick", "", "normal")]
    [TestCase("gap", "gap", "2em 3px", "2em 3px", "2em 3px")]
    [TestCase("gridGap", "grid-gap", "2em", "2em", "2em")]
    [TestCase("gridRowGap", "grid-row-gap", "2em", "2em", "2em")]
    [TestCase("gridColumnGap", "grid-column-gap", "2em", "2em", "2em")]
    public async Task NamedGenericRuleAndLiveComputedSurfacesAgree(string named, string property,
        string value, string specified, string computed)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#box {}</style><div id=box style='font-size:20px'></div>");
        (await page.EvaluateAsync<string>($$"""
            (() => {
                const box = document.getElementById('box'), rule = document.styleSheets[0].cssRules[0].style;
                const result = getComputedStyle(box);
                rule.{{named}} = '{{value}}';
                return [rule.getPropertyValue('{{property}}'), result.{{named}},
                    result.getPropertyValue('{{property}}')].join('|');
            })()
            """)).Should().Be(specified + "|" + computed + "|" + computed);
        (await page.EvaluateAsync<string>($$"""
            (() => {
                const box = document.getElementById('box'), result = getComputedStyle(box);
                box.style.setProperty('{{property}}', '{{value}}');
                return box.style.{{named}} + '|' + result.{{named}};
            })()
            """)).Should().Be(specified + "|" + computed);
        (await page.EvaluateAsync<bool>($$"""
            CSS.supports('{{property}}', '{{value}}') && CSS.supports('{{property}}', 'bogus')
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AccessorsPreserveDescriptorsReceiverGuardsAndReadOnlyComputedValues()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#box {}</style><div id=box></div>");
        (await page.EvaluateAsync<bool>("""
            (() => {
                const box = document.getElementById('box'), computed = getComputedStyle(box);
                const names = ['alignContent','justifyContent','placeContent','gap','rowGap','columnGap',
                    'gridGap','gridRowGap','gridColumnGap'];
                return names.every(name => {
                    const descriptor = Object.getOwnPropertyDescriptor(CSSStyleDeclaration.prototype, name);
                    if (!descriptor || !descriptor.enumerable || !descriptor.configurable ||
                        descriptor.get.length !== 0 || descriptor.set.length !== 1) return false;
                    let conversions = 0;
                    try { descriptor.set.call({}, {toString(){conversions++;return 'normal'}}); return false; }
                    catch (e) { if (!(e instanceof TypeError) || conversions !== 0) return false; }
                    try { descriptor.get.call({}); return false; }
                    catch (e) { if (!(e instanceof TypeError)) return false; }
                    try { computed[name] = 'normal'; return false; }
                    catch (e) { if (e.name !== 'NoModificationAllowedError') return false; }
                    return [box.style, document.styleSheets[0].cssRules[0].style].every(style => {
                        style[name] = 'normal';
                        style[name] = 'bogus';
                        style[name] = undefined;
                        const expected = name === 'placeContent' ? 'undefined' : 'normal';
                        if (style[name] !== expected || Object.hasOwn(style, name)) return false;
                        style[name] = null;
                        return style[name] === '';
                    });
                });
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task GapAliasesAndTextVariablesInvalidateWarmComputedValues()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=box style='font-size:20px;--g:2em;row-gap:var(--g);column-gap:5%;place-content:baseline'></div>");
        (await page.EvaluateAsync<string>("""
            var box = document.getElementById('box'), computed = getComputedStyle(box);
            computed.gap + '|' + computed.placeContent
            """)).Should().Be("2em 5%|baseline");
        (await page.EvaluateAsync<string>("""
            box.style.setProperty('--g','calc(-2px)');
            computed.gridGap + '|' + computed.rowGap + '|' + computed.columnGap
            """)).Should().Be("calc(-2px) 5%|calc(-2px)|5%");
        (await page.EvaluateAsync<string>("""
            box.style.gridRowGap='4px';
            box.style.justifyContent='space-evenly';
            computed.gap + '|' + computed.placeContent
            """)).Should().Be("4px 5%|baseline");
        page.Errors.Should().BeEmpty();
    }
}
