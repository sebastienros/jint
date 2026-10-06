namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssHostTests
{
    [Test]
    public async Task HostStylesRespectEncapsulationAndLiveMutation()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <style>
            :root,:host {color:blue}
            #host {color:green;--normal:outer;--important:outer!important;--rolled:outer!important}
            </style><section id=host class=x style="--inline:outer!important"><b class=light></b></section>
            """);
        (await page.EvaluateAsync<string>("""
            (() => {
              const host=document.getElementById('host'), root=host.attachShadow({mode:'open'});
              root.innerHTML=`<style>
                :host {color:red;--normal:inner;--important:inner!important;--inline:inner!important}
                .x {--leak:bad} :host.x {--leak:bad}
                :host(.x) > span {--child:yes}
                @layer test { :host {--rolled:revert-layer!important} }
              </style><span id=child></span>`;
              const style=getComputedStyle(host), child=getComputedStyle(root.getElementById('child'));
              const values=()=>[style.color,style.getPropertyValue('--normal'),
                style.getPropertyValue('--important'),style.getPropertyValue('--inline'),
                style.getPropertyValue('--leak'),style.getPropertyValue('--rolled'),
                child.getPropertyValue('--child')].join('|');
              if (values()!=='rgb(0, 128, 0)|outer|inner|inner||revert-layer|yes') return values();
              if (root.querySelector(':host')!==null || root.querySelector(':host > span')!==null ||
                  !CSS.supports('selector(:host(.x))')) return 'query scope';
              host.className='';
              if (child.getPropertyValue('--child')!=='') return 'stale class';
              root.innerHTML='';
              return style.getPropertyValue('--important')==='outer' &&
                style.getPropertyValue('--inline')==='outer' ? 'ok' : 'stale sheet';
            })()
            """)).Should().Be("ok");
        page.Errors.Should().BeEmpty();
    }
}
