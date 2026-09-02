using System.Text.Json;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// <c>Debugger.searchInContent</c>: the command behind a front end's "search in this file", answered over the
/// source text the parse retained.
/// </summary>
[NonParallelizable]
public class DebuggerSearchTests
{
    private const string Source = """
        function add(a, b) {
            var sum = a + b;
            return sum;
        }
        var total = add(2, 3);
        var Sum = "capital";
        """;

    [Test]
    public async Task EveryLineHoldingTheQueryIsAnswered()
    {
        await using var session = await CreateAsync();
        var matches = await SearchAsync(session, """{"query":"sum"}""");

        // One match per line rather than per occurrence, which is the shape V8 answers and the shape the
        // front end draws. Case-insensitive by default, so the capitalized declaration is in.
        matches.Should().HaveCount(3);
        matches[0].GetProperty("lineNumber").GetInt32().Should().Be(1, "line numbers are the protocol's, counting from zero");
        matches[0].GetProperty("lineContent").GetString().Should().Be("    var sum = a + b;");
        matches[^1].GetProperty("lineContent").GetString().Should().Be("""var Sum = "capital";""");
    }

    [Test]
    public async Task CaseSensitiveSearchLeavesTheOtherCasingAlone()
    {
        await using var session = await CreateAsync();
        var matches = await SearchAsync(session, """{"query":"Sum","caseSensitive":true}""");

        matches.Should().HaveCount(1);
        matches[0].GetProperty("lineNumber").GetInt32().Should().Be(5);
    }

    [Test]
    public async Task ARegularExpressionSearchesForAPattern()
    {
        await using var session = await CreateAsync();
        var matches = await SearchAsync(session, """{"query":"^var\\s+\\w+","isRegex":true,"caseSensitive":true}""");

        matches.Should().HaveCount(2, "two lines start with a declaration; the indented one does not");
        matches[0].GetProperty("lineNumber").GetInt32().Should().Be(4);
    }

    [Test]
    public async Task AQueryThatMatchesNothingAnswersAnEmptyList()
    {
        await using var session = await CreateAsync();
        (await SearchAsync(session, """{"query":"multiply"}""")).Should().BeEmpty();
    }

    [Test]
    public async Task AnUnparseablePatternIsRefusedRatherThanTreatedAsText()
    {
        await using var session = await CreateAsync();
        var scriptId = await ScriptIdAsync(session);

        var error = await session.ErrorAsync(
            "Debugger.searchInContent",
            $$"""{"scriptId":"{{scriptId}}","query":"(unclosed","isRegex":true}""");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Contain("regular expression");
    }

    [Test]
    public async Task AScriptTheRegistryDoesNotHoldIsRefused()
    {
        await using var session = await CreateAsync();

        var error = await session.ErrorAsync("Debugger.searchInContent", """{"scriptId":"9.99","query":"sum"}""");
        error.GetProperty("message").GetString().Should().Be("No script with given id");
    }

    private static async Task<AttachedSession> CreateAsync()
    {
        var session = await AttachedSession.CreateAsync().ConfigureAwait(false);
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js")).ConfigureAwait(false);
        await session.EnableDebuggerAsync().ConfigureAwait(false);
        return session;
    }

    private static async Task<string> ScriptIdAsync(AttachedSession session)
        => (await session.EventAsync("Debugger.scriptParsed").ConfigureAwait(false)).GetProperty("scriptId").GetString()!;

    private static async Task<JsonElement[]> SearchAsync(AttachedSession session, string parameters)
    {
        var scriptId = await ScriptIdAsync(session).ConfigureAwait(false);
        var request = $$"""{"scriptId":"{{scriptId}}",{{parameters.AsSpan(1)}}""";

        var result = await session.ResultAsync("Debugger.searchInContent", request).ConfigureAwait(false);
        return [.. result.GetProperty("result").EnumerateArray()];
    }
}
