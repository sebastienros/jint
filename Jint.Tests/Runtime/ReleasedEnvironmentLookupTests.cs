using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Environments;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Tests.Runtime;

/// <summary>
/// Identifier resolution must recognise the global environment by its type, not by having no outer
/// environment. Environments are pooled and their outer link is cleared on release, so "no outer" is also
/// true of any released environment that is still reachable — through a closure, a continuation, or a
/// generator that resumes after the block it was declared in has been left.
/// </summary>
public class ReleasedEnvironmentLookupTests
{
    private static DeclarativeEnvironment ReleasedEnvironment(Engine engine, string name, JsValue value)
    {
        var env = new DeclarativeEnvironment(engine);
        env.CreateMutableBindingAndInitialize(name, canBeDeleted: false, value, DisposeHint.Normal);

        // What the pool does on release: JintBlockStatement, JintForInForOfStatement, JintTryStatement,
        // ScriptFunction and EvalFunction all null the outer link before handing the record back.
        env._outerEnv = null;
        return env;
    }

    [Fact]
    public void AReleasedEnvironmentIsNotMistakenForTheGlobalOne()
    {
        var engine = new Engine();
        var env = ReleasedEnvironment(engine, "x", 42);
        var name = new Environment.BindingName("x");

        // The value-returning lookup used to take the "this is the global environment" branch here, cast to
        // GlobalEnvironment and throw. Its sibling takes the same branch but has no cast, so it answers the
        // same thing either way while a released environment's outer link stays null — the type test there
        // is the premise being corrected, not a behaviour change.
        JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(env, name, strict: false, out var record, out var value)
            .Should().BeTrue();
        record.Should().BeSameAs(env);
        value.Should().Be((JsValue) 42);

        JintEnvironment.TryGetIdentifierEnvironmentWithBinding(env, name, out var found).Should().BeTrue();
        found.Should().BeSameAs(env);
    }

    [Fact]
    public void AReleasedEnvironmentWithoutTheBindingReportsMissRatherThanThrowing()
    {
        var engine = new Engine();
        var env = ReleasedEnvironment(engine, "x", 42);
        var name = new Environment.BindingName("somethingElse");

        // The walk ends at the cleared link. A miss is the honest answer — the outer chain this record
        // used to sit in is gone — and it costs the caller a ReferenceError rather than a CLR exception.
        JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(env, name, strict: false, out _, out _)
            .Should().BeFalse();
        JintEnvironment.TryGetIdentifierEnvironmentWithBinding(env, name, out _).Should().BeFalse();
    }

    [Fact]
    public void TheGlobalEnvironmentIsStillAnsweredDirectly()
    {
        var engine = new Engine();
        engine.Execute("var g = 7;");

        var global = engine.Realm.GlobalEnv;
        var name = new Environment.BindingName("g");

        JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(global, name, strict: false, out var record, out var value)
            .Should().BeTrue();
        record.Should().BeSameAs(global);
        value.Should().Be((JsValue) 7);

        JintEnvironment.TryGetIdentifierEnvironmentWithBinding(global, name, out var found).Should().BeTrue();
        found.Should().BeSameAs(global);
    }
}
