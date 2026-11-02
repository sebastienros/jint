using Jint.Browser;
using Jint.Browser.Dom;
using Jint.Browser.Layout;
using Jint.Browser.Runtime;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Layout;

public sealed class SizeMeasurementRetentionTests
{
    private const string Markup = """
        <style>
        .changed > .child, [data-hide] .child {display:none}
        .inherited {visibility:var(--visible, visible)}
        #flex {display:flex}
        #one {flex-grow:1} #two {flex-grow:2}
        </style>
        <section id="stable"><div><span>unchanged</span></div></section>
        <section id="editor"><div class="child"><span>text</span></div></section>
        <div id="flex"><div id="one"><span>A</span></div><div id="two">B</div></div>
        <button id="later">Save</button>
        """;

    [Test]
    public async Task OrdinaryAncestorWritesRetainUnrelatedRowsButNotTheOldReadContext()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        await InstallDiagnostics(page);
        (await page.EvaluateAsync<string>("""
            const before=document.body.offsetHeight, query=readContextStamp(), cache=measurementStamp();
            document.getElementById('editor').className='changed';
            const after=document.body.offsetHeight;
            [readContextStamp()!==query,measurementStamp()===cache,before>after,retainedRowHits()>0].join()
            """)).Should().Be("true,true,true,true");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("document.getElementById('editor').className='changed'")]
    [TestCase("document.getElementById('editor').setAttribute('data-hide','yes')")]
    [TestCase("document.getElementById('editor').style.visibility='hidden'")]
    [TestCase("document.getElementById('editor').className='inherited';document.getElementById('editor').style.setProperty('--visible','hidden')")]
    [TestCase("document.getElementById('editor').firstElementChild.firstElementChild.firstChild.data=''")]
    [TestCase("document.getElementById('editor').innerHTML='<div><div>replacement</div></div>'")]
    [TestCase("document.getElementById('editor').append(document.getElementById('stable').firstElementChild)")]
    [TestCase("document.getElementById('editor').remove()")]
    [TestCase("document.getElementById('one').style.flexGrow='4'")]
    [TestCase("document.getElementById('two').append(document.createElement('div'))")]
    [TestCase("document.getElementById('flex').style.alignItems='center'")]
    [TestCase("document.getElementById('flex').style.direction='rtl'")]
    [TestCase("document.styleSheets[0].insertRule('#stable {display:none}',0)")]
    [TestCase("document.styleSheets[0].cssRules[0].selectorText='#stable'")]
    [TestCase("document.querySelector('style').textContent='#stable{display:none}'")]
    [TestCase("document.querySelector('style').media='print'")]
    [TestCase("const f=document.createDocumentFragment();f.append(document.getElementById('editor').firstElementChild);document.getElementById('stable').append(f)")]
    public async Task RetainedMeasurementsAgreeWithUnretainedGeometry(string mutation)
    {
        await Compare(Markup, mutation);
    }

    [TestCase("body:has(#editor:empty) #later", "document.getElementById('editor').textContent=''")]
    [TestCase("body:has(#editor:empty) #later", "document.getElementById('editor').firstChild.data=''")]
    [TestCase("body:has(#editor[data-x]) #later", "document.getElementById('editor').setAttribute('data-x','yes')")]
    [TestCase("body:has(#editor[STYLE]) #later", "document.getElementById('editor').style.color='red'")]
    [TestCase("#editor[data-x] ~ #later", "document.getElementById('editor').setAttribute('data-x','yes')")]
    [TestCase("#editor:empty ~ #later", "document.getElementById('editor').firstChild.replaceData(0,4,'')")]
    [TestCase("#later:nth-child(3)", "document.getElementById('editor').before(document.createElement('div'))")]
    [TestCase("body:focus-within #later", "document.getElementById('editor').focus()")]
    public async Task NonlocalDependenciesNeverReuseStaleRows(string selector, string mutation)
    {
        await Compare($"<style>{selector}{{display:none}}</style><div id='editor' tabindex='0'>text</div><button id='later'>Save</button>",
            mutation);
    }

    [Test]
    public async Task RelationalRulesAllowStyleOnlyWritesWhenTheirSelectorCannotObserveStyle()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup + "<style>body:has(.missing) #later {display:none}</style>");
        await InstallDiagnostics(page);
        (await page.EvaluateAsync<string>("""
            const before=document.body.offsetHeight, cache=measurementStamp();
            document.getElementById('editor').style.display='none';
            [measurementStamp()===cache,document.body.offsetHeight<before].join()
            """)).Should().Be("true,true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task DetachedFragmentsAndTextControlStateRetainUnrelatedRows()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup + "<textarea id='text'></textarea><input id='input'>");
        await InstallDiagnostics(page);
        (await page.EvaluateAsync<string>("""
            document.body.offsetHeight;
            const cache=measurementStamp(), hits=retainedRowHits();
            const fragment=document.createDocumentFragment(), child=document.createElement('span');
            fragment.append(child); child.textContent='detached';
            fragment.removeChild(child);
            document.getElementById('text').value='changed';
            document.getElementById('text').setSelectionRange(1,2);
            document.getElementById('input').value='changed';
            document.body.offsetHeight;
            [measurementStamp()===cache,retainedRowHits()>hits].join()
            """)).Should().Be("true,true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ChildListEditsPreserveProvenUnaffectedSiblingMeasurements()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        await InstallDiagnostics(page);
        (await page.EvaluateAsync<bool>("""
            const cache=measurementStamp(), probe=document.createElement('span');
            document.body.append(probe);
            readContextStamp();
            const preserved=stableRowsCached();
            probe.remove();
            preserved && measurementStamp()===cache
            """)).Should().BeTrue();
    }

    [Test]
    public async Task NativeInputListenersObserveCommittedStateWithRetainedUnrelatedRows()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup + "<textarea id='text'></textarea>");
        await InstallDiagnostics(page);
        await page.EvaluateAsync("""
            const text=document.getElementById('text');
            text.focus();
            window.cache=measurementStamp();
            window.retained=true;window.values=[];
            text.addEventListener('input',()=>{
                document.getElementById('editor').textContent=text.value;
                retained=retained && measurementStamp()===cache;
                values.push(text.value);
            });
            """);
        (await page.TypeAsync("#text", "ab")).Should().BeTrue();
        (await page.PressAsync("Backspace")).Should().BeTrue();
        (await page.EvaluateAsync<string>("[retained,values.join('|'),document.getElementById('text').value].join()"))
            .Should().Be("true,a|ab|a,a");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task MovingFocusedDescendantsInvalidatesFocusWithinAncestors()
    {
        await Compare(Markup + "<style>#editor:focus-within{display:none}</style><input id='focus'>",
            "document.getElementById('stable').append(document.getElementById('focus'))",
            "document.getElementById('editor').append(document.getElementById('focus'));document.getElementById('focus').focus()");
    }

    [TestCase("body:has(textarea:placeholder-shown) #later", "document.getElementById('text').value='changed'")]
    [TestCase("body:has(input:invalid) #later", "document.getElementById('input').value='changed'")]
    public async Task TextControlStateRespectsNonlocalSemanticSelectors(string selector, string mutation)
    {
        await Compare(Markup + $"<style>{selector}{{display:none}}</style><textarea id='text' placeholder='hint'></textarea><input id='input' required>", mutation);
    }

    [Test]
    public async Task UnclassifiedNativeWritesAndIndependentStyleChangesDiscardMeasurements()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup + "<input id='input'>");
        await InstallDiagnostics(page);
        (await page.EvaluateAsync<string>("""
            const first=measurementStamp();
            markNativeUnknown();
            const second=measurementStamp();
            document.styleSheets[0].insertRule('#stable{display:none}',0);
            [first!==second,measurementStamp()!==second].join()
            """)).Should().Be("true,true");
    }

    [Test]
    public async Task DirtySummaryOverflowFallsBackAndRemovedSubtreesAreNotRetained()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        await InstallDiagnostics(page);
        (await page.EvaluateAsync<string>("""
            measurementStamp();
            document.getElementById('stable').remove();
            const next=measurementStamp(), evicted=removedWasEvicted();
                for(let i=0;i<70;i++){
                  const node=document.createElement('div');
                  document.getElementById('editor').append(node);
                  node.setAttribute('data-x',i);
                }
            [evicted,measurementStamp()!==next].join()
            """)).Should().Be("true,true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task RemovalEvictsVisibilityOnlyEntriesWithoutAnOldChildSnapshot()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        await InstallDiagnostics(page);
        (await page.EvaluateAsync<bool>("""
            document.getElementById('stable').offsetWidth;
            document.getElementById('stable').remove();
            removedWasEvicted()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task NativeDirtySignalsAreDemandInstalledTypedAndSynchronous()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        (await page.RunOnLoopAsync(engine => PageRuntime.Find(engine)!.Document!.MutationSignal is null))
            .Should().BeTrue();
        await page.EvaluateAsync("document.body.offsetHeight");
        (await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var installed = document.MutationSignal is not null;
            var records = new List<(Node? Target, NativeMutationKind Kind, string? Attribute, Node? Child)>();
            void Record(Node? target, NativeMutationKind kind, string? attribute, Node? child)
                => records.Add((target, kind, attribute, child));
            document.MutationSignal += Record;
            try
            {
                var editor = DomDocumentReads.ById(runtime.Dom, document, "editor")!;
                editor.SetAttribute("data-test", "value");
                var attribute = records[^1];
                var text = document.CreateTextNode("text");
                editor.AppendChild(text);
                var insertion = records[^1];
                text.Data = "changed";
                var character = records[^1];
                editor.RemoveChild(text);
                var removal = records[^1];
                document.MarkMutation();
                return installed && ReferenceEquals(attribute.Target, editor)
                    && attribute.Kind == NativeMutationKind.Attributes && attribute.Attribute == "data-test"
                    && insertion.Kind == NativeMutationKind.ChildList && ReferenceEquals(insertion.Child, text)
                    && ReferenceEquals(character.Target, text) && character.Kind == NativeMutationKind.CharacterData
                    && ReferenceEquals(removal.Target, editor) && ReferenceEquals(removal.Child, text)
                    && records[^1].Kind == NativeMutationKind.Unknown;
            }
            finally { document.MutationSignal -= Record; }
        })).Should().BeTrue();
    }

    [Test]
    public async Task DirtySubtreeProcessingHonorsCancellationAndCheckpointFailures()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        await page.RunOnLoopAsync(engine =>
        {
            var document = PageRuntime.Find(engine)!.Document!;
            var root = document.DocumentElement!;
            for (var i = 0; i < 300; i++) root.AppendChild(document.CreateElement("div"));
            var data = new SizeMeasurements();
            using var cancellation = new CancellationTokenSource();
            Action cancelDuringWalk = () => data.Invalidate(root, cancellation.Cancel, cancellation.Token);
            cancelDuringWalk.Should().Throw<OperationCanceledException>();
            var checks = 0;
            var failure = new InvalidOperationException("checkpoint");
            void Checkpoint() { if (++checks == 2) throw failure; }
            Action failDuringWalk = () => data.Invalidate(root, Checkpoint, CancellationToken.None);
            failDuringWalk.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(failure);
            Action failBeforeChildInvalidation = () => data.InvalidateChildren(root, () => throw failure, CancellationToken.None);
            failBeforeChildInvalidation.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(failure);
            return 0;
        });
    }

    // Install read-only diagnostics in a separate request: RunOnLoopAsync itself intentionally
    // suspends retention for arbitrary native callbacks. Public evaluations use tracked bindings.
    private static Task<int> InstallDiagnostics(Page page) => page.RunOnLoopAsync(engine =>
    {
        var runtime = PageRuntime.Find(engine)!;
        var layout = runtime.Layout;
        var diagnostics = layout.Diagnostics = new PageLayoutDiagnostics();
        var removed = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "stable");
        SizeMeasurements? previous = null;
        FlatLayout.SizeQuery? readContext = null;
        var caches = 0;
        var contexts = 0;
        engine.SetValue("measurementStamp", () =>
        {
            var sizes = layout.MeasureSizes();
            _ = sizes.Height(DomDocumentElements.Body(runtime.Document!)!);
            if (!ReferenceEquals(previous, sizes.Measurements)) { previous = sizes.Measurements; caches++; }
            return caches;
        });
        engine.SetValue("readContextStamp", () =>
        {
            var sizes = layout.MeasureSizes();
            if (!ReferenceEquals(readContext, sizes)) { readContext = sizes; contexts++; }
            return contexts;
        });
        engine.SetValue("retainedRowHits", () => diagnostics.RetainedRowHits);
        engine.SetValue("stableRowsCached", () => removed is not null && previous?.Rows.ContainsKey(removed) == true);
        engine.SetValue("markNativeUnknown", () => runtime.Document!.MarkMutation());
        engine.SetValue("removedWasEvicted", () =>
        {
            var data = layout.MeasureSizes().Measurements;
            return removed is not null && !data.Rows.ContainsKey(removed)
                && !data.Rendered.ContainsKey(removed) && !data.Children.ContainsKey(removed);
        });
        return 0;
    });

    private static async Task Compare(string markup, string mutation, string? setup = null)
    {
        string? expected = null;
        foreach (var customized in new[] { true, false })
        {
            var options = new BrowserOptions();
            if (customized) options.ConfigureEngine(_ => { });
            await using var browser = new global::Jint.Browser.Browser(options);
            var page = await browser.NewPageAsync();
            await page.SetContentAsync(markup);
            if (setup is not null) await page.EvaluateAsync(setup + ";void 0");
            await page.EvaluateAsync("""
                window.snapshot=()=>Array.from(document.querySelectorAll('body,body *')).map(e=>{
                  const r=e.getBoundingClientRect();
                  return [e.tagName,e.id,r.x,r.y,r.width,r.height,e.offsetHeight,e.offsetTop];
                });
                snapshot();
                """);
            await page.EvaluateAsync(mutation);
            var actual = await page.EvaluateAsync<string>("JSON.stringify(snapshot())");
            if (customized) expected = actual;
            else actual.Should().Be(expected);
            page.Errors.Should().BeEmpty();
        }
    }
}
