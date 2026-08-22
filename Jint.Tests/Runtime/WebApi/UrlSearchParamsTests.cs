#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The <c>URLSearchParams</c> class as the WHATWG URL Standard specifies it —
/// https://url.spec.whatwg.org/#urlsearchparams.
/// </summary>
public class UrlSearchParamsTests
{
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Url));

    [Fact]
    public void ConstructsFromAString()
    {
        var engine = WebEngine();

        engine.Evaluate("new URLSearchParams().toString()").AsString().Should().Be("");
        engine.Evaluate("new URLSearchParams('').toString()").AsString().Should().Be("");
        engine.Evaluate("new URLSearchParams('a=b').toString()").AsString().Should().Be("a=b");

        // A single leading "?" is removed, and only one.
        engine.Evaluate("new URLSearchParams('?a=b').toString()").AsString().Should().Be("a=b");
        engine.Evaluate("new URLSearchParams('??a=b').toString()").AsString().Should().Be("%3Fa=b");

        // Null is not an object, so it takes the string arm of the union.
        engine.Evaluate("new URLSearchParams(null).toString()").AsString().Should().Be("null=");
    }

    [Fact]
    public void AppliesFormUrlEncodedParsingRules()
    {
        var engine = WebEngine();
        engine.Execute("const p = new URLSearchParams('&a&&& &&&&&a+b=& c&m%c3%b8%c3%b8');");

        engine.Evaluate("p.has('a')").AsBoolean().Should().BeTrue();
        engine.Evaluate("p.has('a b')").AsBoolean().Should().BeTrue();
        engine.Evaluate("p.has(' ')").AsBoolean().Should().BeTrue();
        engine.Evaluate("p.has('c')").AsBoolean().Should().BeFalse();
        engine.Evaluate("p.has(' c')").AsBoolean().Should().BeTrue();
        engine.Evaluate("p.has('møø')").AsBoolean().Should().BeTrue();

        // A "%" that is not followed by two hex digits stays a literal "%".
        engine.Evaluate("new URLSearchParams('b=%2sf%2a').get('b')").AsString().Should().Be("%2sf*");
        engine.Evaluate("new URLSearchParams('b=%%2a').get('b')").AsString().Should().Be("%*");

        // "+" is a space, and an encoded "+" is a plus.
        engine.Evaluate("new URLSearchParams('a=b+c').get('a')").AsString().Should().Be("b c");
        engine.Evaluate("new URLSearchParams('a=%2B').get('a')").AsString().Should().Be("+");
    }

    [Fact]
    public void ConstructsFromASequenceOfPairs()
    {
        var engine = WebEngine();

        engine.Evaluate("new URLSearchParams([]).toString()").AsString().Should().Be("");
        engine.Evaluate("new URLSearchParams([['a', 'b'], ['c', 'd']]).toString()").AsString().Should().Be("a=b&c=d");

        // An inner sequence whose size is not 2 is a TypeError, and so is an element that is not a sequence.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new URLSearchParams([[1]])"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new URLSearchParams([[1, 2, 3]])"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new URLSearchParams([1])"));

        // Anything iterable is a sequence, including another URLSearchParams — and the pairs are copied, so
        // later updates to the source are not observable.
        engine.Execute("const seed = new URLSearchParams('a=b&c=d'); const copy = new URLSearchParams(seed); seed.append('e', 'f');");
        engine.Evaluate("copy.toString()").AsString().Should().Be("a=b&c=d");

        // An own @@iterator wins, exactly as the union conversion says.
        engine.Execute("const custom = new URLSearchParams(); custom[Symbol.iterator] = function* () { yield ['a', 'b']; };");
        engine.Evaluate("new URLSearchParams(custom).get('a')").AsString().Should().Be("b");
    }

    [Fact]
    public void ConstructsFromARecord()
    {
        var engine = WebEngine();

        engine.Evaluate("new URLSearchParams({}).toString()").AsString().Should().Be("");
        engine.Evaluate("new URLSearchParams({ c: 'x', a: '?' }).toString()").AsString().Should().Be("c=x&a=%3F");

        // Non-enumerable own properties and symbol keys are not part of a record.
        engine.Execute("""
            const source = Object.defineProperties({}, {
                visible: { value: '1', enumerable: true },
                hidden: { value: '2', enumerable: false },
            });
            source[Symbol('s')] = '3';
            """);
        engine.Evaluate("new URLSearchParams(source).toString()").AsString().Should().Be("visible=1");

        // A record is an ordered map: two keys that convert to the same USVString collapse to one entry,
        // holding the last value at the first key's position.
        engine.Evaluate("new URLSearchParams({ '\\uD835x': '1', 'xx': '2', '\\uD83Dx': '3' }).toString()")
            .AsString().Should().Be("%EF%BF%BDx=3&xx=2");
    }

    [Fact]
    public void AppendsAndReadsBack()
    {
        var engine = WebEngine();
        engine.Execute("const p = new URLSearchParams(); p.append('a', '1'); p.append('a', '2'); p.append('b', '3');");

        engine.Evaluate("p.get('a')").AsString().Should().Be("1");
        engine.Evaluate("p.getAll('a').join(',')").AsString().Should().Be("1,2");
        engine.Evaluate("p.getAll('missing').length").AsNumber().Should().Be(0);
        engine.Evaluate("Array.isArray(p.getAll('a'))").AsBoolean().Should().BeTrue();
        engine.Evaluate("p.get('missing')").IsNull().Should().BeTrue();
        engine.Evaluate("p.size").AsNumber().Should().Be(3);
    }

    [Fact]
    public void SetReplacesTheFirstMatchAndRemovesTheRest()
    {
        var engine = WebEngine();
        engine.Execute("const p = new URLSearchParams('a=1&b=2&a=3&c=4'); p.set('a', 'x');");

        engine.Evaluate("p.toString()").AsString().Should().Be("a=x&b=2&c=4");

        // A name that is not there is appended at the end.
        engine.Execute("p.set('d', 'y')");
        engine.Evaluate("p.toString()").AsString().Should().Be("a=x&b=2&c=4&d=y");
    }

    [Fact]
    public void DeleteAndHasTakeAnOptionalValue()
    {
        var engine = WebEngine();
        engine.Execute("const p = new URLSearchParams('a=b&a=d&c&e&');");

        engine.Evaluate("p.has('a', 'b')").AsBoolean().Should().BeTrue();
        engine.Evaluate("p.has('a', 'c')").AsBoolean().Should().BeFalse();
        engine.Evaluate("p.has('e', '')").AsBoolean().Should().BeTrue();

        // An explicitly passed undefined means "not given", which is what an optional argument with no default
        // value means in WebIDL.
        engine.Evaluate("p.has('a', undefined)").AsBoolean().Should().BeTrue();

        engine.Execute("p.delete('a', 'b')");
        engine.Evaluate("p.has('a', 'd')").AsBoolean().Should().BeTrue();
        engine.Evaluate("p.getAll('a').length").AsNumber().Should().Be(1);

        engine.Execute("p.delete('a', undefined)");
        engine.Evaluate("p.has('a')").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void SortsByNameStablyInCodeUnitOrder()
    {
        var engine = WebEngine();

        // Equal names keep their relative order, which is what makes getAll survive a sort.
        engine.Execute("const p = new URLSearchParams('b=1&a=first&c=3&a=second&a=third'); p.sort();");
        engine.Evaluate("p.toString()").AsString().Should().Be("a=first&a=second&a=third&b=1&c=3");
        engine.Evaluate("p.getAll('a').join(',')").AsString().Should().Be("first,second,third");

        // Code-unit order, not a culture-aware or code-point one: U+FF3A is above every ASCII letter.
        engine.Execute("const q = new URLSearchParams('\\uFF3A=1&B=2&a=3'); q.sort();");
        engine.Evaluate("[...q.keys()].join(',')").AsString().Should().Be("B,a,Ｚ");
    }

    [Fact]
    public void IteratesInListOrder()
    {
        var engine = WebEngine();
        engine.Execute("const p = new URLSearchParams('a=1&b=2');");

        engine.Evaluate("[...p].map(e => e.join(':')).join(',')").AsString().Should().Be("a:1,b:2");
        engine.Evaluate("[...p.entries()].map(e => e.join(':')).join(',')").AsString().Should().Be("a:1,b:2");
        engine.Evaluate("[...p.keys()].join(',')").AsString().Should().Be("a,b");
        engine.Evaluate("[...p.values()].join(',')").AsString().Should().Be("1,2");

        // The identity the iterable declaration requires.
        engine.Evaluate("URLSearchParams.prototype[Symbol.iterator] === URLSearchParams.prototype.entries")
            .AsBoolean().Should().BeTrue();

        // The iterator inherits %IteratorPrototype%, so the iterator helpers work on it.
        engine.Evaluate("p.keys().toArray().join(',')").AsString().Should().Be("a,b");
        engine.Evaluate("Object.prototype.toString.call(p.keys())").AsString().Should().Be("[object URLSearchParams Iterator]");
    }

    [Fact]
    public void IteratesTheLiveList()
    {
        var engine = WebEngine();
        engine.Execute("""
            const p = new URLSearchParams('a=1&b=2&c=3');
            const seen = [];
            for (const [name] of p) {
                seen.push(name);
                if (name === 'a') { p.delete('b'); }
            }
            """);

        // Deleting the entry the cursor is about to reach shifts the rest down under it, which is what an
        // index-based walk over the live list does.
        engine.Evaluate("seen.join(',')").AsString().Should().Be("a,c");
    }

    [Fact]
    public void ForEachTakesTheValueBeforeTheName()
    {
        var engine = WebEngine();
        engine.Execute("""
            const p = new URLSearchParams('a=1&b=2');
            const seen = [];
            const thisValues = [];
            p.forEach(function (value, name, target) {
                seen.push(name + '=' + value + '(' + (target === p) + ')');
                thisValues.push(this);
            }, 'the this arg');
            """);

        engine.Evaluate("seen.join(',')").AsString().Should().Be("a=1(true),b=2(true)");
        engine.Evaluate("thisValues[0].toString()").AsString().Should().Be("the this arg");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new URLSearchParams().forEach(42)"));
    }

    [Fact]
    public void SerializesAsFormUrlEncoded()
    {
        var engine = WebEngine();

        engine.Evaluate("new URLSearchParams([['a b', 'c d']]).toString()").AsString().Should().Be("a+b=c+d");
        engine.Evaluate("new URLSearchParams([['~', '*']]).toString()").AsString().Should().Be("%7E=*");
        engine.Evaluate("new URLSearchParams([['a', '\\u00F8']]).toString()").AsString().Should().Be("a=%C3%B8");
        engine.Evaluate("new URLSearchParams([['a', '\\uD83D\\uDCA9']]).toString()").AsString().Should().Be("a=%F0%9F%92%A9");

        // A lone surrogate is a USVString conversion away from U+FFFD.
        engine.Evaluate("new URLSearchParams([['a', '\\uD83D']]).toString()").AsString().Should().Be("a=%EF%BF%BD");
    }

    [Fact]
    public void CoercesEveryArgumentToAString()
    {
        var engine = WebEngine();
        engine.Execute("const p = new URLSearchParams(); p.append(1, 2); p.append(null, undefined);");

        engine.Evaluate("p.toString()").AsString().Should().Be("1=2&null=undefined");
        engine.Evaluate("p.has(1)").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void EveryMemberBrandChecksItsReceiver()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("URLSearchParams.prototype.toString()"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("URLSearchParams.prototype.size"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("URLSearchParams.prototype.append.call({}, 'a', 'b')"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("URLSearchParams.prototype.entries.call({})"));
    }

    [Fact]
    public void CarriesTheInterfaceObjectShapeWebIdlAsksFor()
    {
        var engine = WebEngine();

        engine.Evaluate("URLSearchParams.name").AsString().Should().Be("URLSearchParams");
        engine.Evaluate("URLSearchParams.length").AsNumber().Should().Be(0);
        engine.Evaluate("URLSearchParams.prototype.constructor === URLSearchParams").AsBoolean().Should().BeTrue();
        engine.Evaluate("URLSearchParams.prototype[Symbol.toStringTag]").AsString().Should().Be("URLSearchParams");
        engine.Evaluate("Object.prototype.toString.call(new URLSearchParams())").AsString().Should().Be("[object URLSearchParams]");

        engine.Evaluate("URLSearchParams.prototype.append.length").AsNumber().Should().Be(2);
        engine.Evaluate("URLSearchParams.prototype.delete.length").AsNumber().Should().Be(1);
        engine.Evaluate("URLSearchParams.prototype.has.length").AsNumber().Should().Be(1);
        engine.Evaluate("URLSearchParams.prototype.set.length").AsNumber().Should().Be(2);
        engine.Evaluate("URLSearchParams.prototype.sort.length").AsNumber().Should().Be(0);

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("URLSearchParams('a=b')"));
    }

    [Fact]
    public void SupportsSubclassing()
    {
        var engine = WebEngine();
        engine.Execute("""
            class MyParams extends URLSearchParams {
                get first() { return this.get('a'); }
            }
            const sub = new MyParams('a=1');
            """);

        engine.Evaluate("sub instanceof URLSearchParams").AsBoolean().Should().BeTrue();
        engine.Evaluate("sub.first").AsString().Should().Be("1");
        engine.Evaluate("sub.toString()").AsString().Should().Be("a=1");
    }

    [Fact]
    public void TheIteratorPrototypesNextCarriesWebIdlsAttributes()
    {
        // "An iterator prototype object must have a next data property with attributes
        // { [[Writable]]: true, [[Enumerable]]: true, [[Configurable]]: true } and whose value is the
        // built-in function object CreateBuiltinFunction(nextSteps, 0, "next", <<>>)" —
        // https://webidl.spec.whatwg.org/#es-iterator-prototype-object. Enumerable is the surprise: a
        // built-in function property is non-enumerable everywhere in ECMA-262
        // (https://tc39.es/ecma262/#sec-ecmascript-standard-built-in-objects), and WebIDL is the one binding
        // that says otherwise.
        var engine = WebEngine();
        engine.Execute("var proto = Object.getPrototypeOf(new URLSearchParams().entries());");

        engine.Evaluate("Object.getOwnPropertyNames(proto).join(',')").AsString().Should().Be("next");
        engine.Execute("var d = Object.getOwnPropertyDescriptor(proto, 'next');");
        engine.Evaluate("d.writable").AsBoolean().Should().BeTrue("next must be writable");
        engine.Evaluate("d.enumerable").AsBoolean().Should().BeTrue("next must be enumerable");
        engine.Evaluate("d.configurable").AsBoolean().Should().BeTrue("next must be configurable");
        engine.Evaluate("d.value.length").AsNumber().Should().Be(0);
        engine.Evaluate("d.value.name").AsString().Should().Be("next");

        // The three kinds share one prototype, so the attributes above are the attributes of all of them.
        engine.Evaluate("Object.getPrototypeOf(new URLSearchParams().keys()) === proto").AsBoolean().Should().BeTrue("the three kinds share one prototype");
        engine.Evaluate("Object.getPrototypeOf(new URLSearchParams().values()) === proto").AsBoolean().Should().BeTrue("the three kinds share one prototype");

        // The class string is the object's only other own property, and it keeps the attributes a class
        // string carries — { [[Writable]]: false, [[Enumerable]]: false, [[Configurable]]: true } — rather
        // than WebIDL's operation attributes.
        engine.Evaluate("Object.getOwnPropertySymbols(proto).length").AsNumber().Should().Be(1);
        engine.Execute("var t = Object.getOwnPropertyDescriptor(proto, Symbol.toStringTag);");
        engine.Evaluate("t.value").AsString().Should().Be("URLSearchParams Iterator");
        engine.Evaluate("t.writable").AsBoolean().Should().BeFalse();
        engine.Evaluate("t.enumerable").AsBoolean().Should().BeFalse();
        engine.Evaluate("t.configurable").AsBoolean().Should().BeTrue("the class string is configurable");
    }
}
#endif
