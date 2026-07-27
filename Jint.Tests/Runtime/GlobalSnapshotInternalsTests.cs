#nullable enable

using System.Reflection;
using Acornima.Ast;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

/// <summary>
/// The parts of <c>Advanced.CaptureGlobalSnapshot</c> / <c>RestoreGlobalSnapshot</c> that are invisible from
/// the public surface: that the engine warm-up caches — the whole reason the feature exists — survive a
/// restore, that the global re-enters its shared built-in layout after a deopt, and that the version
/// counters every inline cache validates against move strictly forward.
/// </summary>
public class GlobalSnapshotInternalsTests
{
    private static readonly FieldInfo _scriptStatementListsField =
        typeof(Engine).GetField("_scriptStatementLists", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo _evaluatedScriptsField =
        typeof(Engine).GetField("_evaluatedScripts", BindingFlags.Instance | BindingFlags.NonPublic)!;

    // The two caches are private fields with no accessor of their own; reading them by reflection keeps the
    // product surface unchanged for what is purely a test concern.
    private static int ScriptStatementListCount(Engine engine)
        => ((System.Collections.ICollection?) _scriptStatementListsField.GetValue(engine))?.Count ?? 0;

    private static int EvaluatedScriptCount(Engine engine)
        => ((HashSet<Script>) _evaluatedScriptsField.GetValue(engine)!).Count;

    /// <summary>
    /// The feature's entire point. The per-node interpreter caches hang off the cached handler tree, and
    /// that tree is keyed on the AST — a restore must not evict it, or a reusing host would be paying
    /// fresh-engine cost with extra steps.
    /// </summary>
    [Fact]
    public void HandlerTreeCachesSurviveRestore()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        var script = Engine.PrepareScript("function helper(x) { return x + 1; } var total = helper(1); let scoped = total; scoped;");

        // first evaluation caches nothing: reEvaluation is false until the same script comes back
        engine.Evaluate(script).AsNumber().Should().Be(2);
        ScriptStatementListCount(engine).Should().Be(0);
        EvaluatedScriptCount(engine).Should().Be(1);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // second evaluation is the re-evaluation, so the handler tree lands in the cache
        engine.Evaluate(script).AsNumber().Should().Be(2);
        ScriptStatementListCount(engine).Should().Be(1);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // and the restore left it there — the third run reuses the very same tree
        ScriptStatementListCount(engine).Should().Be(1);
        engine.Evaluate(script).AsNumber().Should().Be(2);
        ScriptStatementListCount(engine).Should().Be(1);
        EvaluatedScriptCount(engine).Should().Be(1, "restore must not make the engine forget it has already run this script");
    }

    [Fact]
    public void CachedFunctionDefinitionsStayWarmAcrossRestore()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        var script = Engine.PrepareScript("function f() { return 1; } f();");

        engine.Evaluate(script);
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        engine.Evaluate(script);

