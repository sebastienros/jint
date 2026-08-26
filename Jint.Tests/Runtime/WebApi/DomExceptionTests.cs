#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>DOMException</c> as WebIDL specifies it — https://webidl.spec.whatwg.org/#idl-DOMException.
/// </summary>
/// <remarks>
/// It is installed whenever <i>any</i> web API is enabled, because it is how the rest of them report a
/// failure; <see cref="WebApiFeatures.Console"/> is simply the cheapest way to ask for one here.
/// </remarks>
public class DomExceptionTests
{
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Console));

    [Test]
    public void DefaultsToTheIdlArguments()
    {
        var engine = WebEngine();

        engine.Evaluate("new DOMException().name").AsString().Should().Be("Error");
        engine.Evaluate("new DOMException().message").AsString().Should().Be("");
        engine.Evaluate("new DOMException().code").AsNumber().Should().Be(0);

        // An explicitly passed undefined takes the default too, which is what an optional argument with a
        // default value means in WebIDL.
        engine.Evaluate("new DOMException(undefined, undefined).name").AsString().Should().Be("Error");
    }

    [Test]
    public void CarriesTheGivenMessageAndName()
    {
        var engine = WebEngine();

        engine.Evaluate("new DOMException('boom', 'AbortError').message").AsString().Should().Be("boom");
        engine.Evaluate("new DOMException('boom', 'AbortError').name").AsString().Should().Be("AbortError");
    }

    [TestCase("IndexSizeError", 1)]
    [TestCase("HierarchyRequestError", 3)]
    [TestCase("InvalidCharacterError", 5)]
    [TestCase("NotFoundError", 8)]
    [TestCase("InvalidStateError", 11)]
    [TestCase("SyntaxError", 12)]
    [TestCase("SecurityError", 18)]
    [TestCase("NetworkError", 19)]
    [TestCase("AbortError", 20)]
    [TestCase("QuotaExceededError", 22)]
    [TestCase("TimeoutError", 23)]
    [TestCase("DataCloneError", 25)]
    public void MapsALegacyNameToItsCode(string name, int code)
    {
        var engine = WebEngine();

        engine.Evaluate($"new DOMException('', '{name}').code").AsNumber().Should().Be(code);
    }

    [TestCase("NotAllowedError")]
    [TestCase("EncodingError")]
    [TestCase("SomethingNobodyStandardized")]
    [TestCase("")]
    public void ReportsZeroForANameOutsideTheLegacyTable(string name)
    {
        var engine = WebEngine();

        engine.Evaluate($"new DOMException('', '{name}').code").AsNumber().Should().Be(0);
    }

    [Test]
    public void ExposesTheLegacyConstantsOnBothTheInterfaceObjectAndThePrototype()
    {
        var engine = WebEngine();

        engine.Evaluate("DOMException.INDEX_SIZE_ERR").AsNumber().Should().Be(1);
        engine.Evaluate("DOMException.DATA_CLONE_ERR").AsNumber().Should().Be(25);
        engine.Evaluate("DOMException.prototype.INDEX_SIZE_ERR").AsNumber().Should().Be(1);
        engine.Evaluate("DOMException.prototype.DATA_CLONE_ERR").AsNumber().Should().Be(25);

        // All 25 of them, on both objects.
        engine.Evaluate("Object.getOwnPropertyNames(DOMException).filter(function (n) { return /_ERR$/.test(n); }).length")
            .AsNumber().Should().Be(25);
        engine.Evaluate("Object.getOwnPropertyNames(DOMException.prototype).filter(function (n) { return /_ERR$/.test(n); }).length")
            .AsNumber().Should().Be(25);
    }

    [Test]
    public void EnumeratesTheLegacyConstantsInDeclarationOrder()
    {
        var engine = WebEngine();

        // https://webidl.spec.whatwg.org/#es-constants defines the constants one after another in the order
        // the IDL declares them, so INDEX_SIZE_ERR comes first and DATA_CLONE_ERR last — alphabetical order
        // would start at ABORT_ERR. web-platform-tests reads exactly this through the record conversion:
        // `new URLSearchParams(DOMException).toString()` in url/urlsearchparams-constructor.any.js.
        const string Expected =
            "INDEX_SIZE_ERR,DOMSTRING_SIZE_ERR,HIERARCHY_REQUEST_ERR,WRONG_DOCUMENT_ERR,INVALID_CHARACTER_ERR," +
            "NO_DATA_ALLOWED_ERR,NO_MODIFICATION_ALLOWED_ERR,NOT_FOUND_ERR,NOT_SUPPORTED_ERR,INUSE_ATTRIBUTE_ERR," +
            "INVALID_STATE_ERR,SYNTAX_ERR,INVALID_MODIFICATION_ERR,NAMESPACE_ERR,INVALID_ACCESS_ERR,VALIDATION_ERR," +
            "TYPE_MISMATCH_ERR,SECURITY_ERR,NETWORK_ERR,ABORT_ERR,URL_MISMATCH_ERR,QUOTA_EXCEEDED_ERR,TIMEOUT_ERR," +
            "INVALID_NODE_TYPE_ERR,DATA_CLONE_ERR";

        engine.Evaluate("Object.keys(DOMException).join(',')").AsString().Should().Be(Expected);
        engine.Evaluate("Object.getOwnPropertyNames(DOMException.prototype).filter(function (n) { return /_ERR$/.test(n); }).join(',')")
            .AsString().Should().Be(Expected);

        // The values track the names, which is what the URLSearchParams row actually asserts.
        engine.Evaluate("Object.values(DOMException).join(',')").AsString()
            .Should().Be("1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25");
    }

    [Test]
    public void GivesTheConstantsTheAttributesWebIdlGivesConstants()
    {
        var engine = WebEngine();

        var descriptor = "Object.getOwnPropertyDescriptor(DOMException, 'ABORT_ERR')";
        engine.Evaluate($"{descriptor}.value").AsNumber().Should().Be(20);
        engine.Evaluate($"{descriptor}.writable").AsBoolean().Should().BeFalse();
        engine.Evaluate($"{descriptor}.enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate($"{descriptor}.configurable").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void ChainsThroughItsPrototypeToErrorPrototype()
    {
        var engine = WebEngine();

        engine.Evaluate("new DOMException() instanceof DOMException").AsBoolean().Should().BeTrue();
        engine.Evaluate("new DOMException() instanceof Error").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(DOMException.prototype) === Error.prototype").AsBoolean().Should().BeTrue();

        // The interface object itself is an ordinary function, not a NativeError constructor.
        engine.Evaluate("Object.getPrototypeOf(DOMException) === Function.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("DOMException.prototype.constructor === DOMException").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void InheritsErrorPrototypeToString()
    {
        var engine = WebEngine();

        engine.Evaluate("String(new DOMException('boom', 'AbortError'))").AsString().Should().Be("AbortError: boom");
        engine.Evaluate("String(new DOMException())").AsString().Should().Be("Error");
    }

    [Test]
    public void CarriesAStack()
    {
        var engine = WebEngine();

        var stack = engine.Evaluate("function make() { return new DOMException('x'); } make().stack").AsString();

        stack.Should().NotBeNullOrEmpty();
        stack.Should().Contain("at make");

        // Browsers expose it as an own, non-enumerable property of the instance.
        engine.Evaluate("new DOMException().hasOwnProperty('stack')").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.keys(new DOMException()).length").AsNumber().Should().Be(0);
    }

    [Test]
    public void ExposesNameMessageAndCodeAsPrototypeAccessors()
    {
        var engine = WebEngine();

        // WebIDL attributes live on the interface prototype object, so the instance owns none of them.
        engine.Evaluate("new DOMException('m', 'AbortError').hasOwnProperty('name')").AsBoolean().Should().BeFalse();
        engine.Evaluate("new DOMException('m', 'AbortError').hasOwnProperty('message')").AsBoolean().Should().BeFalse();

        var descriptor = "Object.getOwnPropertyDescriptor(DOMException.prototype, 'name')";
        engine.Evaluate($"typeof {descriptor}.get").AsString().Should().Be("function");
        engine.Evaluate($"{descriptor}.set").IsUndefined().Should().BeTrue();
        engine.Evaluate($"{descriptor}.enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate($"{descriptor}.configurable").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void RefusesAnAccessorReceiverThatIsNotADomException()
    {
        var engine = WebEngine();

        // DOMException.prototype is an interface prototype object, not an instance of the interface.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("DOMException.prototype.name"))!
            .Message.Should().Contain("DOMException");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Object.getOwnPropertyDescriptor(DOMException.prototype, 'code').get.call({})"));
    }

    [Test]
    public void TagsItselfForObjectPrototypeToString()
    {
        var engine = WebEngine();

        engine.Evaluate("Object.prototype.toString.call(new DOMException())").AsString().Should().Be("[object DOMException]");
        engine.Evaluate("DOMException.prototype[Symbol.toStringTag]").AsString().Should().Be("DOMException");
    }

    [Test]
    public void RequiresNew()
    {
        var engine = WebEngine();

        var exception = Assert.Throws<JavaScriptException>(() => engine.Evaluate("DOMException('x')"))!;
        exception.Message.Should().Contain("requires 'new'");
    }

    [Test]
    public void HasTheIdlArity()
    {
        var engine = WebEngine();

        engine.Evaluate("DOMException.length").AsNumber().Should().Be(0);
        engine.Evaluate("DOMException.name").AsString().Should().Be("DOMException");
    }

    [Test]
    public void CarriesTheErrorDataInternalSlot()
    {
        var engine = WebEngine();

        // https://webidl.spec.whatwg.org/#internally-create-a-new-object-implementing-the-interface — "If
        // interface is DOMException, append [[ErrorData]] to slots" — and
        // https://tc39.es/ecma262/#sec-error.iserror is the brand check on exactly that slot.
        engine.Evaluate("Error.isError(new DOMException())").AsBoolean().Should().BeTrue();
        engine.Evaluate("Error.isError(new DOMException('x', 'AbortError'))").AsBoolean().Should().BeTrue();

        // Still an Error by every other measure it already was.
        engine.Evaluate("new DOMException() instanceof Error").AsBoolean().Should().BeTrue();

        // A prototype object is not an instance, whichever hierarchy it belongs to: %Error.prototype% is
        // "an ordinary object … not an Error instance" per
        // https://tc39.es/ecma262/#sec-properties-of-the-error-prototype-object, and DOMException.prototype is
        // left ordinary here for the same reason.
        engine.Evaluate("Error.isError(Error.prototype)").AsBoolean().Should().BeFalse();
        engine.Evaluate("Error.isError(TypeError.prototype)").AsBoolean().Should().BeFalse();
        engine.Evaluate("Error.isError(DOMException.prototype)").AsBoolean().Should().BeFalse();

        // ... and neither is anything merely shaped like one.
        engine.Evaluate("Error.isError({ name: 'AbortError', message: 'x' })").AsBoolean().Should().BeFalse();
        engine.Evaluate("Error.isError(Object.create(DOMException.prototype))").AsBoolean().Should().BeFalse();
        engine.Evaluate("Error.isError('AbortError')").AsBoolean().Should().BeFalse();

        // The ECMAScript errors are unaffected.
        engine.Evaluate("Error.isError(new Error())").AsBoolean().Should().BeTrue();
        engine.Evaluate("Error.isError(new TypeError())").AsBoolean().Should().BeTrue();
        engine.Evaluate("Error.isError(new AggregateError([]))").AsBoolean().Should().BeTrue();
    }
}
#endif
