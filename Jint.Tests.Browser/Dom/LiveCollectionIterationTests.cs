#nullable enable

using System.Collections;
using AngleSharp.Dom;
using Jint.Browser.Dom;
using Jint.Browser;
using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.Dom;

public sealed class LiveCollectionIterationTests
{
    [TestCase(250, false)]
    [TestCase(500, false)]
    [TestCase(1000, false)]
    [TestCase(2000, false)]
    [TestCase(250, true)]
    [TestCase(500, true)]
    [TestCase(1000, true)]
    [TestCase(2000, true)]
    public async Task StableIterationCountsCollectionOnce(int size, bool indexed)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='root'>" + string.Concat(Enumerable.Repeat("<i></i>", size)) + "</div>");
        CountingCollection? source = null;
        await page.RunOnLoopAsync(engine =>
        {
            source = new CountingCollection(PageRuntime.Find(engine)!.Document!.GetElementById("root")!.Children);
            engine.SetValue("counted", DomRealm.Of(engine).WrapCollection<IElement>(source));
            return 0;
        });
        var script = indexed
            ? "(() => { let n=0; for(let i=0;i<counted.length;i++) { if(counted[i]) n++; } return n; })()"
            : "[...counted].length";
        (await page.EvaluateAsync<int>(script)).Should().Be(size);
        source!.LengthReads.Should().Be(1);
        source.CountVisits.Should().Be(size);
        source.IndexVisits.Should().Be((long) size * (size + 1) / 2);
    }

    [TestCase(250)]
    [TestCase(500)]
    [TestCase(1000)]
    [TestCase(2000)]
    public async Task ScalingControlsHaveTheSameChecksum(int size)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='root'>" + string.Concat(Enumerable.Range(0, size).Select(i => $"<i id='{i}'></i>")) + "</div>");
        (await page.EvaluateAsync<string>($$"""
            (() => {
              const fixed = document.querySelectorAll('#root i');
              const array = Array.from({length: {{size}}}, (_,i) => i);
              let fixedSum = 0, arraySum = 0;
              for(let i=0;i<fixed.length;i++) fixedSum += Number(fixed[i].id);
              for(let i=0;i<array.length;i++) arraySum += array[i];
              return [fixedSum, [...fixed].reduce((s,n)=>s+Number(n.id),0),
                      arraySum, [...array].reduce((s,n)=>s+n,0)].join(',');
            })()
            """)).Should().Be(string.Join(',', Enumerable.Repeat((long) size * (size - 1) / 2, 4)));
    }

    private sealed class CountingCollection(IHtmlCollection<IElement> source) : IHtmlCollection<IElement>
    {
        internal int LengthReads { get; private set; }
        internal long CountVisits { get; private set; }
        internal long IndexVisits { get; private set; }
        public int Length
        {
            get
            {
                LengthReads++;
                var count = 0;
                foreach (var unused in source) { CountVisits++; count++; }
                return count;
            }
        }
        public int Count => Length;
        public IElement this[int index] => source[index];
        public IElement? this[string name] => source[name];
        public IEnumerator<IElement> GetEnumerator()
        {
            foreach (var item in source) { IndexVisits++; yield return item; }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private const string Markup = "<div id='root'><i id='a'></i>text<i id='b'></i><i id='c'></i></div>";

    [TestCase("root.appendChild(Object.assign(document.createElement('i'), {id:'d'}))", "a,b,c,d")]
    [TestCase("root.firstElementChild.remove()", "a,c")]
    [TestCase("root.lastElementChild.remove()", "a,b")]
    [TestCase("root.insertBefore(root.lastElementChild, root.firstElementChild)", "a,a,b")]
    [TestCase("root.replaceChildren()", "a")]
    public async Task IteratorStepsObserveMutation(string mutation, string expected)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        (await page.EvaluateAsync<string>($$"""
            (() => {
              const root = document.getElementById('root'), collection = root.children;
              const result = [], iterator = collection[Symbol.iterator]();
              result.push(iterator.next().value.id);
              {{mutation}};
              for (const node of iterator) result.push(node.id);
              return result.join(',');
            })()
            """)).Should().Be(expected);
    }

    [Test]
    public async Task LengthGetterCanMutateAndReenterIteration()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        (await page.EvaluateAsync<string>("""
            (() => {
              const root = document.getElementById('root'), c = root.children;
              const get = Object.getOwnPropertyDescriptor(HTMLCollection.prototype, 'length').get;
              let calls = 0, nested;
              Object.defineProperty(c, 'length', {get() {
                if (++calls === 2) {
                  root.lastElementChild.remove();
                  nested = [...root.children].map(x => x.id).join(',');
                }
                return get.call(c);
              }});
              return [...c].map(x => x.id).join(',') + '|' + nested + '|' + calls;
            })()
            """)).Should().Be("a,b|a,b|6");
    }

    [Test]
    public async Task PrototypeLengthAndIteratorOverridesRemainObservable()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        (await page.EvaluateAsync<string>("""
            (() => {
              const c = document.getElementById('root').children;
              const warm = [...c].length;
              Object.defineProperty(HTMLCollection.prototype, 'length', {get() {return 1;}});
              const shorter = [...c].map(x => x.id).join(',');
              HTMLCollection.prototype[Symbol.iterator] = function* () { yield 'override'; };
              return warm + '|' + shorter + '|' + [...c];
            })()
            """)).Should().Be("3|a|override");
    }

    [Test]
    public async Task ReentrantNativeWritesDoNotReuseCounts()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        await page.EvaluateAsync("var c = document.getElementById('root').children; c.length");
        (await page.RunOnLoopAsync(engine =>
        {
            var root = PageRuntime.Find(engine)!.Document!.GetElementById("root")!;
            var before = engine.Evaluate("c.length").AsNumber();
            root.RemoveChild(root.FirstChild!);
            var after = engine.Evaluate("c.length").AsNumber();
            return $"{before},{after}";
        })).Should().Be("3,2");
        (await page.EvaluateAsync<int>("c.length")).Should().Be(2);
    }

    [Test]
    public async Task ConfiguredNativeWriterCannotLeaveCachedLengthStale()
    {
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions().ConfigureEngine(_ => { }));
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        await page.RunOnLoopAsync(engine =>
        {
            var root = PageRuntime.Find(engine)!.Document!.GetElementById("root")!;
            engine.SetValue("nativeRemove", () => { root.RemoveChild(root.FirstChild!); });
            return 0;
        });
        (await page.EvaluateAsync<string>("var c = document.getElementById('root').children; var before = c.length; nativeRemove(); before + ',' + c.length"))
            .Should().Be("3,2");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DetachedCollectionsAndAttributeFilteredCollectionsStayLive(bool configure)
    {
        var options = new BrowserOptions();
        if (configure) options.ConfigureEngine(_ => { });
        await using var browser = new global::Jint.Browser.Browser(options);
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        (await page.EvaluateAsync<string>("""
            (() => {
              const root = document.getElementById('root'), c = root.children;
              const filtered = root.getElementsByClassName('match');
              const counts = [c.length, filtered.length];
              root.remove();
              root.lastElementChild.remove();
              root.firstElementChild.className = 'match';
              counts.push(c.length, filtered.length);
              const other = document.implementation.createHTMLDocument('other');
              other.body.appendChild(root);
              root.appendChild(other.createElement('i'));
              counts.push(c.length);
              return counts.join(',');
            })()
            """)).Should().Be("3,0,2,1,3");
    }

    [Test]
    public async Task ParserResumptionKeepsPreviouslyReadCollectionsLive()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<body><script>var c = document.body.children; var counts = [c.length];</script><i></i><script>counts.push(c.length);</script><b></b></body>");
        (await page.EvaluateAsync<string>("counts.push(c.length); counts.join(',')")).Should().Be("1,3,4");
    }

    [Test]
    public async Task CustomElementReactionReadsIntermediateAndFinalCounts()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<x-box id='root'><i></i><i></i></x-box>");
        (await page.EvaluateAsync<string>("""
            (() => {
              const root = document.getElementById('root'), c = root.children;
              const counts = [c.length];
              customElements.define('x-box', class extends HTMLElement {
                static get observedAttributes() { return ['data-x']; }
                attributeChangedCallback() {
                  counts.push(c.length);
                  this.firstElementChild.remove();
                  counts.push(c.length);
                  this.appendChild(document.createElement('b'));
                  counts.push(c.length);
                }
              });
              root.setAttribute('data-x', '1');
              counts.push(c.length);
              return counts.join(',');
            })()
            """)).Should().Be("2,2,1,2,2");
    }

    [Test]
    public async Task StaticNodeListAndPlainArrayControlsStayUnchanged()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        (await page.EvaluateAsync<string>("""
            (() => {
              const root = document.getElementById('root');
              const fixed = root.querySelectorAll('i');
              const a = [1,2,3], result = [];
              for (const v of a) { result.push(v); if(v===1) a.push(4); }
              root.replaceChildren();
              return [...fixed].map(x=>x.id).join(',') + '|' + result.join(',');
            })()
            """)).Should().Be("a,b,c|1,2,3,4");
    }
}
