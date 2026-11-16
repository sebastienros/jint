#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// The methods of <c>DisposableStack.prototype</c> and <c>AsyncDisposableStack.prototype</c> resolve every <c>%…%</c>
/// intrinsic, and raise every <c>TypeError</c> and <c>ReferenceError</c>, in the current Realm Record, which for a
/// built-in function is the realm the function was created in (<see href="https://tc39.es/ecma262/#sec-built-in-function-objects">built-in function objects</see>),
/// whichever realm called it or created the stack it is applied to (sebastienros/jint#4534).
/// <list type="bullet">
/// <item><see href="https://tc39.es/ecma262/#sec-disposablestack.prototype.move">DisposableStack.prototype.move</see>
/// and <see href="https://tc39.es/ecma262/#sec-asyncdisposablestack.prototype.move">AsyncDisposableStack.prototype.move</see>
/// create the new stack with <c>OrdinaryCreateFromConstructor(%DisposableStack%, …)</c> (and the async equivalent), so it
/// inherits from the built-in's own realm's <c>%DisposableStack.prototype%</c>.</item>
/// <item><see href="https://tc39.es/ecma262/#sec-asyncdisposablestack.prototype.dispose">AsyncDisposableStack.prototype.disposeAsync</see>
/// starts with <c>NewPromiseCapability(%Promise%)</c>, so the promise it returns, and the <c>TypeError</c> it rejects
/// with for a receiver that is no stack, are the built-in's own realm's.</item>
/// <item><c>use</c>, <c>adopt</c>, <c>defer</c>, <c>move</c> and the <c>disposed</c> getter validate the receiver, the
/// resource and the callback, and report a stack that is already disposed, with that realm's errors.</item>
/// </list>
/// The rows named "other realm" call a built-in of a second realm made with <c>$262.createRealm()</c>, the plumbing
/// <see cref="SuppressedErrorFromDisposalTests"/> uses, on a stack of the main realm; the "main" rows are the controls
/// (the main realm's built-in on a stack of the other realm).
/// </summary>
public class DisposableStackRealmTests
{
    private const string Prelude = """
        var otherGlobal = $262.createRealm().global;

        function protoRealm(o) {
          const p = Object.getPrototypeOf(o);
          if (p === otherGlobal.DisposableStack.prototype || p === otherGlobal.AsyncDisposableStack.prototype || p === otherGlobal.Promise.prototype) return 'other';
          if (p === DisposableStack.prototype || p === AsyncDisposableStack.prototype || p === Promise.prototype) return 'main';
          return 'neither';
        }
        function errorRealm(e) {
          if (e instanceof otherGlobal.TypeError) return 'other TypeError';
          if (e instanceof otherGlobal.ReferenceError) return 'other ReferenceError';
          if (e instanceof TypeError) return 'main TypeError';
          if (e instanceof ReferenceError) return 'main ReferenceError';
          return 'neither: ' + e;
        }
        function thrownBy(f) { try { f(); return 'no throw'; } catch (e) { return errorRealm(e); } }
        async function rejectionOf(p) { try { await p; return 'resolved'; } catch (e) { return errorRealm(e); } }

        """;

    private static string Proto(string kind) => kind == "sync" ? "DisposableStack" : "AsyncDisposableStack";

    // ---- move: the stack it returns inherits from the built-in's own realm ----

    public static TestCases<string, string, string> Move
    {
        get
        {
            var rows = new TestCases<string, string, string>();
            foreach (var kind in new[] { "sync", "async" })
            {
                var ctor = Proto(kind);
                rows.Add($"{kind}, other realm's move on a main realm stack",
                    $"const s = new {ctor}(); const moved = otherGlobal.{ctor}.prototype.move.call(s); protoRealm(moved) + ' ' + (moved instanceof otherGlobal.{ctor})",
                    "other true");
                rows.Add($"{kind}, main realm's move on an other realm stack",
                    $"const s = new otherGlobal.{ctor}(); const moved = {ctor}.prototype.move.call(s); protoRealm(moved) + ' ' + (moved instanceof {ctor})",
                    "main true");
                rows.Add($"{kind}, main realm's move on a main realm stack",
                    $"protoRealm(new {ctor}().move())",
                    "main");
                rows.Add($"{kind}, other realm's move on an other realm stack",
                    $"protoRealm(new otherGlobal.{ctor}().move())",
                    "other");
                rows.Add($"{kind}, the moved stack is a working stack of the built-in's realm and the source is disposed",
                    $"const s = new {ctor}(); let n = 0; s.defer({(kind == "sync" ? "() => { n++; }" : "async () => { n++; }")}); "
                    + $"const moved = otherGlobal.{ctor}.prototype.move.call(s); s.disposed + ' ' + moved.disposed + ' ' + protoRealm(moved)",
                    "true false other");
            }
            return rows;
        }
    }

