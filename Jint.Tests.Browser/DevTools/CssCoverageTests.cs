using System.Text.Json;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// The <c>CSS</c> domain's rule-usage coverage over a real document, driven the way a coverage client
/// drives it.
/// </summary>
/// <remarks>
/// <para>
/// Every assertion here is made through the wire, because the whole feature is a wire contract: a client
/// hears a <c>styleSheetAdded</c>, reads that sheet's text, is handed offsets into it, and slices. So the
/// checks are the slices — the substring a reported range names — rather than an internal set of rules,
/// which is what would still pass if the offsets were nonsense.
/// </para>
/// <para>
/// The pages are deliberately tiny and deliberately have rules that must never be reported: one nothing
/// matches, and one inside an <c>@media</c> block that cannot hold at any viewport.
/// </para>
/// </remarks>
[NonParallelizable]
public class CssCoverageTests
{
    private const string Styled =
        """
        <html><head><style>
        .used { color: rgb(1, 2, 3) }
        .unused { color: rgb(4, 5, 6) }
        @media (min-width: 10px) { .conditional { color: rgb(7, 8, 9) } .conditionalunused { color: rgb(10, 11, 12) } }
        @media (min-width: 99999px) { .impossible { color: rgb(13, 14, 15) } }
        @supports (color: red) { .supported { color: rgb(16, 17, 18) } }
        </style></head>
        <body><p id="box" class="used conditional supported">text</p></body></html>
        """;

