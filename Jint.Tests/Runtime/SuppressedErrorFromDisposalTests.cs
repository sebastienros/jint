#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// The <c>SuppressedError</c> that disposal builds when a dispose method throws over an earlier throw completion is
/// "a newly created SuppressedError object" that
/// <see href="https://tc39.es/ecma262/#sec-disposeresources">DisposeResources</see> gives <c>error</c> and
/// <c>suppressed</c> and nothing else. It does not call the <c>SuppressedError</c> constructor, so it has no own
/// <c>message</c>: it inherits <c>SuppressedError.prototype.message</c>, the empty string
/// (<see href="https://tc39.es/ecma262/#sec-suppressederror.prototype.message">SuppressedError.prototype.message</see>).
/// Jint passed <c>""</c> to its constructor, which defines an own <c>message</c> for every argument but
/// <c>undefined</c> (sebastienros/jint#4494).
/// <para>
/// There is no test262 row for this (the disposal rows read <c>error</c> and <c>suppressed</c> only), and Node 26 gives
/// the error an own <c>message</c> of its own wording, "An error was suppressed during disposal": an implementation
/// choice the specification does not make, so the specification's shape is what is pinned here.
/// </para>
/// <para>
/// The same operation also decides which realm's <c>%SuppressedError.prototype%</c> the error inherits from: the
/// current Realm Record, which is the realm of the running execution context. That is the realm of the function whose
/// body is disposing, even after an <c>await</c> has resumed it, and for <c>DisposableStack.prototype.dispose</c> and
/// <c>AsyncDisposableStack.prototype.disposeAsync</c> the realm of that built-in, whichever realm called it or created
/// the stack. The rows named "other realm" call or run something of a second realm made with
/// <c>$262.createRealm()</c>, the plumbing <see cref="CrossRealmAttributionTests"/> uses.
/// </para>
/// </summary>
public class SuppressedErrorFromDisposalTests
{
    /// <summary>
    /// <c>describe</c> names the realm the error's prototype belongs to and what it owns, so a failure says which part
    /// differs rather than reporting <c>false</c>. <c>chain</c> does the same for the error and every
    /// <c>SuppressedError</c> nested in its <c>suppressed</c>.
    /// </summary>
    private const string Prelude = """
        var otherGlobal = $262.createRealm().global;

        function realmOf(e) {
          if (e instanceof otherGlobal.SuppressedError) return 'other';
          if (e instanceof SuppressedError) return 'main';
          return 'neither';
        }
        function describe(e) {
          return realmOf(e)
            + ' hasOwnMessage=' + Object.hasOwn(e, 'message')
            + ' message=' + JSON.stringify(e.message)
            + ' keys=' + Object.getOwnPropertyNames(e).filter(k => k !== 'stack').join('/')
            + ' error=' + e.error
            + ' suppressed=' + (e.suppressed instanceof Error ? 'nested' : e.suppressed);
        }
        function chain(e) {
          const parts = [];
          while (e instanceof SuppressedError) {
            parts.push(realmOf(e) + ':' + Object.hasOwn(e, 'message') + ':' + e.error);
            e = e.suppressed;
          }
          parts.push('last:' + e);
          return parts.join(' > ');
        }
        function thrower(reason) { return { [Symbol.dispose]() { throw reason; } }; }
        function asyncThrower(reason) { return { async [Symbol.asyncDispose]() { throw reason; } }; }
        function syncAsyncThrower(reason) { return { [Symbol.asyncDispose]() { throw reason; } }; }
        function asyncQuiet() { return { async [Symbol.asyncDispose]() {} }; }
        // A stack that throws each reason when disposed; the reasons are thrown last first.
        function stackOf(Ctor, ...reasons) {
          const s = new Ctor();
          for (const r of reasons) s.defer(Ctor === DisposableStack || Ctor === otherGlobal.DisposableStack ? () => { throw r; } : async () => { throw r; });
          return s;
        }

        """;

    // The dispose that throws 1 over the throw of 2 (or, for a stack, the defer made first, over the one made second),
    // built in the realm named in front.
    private const string MainSpec = "main hasOwnMessage=false message=\"\" keys=error/suppressed error=1 suppressed=2";
    private const string OtherSpec = "other hasOwnMessage=false message=\"\" keys=error/suppressed error=1 suppressed=2";