    [TestCaseSource(nameof(Move)), CancelAfter(10000)]
    public void MoveCreatesTheStackInTheRealmOfTheBuiltIn(string name, string script, string expected)
    {
        Run(script).Should().Be(expected, name);
    }

    // ---- disposeAsync: the promise is the built-in's own realm's ----

    public static TestCases<string, string, string> DisposeAsyncPromise => new()
    {
        {
            "other realm's disposeAsync on a main realm stack",
            "protoRealm(otherGlobal.AsyncDisposableStack.prototype.disposeAsync.call(new AsyncDisposableStack()))",
            "other"
        },
        {
            "main realm's disposeAsync on an other realm stack",
            "protoRealm(AsyncDisposableStack.prototype.disposeAsync.call(new otherGlobal.AsyncDisposableStack()))",
            "main"
        },
        {
            "main realm's disposeAsync on a main realm stack",
            "protoRealm(new AsyncDisposableStack().disposeAsync())",
            "main"
        },
        {
            "other realm's disposeAsync on an other realm stack",
            "protoRealm(new otherGlobal.AsyncDisposableStack().disposeAsync())",
            "other"
        },
        {
            "other realm's disposeAsync on a stack that is already disposed",
            "const s = new AsyncDisposableStack(); s.disposeAsync(); protoRealm(otherGlobal.AsyncDisposableStack.prototype.disposeAsync.call(s))",
            "other"
        },
        {
            "other realm's disposeAsync on a stack with resources",
            "const s = new AsyncDisposableStack(); s.defer(async () => {}); protoRealm(otherGlobal.AsyncDisposableStack.prototype.disposeAsync.call(s))",
            "other"
        },
        {
            "other realm's disposeAsync, the promise of a receiver that is no stack",
            "protoRealm(otherGlobal.AsyncDisposableStack.prototype.disposeAsync.call({}))",
            "other"
        },
        {
            "main realm's disposeAsync, the promise of a receiver that is no stack",
            "protoRealm(AsyncDisposableStack.prototype.disposeAsync.call({}))",
            "main"
        },
        {
            "the @@asyncDispose alias is the same function",
            "protoRealm(otherGlobal.AsyncDisposableStack.prototype[Symbol.asyncDispose].call(new AsyncDisposableStack()))",
            "other"
        },
    };

    [TestCaseSource(nameof(DisposeAsyncPromise)), CancelAfter(10000)]
    public void DisposeAsyncReturnsAPromiseOfTheRealmOfTheBuiltIn(string name, string script, string expected)
    {
        Run(script).Should().Be(expected, name);
    }

    // ---- the errors of every method: the built-in's own realm's ----

