#nullable enable

using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// <see cref="Options.HostOptions.JobCallbacks"/>: the engine's HostMakeJobCallback
/// (https://tc39.es/ecma262/#sec-hostmakejobcallback) and HostCallJobCallback
/// (https://tc39.es/ecma262/#sec-hostcalljobcallback), which carry host state from where script registers a
/// callback to where the engine runs it. Every reaction kind that reaches a callback has a test here, because
/// a lane that skips the hook does not fail — it silently runs the callback under the wrong flow.
/// </summary>
public class JobCallbackHooksTests
{
    /// <summary>
    /// The host's ambient "which flow am I in" state, the way an embedder keeps it: a field the host's own
    /// functions read and set. Capture hands back whatever is current, Enter swaps it in, Exit puts it back.
    /// </summary>
    private sealed class FlowHooks : JobCallbackHooks
    {
        internal string? Current;
        internal int Captures;
        internal int Enters;
        internal int Exits;
        internal readonly List<Engine> Engines = new();

        protected internal override object? Capture(Engine engine)
        {
            Captures++;
            Engines.Add(engine);
            return Current;
        }

        protected internal override object? Enter(Engine engine, object hostDefined)
        {
            Enters++;
            var previous = Current;
            Current = (string) hostDefined;
            return previous;
        }

        protected internal override void Exit(Engine engine, object? token)
        {
            Exits++;
            Current = (string?) token;
        }
    }

    private sealed class Harness
    {
        internal readonly FlowHooks Hooks = new();
        internal readonly List<string> Log = new();
        internal readonly List<ManualPromise> Pending = new();
        internal readonly Engine Engine;

        internal Harness(bool installHooks = true)
        {
            Engine = new Engine(options =>
            {
                if (installHooks)
                {
                    options.Host.JobCallbacks = Hooks;
                }
            });

            // Reads the host's flow, as any host function attributing work would.
            Engine.SetValue("flow", new Func<string>(() => Hooks.Current ?? "none"));
            Engine.SetValue("log", new Action<string>(entry => Log.Add(entry)));

            // Runs fn inside a named host flow, restoring the previous one when fn returns — which, for an
            // async fn, is at its first await, long before the work it started has finished.
            Engine.SetValue("scope", new Func<string, JsValue, JsValue>((name, fn) =>
            {
                var previous = Hooks.Current;
                Hooks.Current = name;
                try
                {
                    return fn.Call();
                }
                finally
                {
                    Hooks.Current = previous;
                }
            }));

            // A host operation that completes when the test says so, so branches can be made to interleave.
            Engine.SetValue("hostOperation", new Func<JsValue>(() =>
            {
                var promise = Engine.Tasks.RegisterPromise();
                Pending.Add(promise);
                return promise.Promise;
            }));
        }

        internal void Run(string script)
        {
            Engine.Execute(script);
            Engine.Tasks.ProcessTasks();
        }

        internal void Complete(int index, object? value = null)
        {
            Pending[index].Resolve(value);
            Engine.Tasks.ProcessTasks();
        }

        internal void AssertBalanced()
        {
            Hooks.Enters.Should().Be(Hooks.Exits);
            Hooks.Current.Should().BeNull("every Enter was undone by its Exit");
        }
    }

