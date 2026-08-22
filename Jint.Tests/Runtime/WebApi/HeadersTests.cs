#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The <c>Headers</c> class as the Fetch Standard specifies it —
/// https://fetch.spec.whatwg.org/#headers-class.
/// </summary>
public class HeadersTests
{
    private static Engine WebEngine() => new(options => options.UseFetch());

    private static JsValue Eval(string source) => WebEngine().Evaluate(source);

    [Fact]
    public void ConstructsEmptyFromNoArguments()
    {
        Eval("[...new Headers()].length").AsNumber().Should().Be(0);

        // init is optional with no default value, so an explicit undefined is still "not present".
        Eval("[...new Headers(undefined)].length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void FillsFromAPairSequence()
    {
        // https://fetch.spec.whatwg.org/#concept-headers-fill, the sequence arm.
        Eval("new Headers([['a', '1'], ['b', '2']]).get('a')").AsString().Should().Be("1");

        // Any iterable of pairs will do, because the union is resolved on @@iterator being callable.
        Eval("new Headers(new Map([['a', '1']])).get('a')").AsString().Should().Be("1");
        Eval("new Headers(new Headers({ a: '1' })).get('a')").AsString().Should().Be("1");
    }

    [Fact]
    public void RefusesASequenceElementThatIsNotAPair()
    {
        Assert.Throws<JavaScriptException>(() => Eval("new Headers([['a']])"))
            .Message.Should().Contain("name/value pair");

        Assert.Throws<JavaScriptException>(() => Eval("new Headers([['a', '1', 'x']])"))
            .Message.Should().Contain("name/value pair");

        Assert.Throws<JavaScriptException>(() => Eval("new Headers([1])"))
            .Message.Should().Contain("sequence");
    }

    [Fact]
    public void FillsFromARecordInOwnPropertyOrder()
    {
        // https://webidl.spec.whatwg.org/#es-record — own enumerable string-keyed properties, in own-key
        // order. Integer-like keys come first, which is observable through iteration order only after the
        // sort, so name order is what this checks.
        Eval("[...new Headers({ b: '2', a: '1' }).keys()].join(',')").AsString().Should().Be("a,b");

        // A non-enumerable own property is not a record member.
        Eval("[...new Headers(Object.defineProperty({}, 'a', { value: '1' })).keys()].length")
            .AsNumber().Should().Be(0);

        // Nor is an inherited one.
        Eval("[...new Headers(Object.create({ a: '1' })).keys()].length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void RefusesAnInitThatIsNotAnObject()
    {
        Assert.Throws<JavaScriptException>(() => Eval("new Headers('a: 1')"));
        Assert.Throws<JavaScriptException>(() => Eval("new Headers(1)"));
    }

    [Fact]
    public void MatchesNamesCaseInsensitively()
    {
        var engine = WebEngine();
        engine.Execute("var h = new Headers({ 'Content-Type': 'text/plain' });");

        engine.Evaluate("h.get('content-TYPE')").AsString().Should().Be("text/plain");
        engine.Evaluate("h.has('CONTENT-TYPE')").AsBoolean().Should().BeTrue();

        engine.Execute("h.delete('CoNtEnT-tYpE');");
        engine.Evaluate("h.has('content-type')").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void AppendCombinesWhereSetReplaces()
    {
        // https://fetch.spec.whatwg.org/#concept-header-list-get — values joined by ", ".
        Eval("(() => { const h = new Headers(); h.append('a', '1'); h.append('a', '2'); return h.get('a'); })()")
            .AsString().Should().Be("1, 2");

        // set replaces every one of them, and keeps the position the first had.
        Eval("(() => { const h = new Headers([['a','1'],['b','2'],['a','3']]); h.set('a', 'x'); return [...h].map(p => p.join(':')).join(','); })()")
            .AsString().Should().Be("a:x,b:2");
    }

    [Fact]
    public void GetAnswersNullForAnAbsentName()
    {
        Eval("new Headers().get('a')").Should().Be(JsValue.Null);
    }

    [Fact]
    public void NormalizesTheValueButRefusesInteriorNewlines()
    {
        // https://fetch.spec.whatwg.org/#concept-header-value-normalize strips the surrounding whitespace...
        Eval("new Headers({ a: '  1 \\t' }).get('a')").AsString().Should().Be("1");
        Eval("new Headers({ a: '\\r\\n 1 \\r\\n' }).get('a')").AsString().Should().Be("1");

        // ... and https://fetch.spec.whatwg.org/#concept-header-value refuses what is left carrying one.
        // This is the request-splitting defence: a value with a CRLF in it could start a second request.
        Assert.Throws<JavaScriptException>(() => Eval("new Headers({ a: '1\\r\\nX-Evil: 2' })"))
            .Message.Should().Contain("Invalid value");

        Assert.Throws<JavaScriptException>(() => Eval("new Headers({ a: '1\\u0000' })"))
            .Message.Should().Contain("Invalid value");
    }

    [Fact]
    public void RefusesANameThatIsNotAToken()
    {
        foreach (var name in new[] { "''", "'a b'", "'a:b'", "'a\\r\\nb'", "'ä'" })
        {
            Assert.Throws<JavaScriptException>(() => Eval($"new Headers().append({name}, '1')"));
            Assert.Throws<JavaScriptException>(() => Eval($"new Headers().get({name})"));
            Assert.Throws<JavaScriptException>(() => Eval($"new Headers().has({name})"));
            Assert.Throws<JavaScriptException>(() => Eval($"new Headers().delete({name})"));
        }

        // Every other tchar is fine, though.
        Eval("new Headers().has(\"!#$%&'*+-.^_`|~0aZ\")").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void RefusesACodeUnitAboveByteRange()
    {
        // https://webidl.spec.whatwg.org/#es-ByteString
        Assert.Throws<JavaScriptException>(() => Eval("new Headers().append('a', '\\u0100')"))
            .Message.Should().Contain("ByteString");
    }

    [Fact]
    public void DeletingAnAbsentNameIsNotAnError()
    {
        Eval("(() => { new Headers().delete('a'); return 'ok'; })()").AsString().Should().Be("ok");
    }

    [Fact]
    public void IteratesSortedLowercasedAndCombined()
    {
        // https://fetch.spec.whatwg.org/#concept-header-list-sort-and-combine
        Eval("[...new Headers([['B','2'],['a','1'],['A','3']])].map(p => p.join('=')).join('|')")
            .AsString().Should().Be("a=1, 3|b=2");

        Eval("[...new Headers({ 'X-Y': '1' }).keys()][0]").AsString().Should().Be("x-y");
    }

    [Fact]
    public void KeepsEverySetCookieValueSeparate()
    {
        // The one name the standard never combines, because a cookie value may itself contain a comma.
        var engine = WebEngine();
        engine.Execute("var h = new Headers([['set-cookie', 'a=1'], ['set-cookie', 'b=2']]);");

        engine.Evaluate("h.getSetCookie().join('|')").AsString().Should().Be("a=1|b=2");
        engine.Evaluate("[...h].length").AsNumber().Should().Be(2);
        engine.Evaluate("[...h].map(p => p[1]).join('|')").AsString().Should().Be("a=1|b=2");

        // get still combines it, which is what the standard says get does for every name.
        engine.Evaluate("h.get('set-cookie')").AsString().Should().Be("a=1, b=2");

        // getSetCookie is empty rather than absent when there is none.
        engine.Evaluate("new Headers().getSetCookie().length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void ExposesTheThreeIteratorsAndForEach()
    {
        var engine = WebEngine();
        engine.Execute("var h = new Headers([['b','2'],['a','1']]);");

        engine.Evaluate("[...h.keys()].join(',')").AsString().Should().Be("a,b");
        engine.Evaluate("[...h.values()].join(',')").AsString().Should().Be("1,2");
        engine.Evaluate("[...h.entries()].map(p => p.join('=')).join(',')").AsString().Should().Be("a=1,b=2");

        // forEach takes the value first and the name second, and the third argument is the object itself.
        engine.Evaluate("(() => { const out = []; h.forEach((v, n, o) => out.push(n + '=' + v + ':' + (o === h))); return out.join(','); })()")
            .AsString().Should().Be("a=1:true,b=2:true");
    }

    [Fact]
    public void SymbolIteratorIsTheSameFunctionAsEntries()
    {
        // https://webidl.spec.whatwg.org/#es-iterable — function identity a script can observe.
        Eval("Headers.prototype[Symbol.iterator] === Headers.prototype.entries").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void IterationIsLive()
    {
        // https://webidl.spec.whatwg.org/#es-default-iterator-object recomputes the pairs on every step, so
        // a header appended during iteration is seen.
        Eval(@"(() => {
                const h = new Headers([['a', '1']]);
                const out = [];
                for (const [n] of h) { out.push(n); if (n === 'a') h.append('b', '2'); }
                return out.join(',');
            })()").AsString().Should().Be("a,b");
    }

    [Fact]
    public void HasNoOwnPropertiesAndTheRightToStringTag()
    {
        Eval("Object.getOwnPropertyNames(new Headers()).length").AsNumber().Should().Be(0);
        Eval("Object.prototype.toString.call(new Headers())").AsString().Should().Be("[object Headers]");
    }

    [Fact]
    public void BrandChecksEveryMember()
    {
        foreach (var member in new[] { "append('a','1')", "delete('a')", "get('a')", "getSetCookie()", "has('a')", "set('a','1')", "forEach(() => {})", "entries()", "keys()", "values()" })
        {
            Assert.Throws<JavaScriptException>(() => Eval($"Headers.prototype.{member}"))
                .Message.Should().Contain("Headers");
        }
    }

    [Fact]
    public void IsConstructibleAsABaseClass()
    {
        Eval("(() => { class H extends Headers {}; const h = new H([['a','1']]); return h.get('a') + ':' + (h instanceof H); })()")
            .AsString().Should().Be("1:true");
    }

    [Fact]
    public void RequiresNew()
    {
        Assert.Throws<JavaScriptException>(() => Eval("Headers()"))
            .Message.Should().Contain("requires 'new'");
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
        engine.Execute("var proto = Object.getPrototypeOf(new Headers().entries());");

        engine.Evaluate("Object.getOwnPropertyNames(proto).join(',')").AsString().Should().Be("next");
        engine.Execute("var d = Object.getOwnPropertyDescriptor(proto, 'next');");
        engine.Evaluate("d.writable").AsBoolean().Should().BeTrue("next must be writable");
        engine.Evaluate("d.enumerable").AsBoolean().Should().BeTrue("next must be enumerable");
        engine.Evaluate("d.configurable").AsBoolean().Should().BeTrue("next must be configurable");
        engine.Evaluate("d.value.length").AsNumber().Should().Be(0);
        engine.Evaluate("d.value.name").AsString().Should().Be("next");

        // The three kinds share one prototype, so the attributes above are the attributes of all of them.
        engine.Evaluate("Object.getPrototypeOf(new Headers().keys()) === proto").AsBoolean().Should().BeTrue("the three kinds share one prototype");
        engine.Evaluate("Object.getPrototypeOf(new Headers().values()) === proto").AsBoolean().Should().BeTrue("the three kinds share one prototype");

        // The class string is the object's only other own property, and it keeps the attributes a class
        // string carries — { [[Writable]]: false, [[Enumerable]]: false, [[Configurable]]: true } — rather
        // than WebIDL's operation attributes.
        engine.Evaluate("Object.getOwnPropertySymbols(proto).length").AsNumber().Should().Be(1);
        engine.Execute("var t = Object.getOwnPropertyDescriptor(proto, Symbol.toStringTag);");
        engine.Evaluate("t.value").AsString().Should().Be("Headers Iterator");
        engine.Evaluate("t.writable").AsBoolean().Should().BeFalse();
        engine.Evaluate("t.enumerable").AsBoolean().Should().BeFalse();
        engine.Evaluate("t.configurable").AsBoolean().Should().BeTrue("the class string is configurable");
    }
}
#endif
