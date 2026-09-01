#nullable enable

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins that the CLR delegate a JavaScript function converts to is the one built for the target type the
/// host asked for, in every engine that asks (sebastienros/jint#3434).
/// </summary>
/// <remarks>
/// <para>
/// The conversion is memoized so <c>host.on(f)</c> and <c>host.off(f)</c> hand the host the same
/// <see cref="Delegate"/> instance, which is the identity <c>-=</c> needs. The memo used to be keyed on the
/// function alone, and one level down on that function's AST node — neither of which carries the delegate
/// type the compiled binder was built for. The AST node is process-wide state and
/// <see cref="Engine.PrepareScript"/> is documented as shareable across engines, so whichever engine
/// evaluated a shared preparation first decided the target type for every engine after it, and the
/// reflection invoke that followed rejected the delegate it was handed with
/// <c>ArgumentException: Object of type 'Notify' cannot be converted to type 'Transform'</c>.
/// </para>
/// <para>
/// This is the embedder-visible half of it: two engines, different host types, one cached
/// <c>Prepared&lt;Script&gt;</c> — the shape the README recommends for production.
/// </para>
/// </remarks>
public class HostDelegateConversionTests
{
    public delegate void Notify(int value);

    public delegate string Transform(string value);

    public sealed class NotifyHost
    {
        public string Call(Notify f)
        {
            f(1);
            return "notify";
        }
    }

    public sealed class TransformHost
    {
        public string Call(Transform f) => "transform:" + f("x");
    }

    public sealed class BothHost
    {
        public string Call(Notify f)
        {
            f(1);
            return "notify";
        }

        public string CallTransform(Transform f) => "transform:" + f("x");
    }

    /// <summary>
    /// One engine, one function <em>instance</em>, two delegate types. The shared-preparation tests below give
    /// each engine a function of its own, so they exercise only the AST-keyed lane; this is the
    /// instance-keyed one, where the same function object is asked for two different targets and the memo has
    /// to answer with two different delegates rather than with the first one twice.
    /// </summary>
    [Test]
    public void OneEngineConvertingOneFunctionToTwoDelegateTypes()
    {
        var engine = new Engine();
        engine.SetValue("host", new BothHost());

        engine.Evaluate("var f = function (x) { return String(x); }; host.call(f);")
            .AsString().Should().Be("notify");

        // the same f, now wanted as a Transform: served the Notify, this was
        // ArgumentException: Object of type 'Notify' cannot be converted to type 'Transform'
        engine.Evaluate("host.callTransform(f);").AsString().Should().Be("transform:x");

        // and back again, so neither conversion evicted the other
        engine.Evaluate("host.call(f);").AsString().Should().Be("notify");
    }

    private const string Source = "host.call(function (x) { return String(x); });";

    [Test]
    public void TwoEnginesSharingOnePreparedScript()
    {
        var prepared = Engine.PrepareScript(Source);

        var first = new Engine();
        first.SetValue("host", new NotifyHost());
        first.Evaluate(prepared).AsString().Should().Be("notify");

        var second = new Engine();
        second.SetValue("host", new TransformHost());
        second.Evaluate(prepared).AsString().Should().Be("transform:x");
    }

    [Test]
    public void TwoEnginesSharingOnePreparedScriptInTheOtherOrder()
    {
        var prepared = Engine.PrepareScript(Source);

        var first = new Engine();
        first.SetValue("host", new TransformHost());
        first.Evaluate(prepared).AsString().Should().Be("transform:x");

        var second = new Engine();
        second.SetValue("host", new NotifyHost());
        second.Evaluate(prepared).AsString().Should().Be("notify");
    }

    /// <summary>
    /// The same preparation alternating between the two engines: each engine keeps answering for its own
    /// host type however many times the other one has run in between.
    /// </summary>
    [Test]
    public void AlternatingEnginesOnOnePreparedScript()
    {
        var prepared = Engine.PrepareScript(Source);

        var notify = new Engine();
        notify.SetValue("host", new NotifyHost());
        var transform = new Engine();
        transform.SetValue("host", new TransformHost());

        for (var i = 0; i < 5; i++)
        {
            notify.Evaluate(prepared).AsString().Should().Be("notify");
            transform.Evaluate(prepared).AsString().Should().Be("transform:x");
        }
    }
}
