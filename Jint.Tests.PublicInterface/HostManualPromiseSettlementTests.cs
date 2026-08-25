#nullable enable

using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>Engine.Advanced.RegisterPromise</c> from the embedder's side: a host that settles a promise from a
/// background thread must never have to build the settlement value on that thread.
/// </summary>
/// <remarks>
/// The settle functions used to take a <see cref="JsValue"/>. A host holding a CLR value — an HTTP body, a
/// row, a dictionary — therefore had to call <c>JsValue.FromObject</c> or a <c>JsonParser</c> where it stood,
/// which is a value built into an engine's realm from a thread that does not own it. Real embedders shipped
/// exactly that, with a lock around the settle and the conversion outside it, because the conversion is an
/// argument and C# evaluates arguments at the call site. The parameter is now <see cref="object"/> and the
/// conversion happens inside the enqueued job, so the unsafe call has nowhere left to be written.
/// </remarks>
public class HostManualPromiseSettlementTests
{
    /// <summary>
    /// The claim itself, in the only shape where the two threads are distinguishable: the engine is owned by
    /// another thread when the settle is made, so the settling thread cannot claim it and the conversion has
    /// to wait for the pump. Nothing of the value is built on the settling thread.
    /// </summary>
    /// <remarks>
    /// When the engine is <em>idle</em> a settling thread claims it and drains inline, and the conversion
    /// then runs on that thread — correctly, because at that moment it is the engine's thread. Exclusive
    /// ownership is the invariant, not any particular thread identity, so an idle engine cannot tell the two
    /// implementations apart and is not what this pins.
    /// </remarks>
    [Fact]
    public void TheConversionWaitsForTheEngineWhenTheSettlingThreadCannotClaimIt()
    {
        var converter = new ThreadRecordingConverter();
        using var engine = new Engine(options => options.AddObjectConverter(converter));

        var manual = engine.Advanced.RegisterPromise();
        engine.SetValue("pending", manual.Promise);
        engine.Execute("var seen; pending.then(v => { seen = v.marker; });");

        var settlingThread = -1;
        var convertedBeforeThePump = true;

        engine.SetValue("busy", new Action(() =>
        {
            // This thread owns the engine for the whole of this call.
            var settler = new Thread(() =>
            {
                settlingThread = Environment.CurrentManagedThreadId;
                manual.Resolve(new Payload("ok"));
            });
            settler.Start();
            settler.Join();

            convertedBeforeThePump = converter.ConvertingThread != -1;
        }));

        engine.Execute("busy();");
        engine.Advanced.ProcessTasks();

        convertedBeforeThePump.Should().BeFalse("the settling thread could not claim the engine, so nothing of the value may have been built yet");
        converter.ConvertingThread.Should().NotBe(settlingThread);
        converter.ConvertingThread.Should().Be(Environment.CurrentManagedThreadId);
        engine.Evaluate("seen").AsString().Should().Be("ok");
    }

    /// <summary>
    /// The embedding shape this exists for: the engine thread is inside a host call when a background
    /// completion settles. Converting on the settling thread here is what a guarded conversion refuses and an
    /// unguarded one corrupts; handing the raw CLR value over cannot do either.
    /// </summary>
    [Fact]
    public void AHostMaySettleWhileTheEngineThreadIsInsideAHostCall()
    {
        using var engine = new Engine();

        var manual = engine.Advanced.RegisterPromise();
        engine.SetValue("pending", manual.Promise);
        engine.Execute("var body; pending.then(v => { body = v.status; });");

        var settled = new ManualResetEventSlim();
        Exception? failure = null;

        engine.SetValue("busy", new Action(() =>
        {
            // The engine is owned by this thread for the whole of this call, which is what makes a
            // conversion on the settling thread a genuine race rather than a theoretical one.
            var settler = new Thread(() =>
            {
                try
                {
                    manual.Resolve(new Dictionary<string, object?> { ["status"] = "done" });
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    settled.Set();
                }
            });
            settler.Start();
            settled.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue();
            settler.Join();
        }));

        engine.Execute("busy();");
        engine.Advanced.ProcessTasks();

        failure.Should().BeNull();
        engine.Evaluate("body").AsString().Should().Be("done");
    }