    public static TestCases<string, string, string> SameRealm => new()
    {
        {
            "using in a block",
            "(() => { try { { using a = thrower(1); throw 2; } } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "using in a function body",
            "(() => { function f() { using a = thrower(1); throw 2; } try { f(); } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "using in an arrow function body",
            "(() => { const f = () => { using a = thrower(1); throw 2; }; try { f(); } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "using in a for statement header",
            "(() => { try { for (using a = thrower(1); ; ) { throw 2; } } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "using in a for-of body",
            "(() => { try { for (const x of [0]) { using a = thrower(1); throw 2; } } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "using in a generator body",
            "(() => { function* g() { using a = thrower(1); throw 2; } try { g().next(); } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "using in a class static block",
            "(() => { try { class C { static { using a = thrower(1); throw 2; } } } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "using three deep, every level",
            // Disposed in reverse order: c throws 3 over the body's 4, b throws 2 over that, a throws 1 over that.
            "(() => { try { using a = thrower(1); using b = thrower(2); using c = thrower(3); throw 4; } catch (e) { return chain(e); } })()",
            "main:false:1 > main:false:2 > main:false:3 > last:4"
        },
        {
            "using with a single throw delivers it as it is",
            "(() => { try { { using a = thrower(1); } } catch (e) { return typeof e + ':' + e; } })()",
            "number:1"
        },
        {
            "DisposableStack.prototype.dispose",
            "(() => { try { stackOf(DisposableStack, 1, 2).dispose(); } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "DisposableStack.prototype.dispose after move",
            "(() => { try { stackOf(DisposableStack, 1, 2).move().dispose(); } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "DisposableStack.prototype.dispose, every level",
            "(() => { const s = stackOf(DisposableStack, 1, 2, 3); try { s.dispose(); } catch (e) { return chain(e); } })()",
            "main:false:1 > main:false:2 > last:3"
        },
    };

    public static TestCases<string, string, string> SameRealmAsync => new()
    {
        {
            "await using in a block, the dispose throws",
            "(async () => { try { { await using a = syncAsyncThrower(1); throw 2; } } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "await using in a block, the dispose rejects",
            "(async () => { try { { await using a = asyncThrower(1); throw 2; } } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "await using in a function body, the dispose rejects",
            "(async () => { async function f() { await using a = asyncThrower(1); throw 2; } try { await f(); } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            // b is disposed first and awaited; a's rejection is a later step, run after that await.
            "await using, a rejection after an earlier await",
            "(async () => { async function f() { await using a = asyncThrower(1); await using b = asyncQuiet(); throw 2; } try { await f(); } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "await using, a throw after an earlier await",
            "(async () => { async function f() { await using a = syncAsyncThrower(1); await using b = asyncQuiet(); throw 2; } try { await f(); } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "await using in a for statement header",
            "(async () => { try { for (await using a = asyncThrower(1); ; ) { throw 2; } } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "await using in a for-of body",
            "(async () => { try { for (const x of [0]) { await using a = asyncThrower(1); throw 2; } } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "await using in an async generator body",
            "(async () => { async function* g() { await using a = asyncThrower(1); throw 2; } try { await g().next(); } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "using beside an await using in an async function",
            "(async () => { async function f() { using a = thrower(1); await using b = asyncQuiet(); throw 2; } try { await f(); } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "await using three deep, every level",
            "(async () => { try { await using a = asyncThrower(1); await using b = asyncThrower(2); await using c = asyncThrower(3); throw 4; } catch (e) { return chain(e); } })()",
            "main:false:1 > main:false:2 > main:false:3 > last:4"
        },
        {
            "AsyncDisposableStack.prototype.disposeAsync",
            "(async () => { try { await stackOf(AsyncDisposableStack, 1, 2).disposeAsync(); } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "AsyncDisposableStack.prototype.disposeAsync after move",
            "(async () => { try { await stackOf(AsyncDisposableStack, 1, 2).move().disposeAsync(); } catch (e) { return describe(e); } })()",
            MainSpec
        },
        {
            "AsyncDisposableStack.prototype.disposeAsync, every level",
            "(async () => { const s = stackOf(AsyncDisposableStack, 1, 2, 3); try { await s.disposeAsync(); } catch (e) { return chain(e); } })()",
            "main:false:1 > main:false:2 > last:3"
        },
    };

    [TestCaseSource(nameof(SameRealm)), CancelAfter(10000)]
    public void ASynchronousDisposalGivesTheErrorOnlyErrorAndSuppressed(string name, string script, string expected)
    {
        Run(script).Should().Be(expected, name);
    }

    [TestCaseSource(nameof(SameRealmAsync)), CancelAfter(10000)]
    public void AnAsynchronousDisposalGivesTheErrorOnlyErrorAndSuppressed(string name, string script, string expected)
    {
        Run(script).Should().Be(expected, name);
    }

    [Test, CancelAfter(10000)]
    public void TheMessageOfADisposalErrorReadsAsTheInheritedEmptyString()
    {
        Run("""
            (() => {
              try { { using a = thrower(1); throw 2; } }
              catch (e) {
                const proto = Object.getPrototypeOf(e);
                return [proto === SuppressedError.prototype, e.message === SuppressedError.prototype.message, e.name, String(e)].join(' ');
              }
            })()
            """).Should().Be("true true SuppressedError SuppressedError");
    }

