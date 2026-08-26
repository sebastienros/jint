using Jint.Native;
using Jint.Native.Error;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A host function must be able to fail with the error the specification would raise — a <c>RangeError</c>
/// for an out-of-range argument, a <c>URIError</c> for malformed input — and not only with the two error
/// constructors that happened to be reachable.
/// </summary>
/// <remarks>
/// Every <c>%NativeError%</c> constructor is a property of <see cref="Jint.Runtime.Intrinsics"/>, and until v5
/// five of the seven were <c>internal</c> — an accident of who had needed what, never a policy, and a porous
/// one: the same objects are installed as globals, so <c>(ErrorConstructor) engine.GetValue("RangeError")</c>
/// reached them anyway. These tests pin the supported spelling instead.
/// </remarks>
public class HostRaisedErrorTests
{
    private static ErrorConstructor ConstructorFor(Engine engine, string name) => name switch
    {
        "Error" => engine.Intrinsics.Error,
        "EvalError" => engine.Intrinsics.EvalError,
        "RangeError" => engine.Intrinsics.RangeError,
        "ReferenceError" => engine.Intrinsics.ReferenceError,
        "SyntaxError" => engine.Intrinsics.SyntaxError,
        "TypeError" => engine.Intrinsics.TypeError,
        // The only intrinsic whose CLR name is not its script name.
        "URIError" => engine.Intrinsics.UriError,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not an error constructor"),
    };

    [TestCase("Error")]
    [TestCase("EvalError")]
    [TestCase("RangeError")]
    [TestCase("ReferenceError")]
    [TestCase("SyntaxError")]
    [TestCase("TypeError")]
    [TestCase("URIError")]
    public void AHostFunctionCanRaiseEveryErrorTypeTheSpecDefines(string name)
    {
        var engine = new Engine();
        var errorConstructor = ConstructorFor(engine, name);

        engine.SetValue("fail", new Action(() => throw new JavaScriptException(errorConstructor, "boom")));

        var caught = engine.Evaluate($$"""
            (function () {
                try {
                    fail();
                } catch (e) {
                    return {
                        instanceOfError: e instanceof Error,
                        instanceOfExact: e instanceof {{name}},
                        constructorIdentity: e.constructor === {{name}},
                        prototypeIdentity: Object.getPrototypeOf(e) === {{name}}.prototype,
                        isError: Error.isError(e),
                        name: e.name,
                        message: e.message,
                        stringForm: String(e)
                    };
                }
                return null;
            })()
            """).AsObject();

        caught.Get("instanceOfError").AsBoolean().Should().BeTrue();
        caught.Get("instanceOfExact").AsBoolean().Should()
            .BeTrue($"an error raised through Intrinsics.{name} must be an instance of the script's {name}");
        caught.Get("constructorIdentity").AsBoolean().Should().BeTrue();
        caught.Get("prototypeIdentity").AsBoolean().Should().BeTrue();
        caught.Get("isError").AsBoolean().Should().BeTrue();
        caught.Get("name").AsString().Should().Be(name);
        caught.Get("message").AsString().Should().Be("boom");
        caught.Get("stringForm").AsString().Should().Be($"{name}: boom");
    }

    [TestCase("Error")]
    [TestCase("EvalError")]
    [TestCase("RangeError")]
    [TestCase("ReferenceError")]
    [TestCase("SyntaxError")]
    [TestCase("TypeError")]
    [TestCase("URIError")]
    public void AnUncaughtHostErrorReachesTheHostAsThatErrorType(string name)
    {
        var engine = new Engine();
        var errorConstructor = ConstructorFor(engine, name);

        engine.SetValue("fail", new Action(() => throw new JavaScriptException(errorConstructor, "boom")));

        var exception = Invoking(() => engine.Evaluate("fail()")).Should().Throw<JavaScriptException>().Which;

        exception.Message.Should().Be("boom");
        var error = exception.Error.AsObject();
        error.Get("name").AsString().Should().Be(name);
        error.Engine.Should().BeSameAs(engine, "the error belongs to the engine whose intrinsic built it");

        engine.SetValue("raised", error);
        engine.Evaluate($"raised instanceof {name} && Object.getPrototypeOf(raised) === {name}.prototype")
            .AsBoolean().Should().BeTrue();
    }

    [Test]
    public void TheErrorBelongsToThePrincipalRealmEvenAfterAnotherRealmExists()
    {
        // Intrinsics is per-realm, and a ShadowRealm builds a second set. An error a host raises through
        // engine.Intrinsics must still be an instance of the RangeError the surrounding script can see.
        var engine = new Engine();
        engine.Evaluate("new ShadowRealm()");

        var rangeError = engine.Intrinsics.RangeError;
        engine.SetValue("fail", new Action(() => throw new JavaScriptException(rangeError, "boom")));

        engine.Evaluate("try { fail(); } catch (e) { e instanceof RangeError && Object.getPrototypeOf(e) === RangeError.prototype; }")
            .AsBoolean().Should().BeTrue("engine.Intrinsics is the principal realm's, whatever was created after it");
    }

    [Test]
    public void TheIntrinsicIsTheObjectTheScriptSeesAsThatGlobal()
    {
        var engine = new Engine();

        engine.SetValue("rangeError", engine.Intrinsics.RangeError);

        engine.Evaluate("rangeError === RangeError").AsBoolean().Should().BeTrue();
        engine.Evaluate("rangeError === globalThis.RangeError").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ARangeErrorCanCarryTheClrExceptionThatCausedIt()
    {
        var engine = new Engine();
        var cause = new ArgumentOutOfRangeException("index");

        engine.SetValue("fail", new Action(() => throw new JavaScriptException(engine.Intrinsics.RangeError, "index out of range", cause)));

        var exception = Invoking(() => engine.Evaluate("fail()")).Should().Throw<JavaScriptException>().Which;

        exception.Error.AsObject().Get("name").AsString().Should().Be("RangeError");
        JintException.TryGetClrException(exception, out var clrException).Should().BeTrue();
        clrException.Should().BeSameAs(cause);
    }

    [Test]
    public void AHostErrorConstructorAlsoBuildsAPlainErrorObject()
    {
        // ErrorConstructor.Construct is the value-producing half of the same capability: a host that wants to
        // hand an error back rather than throw it needs no exception at all.
        var engine = new Engine();

        engine.SetValue("makeRangeError", new Func<string, JsValue>(message => engine.Intrinsics.RangeError.Construct(message)));

        engine.Evaluate("var e = makeRangeError('too big');");
        engine.Evaluate("e instanceof RangeError && e.message === 'too big'").AsBoolean().Should().BeTrue();
    }
}
