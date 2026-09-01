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

    public delegate string Feed(Payload value);

    public sealed class Payload
    {
        public int Value => 42;
    }

    public sealed class PayloadHost
    {
        public string Call(Feed f) => f(new Payload());
    }

    private const string Source = "host.call(function (x) { return String(x); });";

    [Fact]
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

    [Fact]
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
    /// The other half of what the shared AST-node cache holds: the compiled binder marshals the host's
    /// arguments into the realm of the engine that is running, not of whichever engine compiled it. One
    /// delegate type is enough here — the two engines share the binder by design — so this is the case the
    /// target-type key alone does not cover.
    /// </summary>
    [Fact]
    public void ASharedBinderMarshalsIntoTheRunningEnginesRealm()
    {
        var prepared = Engine.PrepareScript("host.call(function (x) { return String(x instanceof Object) + ':' + x.Value; });");

        var first = new Engine();
        first.SetValue("host", new PayloadHost());
        first.Evaluate(prepared).AsString().Should().Be("true:42");

        var second = new Engine();
        second.SetValue("host", new PayloadHost());
        second.Evaluate(prepared).AsString().Should().Be("true:42",
            "an object built by the first engine's realm is not an Object in the second engine's");
    }

    /// <summary>
    /// The same preparation alternating between the two engines: each engine keeps answering for its own
    /// host type however many times the other one has run in between.
    /// </summary>
    [Fact]
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
