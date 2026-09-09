namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <c>document.all</c>:
/// <a href="https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#htmlallcollection">HTML
/// §4.13.2.3's <c>HTMLAllCollection</c></a> and
/// <a href="https://tc39.es/ecma262/#sec-IsHTMLDDA-internal-slot">ECMAScript Annex B.3.6's
/// <c>[[IsHTMLDDA]]</c></a> internal slot.
/// </summary>
/// <remarks>
/// The two halves are one object and are tested as one: the slot is what makes twenty years of
/// <c>if (document.all)</c> browser sniffing take its other branch, and the collection is what the code
/// behind the surviving branch then reads. Everything here is what a browser answers; the markup is the
/// shape upstream's own <c>htmlallcollection.html</c> uses, so a row that moves here moves there too.
/// </remarks>
public sealed class HtmlAllCollectionTests
{
    private const string Markup = """
        <!doctype html><html id="root"><head><meta name="flags" content="TOKENS"></head>
        <body id="tags"><img name="picture"><a name="foo"></a><a name="foo"></a>
        <span id="42"></span><span id="043"></span>
        <div id="4294967294"></div><div id="4294967295"></div><div id="4294967296"></div>
        <div id="undefined"></div><div id="null"></div><div name="divwithname"></div><div id="-0"></div>
        </body></html>
        """;

    /// <summary>
    /// Annex B.3.6 makes the object behave like <c>undefined</c> in exactly three places — <c>typeof</c>,
    /// <c>ToBoolean</c> and loose equality with <c>null</c>/<c>undefined</c> — and nowhere else.
    /// </summary>
    /// <remarks>
    /// The loose comparisons appear twice on purpose: written against the literal they are fused at build
    /// time into one internal-type test, and written against a parameter they take the generic
    /// <c>IsLooselyEqual</c> path. Both have to answer the same thing, and only one of them did before.
    /// </remarks>
    [Test]
    public void TheObjectCarriesTheIsHtmlDdaInternalSlot()
    {
        using var fixture = DomTestFixture.Create(Markup);

        fixture.Text("typeof document.all").Should().Be("undefined");
        fixture.Bool("Boolean(document.all)").Should().BeFalse();
        fixture.Bool("!document.all").Should().BeTrue();
        fixture.Text("if (document.all) { 'then' } else { 'else' }").Should().Be("else");

        fixture.Bool("document.all == undefined").Should().BeTrue();
        fixture.Bool("document.all == null").Should().BeTrue();
        fixture.Bool("undefined == document.all").Should().BeTrue();
        fixture.Bool("null == document.all").Should().BeTrue();
        fixture.Bool("document.all != undefined").Should().BeFalse();
        fixture.Bool("document.all != null").Should().BeFalse();
        fixture.Bool("((a, b) => a == b)(document.all, undefined)").Should().BeTrue();
        fixture.Bool("((a, b) => a == b)(document.all, null)").Should().BeTrue();

        // Strict equality, `??` and `?.` are untouched: the slot is not nullish, it only compares as if it were.
        fixture.Bool("document.all === undefined").Should().BeFalse();
        fixture.Bool("document.all === null").Should().BeFalse();
        fixture.Bool("document.all !== undefined").Should().BeTrue();
        fixture.Number("(document.all ?? { length: -1 }).length").Should().Be(fixture.Number("document.all.length"));
        fixture.Number("document.all?.length").Should().Be(fixture.Number("document.all.length"));

        // A plain collection stays ordinary, so the slot is this object's and not every collection's.
        fixture.Text("typeof document.getElementsByTagName('div')").Should().Be("object");
        fixture.Bool("document.getElementsByTagName('div') == null").Should().BeFalse();
    }