    [Test, CancelAfter(10000)]
    public void AnAssignedMessageOnADisposalErrorIsAnOrdinaryOwnProperty()
    {
        // With no own message the first write creates one, as on any object that inherits a writable data property.
        Run("""
            (() => {
              try { { using a = thrower(1); throw 2; } }
              catch (e) {
                const before = Object.hasOwn(e, 'message');
                e.message = 'later';
                const d = Object.getOwnPropertyDescriptor(e, 'message');
                return [before, e.message, d.writable, d.enumerable, d.configurable].join(' ');
              }
            })()
            """).Should().Be("false later true true true");
    }

    [Test]
    public void TheHostReadsTheSameEmptyMessageOffTheException()
    {
        // JavaScriptException.Message reads message along the prototype chain, so a host sees "" with or without an own one.
        var engine = new Engine();
        var exception = Caught.Exception(() => engine.Execute("{ using a = { [Symbol.dispose]() { throw 1; } }; throw 2; }"));

        var javaScriptException = exception.Should().BeOfType<JavaScriptException>().Subject;
        javaScriptException.Message.Should().BeEmpty();

        var error = javaScriptException.Error.AsObject();
        error.HasOwnProperty("message").Should().BeFalse();
        error.Get("message").AsString().Should().BeEmpty();
        error.Get("error").AsNumber().Should().Be(1);
        error.Get("suppressed").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// The constructor is not DisposeResources and keeps its own rule: a message other than <c>undefined</c> is an own
    /// property, however empty. These rows pass before the change and must go on passing.
    /// </summary>
    [TestCase("new SuppressedError(1, 2)", "false")]
    [TestCase("new SuppressedError(1, 2, undefined)", "false")]
    [TestCase("new SuppressedError(1, 2, '')", "true")]
    [TestCase("new SuppressedError(1, 2, 'm')", "true")]
    [TestCase("SuppressedError(1, 2, 'm')", "true")]
    [TestCase("SuppressedError(1, 2)", "false")]
    public void TheConstructorStillDefinesMessageOnlyForAMessage(string construct, string expected)
    {
        Run($"String(Object.hasOwn({construct}, 'message'))").Should().Be(expected);
    }

    // ---- other realm --------------------------------------------------------------------------------------------

    /// <summary>
    /// The error takes the realm of the running execution context. A function made by the other realm runs in the
    /// other realm, so its disposal's error belongs there even when the main realm called it, and the built-in of one
    /// realm builds the error of its own whichever realm made the stack it disposes.
    /// </summary>
    public static TestCases<string, string, string> OtherRealm => new()
    {
        {
            "other realm, the shape of the error from a function",
            "(() => { const f = otherGlobal.eval('(function (t) { using a = t(1); throw 2; })'); try { f(thrower); } catch (e) { return describe(e); } })()",
            OtherSpec
        },
        {
            "other realm, using in a function",
            "(() => { const f = otherGlobal.eval('(function (t) { using a = t(1); throw 2; })'); try { f(thrower); } catch (e) { return realmOf(e); } })()",
            "other"
        },
        {
            "other realm, using in a block of a function",
            "(() => { const f = otherGlobal.eval('(function (t) { { using a = t(1); throw 2; } })'); try { f(thrower); } catch (e) { return realmOf(e); } })()",
            "other"
        },
        {
            "other realm, using in a generator",
            "(() => { const g = otherGlobal.eval('(function* (t) { using a = t(1); throw 2; })'); try { g(thrower).next(); } catch (e) { return realmOf(e); } })()",
            "other"
        },
        {
            "other realm, the main realm's function called from the other",
            // The function is the main realm's, so its disposal belongs there although the other realm called it.
            "(() => { function f() { using a = thrower(1); throw 2; } try { otherGlobal.Function.prototype.call.call(f); } catch (e) { return realmOf(e); } })()",
            "main"
        },
        {
            "other realm, DisposableStack.prototype.dispose of the other realm on a main realm stack",
            "(() => { try { otherGlobal.DisposableStack.prototype.dispose.call(stackOf(DisposableStack, 1, 2)); } catch (e) { return realmOf(e); } })()",
            "other"
        },
        {
            "other realm, DisposableStack.prototype.dispose of the main realm on an other realm stack",
            "(() => { try { DisposableStack.prototype.dispose.call(stackOf(otherGlobal.DisposableStack, 1, 2)); } catch (e) { return realmOf(e); } })()",
            "main"
        },
        {
            "other realm, a stack of the other realm disposed by its own method",
            "(() => { try { stackOf(otherGlobal.DisposableStack, 1, 2).dispose(); } catch (e) { return realmOf(e); } })()",
            "other"
        },
        {
            "other realm, the reasons cross unchanged",
            "(() => { try { otherGlobal.DisposableStack.prototype.dispose.call(stackOf(DisposableStack, 1, 2)); } catch (e) { return [e.error, e.suppressed, realmOf(e)].join(' '); } })()",
            "1 2 other"
        },
    };

    public static TestCases<string, string, string> OtherRealmAsync => new()
    {
        {
            "other realm, the shape of the error from an async function",
            "(async () => { const f = otherGlobal.eval('(async function (t, q) { await using a = t(1); await using b = q(); throw 2; })'); try { await f(asyncThrower, asyncQuiet); } catch (e) { return describe(e); } })()",
            OtherSpec
        },
        {
            "other realm, await using rejects on the first step",
            "(async () => { const f = otherGlobal.eval('(async function (t) { await using a = t(1); throw 2; })'); try { await f(asyncThrower); } catch (e) { return realmOf(e); } })()",
            "other"
        },
        {
            "other realm, await using throws on the first step",
            "(async () => { const f = otherGlobal.eval('(async function (t) { await using a = t(1); throw 2; })'); try { await f(syncAsyncThrower); } catch (e) { return realmOf(e); } })()",
            "other"
        },
        {
            "other realm, await using rejects after an earlier await",
            "(async () => { const f = otherGlobal.eval('(async function (t, q) { await using a = t(1); await using b = q(); throw 2; })'); try { await f(asyncThrower, asyncQuiet); } catch (e) { return realmOf(e); } })()",
            "other"
        },
        {
            "other realm, await using throws after an earlier await",
            "(async () => { const f = otherGlobal.eval('(async function (t, q) { await using a = t(1); await using b = q(); throw 2; })'); try { await f(syncAsyncThrower, asyncQuiet); } catch (e) { return realmOf(e); } })()",
            "other"
        },
        {
            "other realm, await using in a block with an await in the body",
            "(async () => { const f = otherGlobal.eval('(async function (t) { { await using a = t(1); await null; throw 2; } })'); try { await f(asyncThrower); } catch (e) { return realmOf(e); } })()",
            "other"
        },
        {
            "other realm, await using in an async generator",
            "(async () => { const g = otherGlobal.eval('(async function* (t) { await using a = t(1); throw 2; })'); try { await g(asyncThrower).next(); } catch (e) { return realmOf(e); } })()",
            "other"
        },
        {
            "other realm, await using in an async generator after an earlier await",
            "(async () => { const g = otherGlobal.eval('(async function* (t, q) { await using a = t(1); await using b = q(); throw 2; })'); try { await g(asyncThrower, asyncQuiet).next(); } catch (e) { return realmOf(e); } })()",
            "other"
        },
        {
            "other realm, the main realm's async function called from the other",
            "(async () => { async function f() { await using a = asyncThrower(1); await using b = asyncQuiet(); throw 2; } try { await otherGlobal.Function.prototype.call.call(f); } catch (e) { return realmOf(e); } })()",
            "main"
        },
        {
            "other realm, AsyncDisposableStack.prototype.disposeAsync of the other realm on a main realm stack",
            "(async () => { try { await otherGlobal.AsyncDisposableStack.prototype.disposeAsync.call(stackOf(AsyncDisposableStack, 1, 2)); } catch (e) { return realmOf(e); } })()",
            "other"
        },
        {
            "other realm, AsyncDisposableStack.prototype.disposeAsync of the main realm on an other realm stack",
            "(async () => { try { await AsyncDisposableStack.prototype.disposeAsync.call(stackOf(otherGlobal.AsyncDisposableStack, 1, 2)); } catch (e) { return realmOf(e); } })()",
            "main"
        },
        {
            "other realm, a stack of the other realm disposed by its own method",
            "(async () => { try { await stackOf(otherGlobal.AsyncDisposableStack, 1, 2).disposeAsync(); } catch (e) { return realmOf(e); } })()",
            "other"
        },
    };

    [TestCaseSource(nameof(OtherRealm)), CancelAfter(10000)]
    public void ASynchronousDisposalBuildsItsErrorInTheRealmOfTheRunningCode(string name, string script, string expected)
    {
        Run(script).Should().Be(expected, name);
    }

    [TestCaseSource(nameof(OtherRealmAsync)), CancelAfter(10000)]
    public void AnAsynchronousDisposalBuildsItsErrorInTheRealmOfTheRunningCode(string name, string script, string expected)
    {
        Run(script).Should().Be(expected, name);
    }

    private static string Run(string script)
    {
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        Test262Object.Install(engine);
        return engine.Evaluate(Prelude + script).UnwrapIfPromise().AsString();
    }
}