    public static TestCases<string, string, string> Errors
    {
        get
        {
            var rows = new TestCases<string, string, string>();
            foreach (var kind in new[] { "sync", "async" })
            {
                var ctor = Proto(kind);
                var dispose = kind == "sync" ? "Symbol.dispose" : "Symbol.asyncDispose";

                // A receiver that is no stack of this kind: TypeError of the built-in's realm.
                foreach (var call in new[] { "dispose", "use", "adopt", "defer", "move" })
                {
                    if (call == "dispose" && kind == "async")
                    {
                        continue; // disposeAsync rejects instead of throwing; its rows are below
                    }

                    var method = call == "dispose" ? "dispose" : call;
                    rows.Add($"{kind} {call}, receiver is no stack, other realm's built-in",
                        $"thrownBy(() => otherGlobal.{ctor}.prototype.{method}.call({{}}, {{ [{dispose}]() {{}} }}, () => {{}}))",
                        "other TypeError");
                    rows.Add($"{kind} {call}, receiver is no stack, main realm's built-in",
                        $"thrownBy(() => {ctor}.prototype.{method}.call({{}}, {{ [{dispose}]() {{}} }}, () => {{}}))",
                        "main TypeError");
                }

                rows.Add($"{kind} disposed getter, receiver is no stack, other realm's built-in",
                    $"thrownBy(() => Object.getOwnPropertyDescriptor(otherGlobal.{ctor}.prototype, 'disposed').get.call({{}}))",
                    "other TypeError");
                rows.Add($"{kind} disposed getter, receiver is no stack, main realm's built-in",
                    $"thrownBy(() => Object.getOwnPropertyDescriptor({ctor}.prototype, 'disposed').get.call({{}}))",
                    "main TypeError");

                // A stack of the other kind is no stack of this kind either.
                var otherKind = kind == "sync" ? "AsyncDisposableStack" : "DisposableStack";
                rows.Add($"{kind} use, receiver is a stack of the other kind, other realm's built-in",
                    $"thrownBy(() => otherGlobal.{ctor}.prototype.use.call(new {otherKind}(), null))",
                    "other TypeError");

                // A resource that is not an object, and one without a dispose method.
                rows.Add($"{kind} use, a primitive resource, other realm's built-in on a main realm stack",
                    $"thrownBy(() => otherGlobal.{ctor}.prototype.use.call(new {ctor}(), 1))",
                    "other TypeError");
                rows.Add($"{kind} use, a primitive resource, main realm's built-in on an other realm stack",
                    $"thrownBy(() => {ctor}.prototype.use.call(new otherGlobal.{ctor}(), 1))",
                    "main TypeError");
                rows.Add($"{kind} use, a resource without a dispose method, other realm's built-in",
                    $"thrownBy(() => otherGlobal.{ctor}.prototype.use.call(new {ctor}(), {{}}))",
                    "other TypeError");
                rows.Add($"{kind} use, a resource without a dispose method, main realm's built-in",
                    $"thrownBy(() => {ctor}.prototype.use.call(new otherGlobal.{ctor}(), {{}}))",
                    "main TypeError");
                rows.Add($"{kind} use, a dispose method that is not callable, other realm's built-in",
                    $"thrownBy(() => otherGlobal.{ctor}.prototype.use.call(new {ctor}(), {{ [{dispose}]: 1 }}))",
                    "other TypeError");
                rows.Add($"{kind} use, a dispose method that is not callable, main realm's built-in",
                    $"thrownBy(() => {ctor}.prototype.use.call(new otherGlobal.{ctor}(), {{ [{dispose}]: 1 }}))",
                    "main TypeError");

                // A callback that is not callable.
                rows.Add($"{kind} defer, a callback that is not callable, other realm's built-in",
                    $"thrownBy(() => otherGlobal.{ctor}.prototype.defer.call(new {ctor}(), 1))",
                    "other TypeError");
                rows.Add($"{kind} defer, a callback that is not callable, main realm's built-in",
                    $"thrownBy(() => {ctor}.prototype.defer.call(new otherGlobal.{ctor}(), 1))",
                    "main TypeError");
                rows.Add($"{kind} adopt, a callback that is not callable, other realm's built-in",
                    $"thrownBy(() => otherGlobal.{ctor}.prototype.adopt.call(new {ctor}(), 1, 1))",
                    "other TypeError");
                rows.Add($"{kind} adopt, a callback that is not callable, main realm's built-in",
                    $"thrownBy(() => {ctor}.prototype.adopt.call(new otherGlobal.{ctor}(), 1, 1))",
                    "main TypeError");

                // A stack that is already disposed: ReferenceError.
                var disposeNow = kind == "sync" ? "s.dispose()" : "s.disposeAsync()";
                foreach (var call in new[] { "use", "adopt", "defer", "move" })
                {
                    var args = call switch
                    {
                        "use" => "null",
                        "adopt" => "1, () => {}",
                        "defer" => "() => {}",
                        _ => "",
                    };
                    rows.Add($"{kind} {call}, the stack is disposed, other realm's built-in",
                        $"const s = new {ctor}(); {disposeNow}; thrownBy(() => otherGlobal.{ctor}.prototype.{call}.call(s, {args}))",
                        "other ReferenceError");
                    rows.Add($"{kind} {call}, the stack is disposed, main realm's built-in",
                        $"const s = new otherGlobal.{ctor}(); {(kind == "sync" ? "s.dispose()" : "s.disposeAsync()")}; thrownBy(() => {ctor}.prototype.{call}.call(s, {args}))",
                        "main ReferenceError");
                }
            }

            return rows;
        }
    }

    [TestCaseSource(nameof(Errors)), CancelAfter(10000)]
    public void ErrorsAreRaisedInTheRealmOfTheBuiltIn(string name, string script, string expected)
    {
        Run(script).Should().Be(expected, name);
    }

    public static TestCases<string, string, string> DisposeAsyncRejection => new()
    {
        {
            "other realm's disposeAsync, receiver is no stack",
            "rejectionOf(otherGlobal.AsyncDisposableStack.prototype.disposeAsync.call({}))",
            "other TypeError"
        },
        {
            "main realm's disposeAsync, receiver is no stack",
            "rejectionOf(AsyncDisposableStack.prototype.disposeAsync.call({}))",
            "main TypeError"
        },
        {
            "other realm's disposeAsync, receiver is a DisposableStack",
            "rejectionOf(otherGlobal.AsyncDisposableStack.prototype.disposeAsync.call(new DisposableStack()))",
            "other TypeError"
        },
    };

    [TestCaseSource(nameof(DisposeAsyncRejection)), CancelAfter(10000)]
    public void DisposeAsyncRejectsWithAnErrorOfTheRealmOfTheBuiltIn(string name, string script, string expected)
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
