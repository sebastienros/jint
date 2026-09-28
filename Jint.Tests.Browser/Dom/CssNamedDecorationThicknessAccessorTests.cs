#nullable enable

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

/// <summary>CSSOM's named thickness accessor shares the actual native declaration property.</summary>
/// <remarks>https://drafts.csswg.org/cssom/#the-cssstyleproperties-interface</remarks>
public sealed class CssNamedDecorationThicknessAccessorTests
{
    [Test]
    public async Task NamedAndGenericWritesReachInlineRuleAndLiveComputedDeclarations()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#box { }</style><span id=box style='font-size:20px'>text</span>");
        await page.EvaluateAsync("""
            var box=document.getElementById('box'), inline=box.style;
            var rule=document.styleSheets[0].cssRules[0].style, computed=getComputedStyle(box);
            var prototype=CSSStyleDeclaration.prototype;
            var descriptor=Object.getOwnPropertyDescriptor(prototype,'textDecorationThickness');
            rule.textDecorationThickness='2em';
            """);
        (await page.EvaluateAsync<string>("rule.getPropertyValue('text-decoration-thickness') + '|' + computed.textDecorationThickness"))
            .Should().Be("2em|2em");
        await page.EvaluateAsync("rule.setProperty('text-decoration-thickness','3px')");
        (await page.EvaluateAsync<string>("rule.textDecorationThickness + '|' + computed.textDecorationThickness"))
            .Should().Be("3px|3px");
        await page.EvaluateAsync("inline.textDecorationThickness='2em'");
        (await page.EvaluateAsync<string>("inline.getPropertyValue('text-decoration-thickness') + '|' + computed.getPropertyValue('text-decoration-thickness')"))
            .Should().Be("2em|2em");
        await page.EvaluateAsync("inline.setProperty('text-decoration-thickness','4px')");
        (await page.EvaluateAsync<string>("inline.textDecorationThickness + '|' + computed.textDecorationThickness"))
            .Should().Be("4px|4px");
        (await page.EvaluateAsync<bool>("""
            descriptor.enumerable && descriptor.configurable && descriptor.get.length===0 && descriptor.set.length===1 &&
            [inline,rule,computed].every(style => style instanceof CSSStyleDeclaration &&
                Object.getPrototypeOf(style)===prototype && !Object.prototype.hasOwnProperty.call(style,'textDecorationThickness')) &&
            box.style===inline && document.styleSheets[0].cssRules[0].style===rule &&
            Object.getOwnPropertyDescriptor(prototype,'textDecorationThickness').get===descriptor.get &&
            Object.getOwnPropertyDescriptor(prototype,'textDecorationThickness').set===descriptor.set
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task NullRemovesAndNamedWritesRetainUnvalidatedText()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#box { }</style><span id=box>text</span>");
        (await page.EvaluateAsync<bool>("""
            (() => {
                const styles=[document.getElementById('box').style,document.styleSheets[0].cssRules[0].style];
                return styles.every(style => {
                    style.textDecorationThickness='2px';
                    style.textDecorationThickness=undefined;
                    if (style.textDecorationThickness!=='undefined' || style.getPropertyValue('text-decoration-thickness')!=='undefined') return false;
                    style.textDecorationThickness='bogus';
                    if (style.textDecorationThickness!=='bogus') return false;
                    style.textDecorationThickness=null;
                    return style.textDecorationThickness==='' && style.getPropertyValue('text-decoration-thickness')==='';
                });
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ComputedThicknessRefusesWritesAndReceiverChecksPrecedeConversion()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<span id=box style='text-decoration-thickness:2px'>text</span>");
        (await page.EvaluateAsync<bool>("""
            (() => {
                const box=document.getElementById('box'), computed=getComputedStyle(box);
                const descriptor=Object.getOwnPropertyDescriptor(CSSStyleDeclaration.prototype,'textDecorationThickness');
                for (const input of ['3px',null,undefined,'bogus']) {
                    let error;
                    try { computed.textDecorationThickness=input; } catch (caught) { error=caught; }
                    if (!(error instanceof DOMException) || error.name!=='NoModificationAllowedError') return false;
                    if (computed.textDecorationThickness!=='2px' || computed.getPropertyValue('text-decoration-thickness')!=='2px') return false;
                }
                return [{},box,document].every(receiver => {
                    let conversions=0, getterError, setterError;
                    try { descriptor.get.call(receiver); } catch (caught) { getterError=caught; }
                    try { descriptor.set.call(receiver,{toString(){conversions++; return '3px'}}); }
                    catch (caught) { setterError=caught; }
                    return getterError instanceof TypeError && setterError instanceof TypeError && conversions===0;
                });
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }
}