    /// <summary>
    /// HTML's IDL declares <c>HTMLAllCollection</c> as a standalone interface, so its prototype inherits
    /// <c>Object.prototype</c> — not <c>HTMLCollection.prototype</c>, which is what AngleSharp's
    /// <c>IHtmlAllCollection : IHtmlCollection&lt;IElement&gt;</c> would otherwise have produced.
    /// </summary>
    [Test]
    public void TheInterfaceInheritsNothing()
    {
        using var fixture = DomTestFixture.Create(Markup);

        fixture.Bool("document.all instanceof HTMLAllCollection").Should().BeTrue();
        fixture.Bool("document.all instanceof HTMLCollection").Should().BeFalse();
        fixture.Bool("Object.getPrototypeOf(document.all) === HTMLAllCollection.prototype").Should().BeTrue();
        fixture.Bool("Object.getPrototypeOf(HTMLAllCollection.prototype) === Object.prototype").Should().BeTrue();
        fixture.Text("Object.prototype.toString.call(document.all)").Should().Be("[object HTMLAllCollection]");

        // WebIDL hangs @@iterator off the interface that *declares* indexed property support, which this one
        // now does itself, and the value is %Array.prototype.values% rather than a function of its own.
        fixture.Bool("HTMLAllCollection.prototype[Symbol.iterator] === Array.prototype[Symbol.iterator]").Should().BeTrue();
        fixture.Bool("[...document.all].length === document.all.length").Should().BeTrue();
        fixture.Bool("Array.from(document.all)[0] === document.documentElement").Should().BeTrue();
    }

    /// <summary>
    /// <c>[SameObject]</c>: one live collection for the life of the document, growing as the tree does.
    /// </summary>
    [Test]
    public void TheCollectionIsOneLiveObject()
    {
        using var fixture = DomTestFixture.Create("<!doctype html><html><body></body></html>");

        fixture.Bool("document.all === document.all").Should().BeTrue();
        var before = fixture.Number("document.all.length");
        fixture.Execute("document.body.appendChild(document.createElement('p'))");
        fixture.Number("document.all.length").Should().Be(before + 1);
        fixture.Bool("document.all[document.all.length - 1].tagName === 'P'").Should().BeTrue();
    }

    /// <summary>
    /// The indexed property getter, and the array-index rule that decides whether a key is an index at all:
    /// 2^32-2 is the last array index, so <c>"4294967295"</c> is a name and <c>"4294967294"</c> is an
    /// out-of-range index that answers nothing rather than falling through to the name.
    /// </summary>
    [Test]
    public void AnArrayIndexKeyNeverReachesTheNamedLookup()
    {
        using var fixture = DomTestFixture.Create(Markup);

        fixture.Bool("document.all[0] === document.documentElement").Should().BeTrue();
        fixture.Bool("document.all[-0] === document.documentElement").Should().BeTrue();
        fixture.Bool("document.all[document.all.length] === undefined").Should().BeTrue();
        fixture.Bool("document.all[-1] === undefined").Should().BeTrue();

        // An id that spells an in-range array index is still not reachable as a property.
        fixture.Bool("document.all['42'] === undefined").Should().BeTrue();
        fixture.Bool("document.all[4294967294] === undefined").Should().BeTrue();
        fixture.Bool("document.all['4294967294'] === undefined").Should().BeTrue();

        // ... and the three spellings that are NOT array indices are names.
        fixture.Text("document.all['043'].id").Should().Be("043");
        fixture.Text("document.all['4294967295'].id").Should().Be("4294967295");
        fixture.Text("document.all['4294967296'].id").Should().Be("4294967296");
        fixture.Text("document.all['-0'].id").Should().Be("-0");
        fixture.Text("document.all[undefined].id").Should().Be("undefined");
        fixture.Text("document.all[null].id").Should().Be("null");
    }

    /// <summary>
    /// The named property getter: an element's <c>id</c> always, and a <c>name</c> only on one of HTML's
    /// fourteen "all"-named elements — which is why a <c>&lt;div name&gt;</c> is invisible and an
    /// <c>&lt;img name&gt;</c> is not.
    /// </summary>
    [Test]
    public void OnlyAnAllNamedElementContributesItsName()
    {
        using var fixture = DomTestFixture.Create(Markup);

        fixture.Text("document.all['root'].tagName").Should().Be("HTML");
        fixture.Text("document.all.root.tagName").Should().Be("HTML");
        fixture.Text("document.all['picture'].tagName").Should().Be("IMG");
        fixture.Text("document.all['flags'].content").Should().Be("TOKENS");

        fixture.Bool("document.all['divwithname'] === undefined").Should().BeTrue();
        fixture.Bool("document.all.divwithname === undefined").Should().BeTrue();
        fixture.Bool("document.all[''] === undefined").Should().BeTrue();
        fixture.Bool("document.all['noname'] === undefined").Should().BeTrue();
    }

