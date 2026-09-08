#nullable enable

using Jint.Constraints;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
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
        var third = host.CreateAdditionalRealm();
        first.GlobalObject.Set("marker", "first");
        second.GlobalObject.Set("marker", "second");
        third.GlobalObject.Set("marker", "third");

        engine.Advanced.WithRealm(first, firstEngine =>
        {
            firstEngine.Global.Should().BeSameAs(first.GlobalObject);
            firstEngine.Evaluate("marker").AsString().Should().Be("first");
            firstEngine.Execute("let firstLexical = 42");
            firstEngine.SetValue("wrapped", new object());
            firstEngine.Evaluate("Object.getPrototypeOf(wrapped) === Object.prototype").Should().BeTrue();
            firstEngine.Advanced.WithRealm(second, secondEngine =>
            {
                secondEngine.Global.Should().BeSameAs(second.GlobalObject);
                secondEngine.Evaluate("marker").AsString().Should().Be("second");
                secondEngine.Advanced.WithRealm(first, restoredFirstEngine =>
                {
                    restoredFirstEngine.Evaluate("marker").AsString().Should().Be("first");
                    restoredFirstEngine.Evaluate("firstLexical").AsNumber().Should().Be(42);
                });
                secondEngine.Advanced.WithRealm(third, thirdEngine =>
                    thirdEngine.Evaluate("marker").AsString().Should().Be("third"));
                secondEngine.Evaluate("marker").AsString().Should().Be("second");
            });
            firstEngine.Global.Should().BeSameAs(first.GlobalObject);
            firstEngine.Evaluate("firstLexical").AsNumber().Should().Be(42);
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
    public void InvalidRealmIsRejectedBeforeAConstraintBudgetIsArmed()
    {
        var firstHost = new RealmHost();
        using var firstEngine = new Engine(options => options.UseHostFactory(_ => firstHost));
        var foreignRealm = firstHost.CreateAdditionalRealm();
        var constraint = new ResetCountingConstraint();
        using var secondEngine = new Engine(options => options.AddConstraint(constraint));

        constraint.Resets = 0;
        Invoking(() => secondEngine.Advanced.WithRealm(foreignRealm, _ => { }))
            .Should().ThrowExactly<ArgumentException>()
            .WithParameterName("realm");

        constraint.Resets.Should().Be(0, "argument rejection happens before an engine operation starts");
    }

    [Test]
    public void IncompleteRealmIsRejectedBeforeConstructionRefusal()
    {
        var host = new RealmHost();
        using var engine = new Engine(options => options.UseHostFactory(_ => host));
        Exception? failure = null;
        host.DuringIntrinsics = (scopedEngine, incompleteRealm) =>
        {
            failure = Caught.Exception(() => scopedEngine.Advanced.WithRealm(incompleteRealm, _ => { }));
        };

        host.CreateAdditionalRealm();

        failure.Should().BeOfType<ArgumentException>()
            .Which.ParamName.Should().Be("realm");
    }

    [Test]
    public void CompleteRealmCannotBeEnteredWhileAnotherRealmIsBeingConstructed()
    {
        var host = new RealmHost();
        using var engine = new Engine(options => options.UseHostFactory(_ => host));
        var completeRealm = host.CreateAdditionalRealm();
        Exception? failure = null;
        host.DuringIntrinsics = (scopedEngine, _) =>
        {
            failure = Caught.Exception(() => scopedEngine.Advanced.WithRealm(completeRealm, _ => { }));
        };

        host.CreateAdditionalRealm();

        failure.Should().BeOfType<InvalidOperationException>();
        engine.Global.Should().NotBeSameAs(completeRealm.GlobalObject);
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
    public void ScriptExceptionRestoresTheCallingRealm()
    {
        var host = new RealmHost();
        using var engine = new Engine(options => options.UseHostFactory(_ => host));
        var principalGlobal = engine.Global;
        var realm = host.CreateAdditionalRealm();

        Invoking(() => engine.Advanced.WithRealm(realm, scopedEngine =>
                scopedEngine.Execute("throw new Error('boom')")))
            .Should().ThrowExactly<JavaScriptException>()
            .WithMessage("boom");

        engine.Global.Should().BeSameAs(principalGlobal);
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

    [Test]
    public void ConcurrentThreadIsRefusedWithoutChangingEitherRealm()
    {
        var host = new RealmHost();
        using var engine = new Engine(options => options.UseHostFactory(_ => host));
        var first = host.CreateAdditionalRealm();
        var second = host.CreateAdditionalRealm();
        Exception? failure = null;

        engine.Advanced.WithRealm(first, scopedEngine =>
        {
            failure = Task.Run(() =>
                    Caught.Exception(() => scopedEngine.Advanced.WithRealm(second, _ => { })))
                .GetAwaiter()
                .GetResult();
            scopedEngine.Global.Should().BeSameAs(first.GlobalObject);
        });

        failure.Should().BeOfType<InvalidOperationException>();
        engine.Global.Should().NotBeSameAs(first.GlobalObject);
        engine.Global.Should().NotBeSameAs(second.GlobalObject);
    }

    [Test]
    public async Task OutstandingAsyncOperationRefusesRealmEntryWithoutLeavingAContext()
    {
        var host = new RealmHost();
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var engine = new Engine(options => options.UseHostFactory(_ => host));
        var principalGlobal = engine.Global;
        var realm = host.CreateAdditionalRealm();
        var foreignHost = new RealmHost();
        using var foreignEngine = new Engine(options => options.UseHostFactory(_ => foreignHost));
        var foreignRealm = foreignHost.CreateAdditionalRealm();
        engine.SetValue("wait", new Func<Task<int>>(() => gate.Task));

        var pending = engine.EvaluateAsync("(async () => await wait())()");
        pending.IsCompleted.Should().BeFalse();

        Invoking(() => engine.Advanced.WithRealm(foreignRealm, _ => { }))
            .Should().ThrowExactly<ArgumentException>()
            .WithParameterName("realm");
        Invoking(() => engine.Advanced.WithRealm(realm, _ => { }))
            .Should().ThrowExactly<InvalidOperationException>();

        gate.SetResult(42);
        (await pending).AsNumber().Should().Be(42);
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

    [Test]
    public void RealmSetupIsTransparentToAQueuedJobsEmptyStackCheckpoint()
    {
        var host = new RealmHost();
        var log = new List<string>();
        using var engine = new Engine(options => options
            .UseHostFactory(_ => host)
            .UseWebApis(WebApiFeatures.Events));
        var realm = host.CreateAdditionalRealm();
        engine.SetValue("record", new Action<string>(log.Add));
        var (dispatch, target, eventObject) = CreateEventTarget(engine);

        engine.Tasks.Post(() => engine.Advanced.WithRealm(realm, scopedEngine =>
            scopedEngine.Call(dispatch, target, [eventObject])));
        engine.Tasks.ProcessTasks();

        log.Should().Equal("first", "microtask", "second");
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

    [Test]
    public void TopLevelRealmEntryReportsUnhandledRejectionsWhenItCompletes()
    {
        var host = new RealmHost();
        using var engine = new Engine(options => options.UseHostFactory(_ => host));
        var realm = host.CreateAdditionalRealm();
        var reject = engine.Advanced.WithRealm(realm, scopedEngine =>
            scopedEngine.Evaluate("() => Promise.reject(new Error('boom'))"));
        var reports = new List<PromiseRejectionTrackerEventArgs>();
        engine.Tasks.PromiseRejectionTracker += (_, args) => reports.Add(args);

        engine.Advanced.WithRealm(realm, scopedEngine => scopedEngine.Call(reject));

        reports.Should().ContainSingle()
            .Which.Operation.Should().Be(PromiseRejectionOperation.Reject);
    }

    [Test]
    public void EscapingExceptionRestoresTheRealmWithoutReportingARejection()
    {
        var host = new RealmHost();
        using var engine = new Engine(options => options.UseHostFactory(_ => host));
        var principalGlobal = engine.Global;
        var realm = host.CreateAdditionalRealm();
        var reports = new List<PromiseRejectionTrackerEventArgs>();
        engine.Tasks.PromiseRejectionTracker += (_, args) => reports.Add(args);
        var failure = new InvalidOperationException("callback failed");
        var reject = engine.Advanced.WithRealm(realm, scopedEngine =>
            scopedEngine.Evaluate("() => Promise.reject(new Error('boom'))"));

        Caught.Exception(() => engine.Advanced.WithRealm(realm, scopedEngine =>
        {
            scopedEngine.Call(reject);
            throw failure;
        })).Should().BeSameAs(failure);

        reports.Should().BeEmpty("an escaping callback does not create a successful-entry checkpoint");
        engine.Global.Should().BeSameAs(principalGlobal);
    }

    private sealed class RealmHost : Host
    {
        public Action<Engine, Realm>? DuringIntrinsics { get; set; }

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
