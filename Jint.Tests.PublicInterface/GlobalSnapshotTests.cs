#nullable enable

// Reads a Jint diagnostic API declared outside the compatibility contract. Acknowledged the way an embedder
// acknowledges it; see Jint/JintDiagnosticIds.cs.
#pragma warning disable JINT0001

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>engine.Advanced.CaptureGlobalSnapshot()</c> / <c>RestoreGlobalSnapshot(snapshot)</c> — the
/// configuration-reuse primitive that lets a host evaluate many scripts on one configured engine instead of
/// building a fresh one per evaluation.
///
/// <para>
/// These live in the public-interface suite because the whole point is what a third-party embedder can
/// reach: the project has no internals access, so everything exercised here is genuinely part of the
/// surface. Two groups of tests matter equally. The first pins what restore <em>does</em> revert. The second
/// — the "honesty pins" at the bottom — asserts the documented NON-guarantees as surviving, because the
/// contract is that this is a reuse primitive and not an isolation boundary, and a test that quietly started
/// passing for the opposite reason would be a footgun shipped as a feature.
/// </para>
/// </summary>
public class GlobalSnapshotTests
{
    private sealed class Config
    {
        public int X { get; set; }
    }

    // ---------------------------------------------------------------------------------------------
    // The blocker: top-level lexical declarations
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The red run for the feature's headline blocker. A script's top-level <c>let</c>/<c>const</c>/
    /// <c>class</c> lands in the global environment's declarative record, which nothing else on the public
    /// surface can touch — not deleting the global, not re-setting it, not removing an own property. This
    /// stays true with the feature in place; it is the reason the feature exists.
    /// </summary>
    [Fact]
    public void TopLevelLetCannotBeClearedByAnyOtherPublicMeans()
    {
        var engine = new Engine();
        engine.Evaluate("let blocker = 1;");

        // everything a host could plausibly reach for
        engine.Evaluate("delete globalThis.blocker;");
        engine.SetValue("blocker", JsValue.Undefined);
        engine.Global.RemoveOwnProperty("blocker");
        engine.Advanced.ResetCallStack();

        Invoking(() => engine.Evaluate("let blocker = 1;"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("*already been declared*");
    }

    [Fact]
    public void RestoreClearsTopLevelLexicalDeclarationsSoTheSameScriptRunsAgain()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var script = Engine.PrepareScript("let a = 1; const b = 2; class C {} a + b + (new C() instanceof C ? 0 : 100);");

        engine.Evaluate(script).AsNumber().Should().Be(3);

        // without the restore this is the "already been declared" wall
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        engine.Evaluate(script).AsNumber().Should().Be(3);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        engine.Evaluate(script).AsNumber().Should().Be(3);
    }

    [Fact]
    public void RestoreLeavesLexicalNamesRedeclarableAsAnyKind()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("let shared = 1;");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // the TDZ slot must be gone, not merely re-initialized: a different declaration kind must bind
        engine.Evaluate("const shared = 2; shared").AsNumber().Should().Be(2);
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("var shared = 3; shared").AsNumber().Should().Be(3);
    }

    [Fact]
    public void LexicalDeclarationsPresentAtCaptureTimeAreRestored()
    {
        var engine = new Engine();
        engine.Evaluate("let baseline = 'kept'; const frozenBaseline = 7;");

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("baseline = 'changed'; let extra = 1;");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("baseline").AsString().Should().Be("kept");
        engine.Evaluate("frozenBaseline").AsNumber().Should().Be(7);
        engine.Evaluate("typeof extra").AsString().Should().Be("undefined");
    }

    // ---------------------------------------------------------------------------------------------
    // Global object own properties
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RestoreRemovesScriptAddedGlobals()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("var v = 1; function f() {} implicitGlobal = 3; globalThis.assigned = 4;");

