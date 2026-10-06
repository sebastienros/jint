namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

public sealed class NumberEditingTests
{
    [Test]
    public async Task IncompleteNumberPresentationIsBadInputAndCommitsChangeOnEnterOnlyOnce()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id=n type=number required><input id=other>
            <script>
              window.log = [];
              n.addEventListener('beforeinput', e => log.push('before:' + e.data));
              n.addEventListener('input', () => log.push('input:' + n.value + ':' + n.validity.badInput));
              n.addEventListener('change', () => log.push('change'));
              n.focus();
            </script>
            """);
        await BrowserTestAccess.TypeAsync(page, "-");
        (await page.EvaluateAsync<string>("[n.value, n.validity.badInput, n.validity.valueMissing, n.selectionStart].join('|')"))
            .Should().Be("|true|true|");
        await BrowserTestAccess.DispatchKeyAsync(page, "Enter");
        await page.EvaluateAsync("other.focus()");
        (await page.EvaluateAsync<string>("log.join('|')")).Should().Be("before:-|input::true|change");
    }

    [Test]
    public async Task CanceledBeforeInputDoesNotInstallIncompleteNumberPresentation()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id=n type=number><input id=other>
            <script>
              window.log = [];
              n.addEventListener('beforeinput', e => { log.push('before'); e.preventDefault(); });
              n.addEventListener('input', () => log.push('input'));
              n.addEventListener('change', () => log.push('change'));
              n.focus();
            </script>
            """);
        await BrowserTestAccess.TypeAsync(page, "-");
        await page.EvaluateAsync("other.focus()");
        (await page.EvaluateAsync<string>("[n.value, n.validity.badInput, log.join(',')].join('|')"))
            .Should().Be("|false|before");
    }

    [TestCase("n.value = '41'", "number|412|1")]
    [TestCase("n.type = 'text'; n.value = 'xy'; n.setSelectionRange(1, 1)", "text|x2y|1")]
    [TestCase("n.type = 'checkbox'; n.checked = true", "checkbox|on|0")]
    [TestCase("n.readOnly = true", "number||0")]
    [TestCase("n.disabled = true", "number||0")]
    public async Task AcceptedNumberEditReReadsTheControlAfterBeforeInput(string mutation, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id=n type=number>");
        await page.EvaluateAsync(
            $$"""
            window.inputs = 0;
            n.addEventListener('input', () => inputs++);
            n.addEventListener('beforeinput', () => { {{mutation}} }, {once: true});
            n.focus();
            """);
        await BrowserTestAccess.TypeAsync(page, "2");
        (await page.EvaluateAsync<string>("[n.type, n.value, inputs].join('|')")).Should().Be(expected);
    }

    [Test]
    public async Task NumberDeletionUsesTheValueAndCaretInstalledByBeforeInput()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id=n type=number value=8><script>n.focus()</script>");
        await BrowserTestAccess.DispatchKeyAsync(page, "End");
        await page.EvaluateAsync(
            """
            window.inputs = 0;
            n.addEventListener('input', () => inputs++);
            n.addEventListener('beforeinput', () => { n.value = '432'; }, {once: true});
            """);
        await BrowserTestAccess.DispatchKeyAsync(page, "Backspace");
        (await page.EvaluateAsync<string>("[n.value, inputs].join('|')")).Should().Be("43|1");
    }
}