    /// <summary>
    /// A name matched by several elements answers a live <c>HTMLCollection</c>, and a new one per call — the
    /// one place this interface's named getter is not an element.
    /// </summary>
    [Test]
    public void SeveralMatchesAnswerANewLiveCollectionEachTime()
    {
        using var fixture = DomTestFixture.Create(Markup);

        fixture.Bool("""
            (() => {
              const anchors = document.querySelectorAll('a');
              const collections = [document.all['foo'], document.all.namedItem('foo'), document.all('foo'), document.all.item('foo')];
              for (let i = 0; i < collections.length; i++) {
                if (!(collections[i] instanceof HTMLCollection) || collections[i].length !== 2) return false;
                if (collections[i][0] !== anchors[0] || collections[i][1] !== anchors[1]) return false;
                for (let j = i + 1; j < collections.length; j++) {
                  if (collections[i] === collections[j]) return false;
                }
              }
              anchors[0].name = 'bar';
              return collections.every(c => c.length === 1);
            })()
            """).Should().BeTrue();
    }

    /// <summary>
    /// <c>namedItem(name)</c> is the named lookup with no array-index rule in front of it, so the id
    /// <c>"42"</c> it can find is precisely the one the property getter cannot. Its argument is required.
    /// </summary>
    [Test]
    public void NamedItemLooksUpNamesAndNothingElse()
    {
        using var fixture = DomTestFixture.Create(Markup);

        fixture.Number("document.all.namedItem.length").Should().Be(1);
        fixture.Text("document.all.namedItem('root').tagName").Should().Be("HTML");
        fixture.Text("document.all.namedItem('42').id").Should().Be("42");
        fixture.Text("document.all.namedItem('4294967294').id").Should().Be("4294967294");
        fixture.Text("document.all.namedItem('043').id").Should().Be("043");
        fixture.Text("document.all.namedItem(undefined).id").Should().Be("undefined");
        fixture.Text("document.all.namedItem(null).id").Should().Be("null");

        // null, not undefined, for a name the collection does not support.
        fixture.Bool("document.all.namedItem('') === null").Should().BeTrue();
        fixture.Bool("document.all.namedItem('noname') === null").Should().BeTrue();
        fixture.Bool("document.all.namedItem('divwithname') === null").Should().BeTrue();
        fixture.Bool("document.all.namedItem('0') === null").Should().BeTrue();

        fixture.Text("(() => { try { document.all.namedItem(); return 'no throw'; } catch (e) { return e.constructor.name; } })()")
            .Should().Be("TypeError");
    }

    /// <summary>
    /// <c>item(nameOrIndex)</c> takes an <em>optional</em> <c>DOMString</c>, so its <c>length</c> is 0 and an
    /// explicit <c>undefined</c> is "not provided" and answers <c>null</c> — where <c>namedItem(undefined)</c>
    /// above looks up the name <c>"undefined"</c>. Everything else is the indexed-or-named algorithm.
    /// </summary>
    [Test]
    public void ItemTakesANameOrAnIndexAndUndefinedIsNeither()
    {
        using var fixture = DomTestFixture.Create(Markup);

        fixture.Number("document.all.item.length").Should().Be(0);
        fixture.Bool("document.all.item() === null").Should().BeTrue();
        fixture.Bool("document.all.item(undefined) === null").Should().BeTrue();

        fixture.Bool("document.all.item(0) === document.documentElement").Should().BeTrue();
        fixture.Bool("document.all.item('0') === document.documentElement").Should().BeTrue();
        fixture.Bool("document.all.item(document.all.length) === null").Should().BeTrue();
        fixture.Bool("document.all.item('42') === null").Should().BeTrue();

        fixture.Text("document.all.item('root').tagName").Should().Be("HTML");
        fixture.Text("document.all.item('043').id").Should().Be("043");
        fixture.Text("document.all.item('4294967295').id").Should().Be("4294967295");
        fixture.Text("document.all.item(null).id").Should().Be("null");
        fixture.Bool("document.all.item('') === null").Should().BeTrue();
    }

