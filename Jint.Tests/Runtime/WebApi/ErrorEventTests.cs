#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The two event interfaces the <see cref="WebApiFeatures.GlobalEvents"/> feature adds:
/// <see href="https://html.spec.whatwg.org/multipage/webappapis.html#the-errorevent-interface">ErrorEvent</see>
/// and
/// <see href="https://html.spec.whatwg.org/multipage/webappapis.html#the-promiserejectionevent-interface">PromiseRejectionEvent</see>.
/// </summary>
/// <remarks>
/// What is pinned here is the interface itself — the constructor's WebIDL conversions, the dictionary member
/// order, the prototype chain and the brand checks. What the engine <i>fires</i> at the global scope is
/// <c>GlobalErrorEventTests</c>.
/// </remarks>
public class ErrorEventTests
{
    private static Engine WebEngine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.GlobalEvents));
        engine.Execute("var log = [];");
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    // ErrorEvent

    [Fact]
    public void ErrorEventDefaultsEveryMember()
    {
        var engine = WebEngine();

        engine.Execute("var e = new ErrorEvent('error');");

        engine.Evaluate("e.type").AsString().Should().Be("error");
        engine.Evaluate("e.message").AsString().Should().Be("");
        engine.Evaluate("e.filename").AsString().Should().Be("");
        engine.Evaluate("e.lineno").AsNumber().Should().Be(0);
        engine.Evaluate("e.colno").AsNumber().Should().Be(0);

        // `any error` has no default, so it is undefined rather than null.
        engine.Evaluate("e.error").IsUndefined().Should().BeTrue();

        // The inherited EventInit members default as they do everywhere else, and a constructed event is
        // never trusted.
        engine.Evaluate("e.bubbles").AsBoolean().Should().BeFalse();
        engine.Evaluate("e.cancelable").AsBoolean().Should().BeFalse();
        engine.Evaluate("e.isTrusted").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void ErrorEventReadsItsDictionary()
    {
        var engine = WebEngine();

        engine.Execute("""
            var thrown = new TypeError('nope');
            var e = new ErrorEvent('error', {
                bubbles: true,
                cancelable: true,
                message: 'boom',
                filename: 'app.js',
                lineno: 12,
                colno: 34,
                error: thrown,
            });
            """);

        engine.Evaluate("e.message").AsString().Should().Be("boom");
        engine.Evaluate("e.filename").AsString().Should().Be("app.js");
        engine.Evaluate("e.lineno").AsNumber().Should().Be(12);
        engine.Evaluate("e.colno").AsNumber().Should().Be(34);
        engine.Evaluate("e.error === thrown").AsBoolean().Should().BeTrue();
        engine.Evaluate("e.bubbles").AsBoolean().Should().BeTrue();
        engine.Evaluate("e.cancelable").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ErrorEventCoercesLikeWebIdlDoes()
    {
        var engine = WebEngine();

        // `unsigned long` is ToUint32: NaN and the infinities become 0 and anything beyond 32 bits wraps.
        engine.Execute("var e = new ErrorEvent('error', { lineno: -1, colno: NaN, message: 42, filename: 7 });");

        engine.Evaluate("e.lineno").AsNumber().Should().Be(4294967295d);
        engine.Evaluate("e.colno").AsNumber().Should().Be(0);

        // DOMString and USVString both stringify.
        engine.Evaluate("e.message").AsString().Should().Be("42");
        engine.Evaluate("e.filename").AsString().Should().Be("7");
    }

    [Fact]
    public void ErrorEventReadsItsMembersInWebIdlOrder()
    {
        var engine = WebEngine();

        // The inherited EventInit members first, then this dictionary's own in lexicographical order —
        // https://webidl.spec.whatwg.org/#es-dictionary.
        engine.Execute("""
            var init = {};
            for (const name of ['bubbles', 'cancelable', 'composed', 'colno', 'error', 'filename', 'lineno', 'message']) {
                Object.defineProperty(init, name, { get: function () { log.push(name); return undefined; }, enumerable: true });
            }
            new ErrorEvent('error', init);
            """);

        Log(engine).Should().Be("bubbles,cancelable,composed,colno,error,filename,lineno,message");
    }

    [Fact]
    public void ErrorEventRequiresItsType()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Execute("new ErrorEvent()"))
            .Message.Should().Contain("1 argument required");

        // The dictionary is optional, and a non-object that is not undefined or null is a TypeError.
        engine.Evaluate("new ErrorEvent('error', null).message").AsString().Should().Be("");
        Assert.Throws<JavaScriptException>(() => engine.Execute("new ErrorEvent('error', 5)"));
    }

    [Fact]
    public void ErrorEventInheritsFromEvent()
    {
        var engine = WebEngine();

        engine.Evaluate("new ErrorEvent('error') instanceof Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(ErrorEvent) === Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(ErrorEvent.prototype) === Event.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("ErrorEvent.length").AsNumber().Should().Be(1);
        engine.Evaluate("ErrorEvent.name").AsString().Should().Be("ErrorEvent");
        engine.Evaluate("Object.prototype.toString.call(new ErrorEvent('error'))").AsString().Should().Be("[object ErrorEvent]");
    }

    [Fact]
    public void ErrorEventAccessorsAreBranded()
    {
        var engine = WebEngine();

        // Every attribute is an accessor on the prototype reading CLR state through a brand check, so a plain
        // Event is refused.
        foreach (var member in new[] { "message", "filename", "lineno", "colno", "error" })
        {
            var descriptor = engine.Evaluate($"Object.getOwnPropertyDescriptor(ErrorEvent.prototype, '{member}')");
            descriptor.IsUndefined().Should().BeFalse();

            engine.Evaluate($"Object.getOwnPropertyDescriptor(ErrorEvent.prototype, '{member}').enumerable").AsBoolean().Should().BeTrue();
            engine.Evaluate($"Object.getOwnPropertyDescriptor(ErrorEvent.prototype, '{member}').configurable").AsBoolean().Should().BeTrue();

            Assert.Throws<JavaScriptException>(() =>
                engine.Execute($"Object.getOwnPropertyDescriptor(ErrorEvent.prototype, '{member}').get.call(new Event('x'))"))
                .Message.Should().Contain("not an ErrorEvent");
        }
    }

    // PromiseRejectionEvent

    [Fact]
    public void PromiseRejectionEventRequiresItsDictionaryAndItsPromise()
    {
        var engine = WebEngine();

        // Unlike every other event interface here, eventInitDict is not optional — the dictionary has a
        // required member — so length is 2 and one argument is an arity error.
        engine.Evaluate("PromiseRejectionEvent.length").AsNumber().Should().Be(2);

        Assert.Throws<JavaScriptException>(() => engine.Execute("new PromiseRejectionEvent('unhandledrejection')"))
            .Message.Should().Contain("2 arguments required");

        Assert.Throws<JavaScriptException>(() => engine.Execute("new PromiseRejectionEvent('unhandledrejection', {})"))
            .Message.Should().Contain("required member promise");

        // WebIDL treats an explicit undefined as absent, so it fails the same way.
        Assert.Throws<JavaScriptException>(() => engine.Execute("new PromiseRejectionEvent('unhandledrejection', { promise: undefined })"))
            .Message.Should().Contain("required member promise");

        // And so does a null dictionary, which converts to one with no members at all.
        Assert.Throws<JavaScriptException>(() => engine.Execute("new PromiseRejectionEvent('unhandledrejection', null)"))
            .Message.Should().Contain("required member promise");
    }

    [Fact]
    public void PromiseRejectionEventKeepsAPromiseAndConvertsAnythingElse()
    {
        var engine = WebEngine();

        engine.Execute("""
            var p = Promise.resolve(1);
            var kept = new PromiseRejectionEvent('unhandledrejection', { promise: p, reason: 'why' });
            var converted = new PromiseRejectionEvent('unhandledrejection', { promise: 42 });
            """);

        // Promise<any> is https://webidl.spec.whatwg.org/#es-promise — PromiseResolve, which hands back a
        // native promise unchanged and wraps anything else.
        engine.Evaluate("kept.promise === p").AsBoolean().Should().BeTrue();
        engine.Evaluate("kept.reason").AsString().Should().Be("why");

        engine.Evaluate("converted.promise instanceof Promise").AsBoolean().Should().BeTrue();
        engine.Evaluate("converted.promise === 42").AsBoolean().Should().BeFalse();

        // `any reason` has no default.
        engine.Evaluate("converted.reason").IsUndefined().Should().BeTrue();
    }

    [Fact]
    public void PromiseRejectionEventReadsItsMembersInWebIdlOrder()
    {
        var engine = WebEngine();

        engine.Execute("""
            var init = {};
            for (const name of ['bubbles', 'cancelable', 'composed', 'reason']) {
                Object.defineProperty(init, name, { get: function () { log.push(name); return undefined; }, enumerable: true });
            }
            Object.defineProperty(init, 'promise', { get: function () { log.push('promise'); return Promise.resolve(1); }, enumerable: true });
            new PromiseRejectionEvent('unhandledrejection', init);
            """);

        // `promise` before `reason`, and both after the inherited members.
        Log(engine).Should().Be("bubbles,cancelable,composed,promise,reason");
    }

    [Fact]
    public void PromiseRejectionEventInheritsFromEvent()
    {
        var engine = WebEngine();

        engine.Execute("var e = new PromiseRejectionEvent('unhandledrejection', { promise: Promise.resolve(1) });");

        engine.Evaluate("e instanceof Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(PromiseRejectionEvent) === Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(PromiseRejectionEvent.prototype) === Event.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(e)").AsString().Should().Be("[object PromiseRejectionEvent]");

        Assert.Throws<JavaScriptException>(() =>
            engine.Execute("Object.getOwnPropertyDescriptor(PromiseRejectionEvent.prototype, 'promise').get.call(new Event('x'))"))
            .Message.Should().Contain("not a PromiseRejectionEvent");
    }

    [Fact]
    public void TheInterfaceObjectsAreWebIdlInterfaceObjects()
    {
        var engine = WebEngine();

        foreach (var name in new[] { "ErrorEvent", "PromiseRejectionEvent" })
        {
            // Writable and configurable but not enumerable — https://webidl.spec.whatwg.org/#es-interfaces.
            var descriptor = engine.Realm.GlobalObject.GetOwnProperty(name);
            descriptor.Writable.Should().BeTrue();
            descriptor.Configurable.Should().BeTrue();
            descriptor.Enumerable.Should().BeFalse();
        }
    }

    [Fact]
    public void TheInterfacesAreAbsentWithoutTheFeature()
    {
        // The events feature alone brings Event and EventTarget and neither of these two.
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events));

        engine.Evaluate("typeof Event").AsString().Should().Be("function");
        engine.Evaluate("typeof ErrorEvent").AsString().Should().Be("undefined");
        engine.Evaluate("typeof PromiseRejectionEvent").AsString().Should().Be("undefined");
    }
}
#endif
