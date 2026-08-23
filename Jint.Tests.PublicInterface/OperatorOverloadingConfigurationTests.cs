using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins when <see cref="Options.InteropOptions.AllowOperatorOverloading"/> is read.
/// <para>
/// The engine snapshots it once instead of consulting <see cref="Options"/> on every evaluation, so
/// the moment it takes the reading is observable to a host: everything that runs before it is
/// honoured, everything after it is not. The snapshot is taken after <c>Options.Apply</c>, which is
/// what runs a host's <c>Configure</c> callbacks, so both documented ways of setting the option at
/// construction time work. Mutating the <see cref="Options"/> instance <em>after</em> the engine
/// exists deliberately does not — see the test that says so.
/// </para>
/// </summary>
public class OperatorOverloadingConfigurationTests
{
    /// <summary>
    /// A CLR type whose <c>+</c> is only reachable through operator overloading. With the option off
    /// the addition falls back to string concatenation, so the result has no <c>Value</c> at all —
    /// which makes <c>undefined</c> the answer that means "the option was not in effect".
    /// </summary>
    public sealed class Amount
    {
        public Amount(int value) => Value = value;

        public int Value { get; }

        public static Amount operator +(Amount left, Amount right) => new Amount(left.Value + right.Value);
    }

    private static JsValue Add(Engine engine)
    {
        engine.SetValue("a", new Amount(1));
        engine.SetValue("b", new Amount(2));
        return engine.Evaluate("(a + b).Value");
    }

    [Fact]
    public void TheConstructorConfigureDelegateEnablesIt()
    {
        Add(new Engine(options => options.Interop.AllowOperatorOverloading = true)).Should().Be(3);
    }

    [Fact]
    public void AnOptionsInstanceConfiguredBeforeConstructionEnablesIt()
    {
        var options = new Options();
        options.Interop.AllowOperatorOverloading = true;

        Add(new Engine(options)).Should().Be(3);
    }

    [Fact]
    public void AConfigureCallbackEnablesIt()
    {
        // Configure callbacks run from Options.Apply, i.e. later in the constructor than the point
        // at which the option used to be read. Registering one is the documented way to defer setup
        // to construction time, so a host that enables the option from there has to be honoured.
        var options = new Options();
        options.Configure(_ => options.Interop.AllowOperatorOverloading = true);

        Add(new Engine(options)).Should().Be(3);
    }

    [Fact]
    public void ItIsOffByDefault()
    {
        Add(new Engine()).Should().Be(JsValue.Undefined);
    }

    [Fact]
    public void ADisablingConfigureCallbackWinsOverAnEarlierEnable()
    {
        // The reading is taken once, at the end of construction, so the last writer before that wins
        // in both directions rather than only in the enabling one.
        var options = new Options();
        options.Interop.AllowOperatorOverloading = true;
        options.Configure(_ => options.Interop.AllowOperatorOverloading = false);

        Add(new Engine(options)).Should().Be(JsValue.Undefined);
    }

    [Fact]
    public void MutatingTheOptionsAfterConstructionDoesNotReachTheEngine()
    {
        // Deliberate: the reading belongs to the engine, not to the Options instance it was built
        // from. One Options instance is meant to be shared by many engines, including concurrently
        // running ones, so a live read would let one host thread change how another engine's already
        // running script evaluates `+`. Every other option the engine cares about is snapshotted the
        // same way. A host that wants the option has to ask for it before the engine exists.
        var options = new Options();
        var engine = new Engine(options);

        options.Interop.AllowOperatorOverloading = true;

        Add(engine).Should().Be(JsValue.Undefined);

        // ... and the same Options instance still configures the next engine built from it.
        Add(new Engine(options)).Should().Be(3);
    }
}