    [Test]
    public void EveryBranchOfAPromiseAllFanOutSeesItsOwnFlowAfterEachAwait()
    {
        // The embedder scenario from sebastienros/jint#4200, with the branches' host operations completing in
        // reverse order so that their continuations interleave in one microtask drain.
        var host = new Harness();
        host.Run("""
            (async () => {
                await Promise.all([1, 2, 3].map(async (item) => {
                    await scope(`Item ${item}`, async () => {
                        log(`start ${item}: ${flow()}`);
                        await hostOperation(item);
                        log(`resumed ${item}: ${flow()}`);
                        await hostOperation(item);
                        log(`resumed again ${item}: ${flow()}`);
                    });
                    log(`after scope ${item}: ${flow()}`);
                }));
                log(`all done: ${flow()}`);
            })();
            """);

        host.Log.Should().Equal("start 1: Item 1", "start 2: Item 2", "start 3: Item 3");
        host.Hooks.Current.Should().BeNull();

        host.Complete(2);
        host.Complete(1);
        host.Complete(0);

        host.Log.Skip(3).Should().Equal("resumed 3: Item 3", "resumed 2: Item 2", "resumed 1: Item 1");

        // The second operations were registered from inside the resumed bodies, in the order they resumed:
        // 3 is item 3's, 4 is item 2's, 5 is item 1's. The `after scope` continuations were registered by the
        // outer arrow after scope() had already restored the host's state, so they carry nothing.
        host.Complete(3);
        host.Complete(5);
        host.Complete(4);

        host.Log.Skip(6).Should().Equal(
            "resumed again 3: Item 3",
            "after scope 3: none",
            "resumed again 1: Item 1",
            "after scope 1: none",
            "resumed again 2: Item 2",
            "after scope 2: none",
            "all done: none");
        host.AssertBalanced();
    }

    [Test]
    public void ThenCatchAndFinallyHandlersRunUnderTheFlowTheyWereRegisteredIn()
    {
        var host = new Harness();
        host.Run("""
            const fulfilled = hostOperation();
            const rejected = hostOperation();
            scope('registered', () => {
                fulfilled.then(v => log(`then: ${flow()}`));
                rejected.catch(e => log(`catch: ${flow()}`));
                fulfilled.finally(() => log(`finally: ${flow()}`));
                rejected.then(undefined, e => log(`onRejected: ${flow()}`));
            });
            """);

        host.Log.Should().BeEmpty();

        host.Complete(0);
        host.Pending[1].Reject("boom");
        host.Engine.Tasks.ProcessTasks();

        host.Log.Should().Equal("then: registered", "finally: registered", "catch: registered", "onRejected: registered");
        host.AssertBalanced();
    }

    [Test]
    public void AReactionOnAnAlreadySettledPromiseRunsUnderTheRegisteringFlow()
    {
        var host = new Harness();
        host.Run("""
            scope('registered', () => {
                Promise.resolve(1).then(() => log(`fulfilled: ${flow()}`));
                Promise.reject(1).catch(() => log(`rejected: ${flow()}`));
            });
            """);

        host.Log.Should().Equal("fulfilled: registered", "rejected: registered");
        host.AssertBalanced();
    }

    [Test]
    public void AwaitOfAPrimitiveAndOfASettledPromiseResumesUnderTheAwaitingFlow()
    {
        // `await 1` skips PerformPromiseThen and enqueues its one reaction directly; it must still capture.
        var host = new Harness();
        host.Run("""
            scope('awaiting', async () => {
                await 1;
                log(`primitive: ${flow()}`);
                await Promise.resolve(2);
                log(`settled: ${flow()}`);
                await { then(resolve) { resolve(3); } };
                log(`thenable: ${flow()}`);
            });
            """);

        host.Log.Should().Equal("primitive: awaiting", "settled: awaiting", "thenable: awaiting");
        host.AssertBalanced();
    }

    [Test]
    public void NestedFlowsResumeUnderTheirOwnStateAndTheOuterFlowResumesUnderItsOwn()
    {
        var host = new Harness();
        host.Run("""
            scope('outer', async () => {
                await scope('inner', async () => {
                    await hostOperation();
                    log(`inner resumed: ${flow()}`);
                });
                log(`outer resumed: ${flow()}`);
                await hostOperation();
                log(`outer resumed again: ${flow()}`);
            });
            """);

        host.Complete(0);
        host.Complete(1);

        host.Log.Should().Equal("inner resumed: inner", "outer resumed: outer", "outer resumed again: outer");
        host.AssertBalanced();
    }

    [Test]
    public void AThenableIsAdoptedUnderTheFlowItWasResolvedIn()
    {
        // NewPromiseResolveThenableJob: the thenable's then runs in a job of its own, made by
        // HostMakeJobCallback(then) at resolution (https://tc39.es/ecma262/#sec-promise-resolve-functions).
        var host = new Harness();
        host.Run("""
            const thenable = { then(resolve) { log(`then called: ${flow()}`); resolve(1); } };
            scope('resolving', () => {
                new Promise(resolve => resolve(thenable));
            });
            """);

        host.Log.Should().Equal("then called: resolving");
        host.AssertBalanced();
    }

