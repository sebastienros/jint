#nullable enable

namespace Jint.Tests.Browser.Views;

public sealed class ChildComputedStyleRealmTests
{
    [Test]
    public async Task TheChildOperationKeepsItsOwnCallableDeclarationAndArgumentErrorBrands()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <iframe id=f srcdoc="<div id=t style='opacity:.25'>text</div><script>
            const element=document.getElementById('t');
            window.computed=getComputedStyle(element);
            let badArgument;
            try { getComputedStyle({}); } catch(error) { badArgument=error instanceof TypeError; }
            window.result=[computed.opacity,computed instanceof CSSStyleDeclaration,
                getComputedStyle instanceof Function,badArgument].join('|');
            </script>"></iframe>
            """);
        (await page.EvaluateAsync<string>("f.contentWindow.result")).Should().Be("0.25|true|true|true");
        (await page.EvaluateAsync<bool>("""
            (() => {
                const child=f.contentWindow, element=f.contentDocument.getElementById('t');
                const own=child.getComputedStyle(element), parent=getComputedStyle(element);
                const descriptor=Object.getOwnPropertyDescriptor(child.Window.prototype,'getComputedStyle');
                return own instanceof child.CSSStyleDeclaration && !(own instanceof CSSStyleDeclaration) &&
                    parent instanceof CSSStyleDeclaration && !(parent instanceof child.CSSStyleDeclaration) &&
                    descriptor.enumerable && descriptor.configurable && descriptor.writable &&
                    child.getComputedStyle.length===1 && child.getComputedStyle===child.getComputedStyle &&
                    Object.getPrototypeOf(own)===Object.getPrototypeOf(child.computed);
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }
}
