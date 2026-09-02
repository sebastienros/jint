using System.Text.Json;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// <c>Runtime.getExceptionDetails</c>: what a client asks about an error object it is holding.
/// </summary>
/// <remarks>
/// The front end sends it once per error it renders, which is how it draws the expandable stack under a
/// console message. Nothing here retains the details of an exception past the report that carried them; they
/// are reconstructed from the object the handle names, which is also what makes the command work for an
/// error this server never reported at all.
/// </remarks>
[NonParallelizable]
public class ExceptionDetailsTests
{
    private const string Source = """
        function thrower() { return new Error('boom'); }
        function wrapper() { return thrower(); }
        var caught = wrapper();
        """;

    private static async Task<JsonElement> DetailsOfAsync(AttachedSession session, string expression)
    {
        var handle = await session.HandleAsync(expression);
        var result = await session.ResultAsync(
            "Runtime.getExceptionDetails",
            $$"""{"errorObjectId":{{JsonSerializer.Serialize(handle)}}}""");

        return result.GetProperty("exceptionDetails");
    }

    [Test]
    public async Task AnErrorObjectAnswersItsTextItsLocationAndItsFrames()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.Target.PostAsync(engine => engine.Execute(Source, "app.js"));
        await session.EnableDebuggerAsync();

        var details = await DetailsOfAsync(session, "caught");

        // Chrome answers the error's own "Error: boom" here, not the "Uncaught" it prefixes a thrown one
        // with: nothing was thrown, and the client is asking about a value it is holding.
        details.GetProperty("text").GetString().Should().Be("Error: boom");
        details.GetProperty("exceptionId").GetInt32().Should().BeGreaterThan(0);
        details.GetProperty("exception").GetProperty("subtype").GetString().Should().Be("error");
        details.GetProperty("exception").GetProperty("description").GetString().Should().StartWith("Error: boom");

        var frames = details.GetProperty("stackTrace").GetProperty("callFrames").EnumerateArray().ToArray();
        frames.Select(f => f.GetProperty("functionName").GetString()).Should().Equal("thrower", "wrapper", "");

        // The location is the innermost frame's, in the protocol's zero-based counting, and the frame is
        // matched back to the script so a front end can make the row clickable.
        details.GetProperty("lineNumber").GetInt32().Should().Be(frames[0].GetProperty("lineNumber").GetInt32());
        details.GetProperty("scriptId").GetString().Should().Be(frames[0].GetProperty("scriptId").GetString());
        details.GetProperty("scriptId").GetString().Should().NotBe("0");
        details.GetProperty("url").GetString().Should().Be("app.js");
        frames[0].GetProperty("url").GetString().Should().Be("app.js");
    }

    [Test]
    public async Task AnErrorWithNoFramesStillAnswersItsText()
    {
        await using var session = await AttachedSession.CreateAsync();

        var details = await DetailsOfAsync(session, "Object.defineProperty(new TypeError('nope'), 'stack', { value: '' })");

        details.GetProperty("text").GetString().Should().Be("TypeError: nope");
        details.TryGetProperty("stackTrace", out _).Should().BeFalse("there were no frames to report");
        details.GetProperty("lineNumber").GetInt32().Should().Be(0);
    }

    [Test]
    public async Task AStackAScriptReplacedProducesNoFramesRatherThanWrongOnes()
    {
        await using var session = await AttachedSession.CreateAsync();

        var details = await DetailsOfAsync(
            session,
            "Object.defineProperty(new Error('x'), 'stack', { value: 'not a frame at all' })");

        details.GetProperty("text").GetString().Should().Be("Error: x");
        details.TryGetProperty("stackTrace", out _).Should().BeFalse("a line in no known shape is not guessed at");
    }

    [Test]
    public async Task AHandleThatIsNotAnErrorIsRefusedInChromesWording()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync("({ a: 1 })");
        var error = await session.ErrorAsync(
            "Runtime.getExceptionDetails",
            $$"""{"errorObjectId":{{JsonSerializer.Serialize(handle)}}}""");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("errorObjectId is not a JS error object");
    }

    [Test]
    public async Task AHandleNothingKnowsAboutIsRefused()
    {
        await using var session = await AttachedSession.CreateAsync();

        var error = await session.ErrorAsync("Runtime.getExceptionDetails", """{"errorObjectId":"nope"}""");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
    }
}
