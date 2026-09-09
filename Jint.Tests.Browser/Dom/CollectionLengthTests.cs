using Jint.Browser.Dom;
using Jint.Browser.Dom.Collections;
using Jint.Native.Object;

namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <c>length</c> on a DOM collection is a prototype accessor, as WebIDL requires, and the engine answers it
/// from the wrapper's own count while nothing has redefined it. What is asserted here is that the shortcut is
/// invisible: every way a page can make that accessor answer something else still wins, on every collection
/// interface a document can produce.
/// </summary>
/// <remarks>
/// The three tamper shapes are the ones
/// <c>dom/nodes/NodeList-static-length-getter-tampered-{1,2,3}.html</c> define, and those documents are in the
/// vendored corpus, so they run against this too. They are also the slowest documents in the whole lane, which
/// is where this work started: they spend most of upstream's harness budget on
/// <c>for (var j = 0; j &lt; nodeList.length; j++) nodeList[j]</c>.
/// </remarks>
public sealed class CollectionLengthTests
{
    private const string Markup = """
        <!doctype html>
        <html><head><style>.a { color: red }</style></head>
        <body>
          <form id="f"><input name="one"><input name="two"></form>
          <div id="root" class="a b c" data-x="1" data-y="2">
            <span class="foo"></span><span class="foo"></span><span class="foo"></span>
          </div>
          <select id="s"><option>1</option><option>2</option></select>
        </body></html>
        """;

    private static readonly TestCases<string, int> _collections = new()
    {
        { "document.querySelectorAll('.foo')", 3 },          // NodeList  (static)
        { "root.childNodes", 5 },                            // NodeList  (live)
        { "root.children", 3 },                              // HTMLCollection
        { "document.getElementsByTagName('span')", 3 },      // HTMLCollection (live)
        { "root.classList", 3 },                             // DOMTokenList
        { "root.attributes", 4 },                            // NamedNodeMap
        { "document.styleSheets", 1 },                       // StyleSheetList
        { "document.styleSheets[0].cssRules", 1 },           // CSSRuleList
        { "document.getElementById('f').elements", 2 },      // HTMLFormControlsCollection
        { "document.getElementById('s').options", 2 },       // HTMLOptionsCollection
    };

    [TestCaseSource(nameof(_collections))]
    public void EveryCollectionAnswersItsOwnCount(string source, int expected)
    {
        using var fixture = DomTestFixture.Create(Markup);
        fixture.Execute("var root = document.getElementById('root');");

        fixture.Number($"({source}).length").Should().Be(expected);

        // and the loop the whole lane exists for reads the same count, one element at a time
        fixture.Number($"(function () {{ var c = {source}, n = 0; for (var i = 0; i < c.length; i++) {{ n++; }} return n; }})()")
            .Should().Be(expected);
    }

    [TestCaseSource(nameof(_collections))]
    public void AnOwnLengthOnTheCollectionWins(string source, int expected)
    {
        using var fixture = DomTestFixture.Create(Markup);
        fixture.Execute("var root = document.getElementById('root');");
        fixture.Execute($"var c = {source};");

        fixture.Number("c.length").Should().Be(expected);
        fixture.Execute("Object.defineProperty(c, 'length', { configurable: true, get: function () { return 1; } });");

        fixture.Number("c.length").Should().Be(1);
        fixture.Number("(function () { var n = 0; for (var i = 0; i < c.length; i++) { n++; } return n; })()").Should().Be(1);
        fixture.Number("Array.prototype.indexOf.call(c, c[0])").Should().Be(0);
        fixture.Number("[].slice.call(c).length").Should().Be(1);
    }

    [TestCaseSource(nameof(_collections))]
    public void RePointingThePrototypeWins(string source, int expected)
    {
        using var fixture = DomTestFixture.Create(Markup);
        fixture.Execute("var root = document.getElementById('root');");
        fixture.Execute($"var c = {source};");

        fixture.Number("c.length").Should().Be(expected);
        fixture.Execute("Object.setPrototypeOf(c, { get length() { return 1; } });");

        fixture.Number("c.length").Should().Be(1);
        fixture.Number("(function () { var n = 0; for (var i = 0; i < c.length; i++) { n++; } return n; })()").Should().Be(1);
    }

    [TestCaseSource(nameof(_collections))]
    public void RedefiningTheAccessorOnTheInterfacePrototypeWins(string source, int expected)
    {
        using var fixture = DomTestFixture.Create(Markup);
        fixture.Execute("var root = document.getElementById('root');");
        fixture.Execute($"var c = {source};");

        fixture.Number("c.length").Should().Be(expected);
        fixture.Execute("""
            var proto = Object.getPrototypeOf(c);
            Object.defineProperty(proto, 'length', { configurable: true, get: function () { return 1; } });
            """);

        fixture.Number("c.length").Should().Be(1);
        fixture.Number("(function () { var n = 0; for (var i = 0; i < c.length; i++) { n++; } return n; })()").Should().Be(1);
        fixture.Number("Array.prototype.indexOf.call(c, c[0])").Should().Be(0);
    }

