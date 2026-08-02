#nullable enable

using Jint;
using Jint.Native;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins the one guarantee an engine-pooling host bets correctness on: a lazily declared global that had not
/// been read when a snapshot was captured is returned to its unread state by a restore, so its factory runs
/// again and produces a value for the request that is reading it now.
/// </summary>
/// <remarks>
/// <para>
/// These are contract tests rather than coverage. The behaviour is currently produced by an internal
/// arrangement — the descriptor the registration installs is one the restore path recognizes as owning its
/// value in the inherited field, and reverts. A host cannot see that arrangement, cannot assert it, and has
/// no way to notice if it changes.
/// </para>
/// <para>
/// What it would cost is the reason these exist. Were such a descriptor reinstated by reference with its
/// materialized value intact, an engine reused across requests would hand the next request a global closed
/// over the previous request's state — a scoped service provider, a user, a tenant. Nothing throws, nothing
/// logs, and the only symptom is an answer computed against the wrong request. A test that fails loudly here
/// is the difference between that being caught in CI and being found in production.
/// </para>
/// </remarks>
public class LazyGlobalRestoreContractTests
{
    [Fact]
    public void AnUnreadOptionsGlobalIsRebuiltAfterARestore()
    {
        var built = 0;
        var current = "first";

        // The shape a pooling host has: one Options for the process, the per-request part reached through
        // the factory rather than captured, and a snapshot taken before anything has run.
        var options = new Options().AddLazyGlobal("contextual", engine =>
        {
            built++;
            return JsValue.FromObject(engine, current);
        });

        var engine = new Engine(options);
        var clean = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("contextual").AsString().Should().Be("first");
        built.Should().Be(1);

        // Next "request": same engine, different ambient state.
        engine.Advanced.RestoreGlobalSnapshot(clean);
        current = "second";

        engine.Evaluate("contextual").AsString().Should()
            .Be("second", "a restore must re-arm a global that was unread at capture, or a reused engine serves the previous request's value");
        built.Should().Be(2);
    }

    [Fact]
    public void TheSameHoldsForAPerEngineRegistration()
    {
        var built = 0;
        var current = "first";

        var engine = new Engine();
        var clean = engine.Advanced.CaptureGlobalSnapshot();

        engine.Advanced.AddLazyGlobal("contextual", e =>
        {
            built++;
            return JsValue.FromObject(e, current);
        });

        engine.Evaluate("contextual").AsString().Should().Be("first");
        built.Should().Be(1);

        // A per-engine declaration made *after* the capture is removed by the restore rather than re-armed,
        // so the host declares it again per rental. Both halves matter to a pool: this is why the
        // declaration belongs with the rental and not with the engine's construction.
        engine.Advanced.RestoreGlobalSnapshot(clean);
        engine.Evaluate("typeof contextual").AsString().Should().Be("undefined");

        current = "second";
        engine.Advanced.AddLazyGlobal("contextual", e =>
        {
            built++;
            return JsValue.FromObject(e, current);
        });

        engine.Evaluate("contextual").AsString().Should().Be("second");
        built.Should().Be(2);
    }

    [Fact]
    public void AGlobalAlreadyReadAtCaptureIsRestoredToThatValue()
    {
        // The other side of the same rule, and the one a host must not get wrong in the opposite direction:
        // once a value is part of the captured surface it is restored, not rebuilt.
        var built = 0;
        var current = "first";

        var options = new Options().AddLazyGlobal("contextual", engine =>
        {
            built++;
            return JsValue.FromObject(engine, current);
        });

        var engine = new Engine(options);

        engine.Evaluate("contextual").AsString().Should().Be("first");
        built.Should().Be(1);

        var afterFirstRead = engine.Advanced.CaptureGlobalSnapshot();
        current = "second";

        engine.Advanced.RestoreGlobalSnapshot(afterFirstRead);

        engine.Evaluate("contextual").AsString().Should()
            .Be("first", "the value was part of the captured surface, so it is reinstated rather than recomputed");
        built.Should().Be(1);
    }

    [Fact]
    public void ScriptStateFromThePreviousEvaluationDoesNotSurvive()
    {
        // The rest of what a pool relies on, in one place: a global declared by a script, a redeclarable
        // top-level lexical binding, and a mutated built-in prototype are the three things a host would
        // otherwise have to build a fresh engine to be rid of. The third is deliberately included as the
        // documented non-guarantee — RestoreGlobalSnapshot reverts the global binding table, not intrinsics.
        var engine = new Engine();
        var clean = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("var leaked = 1; globalThis.alsoLeaked = 2; let lexical = 3;");
        engine.Evaluate("Array.prototype.mutated = 4;");

        engine.Advanced.RestoreGlobalSnapshot(clean);

        engine.Evaluate("typeof leaked").AsString().Should().Be("undefined");
        engine.Evaluate("typeof globalThis.alsoLeaked").AsString().Should().Be("undefined");

        // Redeclaring the same lexical name is the sharpest check: without the reset this throws.
        engine.Evaluate("let lexical = 5; lexical").AsNumber().Should().Be(5);

        engine.Evaluate("[].mutated").AsNumber().Should()
            .Be(4, "a restore reverts global bindings and explicitly not intrinsic mutations — a pool is a configuration-reuse primitive, not an isolation boundary");
    }

    [Fact]
    public void APropertyFlagOnARestoredGlobalIsRevertedToo()
    {
        // A host that declares its globals non-enumerable, as a delegate registration would, needs the
        // attributes back as well as the value — otherwise Object.keys(globalThis) drifts across rentals.
        var options = new Options().AddLazyGlobal(
            "hidden",
            static engine => JsValue.FromObject(engine, "value"),
            PropertyFlag.NonEnumerable);

        var engine = new Engine(options);
        var clean = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("Object.keys(globalThis).indexOf('hidden')").AsNumber().Should().Be(-1);
        engine.Evaluate("Object.defineProperty(globalThis, 'hidden', { enumerable: true, value: 'x' });");
        engine.Evaluate("Object.keys(globalThis).indexOf('hidden')").AsNumber().Should().NotBe(-1);

        engine.Advanced.RestoreGlobalSnapshot(clean);

        engine.Evaluate("Object.keys(globalThis).indexOf('hidden')").AsNumber().Should()
            .Be(-1, "the attributes are part of the captured surface, not just the value");
        engine.Evaluate("hidden").AsString().Should().Be("value");
    }
}
