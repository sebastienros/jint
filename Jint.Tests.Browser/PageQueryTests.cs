using Jint.Browser;
using Jint.HtmlParser;
using Jint.Runtime;
using Jint.Tests.Browser.Navigation;
using NativeBrowser = Jint.Browser.Browser;

namespace Jint.Tests.Browser;

public sealed class PageQueryTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Test]
    public async Task NativeQueriesShareVisibilitySemanticsAndSnapshotBatchedText()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <style>.concealed{visibility:hidden}.concealed .escape{visibility:visible}</style>
            <div class=row> A <span hidden>hidden</span><span>B</span></div>
            <div hidden><div class=row>hidden ancestor</div></div>
            <div style="display:none"><div class=row>display ancestor</div></div>
            <div class="row concealed">invisible</div>
            <div class=concealed><div class="row escape">escape</div></div>
            <div class=row style="opacity:0" aria-hidden=true>transparent</div>
            <div class=row style="display:contents">contents</div>
            """);

        (await page.CountAsync(".row")).Should().Be(7);
        (await page.CountAsync(".row", visibleOnly: true)).Should().Be(3);
        (await page.EvaluateAsync<int>("Array.from(document.querySelectorAll('.row')).filter(e=>e.checkVisibility({visibilityProperty:true})).length"))
            .Should().Be(3);
        (await page.IsVisibleAsync(".row")).Should().BeTrue();
        (await page.IsVisibleAsync(".row", 1)).Should().BeFalse();
        (await page.IsVisibleAsync(".row", -1)).Should().BeFalse();
        (await page.IsVisibleAsync("#missing")).Should().BeFalse();
        (await page.InnerTextAsync("#missing")).Should().BeNull();
        (await page.InnerTextAsync(".row")).Should().Be("A B");
        var snapshot = await page.TextsAsync(".row", visibleOnly: true);
        snapshot.Should().Equal(" A hiddenB", "escape", "transparent");
        await page.EvaluateAsync("document.querySelector('.row').textContent='changed'");
        snapshot.Should().Equal(" A hiddenB", "escape", "transparent");
        (await page.TextsAsync(".row")).Should().HaveCount(7);
    }

    [Test]
    public async Task QueriesAndWaitsAcceptLiteralSelectorAndTextData()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        const string text = "quotes ' \" slash \\\n\u2028\u2029 \U0001F34E";
        const string selector = "[data-key='a\"b']";
        await page.SetContentAsync("<div data-key='a&quot;b' hidden></div>");
        (await page.CountAsync(selector)).Should().Be(1);
        (await page.IsVisibleAsync(selector)).Should().BeFalse();
        var visible = page.WaitForVisibleAsync(selector, Patience);
        var textWait = page.WaitForTextAsync(selector, "quotes ' \" slash", Patience);
        await page.EvaluateAsync("arg=>{const el=document.querySelector(arg.selector);el.hidden=false;el.textContent=arg.text;}",
            new { selector, text });
        (await visible).Should().BeTrue();
        (await textWait).Should().BeTrue();
        (await page.TextsAsync(selector)).Should().Equal(text);
    }

    [Test]
    public async Task CountVisibilityAndTextWaitsObserveMutationAndNavigation()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<div class=row hidden>old</div>")
            .MapHtml("/next", "<div class=row>next</div><div class=row>second</div>"));
        var page = fixture.Page;
        await page.NavigateAsync(fixture.Url("/"));
        var count = page.WaitForCountAsync(".row", 2, Patience, visibleOnly: true);
        var visible = page.WaitForVisibleAsync(".row", Patience);
        var text = page.WaitForTextAsync(".row", "next", Patience);
        await page.NavigateAsync(fixture.Url("/next"));
        (await count).Should().BeTrue();
        (await visible).Should().BeTrue();
        (await text).Should().BeTrue();
        var hidden = page.WaitForVisibleAsync(".row", Patience, visible: false);
        await page.EvaluateAsync("document.querySelector('.row').remove()");
        await page.EvaluateAsync("document.querySelector('.row').hidden=true");
        (await hidden).Should().BeTrue();
    }

    [Test]
    public async Task ArgumentEvaluationSnapshotsDataWithoutSourceOrHostObjectExposure()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        const string text = "'\");globalThis.injected=true;//\n\u2028\u2029\\\U0001F34E";
        (await page.EvaluateAsync<string>("value=>value", text)).Should().Be(text);
        (await page.EvaluateAsync<bool>("typeof injected==='undefined'")).Should().BeTrue();
        var argument = new Dictionary<string, object?>
        {
            ["text"] = text, ["values"] = new object?[] { null, true, 42 },
            ["__proto__"] = new { safe = true }
        };
        (await page.EvaluateAsync<bool>("arg=>Object.getPrototypeOf(arg)===Object.prototype && Object.hasOwn(arg,'__proto__') && arg.__proto__.safe", argument))
            .Should().BeTrue();
        var result = await page.EvaluateAsync("arg=>({text:arg.text,values:arg.values})", argument);
        var record = result.Should().BeOfType<Dictionary<string, object?>>().Subject;
        record["text"].Should().Be(text);
        var values = record["values"].Should().BeOfType<object?[]>().Subject;
        values[0].Should().BeNull();
        values[1].Should().Be(true);
        values[2].Should().Be(42d);
        await page.SetContentAsync("<title>new engine</title>");
        record["text"].Should().Be(text);
        (await page.EvaluateAsync<int>("arg=>arg.value+1", new { value = 41 })).Should().Be(42);
        (await page.EvaluateAsync<string>("arg=>arg", argument: null)).Should().BeNull();
    }

    [Test]
    public async Task ArgumentSnapshotIsTakenBeforeAnEvaluationIsPosted()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        var argument = new Dictionary<string, object?> { ["text"] = "before" };
        var evaluation = page.EvaluateAsync<string>("arg=>arg.text", argument);
        argument["text"] = "after";
        (await evaluation).Should().Be("before");
        (await page.EvaluateAsync("arg=>({value:arg,fn:()=>42})", "data"))
            .Should().BeOfType<Dictionary<string, object?>>().Subject.Should().ContainKey("value").And.NotContainKey("fn");
        (await page.EvaluateAsync("arg=>()=>arg", "data")).Should().BeNull();
    }

    [Test]
    public async Task ArgumentAwaitSnapshotsDataAndDetachesStructuredSettlementResults()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        const string text = "'\");globalThis.injected=true;//\n\u2028\u2029\\\U0001F34E";
        var argument = new Dictionary<string, object?>
        {
            ["text"] = text, ["values"] = new object?[] { null, true, 42 },
            ["__proto__"] = new { safe = true }
        };
        var pending = page.EvaluateAndAwaitAsync("""
            async arg=>{
                await new Promise(resolve=>setTimeout(resolve,0));
                return {toJSON(){
                    document.body.setAttribute('data-settled',arg.text);
                    return {text:arg.text,values:arg.values,
                        ownData:Object.hasOwn(arg,'__proto__') && arg.__proto__.safe,
                        plain:Object.getPrototypeOf(arg)===Object.prototype,omitted:()=>42};
                }};
            }
            """, argument);
        argument["text"] = "changed";
        var result = (await pending.WaitAsync(Patience)).Should().BeOfType<Dictionary<string, object?>>().Subject;
        result["text"].Should().Be(text);
        result["ownData"].Should().Be(true);
        result["plain"].Should().Be(true);
        result.Should().NotContainKey("omitted");
        result["values"].Should().BeOfType<object?[]>().Subject.Should().Equal(null, true, 42d);
        (await page.EvaluateAsync<string>("document.body.getAttribute('data-settled')")).Should().Be(text);
        (await page.EvaluateAsync<bool>("typeof injected==='undefined'")).Should().BeTrue();
        await page.SetContentAsync("<title>replacement</title>");
        result["text"].Should().Be(text);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ArgumentAwaitConvertsImmediateAndPromiseScalarsAndKeepsExistingTokenOverloads()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        (await page.EvaluateAndAwaitAsync<int>("arg=>arg.value+1", new { value = 41 })).Should().Be(42);
        (await page.EvaluateAndAwaitAsync<int>("async arg=>arg.value+1", new { value = 41 })).Should().Be(42);
        (await page.EvaluateAndAwaitAsync<bool>("arg=>Promise.resolve(arg)", true)).Should().BeTrue();
        (await page.EvaluateAndAwaitAsync<string>("async arg=>arg", "literal")).Should().Be("literal");
        (await page.EvaluateAndAwaitAsync("arg=>Promise.resolve(arg)", 42)).Should().BeOfType<double>().Subject.Should().Be(42);
        (await page.EvaluateAndAwaitAsync("async arg=>arg", argument: null)).Should().BeNull();
        (await page.EvaluateAndAwaitAsync<string>("async arg=>undefined", argument: null)).Should().BeNull();
        (await page.EvaluateAndAwaitAsync<int>("Promise.resolve(42)", default)).Should().Be(42);
        (await page.EvaluateAndAwaitAsync("Promise.resolve(42)", CancellationToken.None)).Should().Be(42d);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ArgumentAwaitRejectionsUseTheExistingSafeErrorPath(bool generic)
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        const string message = "rejected ' \" \n\u2028\u2029";
        var rejection = () => generic
            ? page.EvaluateAndAwaitAsync<object>("arg=>Promise.reject(new Error(arg))", message)
            : page.EvaluateAndAwaitAsync("arg=>Promise.reject(new Error(arg))", message);
        (await rejection.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Promise was rejected with value Error: " + message);
        var hostile = () => page.EvaluateAndAwaitAsync("""
            arg=>Promise.reject({get message(){globalThis.rejectionGetterRan=true;throw Error(arg);}})
            """, message);
        (await hostile.Should().ThrowAsync<InvalidOperationException>()).WithMessage("Promise was rejected with value Object");
        (await page.EvaluateAsync<bool>("typeof rejectionGetterRan==='undefined'")).Should().BeTrue();
    }

    [Test]
    public async Task ArgumentAwaitReportsConversionAndSerializationFailuresWithoutHanging()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        var conversion = () => page.EvaluateAndAwaitAsync<int>("async arg=>arg", "not a number").WaitAsync(Patience);
        await conversion.Should().ThrowAsync<FormatException>();
        foreach (var function in new[]
        {
            "async arg=>{const value={};value.self=value;return value;}",
            "async arg=>1n",
            "async arg=>({toJSON(){throw new Error(arg);}})"
        })
        {
            var serialization = () => page.EvaluateAndAwaitAsync(function, "serialization failed").WaitAsync(Patience);
            await serialization.Should().ThrowAsync<JavaScriptException>();
        }
        var invalidFunction = () => page.EvaluateAndAwaitAsync("42", argument: null);
        await invalidFunction.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task ArgumentAwaitValidatesFunctionAndClosedPages()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        var missingFunction = () => page.EvaluateAndAwaitAsync(null!, argument: 1);
        await missingFunction.Should().ThrowAsync<ArgumentNullException>();
        var missingGenericFunction = () => page.EvaluateAndAwaitAsync<int>(null!, argument: 1);
        await missingGenericFunction.Should().ThrowAsync<ArgumentNullException>();
        await page.CloseAsync();
        var closed = () => page.EvaluateAndAwaitAsync("async arg=>arg", argument: 1);
        await closed.Should().ThrowAsync<ObjectDisposedException>();
        var closedGeneric = () => page.EvaluateAndAwaitAsync<int>("async arg=>arg", argument: 1);
        await closedGeneric.Should().ThrowAsync<ObjectDisposedException>();
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task ArgumentAwaitPendingPromisesBelongToTheirOriginalDocument(bool generic, bool close)
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        const string function = "arg=>new Promise(()=>{globalThis.awaitArgumentStarted=arg.started;})";
        var pending = generic
            ? page.EvaluateAndAwaitAsync<object>(function, new { started = true })
            : page.EvaluateAndAwaitAsync(function, new { started = true });
        (await page.EvaluateAsync<bool>("awaitArgumentStarted")).Should().BeTrue();
        if (close) await page.CloseAsync();
        else await page.NavigateAsync("data:text/html,<title>replacement</title>");
        var wait = () => pending.WaitAsync(Patience);
        await wait.Should().ThrowAsync<OperationCanceledException>();
        if (!close)
            (await page.EvaluateAndAwaitAsync<string>("async arg=>document.title+arg", "!")).Should().Be("replacement!");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ArgumentAwaitHonorsPreCancellationAndCancellationWhilePending(bool generic)
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var beforePosting = () => generic
            ? page.EvaluateAndAwaitAsync<object>("arg=>{globalThis.cancelledCallRan=true;return arg;}", 42, cancelled.Token)
            : page.EvaluateAndAwaitAsync("arg=>{globalThis.cancelledCallRan=true;return arg;}", 42, cancelled.Token);
        await beforePosting.Should().ThrowAsync<OperationCanceledException>();
        (await page.EvaluateAsync<bool>("typeof cancelledCallRan==='undefined'")).Should().BeTrue();
        using var pendingCancellation = new CancellationTokenSource();
        const string function = "arg=>new Promise(resolve=>{globalThis.resolveOldAwait=resolve;globalThis.awaitArgumentStarted=arg;})";
        var pending = generic
            ? page.EvaluateAndAwaitAsync<object>(function, true, pendingCancellation.Token)
            : page.EvaluateAndAwaitAsync(function, true, pendingCancellation.Token);
        (await page.EvaluateAsync<bool>("awaitArgumentStarted")).Should().BeTrue();
        pendingCancellation.Cancel();
        var wait = () => pending.WaitAsync(Patience);
        await wait.Should().ThrowAsync<OperationCanceledException>();
        await page.EvaluateAsync("resolveOldAwait(99)");
        (await page.EvaluateAndAwaitAsync<int>("async arg=>arg", 42)).Should().Be(42);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ArgumentAwaitUsesCurrentParserOptionsAndThePageExecutionBudget()
    {
        var options = new BrowserOptions { MaxTaskDuration = TimeSpan.FromMilliseconds(100) }.ConfigureEngine(engine =>
        {
            engine.Strict = true;
            engine.Parsing.MaxSourceLength = 500;
        });
        await using var browser = new NativeBrowser(options);
        var page = await browser.NewPageAsync();
        (await page.EvaluateAndAwaitAsync<bool>("function(arg){return Promise.resolve(this===undefined && arg.length===1000);}",
            new string('x', 1000))).Should().BeTrue();
        var runaway = () => page.EvaluateAndAwaitAsync("arg=>{while(arg){}}", true).WaitAsync(Patience);
        await runaway.Should().ThrowAsync<TimeoutException>();
        (await page.EvaluateAndAwaitAsync<int>("async arg=>arg", 42)).Should().Be(42);
    }

    [Test]
    public async Task PrimitiveResultsAreDetachedAndKeepJsonNumberSemantics()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        (await page.EvaluateAsync("arg=>42", null)).Should().BeOfType<double>().Subject.Should().Be(42);
        (await page.EvaluateAsync("arg=>'literal'", null)).Should().BeOfType<string>().Subject.Should().Be("literal");
        (await page.EvaluateAsync("arg=>true", null)).Should().BeOfType<bool>().Subject.Should().BeTrue();
        foreach (var expression in new[] { "null", "undefined", "NaN", "Infinity", "-Infinity", "Symbol('symbol')" })
            (await page.EvaluateAsync("arg=>" + expression, null)).Should().BeNull();
        var zero = (await page.EvaluateAsync("arg=>-0", null)).Should().BeOfType<double>().Subject;
        BitConverter.DoubleToInt64Bits(zero).Should().Be(0);
        var bigint = () => page.EvaluateAsync("arg=>1n", null);
        await bigint.Should().ThrowAsync<JavaScriptException>();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PreparedWaitsRebindToTheCurrentDocument(bool withArgument)
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=old>old</div><script>window.polls=0;</script>");
        var wait = withArgument
            ? page.WaitForAsync("arg=>{window.polls=(window.polls||0)+1;return document.querySelector('#next')?.textContent===arg;}", "new", Patience)
            : page.WaitForAsync("(window.polls=(window.polls||0)+1, document.querySelector('#next')?.textContent==='new')", Patience);
        (await page.WaitForAsync("window.polls>=2", Patience)).Should().BeTrue();
        await page.SetContentAsync("<div id=next>new</div>");
        (await wait).Should().BeTrue();
    }

    [Test]
    public async Task ArgumentWaitUsesFreshDataAndRetriesTransientScriptFailures()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<script>window.polls=0;</script>");
        (await page.WaitForAsync("arg=>{window.polls++;arg.counter++;return polls>=3 && arg.counter===1;}",
            new { counter = 0 }, Patience)).Should().BeTrue();
        var wait = page.WaitForAsync("selector=>document.querySelector(selector).textContent==='ready'", "#later", Patience);
        await page.EvaluateAsync("setTimeout(()=>{document.body.innerHTML='<div id=later>ready</div>';},30)");
        (await wait).Should().BeTrue();
    }

    [Test]
    public async Task PreparedWaitsPreserveEngineStrictnessAndBoundSourceNotArgumentData()
    {
        var options = new BrowserOptions().ConfigureEngine(engine =>
        {
            engine.Strict = true;
            engine.Parsing.MaxSourceLength = 500;
        });
        await using var browser = new NativeBrowser(options);
        var page = await browser.NewPageAsync();
        (await page.WaitForAsync("(function(){return this===undefined;})()", TimeSpan.Zero)).Should().BeTrue();
        (await page.WaitForAsync("function(arg){return this===undefined && arg.length===1000;}",
            new string('x', 1000), TimeSpan.Zero)).Should().BeTrue();
    }

    [Test]
    public async Task WaitTimeoutDoesNotSwallowTheExecutionBudget()
    {
        await using var browser = new NativeBrowser(new BrowserOptions { MaxTaskDuration = TimeSpan.FromMilliseconds(100) });
        var page = await browser.NewPageAsync();
        var wait = () => page.WaitForAsync("arg=>{while(true){}}", 1, Patience);
        await wait.Should().ThrowAsync<TimeoutException>();
    }

    [Test]
    public async Task TimeoutsErrorsAndCloseRemainExplicit()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        (await page.WaitForCountAsync(".absent", 1, TimeSpan.Zero)).Should().BeFalse();
        (await page.WaitForVisibleAsync(".absent", TimeSpan.Zero)).Should().BeFalse();
        (await page.WaitForTextAsync(".absent", "", TimeSpan.Zero)).Should().BeFalse();
        (await page.WaitForAsync("value=>value", false, TimeSpan.Zero)).Should().BeFalse();
        var selector = () => page.CountAsync("[");
        await selector.Should().ThrowAsync<DomException>();
        var invalid = () => page.WaitForAsync("arg=>missing(arg)", "data", TimeSpan.Zero);
        await invalid.Should().ThrowAsync<JavaScriptException>();
        var conversion = () => page.EvaluateAsync<int>("arg=>'not a number'", 1);
        await conversion.Should().ThrowAsync<FormatException>();
        var cycle = () => page.EvaluateAsync("arg=>{const a={};a.a=a;return a;}", 1);
        await cycle.Should().ThrowAsync<JavaScriptException>();
        var wait = page.WaitForAsync("arg=>false", 1, Timeout.InfiniteTimeSpan);
        await page.CloseAsync();
        (await wait).Should().BeFalse();
        var closed = () => page.CountAsync(".absent");
        await closed.Should().ThrowAsync<ObjectDisposedException>();
    }
}