    [Test]
    public void TheAccessorIsStillTheOneWebIdlDescribes()
    {
        using var fixture = DomTestFixture.Create(Markup);

        // The whole lane rests on the accessor being where it always was: reading the collection's own count
        // instead of invoking it must not have turned `length` into an own property, or moved its attributes.
        fixture.Bool("""
            (function () {
              var c = document.querySelectorAll('.foo');
              if (c.hasOwnProperty('length')) return false;
              var d = Object.getOwnPropertyDescriptor(NodeList.prototype, 'length');
              if (typeof d.get !== 'function' || d.set !== undefined) return false;
              if (!d.enumerable || !d.configurable) return false;
              if (d.get.name !== 'get length' || d.get.length !== 0) return false;
              if (d.get.call(c) !== 3) return false;
              return Object.getOwnPropertyNames(c).indexOf('length') === -1;
            })()
            """).Should().BeTrue();
    }

    [Test]
    public void AnIndexedOwnPropertyIsStillRefusedAndChangesNoLength()
    {
        using var fixture = DomTestFixture.Create(Markup);

        // WebIDL's [[DefineOwnProperty]] refusal for a canonical array index is what keeps the projection and
        // the property bag from disagreeing, and nothing about reading `length` from the host's own count may
        // have given an index a way in.
        fixture.Bool("""
            (function () {
              var c = document.querySelectorAll('.foo'), first = c[0];
              if (Reflect.defineProperty(c, 0, { value: 'x' })) return false;
              if (Reflect.defineProperty(c, 9, { value: 'x' })) return false;
              if (c[0] !== first || c.length !== 3) return false;
              c[0] = 'x';
              return c[0] === first && c.length === 3 && !c.hasOwnProperty('length');
            })()
            """).Should().BeTrue();
    }

    [Test]
    public void TheCollectionIsStillLive()
    {
        using var fixture = DomTestFixture.Create(Markup);

        fixture.Bool("""
            (function () {
              var root = document.getElementById('root');
              var live = root.children, statik = document.querySelectorAll('.foo');
              if (live.length !== 3 || statik.length !== 3) return false;
              root.appendChild(document.createElement('span'));
              if (live.length !== 4 || statik.length !== 3) return false;
              root.removeChild(root.lastElementChild);
              root.removeChild(root.lastElementChild);
              return live.length === 2 && statik.length === 3;
            })()
            """).Should().BeTrue();
    }

    [Test]
    public void WrapperIdentityIsUnchangedByTheIndexedReads()
    {
        using var fixture = DomTestFixture.Create(Markup);

        fixture.Bool("""
            (function () {
              var c = document.querySelectorAll('.foo');
              if (c[0] !== c[0]) return false;
              var seen = c[1];
              for (var i = 0; i < c.length; i++) { if (c[i] !== c[i]) return false; }
              return c[1] === seen && c[1] === document.querySelectorAll('.foo')[1];
            })()
            """).Should().BeTrue();
    }

    [Test]
    public void AProxyOverACollectionRunsItsTraps()
    {
        using var fixture = DomTestFixture.Create(Markup);

        fixture.Bool("""
            (function () {
              var c = document.querySelectorAll('.foo');
              var reads = [];
              var p = new Proxy(c, { get: function (t, k, r) { reads.push(String(k)); return k === 'length' ? 1 : Reflect.get(t, k, r); } });
              if (p.length !== 1) return false;
              var n = 0; for (var i = 0; i < p.length; i++) { n++; }
              return n === 1 && reads.indexOf('length') >= 0;
            })()
            """).Should().BeTrue();
    }

    /// <summary>
    /// Every collection interface's prototype declares a <c>length</c> accessor, and
    /// <c>DomRealm</c> captured exactly that function as the one the engine's lane compares against — the
    /// claim <c>DomCollectionBase.PristineLengthGetter</c> makes on behalf of the whole binding.
    /// </summary>
    [Test]
    public void EveryCollectionInterfaceCapturedItsOwnDeclaredAccessor()
    {
        using var fixture = DomTestFixture.Create(Markup);
        var realm = DomRealm.Of(fixture.Engine);

        var collectionInterfaces = 0;
        foreach (var definition in DomInterfaces.All)
        {
            if (definition.WrapperKind is not (DomWrapperKind.Collection or DomWrapperKind.HtmlCollection))
            {
                continue;
            }

            collectionInterfaces++;
            var prototype = realm.PrototypeOf(definition);
            var declared = prototype.GetOwnProperty("length").Get as ObjectInstance;

            realm.PristineLengthGetterOf(definition).Should().BeSameAs(
                declared,
                $"{definition.Name}.prototype.length must be the accessor DomRealm captured");
        }

        collectionInterfaces.Should().BeGreaterThan(10);
    }
}
