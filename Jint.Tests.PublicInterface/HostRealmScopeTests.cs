#nullable enable

using Jint.Constraints;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins the public realm callback as a synchronous, bounded engine entry that a third-party host can use.
/// </summary>
public sealed class HostRealmScopeTests
{
    [Test]
    public void GenericCallbackReturnsFromTheSelectedRealmAndRestoresThePrincipalRealm()
    {
        var host = new RealmHost();
        using var engine = new Engine(options => options.UseHostFactory(_ => host));
        var principalGlobal = engine.Global;
        var realm = host.CreateAdditionalRealm();
        realm.GlobalObject.Set("marker", "selected");

        var result = engine.Advanced.WithRealm(realm, scopedEngine =>
        {
            scopedEngine.Global.Should().BeSameAs(realm.GlobalObject);
            scopedEngine.Intrinsics.Should().BeSameAs(realm.Intrinsics);
            return scopedEngine.Evaluate("marker").AsString();
        });

        result.Should().Be("selected");
        engine.Global.Should().BeSameAs(principalGlobal);
    }

    [Test]
    public void ActionCallbackRestoresThePrincipalRealmAfterAnException()
    {
        var host = new RealmHost();
        using var engine = new Engine(options => options.UseHostFactory(_ => host));
        var principalGlobal = engine.Global;
        var realm = host.CreateAdditionalRealm();
        var failure = new InvalidOperationException("callback failed");

        Caught.Exception(() => engine.Advanced.WithRealm(realm, _ => throw failure))
            .Should().BeSameAs(failure);

        engine.Global.Should().BeSameAs(principalGlobal);
        engine.Evaluate("globalThis").Should().BeSameAs(principalGlobal);
    }

    [Test]
    public void NestedCallbacksRestoreEachCallersRealm()
    {
        var host = new RealmHost();
        using var engine = new Engine(options => options.UseHostFactory(_ => host));
        var principalGlobal = engine.Global;
        var first = host.CreateAdditionalRealm();
        var second = host.CreateAdditionalRealm();

        engine.Advanced.WithRealm(first, firstEngine =>
        {
            firstEngine.Global.Should().BeSameAs(first.GlobalObject);
            firstEngine.Advanced.WithRealm(second, secondEngine =>
            {
                secondEngine.Global.Should().BeSameAs(second.GlobalObject);
            });
            firstEngine.Global.Should().BeSameAs(first.GlobalObject);
        });

        engine.Global.Should().BeSameAs(principalGlobal);
    }

    [Test]
    public void AwaitableResultIsReturnedWithoutExtendingTheRealmScope()
    {
        var host = new RealmHost();
        using var engine = new Engine(options => options.UseHostFactory(_ => host));
        var principalGlobal = engine.Global;
        var realm = host.CreateAdditionalRealm();
        var pending = new TaskCompletionSource<ObjectInstance>();

        var result = engine.Advanced.WithRealm(realm, _ => pending.Task);

        result.IsCompleted.Should().BeFalse("the synchronous scope does not await a generic result");
        engine.Global.Should().BeSameAs(principalGlobal);
        pending.SetResult(realm.GlobalObject);
        result.GetAwaiter().GetResult().Should().BeSameAs(realm.GlobalObject);
    }

    [Test]
    public void RealmFromAnotherEngineIsRejectedBeforeTheCallbackRuns()
    {
        var firstHost = new RealmHost();
        using var firstEngine = new Engine(options => options.UseHostFactory(_ => firstHost));
        var foreignRealm = firstHost.CreateAdditionalRealm();
        using var secondEngine = new Engine();
        var invoked = false;

        Invoking(() => secondEngine.Advanced.WithRealm(foreignRealm, _ => invoked = true))
            .Should().ThrowExactly<ArgumentException>()
            .WithParameterName("realm");

        invoked.Should().BeFalse();
        secondEngine.Global.Should().NotBeSameAs(foreignRealm.GlobalObject);
    }

    [Test]
    public void RealmConstructionCannotEnterARealmCallback()
    {
        var host = new RealmHost
        {
            DuringIntrinsics = (engine, realm) =>
            {
                Invoking(() => engine.Advanced.WithRealm(realm, _ => { }))
                    .Should().ThrowExactly<InvalidOperationException>();
            },
        };

        using var engine = new Engine(options => options.UseHostFactory(_ => host));

        engine.Evaluate("1 + 1").Should().Be(2);
    }

