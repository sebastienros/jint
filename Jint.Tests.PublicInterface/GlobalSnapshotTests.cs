#nullable enable

using System;
using System.Collections.Generic;
using Jint.Native;
using Jint.Runtime;
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
        var engine = new Engine(o => o.AllowClr());
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
}