        engine.Evaluate("typeof v").AsString().Should().Be("number");
        engine.Evaluate("typeof f").AsString().Should().Be("function");
        engine.Evaluate("typeof implicitGlobal").AsString().Should().Be("number");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("typeof v").AsString().Should().Be("undefined");
        engine.Evaluate("typeof f").AsString().Should().Be("undefined");
        engine.Evaluate("typeof implicitGlobal").AsString().Should().Be("undefined");
        engine.Evaluate("typeof globalThis.assigned").AsString().Should().Be("undefined");
    }

    [Fact]
    public void RestoreRemovesScriptAddedSymbolKeyedGlobals()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var pristineTag = new Engine().Evaluate("Object.prototype.toString.call(globalThis)").AsString();

        engine.Evaluate("globalThis[Symbol.for('marker')] = 1; globalThis[Symbol.toStringTag] = 'Hijacked';");
        engine.Evaluate("globalThis[Symbol.for('marker')]").AsNumber().Should().Be(1);
        engine.Evaluate("Object.prototype.toString.call(globalThis)").AsString().Should().Be("[object Hijacked]");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("typeof globalThis[Symbol.for('marker')]").AsString().Should().Be("undefined");
        engine.Evaluate("Object.prototype.toString.call(globalThis)").AsString().Should().Be(pristineTag);
    }

    [Fact]
    public void RestoreReinstatesADeletedBuiltinGlobal()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("delete globalThis.JSON; delete globalThis.parseInt;");
        engine.Evaluate("typeof JSON").AsString().Should().Be("undefined");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("JSON.stringify({a: 1})").AsString().Should().Be("{\"a\":1}");
        engine.Evaluate("parseInt('42')").AsNumber().Should().Be(42);
    }

    [Fact]
    public void RestoreRevertsAnOverwrittenBuiltinGlobal()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("Array = 5; Math = null;");
        engine.Evaluate("typeof Array").AsString().Should().Be("number");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("Array.isArray([])").AsBoolean().Should().BeTrue();
        engine.Evaluate("Math.max(1, 2)").AsNumber().Should().Be(2);
    }

    [Fact]
    public void RestoreRevertsAnOverwrittenHostGlobalWrittenByAssignment()
    {
        var engine = new Engine();
        engine.SetValue("hostValue", 1);
        engine.SetValue("hostFn", new Func<int>(() => 7));
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("hostValue = 99; hostFn = function () { return -1; };");
        engine.Evaluate("hostValue").AsNumber().Should().Be(99);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("hostValue").AsNumber().Should().Be(1);
        engine.Evaluate("hostFn()").AsNumber().Should().Be(7);
    }

    [Fact]
    public void RestoreRevertsAFlagsOnlyDefinePropertyFlip()
    {
        var engine = new Engine();
        engine.SetValue("hostValue", 1);
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        // defineProperty flips the attribute bits on the very descriptor that is already there, without
        // replacing it — the case a "put the captured descriptor back" restore would silently miss.
        engine.Evaluate("Object.defineProperty(globalThis, 'hostValue', { writable: false, enumerable: false });");
        engine.Evaluate("hostValue = 42; hostValue").AsNumber().Should().Be(1, "the sloppy write is dropped while non-writable");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'hostValue').writable").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'hostValue').enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate("hostValue = 42; hostValue").AsNumber().Should().Be(42);
    }

    [Fact]
    public void RestoreRevertsADefinePropertyValueOverwriteOfABuiltin()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("Object.defineProperty(globalThis, 'JSON', { value: 'hijacked' });");
        engine.Evaluate("JSON").AsString().Should().Be("hijacked");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("JSON.stringify(1)").AsString().Should().Be("1");
    }

    [Fact]
    public void RestoreRevertsAnAccessorDefinedOnTheGlobal()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("Object.defineProperty(globalThis, 'trap', { get: function () { return 'gotcha'; }, configurable: true });");
        engine.Evaluate("trap").AsString().Should().Be("gotcha");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("typeof globalThis.trap").AsString().Should().Be("undefined");
        engine.Evaluate("'trap' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void RestoreRevertsAnAccessorDefinedOverAnExistingHostGlobal()
    {
        var engine = new Engine();
        engine.SetValue("hostValue", 1);
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("Object.defineProperty(globalThis, 'hostValue', { get: function () { return 'gotcha'; } });");
        engine.Evaluate("hostValue").AsString().Should().Be("gotcha");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("hostValue").AsNumber().Should().Be(1);
        engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'hostValue').get === undefined").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void RestoreRevertsThePrototypeAndExtensibilityOfTheGlobal()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("Object.setPrototypeOf(globalThis, null); Object.preventExtensions(globalThis);");
        engine.Evaluate("Object.getPrototypeOf(globalThis) === null").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.isExtensible(globalThis)").AsBoolean().Should().BeFalse();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("Object.getPrototypeOf(globalThis) === Object.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.isExtensible(globalThis)").AsBoolean().Should().BeTrue();
        engine.Evaluate("var afterRestore = 1; afterRestore").AsNumber().Should().Be(1);
    }

    [Fact]
    public void RestorePreservesTheIdentityOfTheGlobalObject()
    {
        var engine = new Engine();
        var before = engine.Global;
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("var v = 1; let l = 2; delete globalThis.JSON; Object.setPrototypeOf(globalThis, null);");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Global.Should().BeSameAs(before);
        engine.Evaluate("globalThis === this").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void RestoreKeepsTheGlobalInItsSharedBuiltinLayoutAfterADeopt()
    {
        var engine = new Engine();
        engine.Advanced.GetObjectRepresentation(engine.Global).Should().Be(ObjectRepresentation.SharedBuiltinLayout);

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        // an integer-like own key is the one thing the fixed built-in layout cannot express
        engine.Evaluate("globalThis[0] = 'deopt';");
        engine.Advanced.GetObjectRepresentation(engine.Global).Should().NotBe(ObjectRepresentation.SharedBuiltinLayout);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Advanced.GetObjectRepresentation(engine.Global).Should().Be(ObjectRepresentation.SharedBuiltinLayout);
        engine.Evaluate("typeof globalThis[0]").AsString().Should().Be("undefined");
        engine.Evaluate("JSON.stringify([Math.max(1, 2), parseInt('3')])").AsString().Should().Be("[2,3]");
    }

    [Fact]
    public void ASecondScriptSeesThePristineEnvironment()
    {
        var engine = new Engine();
        engine.SetValue("hostValue", 1);
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var script = Engine.PrepareScript(
            """
            let seen = typeof leftover;
            var leftover = 'from-this-run';
            hostValue = hostValue + 1;
            seen + '/' + hostValue;
            """);

        for (var i = 0; i < 3; i++)
        {
            engine.Evaluate(script).AsString().Should().Be("undefined/2");
            engine.Advanced.RestoreGlobalSnapshot(snapshot);
        }
    }

    /// <summary>
    /// The shape the reports describe: a host that installs dozens of globals per evaluation. Enough of
    /// them to push the global's overflow storage past its small-table cutover, so the rebuild is exercised
    /// on both representations, and the own-key order has to come back exactly as captured.
    /// </summary>
    [Fact]
    public void RestoreRebuildsAWideGlobalSurfaceInItsCapturedOrder()
    {
        var engine = new Engine();
        for (var i = 0; i < 40; i++)
        {
            var captured = i;
            engine.SetValue("host" + i, new Func<int>(() => captured));
        }

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        var orderBefore = engine.Evaluate("Object.getOwnPropertyNames(globalThis).join(',')").AsString();

        engine.Evaluate(
            """
            delete globalThis.host17;
            host23 = 'clobbered';
            for (var i = 0; i < 40; i++) { globalThis['script' + i] = i; }
            """);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("Object.getOwnPropertyNames(globalThis).join(',')").AsString().Should().Be(orderBefore);
        engine.Evaluate("host17()").AsNumber().Should().Be(17);
        engine.Evaluate("host23()").AsNumber().Should().Be(23);
        engine.Evaluate("typeof script39").AsString().Should().Be("undefined");
    }

    // ---------------------------------------------------------------------------------------------
    // Cleared carry-over
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RestoreDiscardsPendingPromiseContinuations()
    {
        static Engine Arrange()
        {
            var engine = new Engine();
            // a throwing script never reaches the end-of-evaluation drain, so the already-queued
            // reaction survives the evaluation
            Invoking(() => engine.Evaluate("Promise.resolve().then(function () { globalThis.ran = true; }); throw new Error('boom');"))
                .Should().Throw<JavaScriptException>();
            return engine;
        }

        // control: the leftover job really does run against the next evaluation
        var control = Arrange();
        control.Advanced.ProcessTasks();
        control.Evaluate("globalThis.ran === true").AsBoolean().Should().BeTrue();

        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        Invoking(() => engine.Evaluate("Promise.resolve().then(function () { globalThis.ran = true; }); throw new Error('boom');"))
            .Should().Throw<JavaScriptException>();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        engine.Advanced.ProcessTasks();

        engine.Evaluate("typeof globalThis.ran").AsString().Should().Be("undefined");
    }

    /// <summary>
    /// The cross-tenant leak the event-loop fence exists for. A fire-and-forget async function suspended on a
    /// host promise leaves nothing in the queue, so the discard at restore time cannot see it; when the host
    /// settles that promise afterwards, the suspended body would resume and write tenant A's data into tenant
    /// B's global surface. Deterministic: <c>RegisterPromise</c>'s resolve enqueues and drains synchronously
    /// on the calling thread, so there is no scheduling to wait on.
    /// </summary>
    [Fact]
    public void APromiseRegisteredBeforeARestoreDoesNotResumeItsContinuationAfterwards()
    {
        static (Engine Engine, ManualPromise Handle) Arrange()
        {
            var engine = new Engine();
            var handle = engine.Advanced.RegisterPromise();
            engine.SetValue("hostWork", handle.Promise);
            return (engine, handle);
        }

        // control: with no restore in between, the continuation runs — the fence must not break the feature
        var (control, controlHandle) = Arrange();
        control.Evaluate("(async () => { await hostWork; globalThis.cache = 'tenantA'; })();");
        controlHandle.Resolve(JsValue.Undefined);
        control.Advanced.ProcessTasks();
        control.Evaluate("globalThis.cache").AsString().Should().Be("tenantA");

        var (engine, handle) = Arrange();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("(async () => { await hostWork; globalThis.cache = 'tenantA'; })();");

        // nothing is queued yet: the promise has not settled, so the discard has nothing to discard
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        handle.Resolve(JsValue.Undefined);
        engine.Advanced.ProcessTasks();

        engine.Evaluate("typeof globalThis.cache").AsString().Should().Be("undefined");
    }

    /// <summary>
    /// The same fence seen from the host side of the guard: while an <c>EvaluateAsync</c> is suspended on a
    /// host task the engine looks idle — the synchronous phase has finished, the stack is back at base depth —
    /// so only the pending-operation count can tell restore that an evaluation the host still holds is in
    /// flight.
    /// </summary>
    [Fact]
    public async Task RestoringWhileAnAsyncEvaluationIsOutstandingIsRejected()
    {
        var engine = new Engine();
        var gate = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SetValue("hostWork", new Func<Task<object>>(() => gate.Task));
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var pending = engine.EvaluateAsync("(async () => { await hostWork(); return 42; })()");
        pending.IsCompleted.Should().BeFalse();

        Invoking(() => engine.Advanced.RestoreGlobalSnapshot(snapshot))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*asynchronous operation in progress*");

        gate.SetResult(1);
        (await pending).AsNumber().Should().Be(42);

        // and the engine is restorable again the moment that evaluation is really over
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
    }

    [Fact]
    public void RestoreClearsTheRegExpLegacyStatics()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("/(\\d+)-(\\w+)/.exec('secret 123-abc tail');");
        engine.Evaluate("RegExp.$1").AsString().Should().Be("123");
        engine.Evaluate("RegExp.input").AsString().Should().Be("secret 123-abc tail");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("RegExp.$1").AsString().Should().BeEmpty();
        engine.Evaluate("RegExp.$2").AsString().Should().BeEmpty();
        engine.Evaluate("RegExp.input").AsString().Should().BeEmpty();
        engine.Evaluate("RegExp.lastMatch").AsString().Should().BeEmpty();
        engine.Evaluate("RegExp.leftContext").AsString().Should().BeEmpty();
        engine.Evaluate("RegExp.rightContext").AsString().Should().BeEmpty();
    }

    [Fact]
    public void RestoreDropsExpandosLeftOnAWrappedHostObject()
    {
        var config = new Config();
        var engine = new Engine(o => o.AllowClr());
        engine.SetValue("getConfig", new Func<Config>(() => config));
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("getConfig().expando = 'leaked';");
        engine.Evaluate("getConfig().expando").AsString().Should().Be("leaked", "the wrapper is cached, so the expando is still there within one run");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("typeof getConfig().expando").AsString().Should().Be("undefined");
    }

    // ---------------------------------------------------------------------------------------------
    // Lazily materialized globals
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void CaptureDoesNotMaterializeALazyGlobal()
    {
        var calls = 0;
        var engine = new Engine(options => options.AddLazyGlobal("hostApi", e =>
        {
            calls++;
            return new ClrFunction(e, "hostApi", (_, _) => "from-host");
        }));

        engine.Advanced.CaptureGlobalSnapshot();
        calls.Should().Be(0);
    }

    [Fact]
    public void RestoreReturnsAnOverwrittenLazyGlobalToItsUnmaterializedState()
    {
        var calls = 0;
        var engine = new Engine(options => options.AddLazyGlobal("hostApi", e =>
        {
            calls++;
            return new ClrFunction(e, "hostApi", (_, _) => "from-host");
        }));

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("hostApi = 'hijacked';");
        engine.Evaluate("hostApi").AsString().Should().Be("hijacked");
        calls.Should().Be(1, "the overwriting write itself resolves the pending value first");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("hostApi()").AsString().Should().Be("from-host");
        calls.Should().Be(2, "the factory had not yet run at capture time, so restoring that state means it runs again on the next read");
    }

    // ---------------------------------------------------------------------------------------------
    // Host-authored storage: what a snapshot can and cannot see
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A host descriptor that keeps its value in state of its own rather than in the inherited field. This is
    /// legal — <c>CustomJsValue</c> is the documented lazy-value hook — but it puts the value where a snapshot
    /// cannot reach it.
    /// </summary>
    private sealed class HostStateDescriptor : PropertyDescriptor
    {
        private JsValue? _state;

        public HostStateDescriptor(JsValue initial)
            : base(PropertyFlag.ConfigurableEnumerableWritable | PropertyFlag.CustomJsValue)
        {
            _state = initial;
        }

        // protected, not protected internal: from outside the Jint assembly that is what the member looks like
        protected override JsValue? CustomValue
        {
            get => _state;
            set => _state = value;
        }
    }

    /// <summary>
    /// Honesty pin. Restore reinstates such a descriptor by reference and reverts its attribute flags, but it
    /// does not revert the value, because the value is not in a field the engine owns. Writing that field
    /// anyway would not restore the property — it would only make the field disagree with the value reads
    /// resolve to, which is a worse failure than the documented one.
    /// </summary>
    [Fact]
    public void AHostDescriptorHoldingItsValueOutsideTheEngineIsNotReverted()
    {
        var engine = new Engine();
        engine.Global.FastSetProperty("cfg", new HostStateDescriptor(JsNumber.Create(1)));
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("cfg = 5;");
        engine.Evaluate("cfg").AsNumber().Should().Be(5);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // documented non-guarantee: the value lives in host state, so it stays
        engine.Evaluate("cfg").AsNumber().Should().Be(5);

        // ... but every lane agrees on that one value — the descriptor is not left half-reverted
        engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'cfg').value").AsNumber().Should().Be(5);
        engine.Evaluate("Object.values(globalThis).indexOf(5) >= 0").AsBoolean().Should().BeTrue();
        engine.Evaluate("JSON.parse(JSON.stringify({ v: cfg })).v").AsNumber().Should().Be(5);
    }

    /// <summary>
    /// The flag half is still reverted: attributes are the engine's own state, wherever the value lives.
    /// </summary>
    [Fact]
    public void AHostDescriptorsAttributeFlagsAreStillReverted()
    {
        var engine = new Engine();
        engine.Global.FastSetProperty("cfg", new HostStateDescriptor(JsNumber.Create(1)));
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("Object.defineProperty(globalThis, 'cfg', { enumerable: false, configurable: false });");
        engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'cfg').enumerable").AsBoolean().Should().BeFalse();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'cfg').enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'cfg').configurable").AsBoolean().Should().BeTrue();
    }

    /// <summary>A global that resolves its own properties itself — the shape a snapshot cannot serve.</summary>
    private sealed class ProjectingGlobalObject : ObjectInstance
    {
        public ProjectingGlobalObject(Engine engine) : base(engine)
        {
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property) => base.GetOwnProperty(property);
    }

    private sealed class ProjectingGlobalHost : Host
    {
        protected override ObjectInstance CreateGlobalObject(Realm realm) => new ProjectingGlobalObject(Engine);
    }

    /// <summary>
    /// Fail fast rather than hand back a snapshot that restores nothing. Capture reads the engine's property
    /// tables and restore writes them; a global that answers own-property questions from its own state would
    /// see both halves quietly no-op, and every restore would report success while the script's changes stayed
    /// in place.
    /// </summary>
    [Fact]
    public void CapturingAGlobalThatResolvesItsOwnPropertiesIsRefused()
    {
        var engine = new Engine(options => options.UseHostFactory(_ => new ProjectingGlobalHost()));

        Invoking(() => engine.Advanced.CaptureGlobalSnapshot())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*GetOwnProperty*");
    }

    // ---------------------------------------------------------------------------------------------
    // Lifecycle and misuse
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ASnapshotFromAnotherEngineIsRejected()
    {
        var a = new Engine();
        var b = new Engine();
        var snapshot = a.Advanced.CaptureGlobalSnapshot();

        Invoking(() => b.Advanced.RestoreGlobalSnapshot(snapshot))
            .Should().Throw<ArgumentException>()
            .WithMessage("*different engine*");
    }

    [Fact]
    public void ANullSnapshotIsRejected()
    {
        var engine = new Engine();
        Invoking(() => engine.Advanced.RestoreGlobalSnapshot(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RestoringDuringAnEvaluationIsRejected()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        Exception? caught = null;
        engine.SetValue("reenter", new Action(() =>
        {
            try
            {
                engine.Advanced.RestoreGlobalSnapshot(snapshot);
            }
            catch (Exception e)
            {
                caught = e;
            }
        }));

        engine.Evaluate("reenter();");

        caught.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void RestoringFromInsideAHostCallWithNoScriptOnTheStackIsRejected()
    {
        // The sibling above re-enters from a callback invoked BY script, so the engine has an
        // execution context pushed. This one calls the delegate directly through Engine.Call: a
        // ClrFunction pushes no execution context at all, so execution-context depth alone cannot
        // see it and the rejection rests on the host-entry count instead.
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        Exception? caught = null;
        engine.SetValue("reenter", new Action(() =>
        {
            try
            {
                engine.Advanced.RestoreGlobalSnapshot(snapshot);
            }
            catch (Exception e)
            {
                caught = e;
            }
        }));

        engine.Invoke("reenter");

        caught.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void RestoringAfterEveryHostCallHasReturnedIsAllowed()
    {
        // The counterpart to the two above: once the entry has unwound the engine is idle again and
        // a restore must be accepted, so the guard cannot simply latch.
        var engine = new Engine();
        engine.Execute("var marker = 1;");
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.SetValue("noop", new Action(() => { }));
        engine.Invoke("noop");
        engine.Evaluate("marker = 2;");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("marker").AsNumber().Should().Be(1);
    }

    [Fact]
    public void RestoringTwiceInARowIsANoOpTheSecondTime()
    {
        var engine = new Engine();
        engine.SetValue("hostValue", 1);
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("var v = 1; let l = 2; hostValue = 9; delete globalThis.JSON;");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("hostValue").AsNumber().Should().Be(1);
        engine.Evaluate("typeof v").AsString().Should().Be("undefined");
        engine.Evaluate("JSON.stringify(2)").AsString().Should().Be("2");
        engine.Evaluate("let l = 5; l").AsNumber().Should().Be(5);
    }

    [Fact]
    public void CaptureRestoreCaptureLayersCorrectly()
    {
        var engine = new Engine();
        var pristine = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("var stage = 'one';");
        var withStage = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("stage = 'two'; var extra = 1;");

        engine.Advanced.RestoreGlobalSnapshot(withStage);
        engine.Evaluate("stage").AsString().Should().Be("one");
        engine.Evaluate("typeof extra").AsString().Should().Be("undefined");

        engine.Advanced.RestoreGlobalSnapshot(pristine);
        engine.Evaluate("typeof stage").AsString().Should().Be("undefined");

        // the older snapshot is still usable afterwards, and still means what it meant
        engine.Advanced.RestoreGlobalSnapshot(withStage);
        engine.Evaluate("stage").AsString().Should().Be("one");
    }

    [Fact]
    public void RestoreWithAnImmediatelyTakenSnapshotDegradesToClearingLexicalDeclarations()
    {
        // the narrower "clear the let/const bindings only" primitive, for free
        var engine = new Engine();
        engine.Evaluate("var kept = 1;");
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("let scoped = 2;");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("kept").AsNumber().Should().Be(1);
        engine.Evaluate("let scoped = 3; scoped").AsNumber().Should().Be(3);
    }

    // ---------------------------------------------------------------------------------------------
    // Honesty pins: the documented NON-guarantees, asserted as surviving
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Prototype pollution survives a restore. Reverting it would mean snapshotting the whole reachable
    /// intrinsic graph, which is re-creating the realm — i.e. <c>new Engine</c>. Asserted here so the
    /// contract cannot drift silently in either direction.
    /// </summary>
    [Fact]
    public void ObjectPrototypePollutionSurvivesRestore()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("Object.prototype.polluted = 1; Array.prototype.first = function () { return this[0]; };");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("({}).polluted").AsNumber().Should().Be(1);
        engine.Evaluate("[7].first()").AsNumber().Should().Be(7);
    }

    [Fact]
    public void AFrozenPrototypeStaysFrozenAcrossRestore()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("Object.freeze(Object.prototype);");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("Object.isFrozen(Object.prototype)").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ADeletedPrototypeMethodStaysDeletedAcrossRestore()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("delete String.prototype.trim;");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("typeof ''.trim").AsString().Should().Be("undefined");
    }

    /// <summary>
    /// Restore reverts the global's own property <em>table</em>, never the contents of the values in it: a
    /// binding is put back pointing at the same object, mutations and all.
    /// </summary>
    [Fact]
    public void MutationsInsideAnObjectGraphBehindARestoredBindingSurvive()
    {
        var engine = new Engine();
        var holder = new JsObject(engine);
        holder.FastSetDataProperty("x", 1);
        engine.SetValue("holder", holder);

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("holder.x = 42; holder.added = 'new';");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("holder.x").AsNumber().Should().Be(42);
        engine.Evaluate("holder.added").AsString().Should().Be("new");
    }

    [Fact]
    public void HostClrStateChangedThroughInteropSurvivesRestore()
    {
        var config = new Config();
        var engine = new Engine(o => o.AllowClr().Interop.AllowWrite = true);
        engine.SetValue("config", config);

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("config.X = 5;");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        config.X.Should().Be(5);
        engine.Evaluate("config.X").AsNumber().Should().Be(5);
    }

    [Fact]
    public void TheSymbolForRegistrySurvivesRestore()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("Symbol.for('shared');");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("Symbol.keyFor(Symbol.for('shared'))").AsString().Should().Be("shared");
    }

    [Fact]
    public void RegisteredModulesSurviveRestore()
    {
        var engine = new Engine();
        engine.Modules.Add("lib", builder => builder.ExportValue("version", 1));

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine.Modules.Import("lib").Get("version").AsNumber().Should().Be(1);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Modules.Import("lib").Get("version").AsNumber().Should().Be(1);
    }

    // ---------------------------------------------------------------------------------------------
    // Lazy globals: a restore reverts the descriptor, never the factory's own memo
    // ---------------------------------------------------------------------------------------------
    //
    // The three tests below are one rule seen from three sides, and it is the rule a host has to apply to
    // its own globals as well as read about ours. A restore returns an unmaterialized lazy descriptor to
    // the "not resolved yet" state it was captured in, and the next read therefore runs the factory AGAIN.
    // Whether that produces a fresh object is decided entirely by the factory — not by the restore, which
    // has no idea what the factory does. A factory that constructs gives the next cycle a new object; a
    // factory that hands back something it is holding gives the next cycle exactly what the previous one
    // mutated. Every global Jint installs itself — the 58 ECMAScript ones and every web API — is the second
    // kind, because its factory is a read of the realm's memoized intrinsic.

    /// <summary>
    /// Honesty pin, and the one a host is most likely to get wrong about its own globals. The factory runs
    /// a second time after the restore, but it answers with the object it kept — so the next cycle sees the
    /// previous cycle's mutations. This is the shape every in-box lazy global has.
    /// </summary>
    [Fact]
    public void ALazyGlobalWhoseFactoryMemoizesHandsBackTheSameObjectAfterRestore()
    {
        var runs = 0;
        ObjectInstance? memo = null;
        var engine = new Engine(options => options.AddLazyGlobal(
            "host",
            e =>
            {
                runs++;
                return memo ??= new JsObject(e);
            }));

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var before = engine.Evaluate("host");
        engine.Evaluate("host.scribble = 1;");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        var after = engine.Evaluate("host");

        // The descriptor really was reverted: the factory was asked a second time.
        runs.Should().Be(2);

        // ... and answered with the same object, mutation and all.
        after.Should().BeSameAs(before);
        engine.Evaluate("host.scribble").AsNumber().Should().Be(1);
    }

    /// <summary>
    /// The other side of the same rule: a factory that builds rather than remembers does give the next
    /// cycle a genuinely fresh object. Jint's own <c>process</c> shim is the one in-box global of this
    /// shape; everything else memoizes.
    /// </summary>
    [Fact]
    public void ALazyGlobalWhoseFactoryConstructsHandsBackAFreshObjectAfterRestore()
    {
        var runs = 0;
        var engine = new Engine(options => options.AddLazyGlobal(
            "host",
            e =>
            {
                runs++;
                return new JsObject(e);
            }));

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var before = engine.Evaluate("host");
        engine.Evaluate("host.scribble = 1;");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        var after = engine.Evaluate("host");

        runs.Should().Be(2);
        after.Should().NotBeSameAs(before);
        engine.Evaluate("typeof host.scribble").AsString().Should().Be("undefined");
    }

    /// <summary>
    /// And the freshness above is only available while the snapshot was captured <em>before</em> the global
    /// was first read. Capture it afterwards and the captured descriptor already holds the object, so the
    /// restore reinstates that object rather than the unmaterialized state — the factory is never asked
    /// again, whatever it would have answered. Which is why the documented recipe is to capture after host
    /// configuration and before evaluating anything.
    /// </summary>
    [Fact]
    public void ALazyGlobalCapturedAfterItsFirstReadIsReinstatedRatherThanRebuilt()
    {
        var runs = 0;
        var engine = new Engine(options => options.AddLazyGlobal(
            "host",
            e =>
            {
                runs++;
                return new JsObject(e);
            }));

        var before = engine.Evaluate("host");
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("host.scribble = 1;");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        var after = engine.Evaluate("host");

        runs.Should().Be(1);
        after.Should().BeSameAs(before);
        engine.Evaluate("host.scribble").AsNumber().Should().Be(1);
    }

    /// <summary>
    /// Honesty pin for the in-box half of that rule, on the plainest engine there is. <c>Math</c> is a
    /// namespace object reached through a lazy global whose factory is a read of the realm's memoized
    /// intrinsic — structurally the same global as <c>console</c>, and the reason the answer for the web
    /// APIs cannot be different from the answer here without the engine contradicting itself.
    /// </summary>
    [Fact]
    public void AnIntrinsicNamespaceObjectIsTheSameObjectAfterRestoreAndKeepsWhatWasWrittenOnIt()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var before = engine.Evaluate("Math");
        engine.Evaluate("Math.scribble = 1; JSON.scribble = 2;");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        var after = engine.Evaluate("Math");

        after.Should().BeSameAs(before);
        engine.Evaluate("Math.scribble").AsNumber().Should().Be(1);
        engine.Evaluate("JSON.scribble").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// The other half of "where does the value live", and the case that makes the rule not simply "nothing
    /// is rebuilt". The global object's own built-in function slots — <c>decodeURI</c> and its eight
    /// siblings — hold their function object in the slot and nowhere else, so returning the slot to
    /// unmaterialized really does mean the next read builds a new function. A host that kept a reference
    /// across the restore is holding a detached one, and a mutation the previous cycle made is gone.
    /// </summary>
    /// <remarks>
    /// Contrast <c>parseInt</c> and <c>parseFloat</c>, which look like the same kind of global and are not:
    /// they are the very function objects <c>Number.parseInt</c> and <c>Number.parseFloat</c> are, so they
    /// live on the realm and come back unchanged. Asserting both here is the point — the difference is not
    /// about laziness, it is about whether anything but the slot is holding the value.
    /// </remarks>
    [Fact]
    public void ABuiltInFunctionSlotOnTheGlobalObjectIsRebuiltByARestore()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var decodeBefore = engine.Evaluate("decodeURI");
        var parseIntBefore = engine.Evaluate("parseInt");
        engine.Evaluate("decodeURI.scribble = 1; parseInt.scribble = 1;");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("decodeURI").Should().NotBeSameAs(decodeBefore);
        engine.Evaluate("typeof decodeURI.scribble").AsString().Should().Be("undefined");
        engine.Evaluate("decodeURI('%41')").AsString().Should().Be("A");

        engine.Evaluate("parseInt").Should().BeSameAs(parseIntBefore);
        engine.Evaluate("parseInt.scribble").AsNumber().Should().Be(1);
        engine.Evaluate("parseInt === Number.parseInt").AsBoolean().Should().BeTrue();
    }

    // ---------------------------------------------------------------------------------------------
    // The point of the feature: host configuration is what survives
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void HostConfigurationSurvivesEveryKindOfScriptDamage()
    {
        var engine = new Engine(o => o.AllowClr());
        engine.SetValue("hostNumber", 41);
        engine.SetValue("hostFn", new Func<int, int>(x => x + 1));
        engine.SetValue("hostList", new List<string> { "a" });
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate(
            """
            hostNumber = 0;
            delete globalThis.hostFn;
            Object.defineProperty(globalThis, 'hostList', { value: null, writable: false });
            globalThis[0] = 'deopt';
            Object.setPrototypeOf(globalThis, null);
            var pollution = 1;
            let lexical = 2;
            """);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("hostFn(hostNumber)").AsNumber().Should().Be(42);
        engine.Evaluate("hostList[0]").AsString().Should().Be("a");
        engine.Evaluate("typeof pollution").AsString().Should().Be("undefined");
        engine.Evaluate("typeof globalThis[0]").AsString().Should().Be("undefined");
        engine.Evaluate("Object.getPrototypeOf(globalThis) === Object.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("let lexical = 3; lexical").AsNumber().Should().Be(3);
    }

    // ---------------------------------------------------------------------------------------------
    // WithRestoredGlobals — the documented recipe, with the finally supplied
    // ---------------------------------------------------------------------------------------------
    //
    // The recipe every reusing host writes by hand is "run, then restore in a finally". The finally is
    // the part that is easy to leave out, and leaving it out is invisible until a script throws: the
    // globals that evaluation declared are then handed to the next caller.

    [Fact]
    public void WithRestoredGlobalsRestoresAfterNormalCompletion()
    {
        var engine = new Engine();
        engine.SetValue("hostValue", 1);
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Advanced.WithRestoredGlobals(snapshot, () =>
        {
            engine.Evaluate("var leaked = 1; let lexical = 2; hostValue = 9;");
            engine.Evaluate("hostValue").AsNumber().Should().Be(9);
        });

        engine.Evaluate("typeof leaked").AsString().Should().Be("undefined");
        engine.Evaluate("hostValue").AsNumber().Should().Be(1);
        engine.Evaluate("let lexical = 3; lexical").AsNumber().Should().Be(3);
    }

    [Fact]
    public void WithRestoredGlobalsRestoresWhenTheActionThrowsAndTheExceptionPropagates()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        Invoking(() => engine.Advanced.WithRestoredGlobals(snapshot, () =>
            {
                engine.Evaluate("var leaked = 1; let lexical = 2;");
                throw new InvalidTimeZoneException("from the action");
            }))
            .Should().Throw<InvalidTimeZoneException>()
            .WithMessage("from the action");

        // the whole point: the throw did not skip the restore
        engine.Evaluate("typeof leaked").AsString().Should().Be("undefined");
        engine.Evaluate("let lexical = 3; lexical").AsNumber().Should().Be(3);
    }

    [Fact]
    public void WithRestoredGlobalsRestoresWhenTheSCRIPTThrows()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        Invoking(() => engine.Advanced.WithRestoredGlobals(snapshot, () =>
                engine.Evaluate("let lexical = 2; null.boom;")))
            .Should().Throw<JavaScriptException>();

        engine.Evaluate("let lexical = 3; lexical").AsNumber().Should().Be(3);
    }

    [Fact]
    public void WithRestoredGlobalsRunsTheRestoreExactlyOnce()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        var ran = 0;

        engine.Advanced.WithRestoredGlobals(snapshot, () => ran++);

        ran.Should().Be(1);
        // and the engine is still usable for the next round
        engine.Advanced.WithRestoredGlobals(snapshot, () => engine.Evaluate("let a = 1;"));
        engine.Advanced.WithRestoredGlobals(snapshot, () => engine.Evaluate("let a = 1;"));
    }

    [Fact]
    public void WithRestoredGlobalsRejectsNullArguments()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        Invoking(() => engine.Advanced.WithRestoredGlobals(null!, () => { })).Should().Throw<ArgumentNullException>();
        Invoking(() => engine.Advanced.WithRestoredGlobals(snapshot, null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithRestoredGlobalsLetsTheRestoresOwnGuardsSpeak()
    {
        var a = new Engine();
        var b = new Engine();
        var foreign = a.Advanced.CaptureGlobalSnapshot();
        var ran = false;

        // A foreign snapshot is RestoreGlobalSnapshot's own rejection, and it fires after the action has
        // run — the wrapper adds a finally, it does not pre-validate on the restore's behalf.
        Invoking(() => b.Advanced.WithRestoredGlobals(foreign, () => ran = true))
            .Should().Throw<ArgumentException>()
            .WithMessage("*different engine*");

        ran.Should().BeTrue();
    }
}