    [Test]
    public void TopLevelCallbackArmsOneConstraintBudgetAndNestedCallbackDoesNotRearmIt()
    {
        var host = new RealmHost();
        var constraint = new ResetCountingConstraint();
        using var engine = new Engine(options =>
        {
            options.UseHostFactory(_ => host);
            options.AddConstraint(constraint);
        });
        var realm = host.CreateAdditionalRealm();

        constraint.Resets = 0;
        engine.Advanced.WithRealm(realm, scopedEngine => scopedEngine.Evaluate("1 + 1"));
        constraint.Resets.Should().Be(2, "the realm callback is one top-level bounded entry");

        engine.SetValue("enterRealm", new Action(() =>
            engine.Advanced.WithRealm(realm, scopedEngine => scopedEngine.Evaluate("2 + 2"))));
        constraint.Resets = 0;
        engine.Evaluate("enterRealm()");
        constraint.Resets.Should().Be(2, "only the outer script entry resets the shared budget");
    }

    [Test]
    public void ScriptRunByTheCallbackIsBoundedByTheEntry()
    {
        var host = new RealmHost();
        using var engine = new Engine(options =>
        {
            options.UseHostFactory(_ => host);
            options.LimitStatements(100);
        });
        var realm = host.CreateAdditionalRealm();

        Invoking(() => engine.Advanced.WithRealm(realm, scopedEngine => scopedEngine.Execute("while (true) {}")))
            .Should().ThrowExactly<StatementsCountOverflowException>();

        engine.Global.Should().NotBeSameAs(realm.GlobalObject);
        engine.Evaluate("1 + 1").Should().Be(2);
    }

    [Test]
    public void CancellationFromScriptPropagatesAndRestoresTheRealm()
    {
        var host = new RealmHost();
        using var cancellation = new CancellationTokenSource();
        using var engine = new Engine(options =>
        {
            options.UseHostFactory(_ => host);
            options.ObserveCancellation(cancellation.Token);
        });
        var principalGlobal = engine.Global;
        var realm = host.CreateAdditionalRealm();
        cancellation.Cancel();

        Invoking(() => engine.Advanced.WithRealm(realm, scopedEngine => scopedEngine.Execute("while (true) {}")))
            .Should().ThrowExactly<ExecutionCanceledException>();

        engine.Global.Should().BeSameAs(principalGlobal);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void RealmSetupIsTransparentToAHostDispatchsEmptyStackCheckpoint()
    {
        var host = new RealmHost();
        var log = new List<string>();
        using var engine = new Engine(options => options
            .UseHostFactory(_ => host)
            .UseWebApis(WebApiFeatures.Events));
        var realm = host.CreateAdditionalRealm();
        engine.SetValue("record", new Action<string>(log.Add));
        var (dispatch, target, eventObject) = CreateEventTarget(engine);

        engine.Advanced.WithRealm(realm, scopedEngine =>
        {
            scopedEngine.Call(dispatch, target, [eventObject]);
        });

        log.Should().Equal("first", "microtask", "second");
    }

    [Test]
    public void RealmSetupDoesNotHideTheScriptFrameAboveTheEmptyStackBoundary()
    {
        var host = new RealmHost();
        var log = new List<string>();
        using var engine = new Engine(options => options
            .UseHostFactory(_ => host)
            .UseWebApis(WebApiFeatures.Events));
        var realm = host.CreateAdditionalRealm();
        engine.SetValue("record", new Action<string>(log.Add));
        var (dispatch, target, eventObject) = CreateEventTarget(engine);
        engine.SetValue("dispatchInRealm", new Action(() =>
            engine.Advanced.WithRealm(realm, scopedEngine =>
                scopedEngine.Call(dispatch, target, [eventObject]))));

        engine.Execute("dispatchInRealm()");

        log.Should().Equal("first", "second", "microtask");
    }

    private static (JsValue Dispatch, JsValue Target, JsValue Event) CreateEventTarget(Engine engine)
    {
        engine.Execute("""
            globalThis.target = new EventTarget();
            target.addEventListener('ping', () => {
                record('first');
                Promise.resolve().then(() => record('microtask'));
            });
            target.addEventListener('ping', () => record('second'));
            """);
        return (
            engine.Evaluate("EventTarget.prototype.dispatchEvent"),
            engine.GetValue("target"),
            engine.Evaluate("new Event('ping')"));
    }
#endif

    private sealed class RealmHost : Host
    {
        public Action<Engine, Realm>? DuringIntrinsics { get; init; }

        public Realm CreateAdditionalRealm() => base.CreateRealm();

        protected override void CreateIntrinsics(Realm realmRec)
        {
            DuringIntrinsics?.Invoke(Engine, realmRec);
            base.CreateIntrinsics(realmRec);
        }
    }

    private sealed class ResetCountingConstraint : Constraint
    {
        public int Resets { get; set; }

        public override void Check()
        {
        }

        public override void Reset() => Resets++;
    }
}
