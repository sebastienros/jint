#nullable enable
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Jint.WebApi;

namespace Jint.Tests.Browser.Fixtures;

[NonParallelizable]
public class ScalarFixtureTests
{
    [Test]
    public async Task ScalarRendersTheCapturedOpenApiOperations()
    {
        var consoleFailures = new ConcurrentQueue<string>();
        await using var course = await FixtureCourse.OpenAsync(
            "scalar-openapi",
            server => FixtureRoutes.Scalar(server),
            options =>
            {
                options.MaxTaskDuration = TimeSpan.FromSeconds(30);
                options.ConfigureEngine(engine => engine.WebApi.Console.Sink =
                    new ScalarConsole(engine.WebApi.Console.Sink, consoleFailures));
            });

        await course.UntilAsync(
            "String(Array.from(document.querySelectorAll('[role=navigation] button')).some(button => button.textContent.includes('GetEndpoint')))",
            "true");
        await ClickButtonAsync(course, "Open Group GetEndpoint");
        await ClickButtonAsync(course, "/api/content/{contentItemId} HTTP Method: GET");
        await course.UntilAsync("String(document.body.textContent.includes('/api/content/{contentItemId}'))", "true");
        await ClickButtonAsync(course, "Test Request (get /api/content/{contentItemId})");
        var client = await course.Page.AccessibilitySnapshotAsync();
        client.Should().Contain("dialog \"API Client\"").And.Contain("Send get request to ").And.Contain("/api/content/{contentItemId}");
        course.Server.Received.Should().ContainSingle(request => request.Path == "/swagger/v1/swagger.json");
        course.Page.Requests.Should().OnlyContain(request => request.Status == 200 && !request.Failed);
        course.ShouldHaveReportedNothing();
        consoleFailures.Should().BeEmpty("framework-caught failures must not pass as a working page");
    }

    private static async Task ClickButtonAsync(FixtureCourse course, string name)
    {
        var snapshot = await course.Page.AccessibilitySnapshotAsync(includeReferences: true);
        var line = snapshot.Split('\n')
            .Where(entry => entry.Contains("button \"" + name + "\"", StringComparison.Ordinal))
            .Should().ContainSingle("the accessible button must be unique in:\n" + snapshot).Which;
        var reference = Regex.Match(line, @"\[ref=(\d+)\]");
        reference.Success.Should().BeTrue();
        await course.ClickAsync("ref=" + reference.Groups[1].Value);
    }

    private sealed class ScalarConsole(ConsoleSink? inner, ConcurrentQueue<string> failures) : ConsoleSink
    {
        public override void Write(ConsoleLogLevel level, string message)
        {
            Capture(level, message);
            inner?.Write(level, message);
        }

        public override void Write(in ConsoleRecord record)
        {
            if (record.Message is { } message) Capture(record.Level, message);
            inner?.Write(in record);
        }

        private void Capture(ConsoleLogLevel level, string message)
        {
            if (level is ConsoleLogLevel.Warn or ConsoleLogLevel.Error && failures.Count < 20)
                failures.Enqueue(message[..Math.Min(message.Length, 2000)]);
        }
    }
}
