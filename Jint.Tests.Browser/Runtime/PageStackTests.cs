using System.Runtime.InteropServices;
using Jint.Browser;
using Jint.Runtime;

namespace Jint.Tests.Browser.Runtime;

using Browser = global::Jint.Browser.Browser;

public sealed class PageStackTests
{
    // Two mutually recursive calls per level, with siblings and DOM writes on the way back out.
    // This is finite framework work, not unbounded recursion or a tail call.
    private const string Traversal = """
        var root = { child: null, sibling: null };
        for (var i = 0; i < 256; i++) {
            root = { child: root, sibling: null };
            root.child.sibling = { child: null, sibling: null };
        }
        var visited = 0;
        var target = document.getElementById('result');
        function commitMutationEffects(node) {
            recursivelyTraverseMutationEffects(node);
            target.textContent = String(++visited);
        }
        function recursivelyTraverseMutationEffects(parent) {
            var child = parent.child;
            while (child !== null) {
                commitMutationEffects(child);
                child = child.sibling;
            }
        }
        """;

    [TestCase("inline")]
    [TestCase("evaluate")]
    [TestCase("timer")]
    public async Task AFiniteDeepCommitTraversalCompletesOnTheProductionPageThread(string entry)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        var invocation = entry == "inline" ? "commitMutationEffects(root);" : "";
        await page.SetContentAsync("<p id='result'></p><script>" + Traversal + invocation + "</script>");

        if (entry == "evaluate")
        {
            await page.EvaluateAsync("commitMutationEffects(root)");
        }
        else if (entry == "timer")
        {
            await page.EvaluateAsync("setTimeout(() => commitMutationEffects(root), 0)");
            await page.WaitForIdleAsync(TimeSpan.FromSeconds(10));
        }

        (await page.EvaluateAsync<string>("target.textContent")).Should().Be("513");
        page.Errors.Should().BeEmpty();

        // Exercise a warmed engine too, without rebuilding the tree or borrowing the test runner's stack.
        await page.EvaluateAsync("visited = 0; commitMutationEffects(root)");
        (await page.EvaluateAsync<int>("visited")).Should().Be(513);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    [Platform("MacOsX", Reason = "pthread_get_stacksize_np measures the native reservation on macOS.")]
    public async Task TheProductionPageThreadHasMultiMegabyteNativeStackHeadroom()
    {
        await using var browser = new Browser();
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var bytes = await page.RunOnLoopAsync(_ => (ulong) NativeStackSize(NativeThreadSelf()));
        TestContext.Progress.WriteLine($"Page thread native stack reservation: {bytes} bytes");
        bytes.Should().BeGreaterThanOrEqualTo(8UL * 1024 * 1024);

        await page.SetContentAsync("<title>replacement engine</title>");
        (await page.RunOnLoopAsync(_ => (ulong) NativeStackSize(NativeThreadSelf()))).Should().Be(bytes);
    }

    [Test]
    public async Task ExcessiveRecursionStillThrowsAndThePageRemainsUsable()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        (await page.RunOnLoopAsync(engine => engine.Options.Constraints.StackOverflowGuard)).Should().BeTrue();
        await page.EvaluateAsync("function runaway() { return 1 + runaway(); }");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var act = async () => await page.EvaluateAsync("runaway()");
            (await act.Should().ThrowAsync<JavaScriptException>())
                .WithMessage("Maximum call stack size exceeded");

            (await page.EvaluateAsync<bool>(
                """
                (() => {
                    try { runaway(); }
                    catch (e) { return e instanceof RangeError && e.message === 'Maximum call stack size exceeded'; }
                    return false;
                })()
                """)).Should().BeTrue();
            (await page.EvaluateAsync<int>("[1, 2, 3].reduce((sum, value) => sum + value, 0)")).Should().Be(6);
            page.Errors.Should().BeEmpty();
        }
    }

    [Test]
    public async Task AnExplicitRecursionLimitStillRejectsBeforeNativeStackExhaustion()
    {
        var options = new BrowserOptions();
        options.ConfigureEngine(o => o.Constraints.MaxRecursionDepth = 32);
        await using var browser = new Browser(options);
        var page = await browser.NewPageAsync();

        var act = async () => await page.EvaluateAsync(
            "function recurse(n) { return n === 0 ? 0 : 1 + recurse(n - 1); } recurse(256)");
        await act.Should().ThrowAsync<RecursionDepthOverflowException>();

        (await page.EvaluateAsync<int>("recurse(16)")).Should().Be(16);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task SequentialPagesReleaseTheirThreads()
    {
        await using var browser = new Browser();
        for (var iteration = 0; iteration < 32; iteration++)
        {
            var page = await browser.NewPageAsync();
            var thread = await page.RunOnLoopAsync(_ => Thread.CurrentThread);
            (await page.EvaluateAsync<int>("1 + 1")).Should().Be(2);

            await page.CloseAsync();

            // Close signals from the thread's finally; join also waits for the native thread to exit.
            thread.Join(TimeSpan.FromSeconds(10)).Should().BeTrue();
            thread.IsAlive.Should().BeFalse();
            browser.Contexts.SelectMany(context => context.Pages).Should().BeEmpty();
        }
    }

    [DllImport("libSystem", EntryPoint = "pthread_self")]
    private static extern nint NativeThreadSelf();

    [DllImport("libSystem", EntryPoint = "pthread_get_stacksize_np")]
    private static extern nuint NativeStackSize(nint thread);
}