    /// <summary>
    /// The legacy caller: <c>document.all(nameOrIndex)</c> is <c>item</c>, it ignores its <c>this</c>, and it
    /// is emphatically not a constructor — including through a <c>Proxy</c> whose handler declares one.
    /// </summary>
    [Test]
    public void TheLegacyCallerIsItemAndIsNotAConstructor()
    {
        using var fixture = DomTestFixture.Create(Markup);

        fixture.Text("document.all('root').tagName").Should().Be("HTML");
        fixture.Text("document.all('picture').tagName").Should().Be("IMG");
        fixture.Bool("document.all(0) === document.documentElement").Should().BeTrue();
        fixture.Bool("document.all('0') === document.documentElement").Should().BeTrue();
        fixture.Bool("document.all() === null").Should().BeTrue();
        fixture.Bool("document.all(undefined) === null").Should().BeTrue();
        fixture.Text("document.all(null).id").Should().Be("null");
        fixture.Bool("document.all('') === null").Should().BeTrue();
        fixture.Bool("document.all('42') === null").Should().BeTrue();
        fixture.Text("document.all('043').id").Should().Be("043");

        fixture.Bool("""
            [undefined, null, {}, document.body].every(t => Function.prototype.call.call(document.all, t, '043').id === '043')
            """).Should().BeTrue();

        fixture.Text("(() => { try { new document.all('picture'); return 'no throw'; } catch (e) { return e.constructor.name; } })()")
            .Should().Be("TypeError");
        fixture.Text("""
            (() => {
              try { new (new Proxy(document.all, { construct: () => ({}) })); return 'no throw'; }
              catch (e) { return e.constructor.name; }
            })()
            """).Should().Be("TypeError");
    }

    /// <summary>
    /// <c>[LegacyUnenumerableNamedProperties]</c>: a supported name is an own property that <c>in</c> and
    /// <c>getOwnPropertyNames</c> see and <c>Object.keys</c> does not — and a name spelling an array index is
    /// in neither, because the indexed half answers that key first.
    /// </summary>
    [Test]
    public void SupportedNamesAreOwnPropertiesAndNotEnumerable()
    {
        using var fixture = DomTestFixture.Create(Markup);

        fixture.Bool("'root' in document.all").Should().BeTrue();
        fixture.Bool("document.all.hasOwnProperty('picture')").Should().BeTrue();
        fixture.Bool("document.all.propertyIsEnumerable('root')").Should().BeFalse();
        fixture.Bool("Object.keys(document.all).every(k => String(Number(k)) === k)").Should().BeTrue();
        fixture.Bool("Object.getOwnPropertyNames(document.all).includes('root')").Should().BeTrue();
        fixture.Bool("Object.getOwnPropertyNames(document.all).includes('42')").Should().BeFalse();
        fixture.Bool("Object.getOwnPropertyNames(document.all).includes('divwithname')").Should().BeFalse();
    }

    /// <summary>
    /// The named-property visibility rule this collection shares with <c>HTMLCollection</c>: an ordinary own
    /// property created before the element arrives keeps its value, and <c>namedItem</c> looks through it.
    /// </summary>
    /// <remarks>
    /// A supported name is a non-writable own property, so an assignment <em>over</em> one is the ordinary
    /// refusal — silent in sloppy mode — which is why the property has to be created first for there to be
    /// anything to shadow with. That is what a browser answers too.
    /// </remarks>
    [Test]
    public void AnExpandoCreatedFirstShadowsTheLaterNameButNotNamedItem()
    {
        using var fixture = DomTestFixture.Create(Markup);

        fixture.Bool("""
            (() => {
              const all = document.all;
              all.later = 'own';
              const element = document.createElement('span');
              element.id = 'later';
              document.body.appendChild(element);
              if (all.later !== 'own') return false;
              if (all.namedItem('later') !== element) return false;
              if (Object.getOwnPropertyNames(all).filter(n => n === 'later').length !== 1) return false;
              if (!Object.keys(all).includes('later')) return false;
              delete all.later;
              return all.later === element && !Object.keys(all).includes('later');
            })()
            """).Should().BeTrue();

        // An assignment over a name the collection already supports is refused, the way every non-writable
        // own property is: sloppy mode ignores it, strict mode throws.
        fixture.Bool("(() => { document.all.root = 'own'; return document.all.root === document.documentElement; })()").Should().BeTrue();
        fixture.Text("""
            (() => { 'use strict'; try { document.all.root = 'own'; return 'no throw'; } catch (e) { return e.constructor.name; } })()
            """).Should().Be("TypeError");
    }
}
