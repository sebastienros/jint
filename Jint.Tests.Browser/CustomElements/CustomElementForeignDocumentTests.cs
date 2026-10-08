#nullable enable

using System.Runtime.CompilerServices;
using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.CustomElements;

using Browser = global::Jint.Browser.Browser;

public sealed class CustomElementForeignDocumentTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task ExplicitRoundTripPreservesReactionsIdentityAndObserverTargets(bool customized, bool parsed)
    {
        // DOM adopt/remove/insert order, with callbacks from the element's saved definition.
        // https://dom.spec.whatwg.org/#concept-node-adopt
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><body></body>");
        var script = """
            (() => {
              const other=OTHER, order=[];
              let constructions=0;
              class ForeignElement extends BASE {
                constructor() { super(); constructions++; }
                static observedAttributes=['a'];
                connectedCallback() { order.push('connected'); }
                disconnectedCallback() { order.push('disconnected'); }
                adoptedCallback(from,to) { order.push('adopted:'+(from!==to && to===this.ownerDocument)); }
                attributeChangedCallback(name,oldValue,newValue) { order.push('attribute:'+oldValue+':'+newValue); }
              }
              customElements.define('x-foreign-test',ForeignElementOPTIONS);
              const node=CREATE, prototype=Object.getPrototypeOf(node);
              const attributes=new MutationObserver(()=>{}), tree=new MutationObserver(()=>{});
              attributes.observe(node,{attributes:true,attributeOldValue:true});
              tree.observe(other.body,{childList:true});
              document.body.append(node);
              const result=other.adoptNode(node);
              other.body.append(node);node.setAttribute('a','foreign');
              node.remove();node.setAttribute('a','detached');other.body.append(node);
              document.adoptNode(node);document.body.append(node);node.removeAttribute('a');
              const a=attributes.takeRecords(), t=tree.takeRecords();
              const valid=result===node && node.ownerDocument===document &&
                Object.getPrototypeOf(node)===prototype && node instanceof ForeignElement &&
                node instanceof BASE && constructions===1 &&
                a.length===3 && a.every(r=>r.target===node) &&
                a.map(r=>r.oldValue).join('|')==='|foreign|detached' &&
                t.length===4 && t.every(r=>r.target===other.body) &&
                t[0].addedNodes[0]===node && t[1].removedNodes[0]===node &&
                t[2].addedNodes[0]===node && t[3].removedNodes[0]===node;
              attributes.disconnect();tree.disconnect();
              return valid+':'+order.join('|');
            })()
            """.Replace("OTHER", parsed
                ? "new DOMParser().parseFromString('<!doctype html><body></body>','text/html')"
                : "document.implementation.createHTMLDocument()", StringComparison.Ordinal)
                .Replace("BASE", customized ? "HTMLButtonElement" : "HTMLElement", StringComparison.Ordinal)
                .Replace("OPTIONS", customized ? ",{extends:'button'}" : "", StringComparison.Ordinal)
                .Replace("CREATE", customized ? "document.createElement('button',{is:'x-foreign-test'})" : "document.createElement('x-foreign-test')", StringComparison.Ordinal);
        (await page.EvaluateAsync<string>(script)).Should().Be(
            "true:connected|disconnected|adopted:true|connected|attribute:null:foreign|disconnected|attribute:foreign:detached|connected|disconnected|adopted:true|connected|attribute:detached:null");
        page.Errors.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    [NonParallelizable]
    public async Task CompletedForeignDocumentWorkDoesNotRetainTemporaryDocuments(bool returnToPage)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><body></body>");
        await page.EvaluateAsync("""
            class TransientForeign extends HTMLElement {
              static observedAttributes=['a'];
              connectedCallback() {}
              disconnectedCallback() {}
              adoptedCallback(from,to) {}
              attributeChangedCallback() {}
            }
            customElements.define('x-transient-foreign',TransientForeign);
            var survivingForeignNode=null;
            """);
        var weak = await page.RunOnLoopAsync(engine => CreateTemporaryDocument(engine, returnToPage));
        // Drain scheduled jobs before collection; the page, registry and optional survivor stay alive.
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        weak.TryGetTarget(out _).Should().BeFalse();
        (await page.EvaluateAsync<bool>(returnToPage
            ? "survivingForeignNode.ownerDocument===document && survivingForeignNode instanceof TransientForeign"
            : "survivingForeignNode===null")).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Document> CreateTemporaryDocument(Engine engine, bool returnToPage)
    {
        var value = engine.Evaluate("""
            (()=>{
              const temporary=document.implementation.createHTMLDocument();
              const node=document.createElement('x-transient-foreign');
              document.body.append(node);temporary.adoptNode(node);temporary.body.append(node);
              node.setAttribute('a','foreign');
              RETURN
              return temporary;
            })()
            """.Replace("RETURN", returnToPage
                ? "document.adoptNode(node);document.body.append(node);survivingForeignNode=node;"
                : "", StringComparison.Ordinal));
        var document = (Document) ((DomNodeObject) value).Node!;
        // Reused constructor/callback environments can keep their last receiver/arguments alive.
        // Displace those independent references without disposing the page or registry under test.
        engine.Execute("""
            (()=>{
              const replacement=document.createElement('x-transient-foreign');
              const control=document.implementation.createHTMLDocument();
              document.body.append(replacement);control.adoptNode(replacement);control.body.append(replacement);
              replacement.setAttribute('a','control');
              document.adoptNode(replacement);document.body.append(replacement);replacement.remove();
            })();
            """);
        return new WeakReference<Document>(document);
    }
}
