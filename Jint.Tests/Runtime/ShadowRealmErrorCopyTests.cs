#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A failure that leaves a <c>ShadowRealm</c> is replaced by a fresh <c>TypeError</c> of the calling realm,
/// and https://tc39.es/proposal-shadowrealm/#sec-create-type-error-copy says building it "must not cause any
/// ECMAScript code execution". The copy's message is the host's to choose, so it may describe the value that
/// was thrown — but only from what can be read without calling anything: the string a primitive is, and an
/// error's <c>name</c> and <c>message</c> where each is a data property holding a string.
/// <para>
/// <c>ShadowRealm.prototype.evaluate</c> used to format the thrown value with <c>ToString</c>, which calls
/// <c>toString</c>, <c>@@toPrimitive</c>, a <c>name</c> or <c>message</c> getter and every proxy trap on the
/// way — and when one of those threw, what reached the caller was that exception, an object of the shadow
/// realm, instead of the <c>TypeError</c>.
/// </para>
/// </summary>
public class ShadowRealmErrorCopyTests
{
    /// <summary>
    /// <c>copyOf</c> answers "&lt;kind&gt;|&lt;message&gt;|ran=&lt;n&gt;", where <c>ran</c> counts every call a
    /// row's script makes into its own code, so a failure says both what crossed and whether anything ran.
    /// </summary>
    private const string Preamble = """
        var sr = new ShadowRealm();
        sr.evaluate('var ran = 0;');

        function copyOf(source) {
            try {
                sr.evaluate(source);
                return 'no throw';
            } catch (e) {
                var kind = e instanceof TypeError ? 'TypeError' : 'not a TypeError of this realm';
                return kind + '|' + e.message + '|ran=' + sr.evaluate('ran');
            }
        }
        """;

    private const string NotAnError = "Cross-Realm Error: an object that is not an Error was thrown";

    /// <summary>A handler every one of whose traps counts itself before forwarding to <c>Reflect</c>.</summary>
    private const string CountingHandler = """
        var handler = {};
        ['get', 'set', 'has', 'deleteProperty', 'defineProperty', 'getOwnPropertyDescriptor', 'ownKeys',
         'getPrototypeOf', 'setPrototypeOf', 'isExtensible', 'preventExtensions', 'apply', 'construct']
            .forEach(function (trap) {
                handler[trap] = function () { ran++; return Reflect[trap].apply(null, arguments); };
            });
        """;

    private static string CopyOf(string source)
    {
        var engine = new Engine();
        engine.Execute(Preamble);
        engine.SetValue("source", source);
        return engine.Evaluate("copyOf(source)").AsString();
    }

    [TestCase("throw { toString() { ran++; return 'custom'; } };", TestName = "a toString")]
    [TestCase("throw { toString() { ran++; throw new RangeError('from toString'); } };", TestName = "a throwing toString")]
    [TestCase("throw { [Symbol.toPrimitive]() { ran++; return 'primitive'; } };", TestName = "a Symbol.toPrimitive")]
    [TestCase("throw { valueOf() { ran++; return 1; }, toString: undefined };", TestName = "a valueOf")]
    [TestCase("throw { message: 'looks like an error' };", TestName = "an object that only looks like an error")]
    public void AThrownObjectIsNotConvertedToAString(string source)
    {
        CopyOf(source).Should().Be("TypeError|" + NotAnError + "|ran=0");
    }

    [TestCase(
        "var e = new Error('x'); Object.defineProperty(e, 'message', { get() { ran++; return 'from getter'; } }); throw e;",
        "Error",
        TestName = "a message getter")]
    [TestCase(
        "var e = new Error('x'); Object.defineProperty(e, 'message', { get() { ran++; throw new RangeError('from getter'); } }); throw e;",
        "Error",
        TestName = "a throwing message getter")]
    [TestCase(
        "var e = new Error('x'); Object.defineProperty(e, 'name', { get() { ran++; return 'FromGetter'; } }); throw e;",
        "Error: x",
        TestName = "a name getter")]
    [TestCase(
        "class E extends Error { get message() { ran++; return 'from getter'; } } throw new E();",
        "Error",
        TestName = "a message getter on a subclass prototype")]
    [TestCase(
        "var e = new Error(); e.message = { toString() { ran++; return 'from toString'; } }; throw e;",
        "Error",
        TestName = "a message that is not a string")]
    [TestCase(
        CountingHandler + "var e = new Error('x'); Object.setPrototypeOf(e, new Proxy(Error.prototype, handler)); ran = 0; throw e;",
        "Error: x",
        TestName = "a proxy on the error's prototype chain")]
    public void AnErrorsNameAndMessageAreReadWithoutCallingAnything(string source, string described)
    {
        CopyOf(source).Should().Be("TypeError|Cross-Realm Error: " + described + "|ran=0");
    }

