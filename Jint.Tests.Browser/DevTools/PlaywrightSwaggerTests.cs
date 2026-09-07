using System.Collections.Concurrent;
using System.Diagnostics;
using Jint.Browser;
using Jint.Tests.Browser.Navigation;
using Jint.WebApi;
using Microsoft.Playwright;

namespace Jint.Tests.Browser.DevTools;

public partial class PlaywrightCourseTests
{
    [Test]
    public async Task PlaywrightRendersSwaggerOperationsAndExecutesARequest()
    {
        var diagnosticStacks = new ConcurrentQueue<string>();
        var options = new BrowserOptions { MaxTaskDuration = TimeSpan.FromSeconds(30) }
            .ConfigureEngine(options => options.WebApi.Diagnostics.Sink =
                new SwaggerDiagnostics(options.WebApi.Diagnostics.Sink, diagnosticStacks));
        await using var lane = await ClientLane.OpenAsync(
            server => server.Map("/api/content/does-not-exist",
                _ => new LoopbackResponse { Status = 404, Reason = "Not Found", Body = """{"message":"Content item not found"}""" }
                    .With("Content-Type", "application/json")),
            options);
        var page = await lane.Context.NewPageAsync();
        var errors = new ConcurrentQueue<string>();
        page.PageError += (_, error) => errors.Enqueue("Page: " + error);
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                errors.Enqueue("Console: " + message.Text);
            }
        };
        page.RequestFailed += (_, request) => errors.Enqueue("Request: " + request.Url + " " + request.Failure);
        var started = Stopwatch.StartNew();
        var allocatedBefore = GC.GetTotalAllocatedBytes();
        var completed = false;
        try
        {
            await page.GotoAsync(lane.Url("swagger-ui"));
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            (await page.TitleAsync()).Should().Be("OrchardCore OpenAPI Documentation");
            lane.Server.Received.Should().ContainSingle(request => request.Path == "/swagger-ui/schema.json");
            var operation = page.Locator("#operations-GetEndpoint-ApiGetContentItem");
            await operation.Locator(".opblock-summary").ClickAsync();
            await operation.Locator("button.try-out__btn").ClickAsync();
            await operation.Locator("tr[data-param-name='contentItemId'] input").FillAsync("does-not-exist");
            var response = await page.RunAndWaitForResponseAsync(
                () => operation.Locator("button.execute").ClickAsync(),
                response => response.Url.EndsWith("/api/content/does-not-exist", StringComparison.Ordinal));
            response.Status.Should().Be(404);
            (await response.TextAsync()).Should().Contain("Content item not found");
            await operation.Locator(".live-responses-table").WaitForAsync();
            (await operation.Locator(".live-responses-table").TextContentAsync()).Should().Contain("404");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            errors.Should().BeEmpty();
            foreach (var hostPage in lane.Pages.Contexts.SelectMany(context => context.Pages))
            {
                hostPage.Errors.Should().BeEmpty();
            }
            completed = true;
        }
        finally
        {
            using var process = Process.GetCurrentProcess();
            TestContext.Out.WriteLine(
                $"Swagger workflow: {started.Elapsed}; process working set {process.WorkingSet64} bytes; "
                + $"process allocations {GC.GetTotalAllocatedBytes() - allocatedBefore} bytes.");
            if (!completed)
            {
                TestContext.Out.WriteLine("Errors: " + string.Join("\n", errors));
                TestContext.Out.WriteLine("Diagnostic stacks: " + string.Join("\n", diagnosticStacks));
                foreach (var hostPage in lane.Pages.Contexts.SelectMany(context => context.Pages))
                {
                    TestContext.Out.WriteLine("Host errors: " + string.Join("\n", hostPage.Errors));
                }

                TestContext.Out.WriteLine("Requests: " + string.Join("\n", lane.Server.Received.Select(request => request.Path)));
                TestContext.Out.WriteLine(await page.EvaluateAsync<string>(
                    "() => document.querySelector('#operations-GetEndpoint-ApiGetContentItem')?.outerHTML || document.body.innerHTML.slice(-5000)"));
            }
        }

    }

    private sealed class SwaggerDiagnostics(DiagnosticsSink? inner, ConcurrentQueue<string> stacks) : DiagnosticsSink
    {
        public override void Report(DiagnosticEvent report)
        {
            stacks.Enqueue(report.CallbackSource + ": " + report.Exception?.ToString());
            inner?.Report(report);
        }
    }
}
