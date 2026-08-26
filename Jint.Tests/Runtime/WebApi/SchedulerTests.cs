#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The prioritized task scheduling API against its specification —
/// https://wicg.github.io/scheduling-apis/.
/// </summary>
/// <remarks>
/// <para>
/// Most of these are about <i>order</i>: which of several pending tasks runs next, and where a task sits
/// relative to the microtasks and the timers around it. <c>Engine.Execute</c> drains the event loop once the
/// script has finished, so a task posted with no delay has already run by the time it returns; a delayed one
/// needs the clock moved and an explicit <c>Tasks.ProcessTasks()</c>.
/// </para>
/// <para>
/// The three ordering guarantees under test, all documented on <c>SchedulerQueue</c>: every microtask runs
/// before the next task, whichever order the two were queued in; among pending tasks the highest effective
/// priority wins with ties going to the oldest; and every runnable task runs before any due timer.
/// </para>
/// </remarks>
public class SchedulerTests
{
    /// <summary>
    /// A <see cref="TimeProvider"/> whose clock only moves when a test moves it.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }

    private static (Engine Engine, ManualClock Clock) SchedulerEngine(int? maxActiveTimers = null)
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi =>
        {
            webApi.Timers.TimeProvider = clock;
            if (maxActiveTimers is { } max)
            {
                webApi.Timers.MaxActiveTimers = max;
            }
        }));

        engine.Execute("var log = [];");
        return (engine, clock);
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    // --- postTask: the callback, its result and its failures ------------------------------------------

    [Fact]
    public void PostTaskRunsTheCallbackAsATaskAndResolvesWithItsReturnValue()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            var result = 'pending';
            scheduler.postTask(() => 42).then(v => { result = v; log.push('resolved'); });
            log.push('script');
            """);

        // The callback did not run synchronously — it ran as its own task, during the drain Execute performs.
        Log(engine).Should().Be("script,resolved");
        engine.Evaluate("result").AsNumber().Should().Be(42);
    }

    [Fact]
    public void ACallbackThatThrowsRejectsThePromiseWithWhatItThrew()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            scheduler.postTask(() => { throw new RangeError('boom'); })
                .then(() => log.push('resolved'), e => log.push(e.name + ':' + e.message));
            """);

        Log(engine).Should().Be("RangeError:boom");
    }

    [Fact]
    public void NeitherOperationEverThrows()
    {
        var (engine, _) = SchedulerEngine();

        // A promise-returning operation rejects rather than throws, whatever went wrong —
        // https://webidl.spec.whatwg.org/#dfn-create-operation-function.
        engine.Execute("""
            function outcome(label, thunk) {
                try {
                    thunk().then(() => log.push(label + ':resolved'), e => log.push(label + ':' + e.constructor.name));
                } catch (e) {
                    log.push(label + ':THREW');
                }
            }

            outcome('callable', () => scheduler.postTask(42));
            outcome('priority', () => scheduler.postTask(() => {}, { priority: 'urgent' }));
            outcome('signal', () => scheduler.postTask(() => {}, { signal: 5 }));
            outcome('delay', () => scheduler.postTask(() => {}, { delay: NaN }));
            outcome('options', () => scheduler.postTask(() => {}, 7));
            outcome('receiver', () => scheduler.postTask.call({}, () => {}));
            outcome('yield', () => scheduler.yield.call({}));
            """);

        Log(engine).Should().Be(
            "callable:TypeError,priority:TypeError,signal:TypeError,delay:TypeError,options:TypeError,receiver:TypeError,yield:TypeError");
    }

    [Fact]
    public void ADictionaryMembersAreReadInLexicographicalOrder()
    {
        var (engine, _) = SchedulerEngine();

        // delay, priority, signal — the order https://webidl.spec.whatwg.org/#es-dictionary specifies, not
        // the order the IDL declares them in.
        engine.Execute("""
            const options = {
                get signal() { log.push('signal'); return undefined; },
                get priority() { log.push('priority'); return undefined; },
                get delay() { log.push('delay'); return 0; },
            };

            scheduler.postTask(() => {}, options);
            """);

        Log(engine).Should().StartWith("delay,priority,signal");
    }

    // --- Priority ordering ----------------------------------------------------------------------------

    [Fact]
    public void TasksRunInStrictPriorityOrder()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            scheduler.postTask(() => log.push('background'), { priority: 'background' });
            scheduler.postTask(() => log.push('user-visible'));
            scheduler.postTask(() => log.push('user-blocking'), { priority: 'user-blocking' });
            """);

        Log(engine).Should().Be("user-blocking,user-visible,background");
    }

    [Fact]
    public void TasksOfEqualPriorityRunInTheOrderTheyWerePosted()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            for (const name of ['a', 'b', 'c', 'd']) {
                scheduler.postTask(() => log.push(name));
            }
            """);

        Log(engine).Should().Be("a,b,c,d");
    }

    [Fact]
    public void AUserBlockingTaskPostedLaterPreemptsQueuedUserVisibleOnes()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            scheduler.postTask(() => {
                log.push('first');
                scheduler.postTask(() => log.push('urgent'), { priority: 'user-blocking' });
            });
            scheduler.postTask(() => log.push('second'));
            scheduler.postTask(() => log.push('third'));
            """);

        // The choice is made when a task is about to run, not when it was posted, so the urgent one overtakes
        // the two that were already waiting.
        Log(engine).Should().Be("first,urgent,second,third");
    }

    // --- Ordering against microtasks and timers -------------------------------------------------------

    [Fact]
    public void EveryMicrotaskRunsBeforeTheNextTaskWhicheverWasQueuedFirst()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            scheduler.postTask(() => log.push('task'), { priority: 'user-blocking' });
            Promise.resolve().then(() => log.push('microtask'));
            log.push('script');
            """);

        // The task was posted first and has the highest priority, and the microtask still wins: HTML runs a
        // task only at a microtask checkpoint.
        Log(engine).Should().Be("script,microtask,task");
    }

    [Fact]
    public void EachTaskGetsItsOwnMicrotaskCheckpoint()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            scheduler.postTask(() => {
                log.push('t1');
                Promise.resolve().then(() => log.push('t1-micro'));
            });
            scheduler.postTask(() => log.push('t2'));
            """);

        Log(engine).Should().Be("t1,t1-micro,t2");
    }

    [Fact]
    public void EveryRunnableTaskRunsBeforeADueTimer()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            setTimeout(() => log.push('timeout'), 0);
            scheduler.postTask(() => log.push('background'), { priority: 'background' });
            """);

        // A documented divergence from a browser, which would run the timer first for a background task: HTML
        // leaves the choice implementation-defined, and Jint's timers are promoted only once the job queue —
        // which the scheduler's drain job is part of — has run dry.
        Log(engine).Should().Be("background,timeout");
    }

    // --- Signals: priority inheritance and reprioritization -------------------------------------------

    [Fact]
    public void ATaskSignalGovernsThePriorityWhenNoPriorityOptionIsGiven()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const controller = new TaskController({ priority: 'background' });
            scheduler.postTask(() => log.push('signal'), { signal: controller.signal });
            scheduler.postTask(() => log.push('plain'));
            """);

        Log(engine).Should().Be("plain,signal");
    }

    [Fact]
    public void SetPriorityReprioritizesTasksThatAreStillQueued()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const controller = new TaskController({ priority: 'background' });
            scheduler.postTask(() => log.push('signal'), { signal: controller.signal });
            scheduler.postTask(() => log.push('plain'));
            controller.setPriority('user-blocking');
            """);

        // Same two tasks as the test above, and the order is reversed by the priority change alone.
        Log(engine).Should().Be("signal,plain");
    }

    [Fact]
    public void AnExplicitPriorityOptionWinsOverTheSignalAndIsImmutable()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const controller = new TaskController({ priority: 'background' });
            scheduler.postTask(() => log.push('fixed'), { signal: controller.signal, priority: 'background' });
            scheduler.postTask(() => log.push('plain'));
            controller.setPriority('user-blocking');
            """);

        // The signal still aborts the task, but its priority no longer has anything to say about it.
        Log(engine).Should().Be("plain,fixed");
    }

    [Fact]
    public void APriorityChangeFiresPrioritychangeAtTheSignalInListenerOrder()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const controller = new TaskController();
            controller.signal.addEventListener('prioritychange', e => log.push('listener:' + e.previousPriority));
            controller.signal.onprioritychange = e => log.push('handler:' + e.previousPriority);
            controller.signal.addEventListener('prioritychange', e => log.push('third:' + e.target.priority));
            controller.setPriority('background');
            log.push('after:' + controller.signal.priority);
            """);

        Log(engine).Should().Be("listener:user-visible,handler:user-visible,third:background,after:background");
    }

    [Fact]
    public void ThePrioritychangeEventIsATrustedTaskPriorityChangeEvent()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const controller = new TaskController({ priority: 'user-blocking' });
            controller.signal.onprioritychange = e => {
                log.push(e.type);
                log.push(e instanceof TaskPriorityChangeEvent);
                log.push(e instanceof Event);
                log.push(e.isTrusted);
                log.push(e.previousPriority);
                log.push(e.target === controller.signal);
                log.push(e.target.priority);
            };
            controller.setPriority('background');
            """);

        Log(engine).Should().Be("prioritychange,true,true,true,user-blocking,true,background");
    }

    [Fact]
    public void SettingThePriorityASignalAlreadyHasChangesNothing()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const controller = new TaskController({ priority: 'background' });
            controller.signal.onprioritychange = () => log.push('fired');
            controller.setPriority('background');
            log.push('done');
            """);

        Log(engine).Should().Be("done");
    }

    [Fact]
    public void ChangingThePriorityFromInsideAPrioritychangeListenerIsANotAllowedError()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const controller = new TaskController();
            controller.signal.onprioritychange = () => {
                try {
                    controller.setPriority('background');
                } catch (e) {
                    log.push(e.name + ':' + (e instanceof DOMException));
                }
            };
            controller.setPriority('user-blocking');

            // The signal is usable again afterwards: the flag is cleared however the listener ended. The
            // handler goes first, or it would refuse this change from inside it as well.
            controller.signal.onprioritychange = null;
            controller.setPriority('background');
            log.push(controller.signal.priority);
            """);

        Log(engine).Should().Be("NotAllowedError:true,background");
    }

    // --- Aborting -------------------------------------------------------------------------------------

    [Fact]
    public void AnAlreadyAbortedSignalRejectsWithoutQueueingAnything()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const controller = new TaskController();
            controller.abort('early');
            scheduler.postTask(() => log.push('never'), { signal: controller.signal })
                .then(() => log.push('resolved'), reason => log.push('rejected:' + reason));
            """);

        Log(engine).Should().Be("rejected:early");
    }

    [Fact]
    public void AbortingBeforeTheTaskRunsRejectsWithTheReasonAndDropsTheTask()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const controller = new TaskController();
            scheduler.postTask(() => log.push('never'), { signal: controller.signal })
                .then(() => log.push('resolved'), e => log.push('rejected:' + e.name));
            scheduler.postTask(() => log.push('survivor'));
            controller.abort();
            """);

        // The default reason is an AbortError DOMException, and the aborted task is simply gone.
        Log(engine).Should().Be("rejected:AbortError,survivor");
    }

    [Fact]
    public void AbortingFromInsideTheCallbackRejectsRatherThanResolves()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const controller = new TaskController();
            scheduler.postTask(() => { controller.abort(); return 'value'; }, { signal: controller.signal })
                .then(v => log.push('resolved:' + v), e => log.push('rejected:' + e.name));
            """);

        // The abort steps settle the promise first, and settling is once-only — which is the order the
        // specification's own steps produce.
        Log(engine).Should().Be("rejected:AbortError");
    }

    [Fact]
    public void AbortingAfterTheTaskRanLeavesItsResultAlone()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const controller = new TaskController();
            scheduler.postTask(() => 'done', { signal: controller.signal })
                .then(v => log.push('resolved:' + v), e => log.push('rejected:' + e.name));
            """);

        Log(engine).Should().Be("resolved:done");

        engine.Execute("controller.abort(); log.push('aborted');");
        Log(engine).Should().Be("resolved:done,aborted");
    }

    [Fact]
    public void APlainAbortSignalIsEnoughToCancelATask()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const controller = new AbortController();
            scheduler.postTask(() => log.push('never'), { signal: controller.signal })
                .then(() => log.push('resolved'), e => log.push('rejected:' + e.name));
            controller.abort();

            // ... and it leaves the task at the default priority, since it carries none.
            const plain = new AbortController();
            scheduler.postTask(() => log.push('plain-signal'), { signal: plain.signal });
            scheduler.postTask(() => log.push('urgent'), { priority: 'user-blocking' });
            """);

        Log(engine).Should().Be("rejected:AbortError,urgent,plain-signal");
    }

    // --- delay ----------------------------------------------------------------------------------------

    [Fact]
    public void ADelayedTaskIsNotEvenQueuedUntilItsDelayElapses()
    {
        var (engine, clock) = SchedulerEngine();

        engine.Execute("""
            scheduler.postTask(() => log.push('delayed'), { delay: 10, priority: 'user-blocking' });
            scheduler.postTask(() => log.push('background'), { priority: 'background' });
            """);

        // Priority orders the tasks that are pending; a delayed one is not pending yet, however urgent.
        Log(engine).Should().Be("background");

        clock.Advance(10);
        engine.Tasks.ProcessTasks();

        Log(engine).Should().Be("background,delayed");
    }

    [Fact]
    public void ADelayedTaskArrivesOnlyOnceTheTasksAheadOfItHaveDrained()
    {
        var (engine, clock) = SchedulerEngine();

        engine.Execute("""
            scheduler.postTask(() => log.push('delayed-blocking'), { delay: 10, priority: 'user-blocking' });
            setTimeout(() => {
                scheduler.postTask(() => log.push('visible'));
                scheduler.postTask(() => log.push('blocking'), { priority: 'user-blocking' });
            }, 5);
            """);

        clock.Advance(10);
        engine.Tasks.ProcessTasks();

        // The consequence of "every runnable task runs before any due timer": the delay is itself a timer, and
        // a timer is promoted only when the job queue has run dry — which the drain job keeps non-empty while
        // tasks remain. So the two tasks the 5ms timeout posted both run first, and the delayed task, however
        // urgent, only joins the queue afterwards. Two delayed tasks that come due together are ordered by due
        // time for the same reason, rather than by priority.
        Log(engine).Should().Be("blocking,visible,delayed-blocking");
    }

    [Fact]
    public void ADelayedTaskCountsAgainstTheTimerLimitAndTheRefusalIsARejection()
    {
        var (engine, _) = SchedulerEngine(maxActiveTimers: 1);

        engine.Execute("""
            scheduler.postTask(() => log.push('first'), { delay: 50 }).catch(e => log.push('first:' + e.name));
            scheduler.postTask(() => log.push('second'), { delay: 50 })
                .then(() => log.push('resolved'), e => log.push('second:' + e.name + ':' + (e instanceof QuotaExceededError) + ':' + e.quota + ':' + e.requested));
            """);

        // https://webidl.spec.whatwg.org/#quotaexceedederror, carrying the cap and the count the refused
        // registration would have taken the engine to.
        Log(engine).Should().Be("second:QuotaExceededError:true:1:2");
    }

    [Fact]
    public void AbortingADelayedTaskGivesItsTimerSlotBack()
    {
        var (engine, clock) = SchedulerEngine(maxActiveTimers: 1);

        engine.Execute("""
            const controller = new TaskController();
            scheduler.postTask(() => log.push('never'), { delay: 50, signal: controller.signal })
                .catch(e => log.push('rejected:' + e.name));
            controller.abort();

            scheduler.postTask(() => log.push('second'), { delay: 10 }).catch(e => log.push('second:' + e.name));
            """);

        Log(engine).Should().Be("rejected:AbortError");
        engine._webApi!.Timers!.Count.Should().Be(1);

        clock.Advance(50);
        engine.Tasks.ProcessTasks();

        Log(engine).Should().Be("rejected:AbortError,second");
    }

    // --- yield ----------------------------------------------------------------------------------------

    [Fact]
    public void AContinuationOutranksATaskOfTheSamePriority()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            scheduler.postTask(() => {
                log.push('a1');
                scheduler.postTask(() => log.push('other'));
                scheduler.yield().then(() => log.push('a2'));
            });
            """);

        // 'other' was posted first and is the same user-visible priority, and the continuation still goes
        // ahead of it — that is the effective-priority table's whole point.
        Log(engine).Should().Be("a1,a2,other");
    }

    [Fact]
    public void AContinuationInheritsThePriorityOfTheTaskItWasCalledIn()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            scheduler.postTask(() => {
                log.push('background');
                scheduler.postTask(() => log.push('other'));
                scheduler.yield().then(() => log.push('continuation'));
            }, { priority: 'background' });
            """);

        // A background continuation (effective priority 1) loses to a user-visible task (2). Had the
        // continuation defaulted to user-visible it would have won, as the test above shows.
        Log(engine).Should().Be("background,other,continuation");
    }

    [Fact]
    public void AContinuationFollowsASignalsPriorityToo()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const controller = new TaskController({ priority: 'background' });
            scheduler.postTask(() => {
                log.push('task');
                scheduler.postTask(() => log.push('other'));
                scheduler.yield().then(() => log.push('continuation'));
            }, { signal: controller.signal });
            """);

        Log(engine).Should().Be("task,other,continuation");
    }

    [Fact]
    public void AYieldOutsideATaskIsAUserVisibleContinuation()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            scheduler.yield().then(() => log.push('continuation'));
            scheduler.postTask(() => log.push('visible'));
            scheduler.postTask(() => log.push('blocking'), { priority: 'user-blocking' });
            """);

        // Effective priorities 3, 2 and 4: the continuation sits between the two tasks.
        Log(engine).Should().Be("blocking,continuation,visible");
    }

    [Fact]
    public void AContinuationInheritsTheAbortSignalOfTheTaskItWasCalledIn()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const controller = new TaskController();
            scheduler.postTask(() => {
                scheduler.yield().then(() => log.push('continuation'), e => log.push('rejected:' + e.name));
                controller.abort();
            }, { signal: controller.signal }).catch(() => {});
            """);

        Log(engine).Should().Be("rejected:AbortError");
    }

    [Fact]
    public void AYieldInsideAnAlreadyAbortedTaskRejectsImmediately()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const controller = new TaskController();
            scheduler.postTask(() => {
                controller.abort('gone');
                scheduler.yield().then(() => log.push('continuation'), reason => log.push('rejected:' + reason));
                log.push('still running');
            }, { signal: controller.signal }).catch(() => {});
            """);

        Log(engine).Should().Be("still running,rejected:gone");
    }

    [Fact]
    public void AnAwaitedYieldChunksWorkWithoutLosingItsPlace()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            scheduler.postTask(async () => {
                log.push('chunk1');
                await scheduler.yield();
                log.push('chunk2');
            }, { priority: 'user-blocking' });
            scheduler.postTask(() => log.push('other'), { priority: 'user-blocking' });
            """);

        // The first hop of an `await scheduler.yield()` chain inherits, so the continuation is a
        // user-blocking one (effective priority 5) and beats the user-blocking task waiting behind it (4). A
        // continuation that had fallen back to user-visible (3) would have lost.
        Log(engine).Should().Be("chunk1,chunk2,other");
    }

    // --- TaskController, TaskSignal, TaskPriorityChangeEvent ------------------------------------------

    [Fact]
    public void TheInterfacesInheritFromTheirAbortAndEventCounterparts()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const controller = new TaskController();
            log.push(controller instanceof TaskController);
            log.push(controller instanceof AbortController);
            log.push(Object.getPrototypeOf(TaskController) === AbortController);
            log.push(controller.signal instanceof TaskSignal);
            log.push(controller.signal instanceof AbortSignal);
            log.push(controller.signal instanceof EventTarget);
            log.push(Object.getPrototypeOf(TaskSignal) === AbortSignal);
            log.push(Object.getPrototypeOf(TaskPriorityChangeEvent) === Event);
            log.push(Object.prototype.toString.call(controller));
            log.push(Object.prototype.toString.call(controller.signal));
            log.push(Object.prototype.toString.call(scheduler));
            """);

        Log(engine).Should().Be(
            "true,true,true,true,true,true,true,true,[object TaskController],[object TaskSignal],[object Scheduler]");
    }

    [Fact]
    public void ATaskControllersSignalCarriesTheInitialPriority()
    {
        var (engine, _) = SchedulerEngine();

        engine.Evaluate("new TaskController().signal.priority").AsString().Should().Be("user-visible");
        engine.Evaluate("new TaskController({}).signal.priority").AsString().Should().Be("user-visible");
        engine.Evaluate("new TaskController({ priority: undefined }).signal.priority").AsString().Should().Be("user-visible");
        engine.Evaluate("new TaskController({ priority: 'background' }).signal.priority").AsString().Should().Be("background");

        // The signal is [SameObject]: the same instance on every read.
        engine.Evaluate("(() => { const c = new TaskController(); return c.signal === c.signal; })()").AsBoolean().Should().BeTrue();

        // An unknown enumeration value is a TypeError, never a silent default.
        var error = Assert.Throws<JavaScriptException>(() => engine.Execute("new TaskController({ priority: 'urgent' })"));
        error.Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void TaskSignalIsNotConstructibleAndSetPriorityIsBranded()
    {
        var (engine, _) = SchedulerEngine();

        Assert.Throws<JavaScriptException>(() => engine.Execute("new TaskSignal()"))
            .Error.Get("message").AsString().Should().Be("Illegal constructor");

        // A plain AbortController has no priority to change.
        Assert.Throws<JavaScriptException>(() => engine.Execute("TaskController.prototype.setPriority.call(new AbortController(), 'background')"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        Assert.Throws<JavaScriptException>(() => engine.Execute("Object.getOwnPropertyDescriptor(TaskSignal.prototype, 'priority').get.call({})"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void TaskPriorityChangeEventRequiresItsPreviousPriority()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const ev = new TaskPriorityChangeEvent('prioritychange', { previousPriority: 'background', bubbles: true });
            log.push(ev.previousPriority);
            log.push(ev.type);
            log.push(ev.bubbles);
            log.push(ev.isTrusted);
            log.push(ev instanceof Event);
            """);

        Log(engine).Should().Be("background,prioritychange,true,false,true");

        // The dictionary is not optional and previousPriority is required.
        Assert.Throws<JavaScriptException>(() => engine.Execute("new TaskPriorityChangeEvent('x')"))
            .Error.Get("name").AsString().Should().Be("TypeError");
        Assert.Throws<JavaScriptException>(() => engine.Execute("new TaskPriorityChangeEvent('x', {})"))
            .Error.Get("name").AsString().Should().Be("TypeError");
        Assert.Throws<JavaScriptException>(() => engine.Execute("new TaskPriorityChangeEvent('x', { previousPriority: 'nope' })"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void TaskSignalAnyIsATaskSignalWithItsOwnPriority()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const first = new AbortController();
            const composite = TaskSignal.any([first.signal], { priority: 'background' });
            log.push(composite instanceof TaskSignal);
            log.push(composite.priority);

            scheduler.postTask(() => log.push('composite'), { signal: composite });
            scheduler.postTask(() => log.push('plain'));
            """);

        // The composite's priority is fixed, so the task it governs sits in the ordinary background queue.
        Log(engine).Should().Be("true,background,plain,composite");

        engine.Execute("log.length = 0; first.abort('stop'); log.push(composite.aborted + ':' + composite.reason);");
        Log(engine).Should().Be("true:stop");
    }

    [Fact]
    public void TaskSignalAnyCanFollowAnotherSignalsPriority()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("""
            const source = new TaskController({ priority: 'background' });
            const composite = TaskSignal.any([], { priority: source.signal });
            log.push(composite.priority);

            composite.onprioritychange = e => log.push('composite:' + e.previousPriority + '->' + e.target.priority);
            scheduler.postTask(() => log.push('composite-task'), { signal: composite });
            scheduler.postTask(() => log.push('plain'));

            source.setPriority('user-blocking');
            """);

        // The change reaches the composite, which fires its own event and re-prioritizes the task following it.
        Log(engine).Should().Be("background,composite:background->user-blocking,composite-task,plain");
    }

    // --- Installation ---------------------------------------------------------------------------------

    [Fact]
    public void NothingIsInstalledUnlessTheSchedulerFeatureIsNamed()
    {
        var plain = new Engine();
        var console = new Engine(options => options.UseWebApis(WebApiFeatures.Console));
        var scheduler = new Engine(options => options.UseWebApis(WebApiFeatures.Scheduler));

        foreach (var name in new[] { "scheduler", "TaskController", "TaskSignal", "TaskPriorityChangeEvent" })
        {
            plain.Evaluate($"typeof {name}").AsString().Should().Be("undefined");
            console.Evaluate($"typeof {name}").AsString().Should().Be("undefined");
            scheduler.Evaluate($"typeof {name}").AsString().Should().NotBe("undefined");

            // ... and never inside a shadow realm.
            scheduler.Evaluate($"new ShadowRealm().evaluate('typeof {name}')").AsString().Should().Be("undefined");
        }

        // The feature is part of the default set.
        new Engine(options => options.UseWebApis()).Evaluate("typeof scheduler").AsString().Should().Be("object");
    }

    [Fact]
    public void TheGlobalsCarryTheAttributesWebIdlAsksFor()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Scheduler));
        var global = engine.Realm.GlobalObject;

        // scheduler is an ordinary enumerable data property — the documented simplification of the
        // [Replaceable] accessor pair, the same one console, crypto and performance carry.
        var scheduler = global.GetOwnProperty("scheduler");
        scheduler.Should().BeOfType<LazyPropertyDescriptor<Engine>>();
        scheduler.Enumerable.Should().BeTrue();
        scheduler.Writable.Should().BeTrue();
        scheduler.Configurable.Should().BeTrue();

        // Still unmaterialized: enabling a feature nobody uses costs one descriptor and nothing else.
        (scheduler._flags & PropertyFlag.CustomJsValue).Should().NotBe(PropertyFlag.None);
        scheduler._value.Should().BeNull();

        foreach (var name in new[] { "Scheduler", "TaskController", "TaskSignal", "TaskPriorityChangeEvent" })
        {
            var descriptor = global.GetOwnProperty(name);
            descriptor.Should().BeOfType<LazyPropertyDescriptor<Engine>>();
            descriptor.Enumerable.Should().BeFalse();
            descriptor.Writable.Should().BeTrue();
            descriptor.Configurable.Should().BeTrue();
        }
    }

    [Fact]
    public void LeavesAGlobalTheHostAlreadyOwns()
    {
        var marker = new JsString("host's own");
        var engine = new Engine(options => options
            .Configure(e => e.SetValue("scheduler", marker))
            .UseWebApis());

        engine.Evaluate("scheduler").Should().BeSameAs(marker);
    }

    [Fact]
    public void TheOperationsHaveTheirWebIdlShape()
    {
        var (engine, _) = SchedulerEngine();

        engine.Evaluate("typeof scheduler.postTask").AsString().Should().Be("function");
        engine.Evaluate("scheduler.postTask.length").AsNumber().Should().Be(1);
        engine.Evaluate("scheduler.postTask.name").AsString().Should().Be("postTask");
        engine.Evaluate("scheduler.yield.length").AsNumber().Should().Be(0);
        engine.Evaluate("scheduler.yield.name").AsString().Should().Be("yield");

        // The operations live on the interface prototype object, where WebIDL puts them and where they are
        // enumerable — https://webidl.spec.whatwg.org/#es-operations. The instance carries nothing of its
        // own, so it looks as empty as a browser's does.
        foreach (var member in new[] { "postTask", "yield" })
        {
            engine.Evaluate($"Object.prototype.hasOwnProperty.call(Scheduler.prototype, '{member}')").AsBoolean().Should().BeTrue(member);
            engine.Evaluate($"Object.prototype.hasOwnProperty.call(scheduler, '{member}')").AsBoolean().Should().BeFalse(member);
            engine.Evaluate($"Object.keys(Scheduler.prototype).indexOf('{member}') >= 0").AsBoolean().Should().BeTrue(member);
        }

        engine.Evaluate("Object.keys(scheduler).length").AsNumber().Should().Be(0);
        engine.Evaluate("Reflect.ownKeys(scheduler).length").AsNumber().Should().Be(0);

        // The scheduler object is the same one on every read.
        engine.Evaluate("scheduler === scheduler").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The interface object and the interface prototype object, which <c>scheduler</c> had neither of.
    /// https://wicg.github.io/scheduling-apis/#sec-scheduler declares
    /// <c>[Exposed=(Window, Worker)] interface Scheduler</c> with no constructor operation, which is what
    /// Chrome ships and what these lines assert.
    /// </summary>
    [Fact]
    public void HasARealInterfaceObjectAndInterfacePrototypeObject()
    {
        var (engine, _) = SchedulerEngine();

        engine.Evaluate("typeof Scheduler").AsString().Should().Be("function");
        engine.Evaluate("Scheduler.name").AsString().Should().Be("Scheduler");
        engine.Evaluate("Scheduler.length").AsNumber().Should().Be(0);

        engine.Evaluate("scheduler instanceof Scheduler").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(scheduler) === Scheduler.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(Scheduler.prototype) === Object.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Scheduler.prototype.constructor === Scheduler").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(scheduler)").AsString().Should().Be("[object Scheduler]");

        // instanceof is answered by the chain, never by a shim.
        engine.Evaluate("Object.prototype.hasOwnProperty.call(Scheduler, Symbol.hasInstance)").AsBoolean().Should().BeFalse();

        // No constructor operation means an interface object that refuses to construct —
        // https://webidl.spec.whatwg.org/#es-interface-call.
        Assert.Throws<JavaScriptException>(() => engine.Execute("new Scheduler()"))
            .Error.Get("message").AsString().Should().Be("Illegal constructor");
    }

    /// <summary>
    /// The containment half of https://github.com/sebastienros/jint/issues/3257, which #3266 closed for
    /// <c>console</c> and left open here: while the singleton sat directly on <c>%Object.prototype%</c>,
    /// <c>scheduler.__proto__.foo = …</c> poisoned every object in the realm.
    /// </summary>
    [Fact]
    public void PatchingSchedulersPrototypeDoesNotReachObjectPrototype()
    {
        var (engine, _) = SchedulerEngine();

        engine.Execute("scheduler.__proto__.decorated = 42;");

        engine.Evaluate("scheduler.decorated").AsNumber().Should().Be(42);

        engine.Evaluate("Object.prototype.hasOwnProperty.call(Object.prototype, 'decorated')").AsBoolean().Should().BeFalse();
        engine.Evaluate("'decorated' in {}").AsBoolean().Should().BeFalse();
        engine.Evaluate("({}).decorated").IsUndefined().Should().BeTrue();
        engine.Evaluate("[].decorated").IsUndefined().Should().BeTrue();
        engine.Evaluate("(function () {}).decorated").IsUndefined().Should().BeTrue();
    }

    // --- Engine lifecycle -----------------------------------------------------------------------------

    [Fact]
    public void AGlobalSnapshotRestoreDropsEverythingStillScheduled()
    {
        var (engine, clock) = SchedulerEngine();

        var ran = false;
        engine.SetValue("mark", new Action(() => ran = true));

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("""
            const controller = new TaskController({ priority: 'background' });
            scheduler.postTask(mark, { signal: controller.signal, delay: 50 });
            scheduler.postTask(mark, { delay: 50 });
            """);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        clock.Advance(1000);
        engine.Tasks.ProcessTasks();

        ran.Should().BeFalse();
        engine._webApi!.Timers!.Count.Should().Be(0);

        // ... and the engine still schedules normally in the next cycle.
        engine.Execute("var log = []; scheduler.postTask(() => log.push('next'));");
        Log(engine).Should().Be("next");
    }

    [Fact]
    public void ABlockingUnwrapRunsTheWholeChain()
    {
        var engine = new Engine(options => options.UseWebApis());

        var result = engine
            .Evaluate("""
                (async () => {
                    let total = 0;
                    for (let i = 0; i < 5; i++) {
                        total += await scheduler.postTask(() => i, { priority: 'background' });
                        await scheduler.yield();
                    }
                    return total;
                })()
                """)
            .UnwrapIfPromise();

        result.AsNumber().Should().Be(10);
    }

    [Fact]
    public async Task AnAsynchronousEvaluationRunsTheWholeChain()
    {
        var engine = new Engine(options => options.UseWebApis());

        var result = await engine.EvaluateAsync("""
            (async () => {
                const first = await scheduler.postTask(() => 'a');
                const second = await scheduler.postTask(() => 'b', { delay: 5 });
                await scheduler.yield();
                return first + second;
            })()
            """);

        result.AsString().Should().Be("ab");
    }

    /// <summary>
    /// Nothing but a pump the host calls itself runs a task: Jint starts no thread and arms no background
    /// timer, so a delayed task that is due sits there until <c>ProcessTasks</c> is called.
    /// </summary>
    /// <remarks>
    /// On the same manual clock as the rest of this class, which is what makes each of the three states below
    /// a fact rather than a race (#3372). The first assertion used to be a statement about how long the
    /// enclosing <c>Execute</c> took: the outer background task runs during that call's own drain and posts
    /// the inner one with a twenty-millisecond delay, so on a machine where the drain outlived twenty
    /// milliseconds of <em>wall</em> clock the inner task came due inside the same <c>Execute</c> and
    /// <c>done</c> was already true. A clock that only this test moves cannot be outlived, and it buys the
    /// middle state as well — due, and still not run, which is the actual claim and which no amount of
    /// polling could have asserted.
    /// </remarks>
    [Fact]
    public void AHostsOwnPumpRunsTheTasks()
    {
        var (engine, clock) = SchedulerEngine();

        var done = false;
        engine.SetValue("done", new Action(() => done = true));

        engine.Execute("scheduler.postTask(() => scheduler.postTask(done, { delay: 20 }), { priority: 'background' });");
        done.Should().BeFalse("the clock has not moved, so the inner task is not due");

        clock.Advance(20);
        done.Should().BeFalse("being due is not enough — only a pump the host calls may run it");

        engine.Tasks.ProcessTasks();
        done.Should().BeTrue("the host's own pump is what runs it");
    }
}
#endif
