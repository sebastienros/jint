#nullable enable

using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Which authorized cross-thread callbacks a <em>blocking drain</em> admits, from the outside. README's
/// Thread-safety section names the drain as one of the engine's callback-admission windows; these pin the
/// two things about it that a reader comparing it with the pump would reasonably expect to be otherwise,
/// and that a change made for symmetry would silently take away (sebastienros/jint#3262).
/// </summary>
/// <remarks>
/// <para>
/// The drain opens its window unconditionally — nested inside a running evaluation, and inside
/// <see cref="Engine.ModuleOperations.Import(string)"/>, which claims the engine before the drain re-enters
/// and so is <em>not</em> a claiming scope even on a wholly idle engine. Keying the window on the claiming
/// scope, the way a park on the pump must be keyed, would therefore refuse callbacks at a documented
/// top-level window rather than only at a nested frame. The reason the two frames differ is that a park
/// runs no script, so a callback admitted there would be the only script interleaved into somebody else's
/// evaluation, whereas a drain runs queued jobs on every iteration whether one is admitted or not.
/// </para>
/// <para>
/// Every test here arms its prober from <em>inside</em> the drain and ends the drain from the prober, so
/// the window provably covers every attempt whatever the machine does: what is measured is never a race
/// between "the drain has started" and "the callback was dispatched". A refusal fails the assertion rather
/// than hanging, because a refused attempt still counts down to the one that releases the drain.
/// </para>
/// </remarks>
public class HostDrainAdmissionTests
{
    /// <summary>
    /// A wedge ceiling, never an assertion: nothing here waits it out on a healthy run.
    /// </summary>
    private static readonly TimeSpan Ceiling = TestBudgets.WedgeCeiling;

    private const string ConcurrentUseMessage =
        "This Engine is already in use by another thread or has an asynchronous operation in progress.";

    /// <summary>
    /// Invokes a converted engine callback from a thread of its own, a fixed number of times, and records
    /// how each attempt ended. It is armed from inside the frame under test and, once its <em>last</em>
    /// attempt has returned, completes <see cref="Finished"/> — which is the only thing the frame under
    /// test is waiting for, so the window cannot close while an attempt is outstanding.
    /// </summary>
    private sealed class CallbackProber
    {
        private readonly ManualResetEventSlim _armed = new(false);

        public int Admitted;
        public int Refused;
        public int Attempts;
        public string? UnexpectedFailure;

        public readonly TaskCompletionSource<int> Finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CallbackProber(Func<Func<int>> callback, int attempts)
        {
            var thread = new Thread(() =>
            {
                _armed.Wait();

                for (var i = 0; i < attempts; i++)
                {
                    Interlocked.Increment(ref Attempts);
                    try
                    {
                        callback()();
                        Interlocked.Increment(ref Admitted);
                    }
                    catch (InvalidOperationException e) when (e.Message.Contains(ConcurrentUseMessage, StringComparison.Ordinal))
                    {
                        Interlocked.Increment(ref Refused);
                    }
                    catch (Exception e)
                    {
                        UnexpectedFailure ??= e.GetType().Name + ": " + e.Message;
                    }
                }

                Finished.TrySetResult(1);
            })
            {
                IsBackground = true,
            };

            thread.Start();
        }

        public void Arm() => _armed.Set();

        public string Describe() =>
            $"admitted={Admitted} refused={Refused} attempts={Attempts} unexpected={UnexpectedFailure ?? "none"}";
    }

    private const int Attempts = 4;

    /// <summary>
    /// Hands a callback out under an evaluation that then ends, so what is dispatched later is authorized
    /// but belongs to no operation still in force — the case an anonymous window admits and an
    /// operation-scoped one does not.
    /// </summary>
    private static Func<Func<int>> StashACallbackFromAnEndedEvaluation(Engine engine)
    {
        Func<int>? stashed = null;
        engine.SetValue("stash", new Action<Func<int>>(callback => stashed = callback));
        engine.Execute("stash(() => 42);");
        return () => stashed!;
    }

