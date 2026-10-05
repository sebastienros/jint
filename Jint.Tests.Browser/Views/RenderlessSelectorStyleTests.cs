namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class RenderlessSelectorStyleTests
{
    [TestCase("text", "checkbox", ":checked", "checked")]
    [TestCase("text", "checkbox", ":indeterminate", "")]
    [TestCase("text", "radio", ":indeterminate", "")]
    [TestCase("text", "number", ":in-range", "min=0 max=10 value=5")]
    [TestCase("text", "number", ":out-of-range", "min=0 max=10 value=20")]
    [TestCase("button", "text", ":placeholder-shown", "placeholder=hint")]
    [TestCase("button", "text", ":read-write", "")]
    [TestCase("hidden", "text", ":required", "required")]
    public async Task TypeChangesInvalidateSelectorStyles(
        string before, string after, string selector, string attributes)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<style>input{{color:red}} input{selector}{{color:green}}</style>" +
            $"<input id=t type={before} {attributes}>");
        var result = await page.EvaluateAsync<string>($$"""
            (() => {
              const el = document.getElementById('t');
              el.indeterminate = true;
              const style = getComputedStyle(el);
              const before = el.matches('{{selector}}') + ':' + style.color;
              el.type = '{{after}}';
              return before + '|' + el.matches('{{selector}}') + ':' + style.color;
            })()
            """);
        result.Should().Be("false:rgb(255, 0, 0)|true:rgb(0, 128, 0)");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task RetainedStyleReadsRefreshNthIndexesAfterTreeAndFilterChanges()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>span:nth-child(2) { opacity:.5 } span:nth-last-of-type(1) { --last:yes } "
            + "span:nth-child(2 of .x) { --filtered:yes }</style><main><span id=a class=x></span><!--gap-->"
            + "<span id=b class=x></span><span id=c class=x></span></main>");
        await page.EvaluateAsync("var a=document.getElementById('a'), b=document.getElementById('b'), c=document.getElementById('c'); var style=getComputedStyle(b);");
        (await page.EvaluateAsync<string>("style.opacity + ':' + style.getPropertyValue('--filtered')")).Should().Be("0.5:yes");
        await page.EvaluateAsync("b.parentNode.insertBefore(c,a); a.className='y';");
        (await page.EvaluateAsync<string>("style.opacity + ':' + style.getPropertyValue('--filtered') + ':' + style.getPropertyValue('--last')")).Should().Be("1:yes:yes");
        await page.EvaluateAsync("c.remove(); a.className='x';");
        (await page.EvaluateAsync<string>("style.opacity + ':' + style.getPropertyValue('--filtered')")).Should().Be("0.5:yes");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task FilteredNthControlStateRefreshesWithoutAttributeMutation()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>input:nth-child(2 of :valid) { opacity:.5 }</style><form>"
            + "<input id=a required value=yes><input id=b required value=yes><input id=c required value=yes></form>");
        await page.EvaluateAsync("var a=document.getElementById('a'), b=document.getElementById('b'), c=document.getElementById('c'); var bs=getComputedStyle(b), cs=getComputedStyle(c);");
        (await page.EvaluateAsync<string>("bs.opacity + ':' + cs.opacity")).Should().Be("0.5:1");
        await page.EvaluateAsync("a.value='';");
        (await page.EvaluateAsync<string>("bs.opacity + ':' + cs.opacity")).Should().Be("1:0.5");
        await page.EvaluateAsync("a.value='yes';");
        (await page.EvaluateAsync<string>("bs.opacity + ':' + cs.opacity")).Should().Be("0.5:1");
        page.Errors.Should().BeEmpty();
    }
}
