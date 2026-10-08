#nullable enable

using System.Runtime.CompilerServices;
using Jint.Browser;
using Jint.Browser.CustomElements;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.CustomElements;

using Browser = global::Jint.Browser.Browser;

public sealed class CustomElementDrainTests
{
    [TestCase(4)]
    [TestCase(12)]
    public async Task RepeatedNestedDrainsKeepWaitingElementsAndSameElementReactionsInOrder(int depth)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><body></body>");
        (await page.EvaluateAsync<bool>("const depth=" + depth + ";" + """
            var order=[];
            class DrainElement extends HTMLElement {
              static observedAttributes=['a','b'];
              connectedCallback() {
                order.push(this.id+':connected');
                if(this.waiting) this.waiting.setAttribute('a','waiting');
              }
              attributeChangedCallback(name) {
                order.push(this.id+':'+name+':begin');
                this.setAttribute('ignored','empty drain');
                if(name==='a') {
                  this.setAttribute('b','same element');
                  if(this.next) this.next.setAttribute('a','nested');
                }
                order.push(this.id+':'+name+':end');
              }
            }
            customElements.define('x-drain-test',DrainElement);
            var nodes=Array.from({length:depth},(_,i)=>{
              const e=document.createElement('x-drain-test');e.id='n'+i;return e;
            });
            nodes[0].waiting=nodes[1];
            for(let i=1;i<depth-1;i++) nodes[i].next=nodes[i+1];
            const events=['n0:connected','n1:connected'];
            for(let i=1;i<depth;i++) events.push('n'+i+':a:begin');
            for(let i=depth-1;i>=1;i--) events.push('n'+i+':a:end','n'+i+':b:begin','n'+i+':b:end');
            const expected=events.join('|');
            let valid=true;
            for(let i=0;i<50;i++) {
              const fragment=document.createDocumentFragment();
              fragment.append(nodes[0],nodes[1]);
              order=[];document.body.append(fragment);
              valid=valid && order.join('|')===expected;
              nodes[0].remove();nodes[1].remove();
            }
            valid;
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AReportedNestedCallbackErrorDoesNotLoseLaterReactionsOrPoisonTheNextDrain()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><body></body>");
        (await page.EvaluateAsync<string>("""
            var order=[];
            class DrainThrow extends HTMLElement {
              static observedAttributes=['a'];
              attributeChangedCallback(name,oldValue,newValue) {
                order.push(this.id+':begin');
                if(this.next) this.next.setAttribute('a',newValue);
                if(this.id==='inner' && newValue==='throw') throw new Error('nested drain failure');
                order.push(this.id+':end');
              }
            }
            customElements.define('x-drain-throw',DrainThrow);
            var outer=document.createElement('x-drain-throw'),inner=document.createElement('x-drain-throw');
            outer.id='outer';inner.id='inner';outer.next=inner;
            outer.setAttribute('a','throw');outer.setAttribute('a','recover');
            order.join('|');
            """)).Should().Be("outer:begin|inner:begin|outer:end|outer:begin|inner:begin|inner:end|outer:end");
        page.Errors.Should().ContainSingle().Which.Message.Should().Contain("nested drain failure");
    }

    [Test]
    public async Task FatalNestedCallbackFailurePropagatesAndAnUnrelatedDrainStillWorks()
    {
        var probe = new FatalProbe();
        await using var browser = new Browser(new BrowserOptions().ConfigureEngine(options => options.AddConstraint(probe)));
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><body></body>");
        await page.RunOnLoopAsync(engine =>
        {
            engine.SetValue("armDrainFailure", () => probe.Armed = true);
            engine.Execute("""
                var calls=0;
                class FatalDrain extends HTMLElement {
                  static observedAttributes=['a'];
                  attributeChangedCallback() {
                    calls++;
                    if(this.next) this.next.setAttribute('a','nested');
                    if(this.fail) { armDrainFailure(); calls++; }
                  }
                }
                customElements.define('x-fatal-drain',FatalDrain);
                var outer=document.createElement('x-fatal-drain'),inner=document.createElement('x-fatal-drain');
                outer.next=inner;inner.fail=true;
                """);
            try
            {
                Assert.Throws<OperationCanceledException>(() => engine.Execute("outer.setAttribute('a','fail');"));
            }
            finally
            {
                probe.Armed = false;
            }
            // A fatal interruption keeps the interrupted records' existing Queued state. A different
            // element must still get a distinct usable queue after both nested drains unwind.
            engine.Evaluate("var fresh=document.createElement('x-fatal-drain'); calls=0; fresh.setAttribute('a','ok'); calls;").Should().Be(1);
            return true;
        });
        page.Errors.Should().BeEmpty();
    }

    private sealed class FatalProbe : Constraint
    {
        internal bool Armed;
        public override void Check()
        {
            if (Armed) throw new OperationCanceledException();
        }
        public override void Reset() { }
    }

    [Test]
    [NonParallelizable]
    public async Task CompletedDrainsDoNotRetainDetachedElementsInReusableStorage()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><body></body>");
        var weak = await page.RunOnLoopAsync(CreateTransientElement);
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        weak.TryGetTarget(out _).Should().BeFalse();
        page.Errors.Should().BeEmpty();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Element> CreateTransientElement(Engine engine)
    {
        engine.Execute("class TransientDrain extends HTMLElement { static observedAttributes=['a']; attributeChangedCallback() {} } customElements.define('x-transient-drain',TransientDrain);");
        var runtime = PageRuntime.Find(engine)!;
        var element = runtime.Document!.CreateElement("x-transient-drain");
        var registry = CustomElementRegistry.Of(engine)!;
        registry.TryUpgrade(element);
        registry.Drain();
        element.SetAttribute("a", "value");
        registry.Drain();
        // Constructor/callback environment reuse can keep their most recent receiver alive.
        // Move those independent references to another element before testing queue retention.
        var replacement = runtime.Document.CreateElement("x-transient-drain");
        registry.TryUpgrade(replacement);
        registry.Drain();
        replacement.SetAttribute("a", "replacement");
        registry.Drain();
        return new WeakReference<Element>(element);
    }
}
