#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>FormData</c> as the XMLHttpRequest Standard specifies it —
/// https://xhr.spec.whatwg.org/#interface-formdata.
/// </summary>
public class FormDataTests
{
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Files));

    private static JsValue Eval(string source) => WebEngine().Evaluate(source);

    private static Engine WithEntries()
    {
        var engine = WebEngine();
        engine.Execute("var fd = new FormData(); fd.append('a', '1'); fd.append('b', '2'); fd.append('a', '3');");
        return engine;
    }

    [Fact]
    public void StartsEmptyAndTakesNoForm()
    {
        Eval("[...new FormData()].length").AsNumber().Should().Be(0);

        // There is no DOM, so there is no form to scrape — and a passed argument is refused rather than
        // silently ignored.
        Assert.Throws<JavaScriptException>(() => Eval("new FormData(null)"));
        Assert.Throws<JavaScriptException>(() => Eval("new FormData({})"));
        Assert.Throws<JavaScriptException>(() => Eval("new FormData(1)"));

        // An explicitly passed undefined is a missing optional argument, so it is fine.
        Eval("[...new FormData(undefined)].length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void RequiresNew()
    {
        Assert.Throws<JavaScriptException>(() => Eval("FormData()"));
    }

    [Fact]
    public void AppendsInOrderAndKeepsDuplicates()
    {
        var engine = WithEntries();

        // https://xhr.spec.whatwg.org/#dom-formdata-append — the entry list is ordered and admits
        // duplicate names.
        engine.Evaluate("JSON.stringify([...fd])").AsString().Should().Be("""[["a","1"],["b","2"],["a","3"]]""");
    }

    [Fact]
    public void GetAnswersTheFirstMatchAndNullOtherwise()
    {
        var engine = WithEntries();

        engine.Evaluate("fd.get('a')").AsString().Should().Be("1");
        engine.Evaluate("fd.get('missing')").IsNull().Should().BeTrue();
    }

    [Fact]
    public void GetAllAnswersEveryMatchInOrder()
    {
        var engine = WithEntries();

        engine.Evaluate("fd.getAll('a').join(',')").AsString().Should().Be("1,3");
        engine.Evaluate("Array.isArray(fd.getAll('missing')) && fd.getAll('missing').length === 0").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void HasAnswersWhetherAnyEntryCarriesTheName()
    {
        var engine = WithEntries();

        engine.Evaluate("fd.has('a')").AsBoolean().Should().BeTrue();
        engine.Evaluate("fd.has('missing')").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void DeleteRemovesEveryEntryWithTheName()
    {
        var engine = WithEntries();
        engine.Execute("fd.delete('a');");

        engine.Evaluate("JSON.stringify([...fd])").AsString().Should().Be("""[["b","2"]]""");

        // Deleting a name that is not there is not an error.
        engine.Execute("fd.delete('missing');");
        engine.Evaluate("[...fd].length").AsNumber().Should().Be(1);
    }

    [Fact]
    public void SetReplacesTheFirstMatchInPlaceAndRemovesTheRest()
    {
        var engine = WithEntries();
        engine.Execute("fd.set('a', '9');");

        // The replacement keeps the first match's position — 'a' stays ahead of 'b'.
        engine.Evaluate("JSON.stringify([...fd])").AsString().Should().Be("""[["a","9"],["b","2"]]""");
    }

    [Fact]
    public void SetAppendsWhenTheNameIsAbsent()
    {
        var engine = WithEntries();
        engine.Execute("fd.set('c', '4');");

        engine.Evaluate("JSON.stringify([...fd])").AsString().Should().Be("""[["a","1"],["b","2"],["a","3"],["c","4"]]""");
    }

    [Fact]
    public void ConvertsNamesAndStringValuesToScalarValueStrings()
    {
        var engine = WebEngine();
        engine.Execute("var fd = new FormData(); fd.append(1, 2); fd.append('s\\uD800', 'v\\uDFFF');");

        engine.Evaluate("fd.get('1')").AsString().Should().Be("2");
        engine.Evaluate("fd.get('s\\uFFFD')").AsString().Should().Be("v�");
    }

    [Fact]
    public void WrapsABlobValueIntoAFileNamedBlob()
    {
        var engine = WebEngine();
        engine.Execute("var fd = new FormData(); fd.append('f', new Blob(['zz'], { type: 'text/x' }));");

        // https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#create-an-entry
        engine.Evaluate("fd.get('f') instanceof File").AsBoolean().Should().BeTrue();
        engine.Evaluate("fd.get('f').name").AsString().Should().Be("blob");
        engine.Evaluate("fd.get('f').size").AsNumber().Should().Be(2);

        // "representing the same bytes" — the media type is carried over.
        engine.Evaluate("fd.get('f').type").AsString().Should().Be("text/x");
    }

    [Fact]
    public void UsesTheFilenameWheneverOneIsGiven()
    {
        var engine = WebEngine();
        engine.Execute("""
            var fd = new FormData();
            fd.append('a', new Blob(['x']), 'named.txt');
            fd.append('b', new File(['x'], 'own.txt'));
            fd.append('c', new File(['x'], 'own.txt'), 'override.txt');
            """);

        engine.Evaluate("fd.get('a').name").AsString().Should().Be("named.txt");

        // A file with no filename argument keeps its own name.
        engine.Evaluate("fd.get('b').name").AsString().Should().Be("own.txt");
        engine.Evaluate("fd.get('c').name").AsString().Should().Be("override.txt");
    }

    [Fact]
    public void AlwaysStoresAFreshFileRatherThanTheValueItWasGiven()
    {
        var engine = WebEngine();
        engine.Execute("var f = new File(['x'], 'a.txt', { lastModified: 7 }); var fd = new FormData(); fd.append('k', f);");

        // The algorithm creates a new File even from a File, so the entry never aliases the object the
        // script still holds ...
        engine.Evaluate("fd.get('k') === f").AsBoolean().Should().BeFalse();

        // ... while representing the same bytes, name and modification time.
        engine.Evaluate("fd.get('k').name").AsString().Should().Be("a.txt");
        engine.Evaluate("fd.get('k').lastModified").AsNumber().Should().Be(7);
        engine.Evaluate("fd.get('k').text()").UnwrapIfPromise().AsString().Should().Be("x");
    }

    [Fact]
    public void ResolvesTheOverloadOnTheValueNotTheFilename()
    {
        var engine = WebEngine();
        engine.Execute("var fd = new FormData();");

        // With three arguments only the Blob signature exists, so a string value is a TypeError — which is
        // what a browser answers too.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("fd.append('a', 'b', 'c')"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("fd.set('a', 'b', 'c')"));

        // A trailing undefined for an optional argument with no default value means it was not passed.
        engine.Execute("fd.append('a', 'b', undefined);");
        engine.Evaluate("fd.get('a')").AsString().Should().Be("b");

        // Overload resolution precedes every conversion, so the name is not stringified on the way to the
        // failure.
        engine.Execute("var touched = false; var probe = { toString() { touched = true; return 'n'; } };");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("fd.append(probe, 'b', 'c')"));
        engine.Evaluate("touched").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void StringifiesANonBlobValue()
    {
        var engine = WebEngine();
        engine.Execute("var fd = new FormData(); fd.append('n', 5); fd.append('o', { toString() { return 'z'; } }); fd.append('u', undefined);");

        engine.Evaluate("fd.get('n')").AsString().Should().Be("5");
        engine.Evaluate("fd.get('o')").AsString().Should().Be("z");
        engine.Evaluate("fd.get('u')").AsString().Should().Be("undefined");
    }

    [Fact]
    public void IteratesEntriesKeysAndValues()
    {
        var engine = WithEntries();

        engine.Evaluate("JSON.stringify([...fd.entries()])").AsString().Should().Be("""[["a","1"],["b","2"],["a","3"]]""");
        engine.Evaluate("[...fd.keys()].join(',')").AsString().Should().Be("a,b,a");
        engine.Evaluate("[...fd.values()].join(',')").AsString().Should().Be("1,2,3");
    }

    [Fact]
    public void ItsDefaultIteratorIsEntriesItself()
    {
        var engine = WebEngine();

        // https://webidl.spec.whatwg.org/#es-iterable — @@iterator is the very same function object.
        engine.Evaluate("FormData.prototype[Symbol.iterator] === FormData.prototype.entries").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(new FormData().entries())").AsString().Should().Be("[object FormData Iterator]");

        // Its prototype chain reaches %IteratorPrototype%, so the iterator helpers work on it.
        engine.Evaluate("Object.getPrototypeOf(Object.getPrototypeOf(new FormData().keys())) === Object.getPrototypeOf(Object.getPrototypeOf([][Symbol.iterator]()))")
            .AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void IteratesTheLiveEntryList()
    {
        var engine = WebEngine();
        engine.Execute("""
            var fd = new FormData();
            fd.append('a', '1');
            var seen = [];
            for (const [k, v] of fd) {
                seen.push(k + v);
                if (seen.length === 1) { fd.append('b', '2'); }
            }
            """);

        // An entry appended during iteration is reached: the iterator indexes the live list.
        engine.Evaluate("seen.join(',')").AsString().Should().Be("a1,b2");
    }

    [Fact]
    public void ForEachVisitsValueKeyAndTheFormData()
    {
        var engine = WithEntries();
        engine.Execute("var seen = []; fd.forEach(function (v, k, o) { seen.push(k + '=' + v + (o === fd)); });");

        engine.Evaluate("seen.join(',')").AsString().Should().Be("a=1true,b=2true,a=3true");
    }

    [Fact]
    public void ForEachHonoursItsThisArgAndRejectsANonCallable()
    {
        var engine = WithEntries();

        engine.Execute("var marker = {}; var got; fd.forEach(function () { got = this; }, marker);");
        engine.Evaluate("got === marker").AsBoolean().Should().BeTrue();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("fd.forEach(5)"));
    }

    [Fact]
    public void BrandChecksEveryMember()
    {
        var engine = WebEngine();

        foreach (var member in new[] { "append", "delete", "get", "getAll", "has", "set", "forEach", "entries", "keys", "values" })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"FormData.prototype.{member}.call({{}}, 'a', 'b')"));
        }
    }

    [Fact]
    public void HasTheShapeWebIdlAsksFor()
    {
        var engine = WebEngine();

        engine.Evaluate("Object.prototype.toString.call(new FormData())").AsString().Should().Be("[object FormData]");
        engine.Evaluate("new FormData().constructor === FormData").AsBoolean().Should().BeTrue();
        engine.Evaluate("FormData.length").AsNumber().Should().Be(0);
        engine.Evaluate("FormData.name").AsString().Should().Be("FormData");

        engine.Evaluate("FormData.prototype.append.length").AsNumber().Should().Be(2);
        engine.Evaluate("FormData.prototype.set.length").AsNumber().Should().Be(2);
        engine.Evaluate("FormData.prototype.delete.length").AsNumber().Should().Be(1);
        engine.Evaluate("FormData.prototype.get.length").AsNumber().Should().Be(1);
        engine.Evaluate("FormData.prototype.getAll.length").AsNumber().Should().Be(1);
        engine.Evaluate("FormData.prototype.has.length").AsNumber().Should().Be(1);
        engine.Evaluate("FormData.prototype.forEach.length").AsNumber().Should().Be(1);
        engine.Evaluate("FormData.prototype.entries.length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void SupportsSubclassing()
    {
        var engine = WebEngine();

        engine.Execute("class MyForm extends FormData {}");
        engine.Evaluate("new MyForm() instanceof FormData").AsBoolean().Should().BeTrue();
        engine.Execute("var m = new MyForm(); m.append('a', 'b');");
        engine.Evaluate("m.get('a')").AsString().Should().Be("b");
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
        engine.Execute("var proto = Object.getPrototypeOf(new FormData().entries());");

        engine.Evaluate("Object.getOwnPropertyNames(proto).join(',')").AsString().Should().Be("next");
        engine.Execute("var d = Object.getOwnPropertyDescriptor(proto, 'next');");
        engine.Evaluate("d.writable").AsBoolean().Should().BeTrue("next must be writable");
        engine.Evaluate("d.enumerable").AsBoolean().Should().BeTrue("next must be enumerable");
        engine.Evaluate("d.configurable").AsBoolean().Should().BeTrue("next must be configurable");
        engine.Evaluate("d.value.length").AsNumber().Should().Be(0);
        engine.Evaluate("d.value.name").AsString().Should().Be("next");

        // The three kinds share one prototype, so the attributes above are the attributes of all of them.
        engine.Evaluate("Object.getPrototypeOf(new FormData().keys()) === proto").AsBoolean().Should().BeTrue("the three kinds share one prototype");
        engine.Evaluate("Object.getPrototypeOf(new FormData().values()) === proto").AsBoolean().Should().BeTrue("the three kinds share one prototype");

        // The class string is the object's only other own property, and it keeps the attributes a class
        // string carries — { [[Writable]]: false, [[Enumerable]]: false, [[Configurable]]: true } — rather
        // than WebIDL's operation attributes.
        engine.Evaluate("Object.getOwnPropertySymbols(proto).length").AsNumber().Should().Be(1);
        engine.Execute("var t = Object.getOwnPropertyDescriptor(proto, Symbol.toStringTag);");
        engine.Evaluate("t.value").AsString().Should().Be("FormData Iterator");
        engine.Evaluate("t.writable").AsBoolean().Should().BeFalse();
        engine.Evaluate("t.enumerable").AsBoolean().Should().BeFalse();
        engine.Evaluate("t.configurable").AsBoolean().Should().BeTrue("the class string is configurable");
    }
}
#endif
