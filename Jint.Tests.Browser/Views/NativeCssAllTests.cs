namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssAllTests
{
    [Test]
    public async Task AllResetsSpecifiedValuesWithoutResettingDirectionOrVariables()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=p style='color:red'><div id=t style='display:flex;direction:rtl;--x:blue'></div></div>");
        (await page.EvaluateAsync<string>("""
            (() => {
              const t=document.getElementById('t'), s=t.style;
              s.all='unset';
              const c=getComputedStyle(t);
              if(c.all!=='' || c.getPropertyValue('all')!=='') throw new Error('Computed all must be empty');
              return [s.all,s.getPropertyValue('all'),c.display,c.color,c.direction,c.getPropertyValue('--x')].join('|');
            })()
            """)).Should().Be("unset|unset|inline|rgb(255, 0, 0)|rtl|blue");
        (await page.EvaluateAsync<bool>("""
            (() => {
              const s=document.getElementById('t').style;
              s.all='red';
              if(s.all!=='unset') return false;
              s.setProperty('all','initial','important');
              if(s.getPropertyPriority('display')!=='important') return false;
              if(!CSS.supports('all','unset') || CSS.supports('all','blue')) return false;
              s.all=null;
              return s.all==='' && s.direction==='rtl' && s.getPropertyValue('--x')==='blue';
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task PendingAllSubstitutionAndInvalidComputedValuesUseTheResetMembership()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <style>
              #p { color:red }
              #t { --reset:inherit; all:var(--reset); direction:rtl }
            </style>
            <div id=p><span id=t></span></div>
            """);
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('t')).color")).Should().Be("rgb(255, 0, 0)");
        await page.EvaluateAsync("document.getElementById('t').style.setProperty('--reset','red')");
        (await page.EvaluateAsync<string>("""
            (() => {const s=getComputedStyle(document.getElementById('t'));return [s.display,s.color,s.direction].join('|')})()
            """)).Should().Be("inline|rgb(255, 0, 0)|rtl");
        page.Errors.Should().BeEmpty();
    }
}