    [TestCase("throw new Proxy({}, handler);", TestName = "a proxy of an object")]
    [TestCase("throw new Proxy(new Error('proxied'), handler);", TestName = "a proxy of an error")]
    [TestCase("throw new Proxy(function () {}, handler);", TestName = "a proxy of a function")]
    public void AThrownProxyRunsNoTrap(string source)
    {
        CopyOf(CountingHandler + source).Should().Be("TypeError|" + NotAnError + "|ran=0");
    }

    /// <summary>
    /// What the copy says for an ordinary error and for a primitive is unchanged, and is pinned so that making
    /// the copy safe does not quietly cost the diagnostic.
    /// </summary>
    [TestCase("throw new ReferenceError('aaa');", "ReferenceError: aaa")]
    [TestCase("throw new Error();", "Error")]
    [TestCase("var e = new Error('x'); e.name = ''; throw e;", "x")]
    [TestCase("throw 42;", "42")]
    [TestCase("throw 'boom';", "boom")]
    [TestCase("throw Symbol('s');", "Symbol(s)")]
    [TestCase("throw undefined;", "undefined")]
    [TestCase("throw 10n;", "10")]
    public void AnOrdinaryErrorOrAPrimitiveIsStillDescribed(string source, string described)
    {
        CopyOf(source).Should().Be("TypeError|Cross-Realm Error: " + described + "|ran=0");
    }

    /// <summary>
    /// A failure copied out of a shadow realm nested inside another one is copied again on the way out of the
    /// outer realm. The copy it crosses with already says it crossed, so it is marked once, exactly as a chain
    /// of wrapped functions is.
    /// </summary>
    [TestCase("new ShadowRealm().evaluate(\"throw new Error('boom')\");", TestName = "two realms deep")]
    [TestCase("new ShadowRealm().evaluate(\"new ShadowRealm().evaluate('throw new Error(\\\\'boom\\\\')')\");", TestName = "three realms deep")]
    public void ACopyCrossingNestedRealmsIsMarkedOnce(string source)
    {
        CopyOf(source).Should().Be("TypeError|Cross-Realm Error: Error: boom|ran=0");
    }

    /// <summary>
    /// <c>importValue</c> copies a failed import through
    /// https://tc39.es/proposal-shadowrealm/#sec-import-value-error-functions, the same operation, so its
    /// rejection describes what the module threw in the same terms and calls nothing to do it.
    /// </summary>
    [TestCase("throw new RangeError('module failed');", "Cross-Realm Error: RangeError: module failed", TestName = "an error")]
    [TestCase("throw { toString() { globalThis.ran = (globalThis.ran || 0) + 1; return 'custom'; } };", NotAnError, TestName = "a toString")]
    public void AFailedImportIsCopiedWithoutCallingAnything(string moduleSource, string expectedMessage)
    {
        var engine = new Engine();
        engine.Modules.Add("throws", moduleSource);

        engine.Execute("""
            var sr = new ShadowRealm();
            var outcome = 'pending';
            sr.importValue('throws', 'x').then(
                function () { outcome = 'fulfilled'; },
                function (e) {
                    var kind = e instanceof TypeError ? 'TypeError' : 'not a TypeError of this realm';
                    outcome = kind + '|' + e.message + '|ran=' + sr.evaluate('globalThis.ran || 0');
                });
            """);

        engine.Evaluate("outcome").AsString().Should().Be("TypeError|" + expectedMessage + "|ran=0");
    }
}
