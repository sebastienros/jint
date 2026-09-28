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
    public async Task TypeChangesInvalidateSelectorStylesWithoutColorConversion(
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
        result.Should().Be("false:red|true:green");
        page.Errors.Should().BeEmpty();
    }
}