    /// <summary>
    /// A blocking <c>Modules.Import</c> is one of the windows README enumerates, and it is reached here on
    /// an otherwise idle engine from the host's own thread — yet the drain inside it is not the scope that
    /// claimed the engine, because <c>Import</c> claimed it first. Guarding the window on the claiming
    /// scope would close this one.
    /// </summary>
    [Fact]
    public void ABlockingImportAdmitsACallbackAuthorizedUnderAnEarlierEvaluation()
    {
        var engine = new Engine(options => options.Constraints.PromiseTimeout = Ceiling);
        var stashed = StashACallbackFromAnEndedEvaluation(engine);

        var prober = new CallbackProber(stashed, Attempts);
        engine.SetValue("arm", new Action(prober.Arm));
        engine.SetValue("tick", new Func<Task>(() => Task.Delay(20)));
        engine.SetValue("pending", new Func<Task<int>>(async () => await prober.Finished.Task.ConfigureAwait(false)));

        // The first await suspends the module and hands the thread to Import's drain; everything after it
        // therefore runs from a continuation the drain itself is executing, which is where arm() belongs.
        engine.Modules.Add("m", """
            await tick();
            arm();
            export const v = await pending();
            """);

        engine.Modules.Import("m");

        prober.UnexpectedFailure.Should().BeNull();
        prober.Attempts.Should().Be(Attempts);
        prober.Refused.Should().Be(0, "a blocking import is a callback-admission window: " + prober.Describe());
        prober.Admitted.Should().Be(Attempts, prober.Describe());
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// The frame sebastienros/jint#3262 asks about: a drain reached from a host call the script itself
    /// made, so an admitted callback takes its turn in the middle of a running evaluation. That is
    /// deliberate — the drain is already interleaving arbitrary script from its own queue while it waits,
    /// so refusing the callback would remove the callback and not the interleaving.
    /// </summary>
    [Fact]
    public void ADrainNestedInsideARunningEvaluationAdmitsOneToo()
    {
        var engine = new Engine();
        var stashed = StashACallbackFromAnEndedEvaluation(engine);

        var prober = new CallbackProber(stashed, Attempts);
        engine.SetValue("arm", new Action(prober.Arm));
        engine.SetValue("tick", new Func<Task>(() => Task.Delay(20)));
        engine.SetValue("pending", new Func<Task<int>>(async () => await prober.Finished.Task.ConfigureAwait(false)));
        engine.SetValue("blockOn", new Func<JsValue, JsValue>(value => value.UnwrapIfPromise(Ceiling)));

        // The reaction can only run from inside blockOn's drain — nothing else drains between statements —
        // so the window is provably open before the first attempt and until after the last one returns.
        engine.Execute("""
            tick().then(() => arm());
            blockOn(pending());
            """);

        prober.UnexpectedFailure.Should().BeNull();
        prober.Attempts.Should().Be(Attempts);
        prober.Refused.Should().Be(0, "a nested drain is a window on the same terms: " + prober.Describe());
        prober.Admitted.Should().Be(Attempts, prober.Describe());
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// The other half, and what keeps the paragraph above from meaning "a drain admits everything": how
    /// narrowly a window admits is decided by whether an operation token is in force under it, never by
    /// where the frame sits. Once a host call in this evaluation has been handed a callback, a token is in
    /// force and the very same nested drain admits only that operation's callbacks.
    /// </summary>
    [Fact]
    public void ADrainUnderAnOperationTokenAdmitsOnlyThatOperationsCallbacks()
    {
        var engine = new Engine();
        var stashed = StashACallbackFromAnEndedEvaluation(engine);

        var prober = new CallbackProber(stashed, Attempts);
        engine.SetValue("arm", new Action(prober.Arm));
        engine.SetValue("tick", new Func<Task>(() => Task.Delay(20)));
        engine.SetValue("pending", new Func<Task<int>>(async () => await prober.Finished.Task.ConfigureAwait(false)));
        engine.SetValue("blockOn", new Func<JsValue, JsValue>(value => value.UnwrapIfPromise(Ceiling)));
        engine.SetValue("takesACallback", new Action<Func<int>>(_ => { }));

        engine.Execute("""
            takesACallback(() => 1);
            tick().then(() => arm());
            blockOn(pending());
            """);

        prober.UnexpectedFailure.Should().BeNull();
        prober.Attempts.Should().Be(Attempts);
        prober.Admitted.Should().Be(0, "the enclosing entry's token is in force: " + prober.Describe());
        prober.Refused.Should().Be(Attempts, prober.Describe());
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }
}