        var declaration = (FunctionDeclaration) script.Program!.Body[0];
        engine.TryGetFunctionDefinition(declaration, out var definition).Should().BeTrue();
        definition.Should().NotBeNull();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.TryGetFunctionDefinition(declaration, out var afterRestore).Should().BeTrue();
        afterRestore.Should().BeSameAs(definition, "the definition owns the body handler tree and its inline caches");
    }

    [Fact]
    public void GlobalReEntersBuiltinShapeStorageAfterADeoptAndRestore()
    {
        var engine = new Engine();
        var global = engine.Realm.GlobalObject;
        var shaped = (IBuiltinShaped) global;

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        var slotsBefore = shaped.BuiltinDescriptors!.Length;

        engine.Evaluate("globalThis[0] = 'deopt';");
        shaped.BuiltinDescriptors.Should().BeNull();
        (global._type & InternalTypes.BuiltinShapeMode).Should().Be(InternalTypes.Empty);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        (global._type & InternalTypes.BuiltinShapeMode).Should().NotBe(InternalTypes.Empty);
        shaped.BuiltinDescriptors.Should().NotBeNull();
        shaped.BuiltinDescriptors!.Length.Should().Be(slotsBefore);
        global._properties.Should().BeNull("the deopt's dictionary must not survive as hybrid overflow");
    }

    [Fact]
    public void EachRestoreHandsOutAFreshDescriptorArray()
    {
        // Slot materialization writes back into the array the object is holding, so handing out the
        // captured instance would let one evaluation mutate the snapshot for every later restore.
        var engine = new Engine();
        var shaped = (IBuiltinShaped) engine.Realm.GlobalObject;
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        var first = shaped.BuiltinDescriptors;

        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        shaped.BuiltinDescriptors.Should().NotBeSameAs(first);
    }

    [Fact]
    public void VersionCountersStrictlyIncreaseAcrossRestore()
    {
        var engine = new Engine();
        var global = engine.Realm.GlobalObject;
        var globalEnv = engine.Realm.GlobalEnv;
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("var v = 1; let l = 2;");

        var propertiesVersion = global._propertiesVersion;
        var lexicalMutations = globalEnv._lexicalMutations;
        var injectionEpoch = engine._envBindingInjectionEpoch;

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // Never restored to the captured values: moving a counter backwards could revalidate an inline
        // cache entry built before the capture against state it never saw.
        global._propertiesVersion.Should().NotBe(propertiesVersion);
        globalEnv._lexicalMutations.Should().BeGreaterThan(lexicalMutations);
        engine._envBindingInjectionEpoch.Should().BeGreaterThan(injectionEpoch);

        var afterFirst = global._propertiesVersion;
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        global._propertiesVersion.Should().NotBe(afterFirst, "an idempotent restore still has to invalidate");
    }

    [Fact]
    public void CaptureDoesNotMaterializeALazyGlobalDescriptor()
    {
        var engine = new Engine(options => options.AddLazyGlobal(
            "hostApi",
            e => new ClrFunction(e, "hostApi", (_, _) => "from-host")));

        var global = engine.Realm.GlobalObject;

        engine.Advanced.CaptureGlobalSnapshot();

        var descriptor = global.GetOwnProperty("hostApi");
        descriptor.Should().NotBe(PropertyDescriptor.Undefined);
        // still lazy: CustomJsValue is what routes the read through the resolver, and it is cleared the
        // moment the value materializes
        (descriptor._flags & PropertyFlag.CustomJsValue).Should().NotBe(PropertyFlag.None);
        descriptor._value.Should().BeNull();
    }

    [Fact]
    public void RestoreEmptiesTheGlobalDeclarativeRecord()
    {
        var engine = new Engine();
        var declarativeRecord = engine.Realm.GlobalEnv._declarativeRecord;
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("let a = 1; const b = 2; class C {}");
        declarativeRecord._dictionary!.Count.Should().Be(3);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        declarativeRecord._dictionary.Should().BeNull();
        declarativeRecord.Should().BeSameAs(engine.Realm.GlobalEnv._declarativeRecord, "environment identity is pinned by per-node caches");
    }

    [Fact]
    public void RestoreDrainsTheEventLoopWithoutRunningTheJobs()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        Assert.Throws<JavaScriptException>(
            () => engine.Evaluate("Promise.resolve().then(function () { globalThis.ran = true; }); throw new Error('boom');"));
        engine.EventLoop.IsEmpty.Should().BeFalse();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.EventLoop.IsEmpty.Should().BeTrue();
        engine.Evaluate("typeof globalThis.ran").AsString().Should().Be("undefined");
    }

    [Fact]
    public void RestoreKeepsHostGlobalsInTheHybridOverflowRatherThanDeoptingTheShape()
    {
        var engine = new Engine();
        engine.SetValue("hostValue", 1);
        var global = engine.Realm.GlobalObject;
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("var scriptGlobal = 1; delete globalThis.hostValue;");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        (global._type & InternalTypes.BuiltinShapeMode).Should().NotBe(InternalTypes.Empty);
        global._properties!.Count.Should().Be(1);
        global._properties.ContainsKey("hostValue").Should().BeTrue();
        global._properties.ContainsKey("scriptGlobal").Should().BeFalse();
    }
}