    [Test]
    public void AsyncGeneratorsAndForAwaitResumeUnderTheConsumingFlow()
    {
        var host = new Harness();
        host.Run("""
            async function* produce() {
                yield 1;
                await hostOperation();
                log(`generator resumed: ${flow()}`);
                yield 2;
            }
            scope('consuming', async () => {
                for await (const value of produce()) {
                    log(`got ${value}: ${flow()}`);
                }
                log(`loop done: ${flow()}`);
            });
            """);

        host.Complete(0);

        host.Log.Should().Equal("got 1: consuming", "generator resumed: consuming", "got 2: consuming", "loop done: consuming");
        host.AssertBalanced();
    }

    [Test]
    public void AThrowingHandlerOrResumedBodyStillRestoresTheHostState()
    {
        var host = new Harness();
        host.Run("""
            const pending = hostOperation();
            const derived = scope('throwing', () => pending.then(() => { throw new Error('from then'); }));
            derived.catch(e => log(`then rejected: ${e.message} in ${flow()}`));
            scope('throwing body', async () => {
                await pending;
                throw new Error('from body');
            }).catch(e => log(`body rejected: ${e.message} in ${flow()}`));
            """);

        host.Complete(0);

        // Each catch was registered outside any flow, so it runs under the host's own (empty) state — which it
        // can only see if the throwing callback's Exit ran.
        host.Log.Should().Equal("then rejected: from then in none", "body rejected: from body in none");
        host.AssertBalanced();
    }

    [Test]
    public void AThrowingThenableStillRestoresTheHostStateAndRejects()
    {
        var host = new Harness();
        host.Run("""
            const thenable = { then() { throw new Error('from thenable'); } };
            let adopted;
            scope('resolving', () => {
                adopted = new Promise(resolve => resolve(thenable));
            });
            adopted.catch(e => log(`rejected: ${e.message} in ${flow()}`));
            """);

        host.Log.Should().Equal("rejected: from thenable in none");
        host.AssertBalanced();
    }

    [Test]
    public void ACaptureOfNullRunsTheCallbackWithoutEnterOrExit()
    {
        var host = new Harness();
        host.Run("""
            const pending = hostOperation();
            pending.then(() => log(`then: ${flow()}`));
            (async () => { await pending; log(`await: ${flow()}`); })();
            """);

        host.Complete(0);

        host.Log.Should().Equal("then: none", "await: none");
        host.Hooks.Captures.Should().Be(2);
        host.Hooks.Enters.Should().Be(0);
        host.Hooks.Exits.Should().Be(0);
    }

    [Test]
    public void CaptureRunsOncePerRegistrationAndNeverForAReactionWithoutAHandler()
    {
        var host = new Harness();
        host.Run("""
            const p = hostOperation();
            scope('counting', () => {
                p.then();                  // no callable handler: no job callback at all
                p.then(undefined, null);   // the same
                p.then(() => {}, () => {}); // one registration, two handlers
            });
            """);

        host.Hooks.Captures.Should().Be(1);
        host.Hooks.Engines.Should().AllSatisfy(engine => engine.Should().BeSameAs(host.Engine));
    }

    [Test]
    public void WithoutHooksNothingIsCarried()
    {
        var host = new Harness(installHooks: false);
        host.Run("""
            scope('lost', async () => {
                await hostOperation();
                log(`resumed: ${flow()}`);
            });
            """);

        host.Complete(0);

        host.Log.Should().Equal("resumed: none");
        host.Hooks.Captures.Should().Be(0);
    }

    [Test]
    public void TheHooksAreFrozenWithTheRestOfTheOptions()
    {
        var engine = new Engine();
        var caught = Caught.Exception(() => engine.Options.Host.JobCallbacks = new FlowHooks());
        caught.Should().BeOfType<InvalidOperationException>();
    }
}