    /// <summary>
    /// A host that already holds a <see cref="JsValue"/> — built on the engine's thread, as it should be —
    /// loses nothing: the conversion returns it unchanged, so identity survives.
    /// </summary>
    [Fact]
    public void AJsValueSettlesAsItselfWithIdentityIntact()
    {
        using var engine = new Engine();

        var manual = engine.Advanced.RegisterPromise();
        var value = new JsObject(engine);
        value.Set("tag", "same");

        engine.SetValue("pending", manual.Promise);
        engine.Execute("var received; pending.then(v => { received = v; });");

        manual.Resolve(value);
        engine.Advanced.ProcessTasks();

        engine.Evaluate("received").Should().BeSameAs(value);
    }

    /// <summary>
    /// <see langword="null"/> is a value a CLR settle can genuinely carry, and it has an unambiguous
    /// JavaScript spelling. Under the old signature it was a null reference in a <see cref="JsValue"/>
    /// parameter, which had no defined meaning at all.
    /// </summary>
    [Fact]
    public void SettlingWithNullFulfilsWithJavaScriptNull()
    {
        using var engine = new Engine();

        var manual = engine.Advanced.RegisterPromise();
        engine.SetValue("pending", manual.Promise);
        engine.Execute("var received = 'untouched'; pending.then(v => { received = v; });");

        manual.Resolve(null);
        engine.Advanced.ProcessTasks();

        engine.Evaluate("received").Should().Be(JsValue.Null);
    }

    /// <summary>
    /// Reject converts by the same rule as resolve — they are one implementation, and a rejection reason is
    /// as likely to start life as a CLR object as a fulfilment value is.
    /// </summary>
    [Fact]
    public void RejectConvertsItsReasonTheSameWay()
    {
        using var engine = new Engine();

        var manual = engine.Advanced.RegisterPromise();
        engine.SetValue("pending", manual.Promise);
        engine.Execute("var reason; pending.catch(e => { reason = e.status; });");

        var settler = new Thread(() => manual.Reject(new Dictionary<string, object?> { ["status"] = "nope" }));
        settler.Start();
        settler.Join();

        engine.Advanced.ProcessTasks();

        engine.Evaluate("reason").AsString().Should().Be("nope");
    }

    /// <summary>
    /// The recipe README documents, end to end: a host function returns a registered promise, a background
    /// task settles it with a CLR value, and <c>EvaluateAsync</c> awaits the result. Pinned because a broken
    /// example in the README is worse than none.
    /// </summary>
    [Fact]
    public async Task TheDocumentedHostPromiseRecipeWorksEndToEnd()
    {
        using var engine = new Engine();

        engine.SetValue("getJSON", new Func<string, JsValue>(url =>
        {
            var (promise, resolve, reject) = engine.Advanced.RegisterPromise();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Yield();
                    resolve($$"""{"url":"{{url}}"}""");
                }
                catch (Exception ex)
                {
                    reject(ex.Message);
                }
            });

            return promise;
        }));

        var body = await engine.EvaluateAsync("getJSON('https://example.org/api')");

        body.AsString().Should().Be("""{"url":"https://example.org/api"}""");
    }

    private sealed record Payload(string Marker);

    private sealed class ThreadRecordingConverter : ObjectConverter
    {
        private volatile int _convertingThread = -1;

        public int ConvertingThread => _convertingThread;

        public override bool TryConvert(Engine engine, object value, [NotNullWhen(true)] out JsValue? result)
        {
            if (value is not Payload payload)
            {
                result = null;
                return false;
            }

            _convertingThread = Environment.CurrentManagedThreadId;

            var converted = new JsObject(engine);
            converted.Set("marker", payload.Marker);
            result = converted;
            return true;
        }
    }
}