    [Test]
    public async Task ARuleThatMatchedIsReportedAndOneThatNeverDidIsNot()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session, Styled);

        await session.ResultAsync("CSS.enable", null, attachment);
        await session.ResultAsync("CSS.startRuleUsageTracking", null, attachment);

        var usage = (await session.ResultAsync("CSS.stopRuleUsageTracking", null, attachment))
            .GetProperty("ruleUsage");

        var covered = await SlicesAsync(session, attachment, usage);

        covered.Should().Contain(slice => slice.Contains(".used", StringComparison.Ordinal));
        covered.Should().NotContain(slice => slice.Contains(".unused", StringComparison.Ordinal),
            "no element carries that class, so nothing ever matched the rule");
        covered.Should().NotContain(slice => slice.Contains(".impossible", StringComparison.Ordinal),
            "a rule inside a media block that cannot hold is never part of the cascade at all");

        foreach (var entry in usage.EnumerateArray())
        {
            entry.GetProperty("used").GetBoolean().Should().BeTrue("every reported rule is one that matched");
            entry.GetProperty("endOffset").GetDouble().Should()
                .BeGreaterThan(entry.GetProperty("startOffset").GetDouble());
        }
    }

    [Test]
    public async Task EveryReportedRangeSlicesTheTextGetStyleSheetTextAnswers()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session, Styled);

        await session.ResultAsync("CSS.enable", null, attachment);

        var header = (await session.EventAsync("CSS.styleSheetAdded", 0, attachment)).GetProperty("header");
        var styleSheetId = header.GetProperty("styleSheetId").GetString()!;

        header.GetProperty("sourceURL").GetString().Should().NotBeNullOrEmpty(
            "Puppeteer and Playwright both drop a header whose sourceURL is empty, so an inline sheet reported "
            + "with none would be invisible to the clients this exists for");
        header.GetProperty("isInline").GetBoolean().Should().BeTrue();
        header.GetProperty("origin").GetString().Should().Be("regular");
        header.GetProperty("frameId").GetString().Should().NotBeNullOrEmpty();

        var text = (await session.ResultAsync(
            "CSS.getStyleSheetText",
            $$"""{"styleSheetId":"{{styleSheetId}}"}""",
            attachment)).GetProperty("text").GetString()!;

        header.GetProperty("length").GetInt32().Should().Be(text.Length);

        await session.ResultAsync("CSS.startRuleUsageTracking", null, attachment);
        var usage = (await session.ResultAsync("CSS.stopRuleUsageTracking", null, attachment))
            .GetProperty("ruleUsage");

        usage.GetArrayLength().Should().BeGreaterThan(0);

        foreach (var entry in usage.EnumerateArray())
        {
            entry.GetProperty("styleSheetId").GetString().Should().Be(styleSheetId);

            var start = entry.GetProperty("startOffset").GetInt32();
            var end = entry.GetProperty("endOffset").GetInt32();

            end.Should().BeLessThanOrEqualTo(text.Length, "an offset that does not index this text is unusable");
            text[start..end].Should().EndWith("}", "a range runs from the selector to the closing brace");
        }
    }

    [Test]
    public async Task TwoDeltasReportEachRuleOnceAndTheSecondOnlyWhatBecameUsed()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session, Styled);

        await session.ResultAsync("CSS.enable", null, attachment);
        await session.ResultAsync("CSS.startRuleUsageTracking", null, attachment);

        var first = await session.ResultAsync("CSS.takeCoverageDelta", null, attachment);
        var firstSlices = await SlicesAsync(session, attachment, first.GetProperty("coverage"));

        firstSlices.Should().Contain(slice => slice.Contains(".used", StringComparison.Ordinal));
        firstSlices.Should().NotContain(slice => slice.Contains(".unused", StringComparison.Ordinal));
        first.GetProperty("timestamp").GetDouble().Should().BeGreaterThan(0);

        // Nothing has changed, so nothing became used: a rule that matched in one query and not in the
        // next is used exactly once.
        var empty = await session.ResultAsync("CSS.takeCoverageDelta", null, attachment);
        empty.GetProperty("coverage").GetArrayLength().Should().Be(0);

        // Now make the unused rule match, and compute a cascade over it the way a page does.
        await session.EvaluateAsync(
            "document.getElementById('box').className = 'unused'; getComputedStyle(document.getElementById('box')).color",
            attachment);

        var second = await session.ResultAsync("CSS.takeCoverageDelta", null, attachment);
        var secondSlices = await SlicesAsync(session, attachment, second.GetProperty("coverage"));

        secondSlices.Should().Contain(slice => slice.Contains(".unused", StringComparison.Ordinal),
            "the cascade the page just computed matched it, which is what used means");
        secondSlices.Should().NotContain(slice => slice.Contains(".used", StringComparison.Ordinal),
            "a delta is what became used since the last one");
    }

    [Test]
    public async Task ARuleInsideAConditionalGroupIsCountedOnItsOwn()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session, Styled);

        await session.ResultAsync("CSS.enable", null, attachment);
        await session.ResultAsync("CSS.startRuleUsageTracking", null, attachment);

        var usage = (await session.ResultAsync("CSS.stopRuleUsageTracking", null, attachment))
            .GetProperty("ruleUsage");

        var text = await TextAsync(session, attachment, usage[0].GetProperty("styleSheetId").GetString()!);
        var covered = Slices(text, usage);

        covered.Should().Contain(slice => slice.StartsWith(".conditional ", StringComparison.Ordinal),
            "a rule inside a media block whose condition holds is matched individually and reported on its own");
        covered.Should().NotContain(slice => slice.StartsWith("@media", StringComparison.Ordinal),
            "the group itself is not a style rule and matches nothing");
        covered.Should().Contain(slice => slice.StartsWith(".supported ", StringComparison.Ordinal),
            "and so is one inside an @supports whose condition holds");
        covered.Should().NotContain(slice => slice.Contains(".conditionalunused", StringComparison.Ordinal),
            "its sibling in the same block matches nothing");

        // The nested rule's range sits inside its group's, which is what makes a client's slice legible.
        var conditional = covered.Single(slice => slice.StartsWith(".conditional ", StringComparison.Ordinal));
        var group = text.IndexOf("@media (min-width: 10px)", StringComparison.Ordinal);
        text.IndexOf(conditional, StringComparison.Ordinal).Should().BeGreaterThan(group);
    }

    [Test]
    public async Task ASheetAddedDuringTheWindowIsAnnouncedAndItsRulesAreCounted()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session, Styled);

        await session.ResultAsync("CSS.enable", null, attachment);
        await session.ResultAsync("CSS.startRuleUsageTracking", null, attachment);
        await session.ResultAsync("CSS.takeCoverageDelta", null, attachment);

        var announced = session.EventsOf("CSS.styleSheetAdded", attachment).Count;

        await session.EvaluateAsync(
            """
            var sheet = document.createElement('style');
            sheet.textContent = '#box { padding: 1px } .neverthere { padding: 2px }';
            document.head.appendChild(sheet);
            getComputedStyle(document.getElementById('box')).padding;
            """,
            attachment);

        var delta = await session.ResultAsync("CSS.takeCoverageDelta", null, attachment);
        var coverage = delta.GetProperty("coverage");

        session.EventsOf("CSS.styleSheetAdded", attachment).Count.Should().BeGreaterThan(announced,
            "the sheet the page just inserted has to be announced before a client is handed an offset into it");

        var slices = await SlicesAsync(session, attachment, coverage);
        slices.Should().Contain(slice => slice.StartsWith("#box ", StringComparison.Ordinal));
        slices.Should().NotContain(slice => slice.Contains(".neverthere", StringComparison.Ordinal));
    }

    [Test]
    public async Task CoverageIsRefusedUntilAWindowIsOpen()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session, Styled);

        await session.ResultAsync("CSS.enable", null, attachment);

        foreach (var method in (string[]) ["CSS.takeCoverageDelta", "CSS.stopRuleUsageTracking"])
        {
            var error = await session.ErrorAsync(method, null, attachment);
            error.GetProperty("code").GetInt32().Should().Be(-32000);
            error.GetProperty("message").GetString().Should().Contain("not enabled");
        }

        var unknown = await session.ErrorAsync("CSS.getStyleSheetText", """{"styleSheetId":"nope"}""", attachment);
        unknown.GetProperty("code").GetInt32().Should().Be(-32000);
    }

    [Test]
    public async Task StoppingTwiceIsRefusedBecauseTheWindowIsClosed()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session, Styled);

        await session.ResultAsync("CSS.enable", null, attachment);
        await session.ResultAsync("CSS.startRuleUsageTracking", null, attachment);
        await session.ResultAsync("CSS.stopRuleUsageTracking", null, attachment);

        var error = await session.ErrorAsync("CSS.stopRuleUsageTracking", null, attachment);
        error.GetProperty("code").GetInt32().Should().Be(-32000);
    }

    /// <summary>The substrings a coverage report names, read out of the sheet's own text.</summary>
    private static async Task<string[]> SlicesAsync(PageSession session, string attachment, JsonElement usage)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        var slices = new List<string>();

        foreach (var entry in usage.EnumerateArray())
        {
            var id = entry.GetProperty("styleSheetId").GetString()!;
            if (!texts.TryGetValue(id, out var text))
            {
                text = await TextAsync(session, attachment, id).ConfigureAwait(false);
                texts[id] = text;
            }

            slices.Add(text[entry.GetProperty("startOffset").GetInt32()..entry.GetProperty("endOffset").GetInt32()]);
        }

        return [.. slices];
    }

    private static string[] Slices(string text, JsonElement usage)
    {
        var slices = new List<string>();
        foreach (var entry in usage.EnumerateArray())
        {
            slices.Add(text[entry.GetProperty("startOffset").GetInt32()..entry.GetProperty("endOffset").GetInt32()]);
        }

        return [.. slices];
    }

    private static async Task<string> TextAsync(PageSession session, string attachment, string styleSheetId)
        => (await session.ResultAsync(
            "CSS.getStyleSheetText",
            $$"""{"styleSheetId":"{{styleSheetId}}"}""",
            attachment).ConfigureAwait(false)).GetProperty("text").GetString()!;

    private static async Task<string> OpenAsync(PageSession session, string html)
    {
        var attachment = await session.OpenPageAsync().ConfigureAwait(false);
        await session.EnablePageAsync(attachment).ConfigureAwait(false);

        var tree = await session.ResultAsync("Page.getFrameTree", null, attachment).ConfigureAwait(false);
        var frameId = tree.GetProperty("frameTree").GetProperty("frame").GetProperty("id").GetString()!;

        var payload = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["frameId"] = frameId,
            ["html"] = html,
        });

        await session.ResultAsync("Page.setDocumentContent", payload, attachment).ConfigureAwait(false);
        return attachment;
    }
}
