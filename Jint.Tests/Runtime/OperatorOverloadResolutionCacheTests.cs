#nullable enable

using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

/// <summary>
/// Which table an engine remembers its operator-overload resolutions in. The public-interface suite pins the
/// answers two engines must not take from each other; this pins the mechanism that keeps them apart, since
/// "both engines were right" is also what a cache that was never consulted looks like.
/// </summary>
public class OperatorOverloadResolutionCacheTests
{
    public sealed class Amount
    {
        public static string operator +(Amount left, Amount right) => "operator";
    }

    /// <summary>Behaves exactly like the stock converter but is not it, which is what makes it a host one.</summary>
    private sealed class WrappingTypeConverter : DefaultTypeConverter
    {
        public WrappingTypeConverter(Engine engine) : base(engine)
        {
        }
    }

    private static Engine Evaluate(Action<Options>? configure = null)
    {
        var engine = new Engine(options =>
        {
            options.Interop.AllowOperatorOverloading = true;
            configure?.Invoke(options);
        });

        engine.SetValue("a", new Amount());
        engine.SetValue("b", new Amount());
        engine.Evaluate("a + b").AsString().Should().Be("operator");
        return engine;
    }

    [Fact]
    public void AStockEngineRemembersInTheProcessWideTable()
    {
        var engine = Evaluate();

        engine._engineOperatorOverloads.Should().BeNull(
            "the stock converter resolves what every other stock engine would, so the answer is shareable");
    }

    [Fact]
    public void AnEngineWithItsOwnConverterRemembersOnItself()
    {
        var engine = Evaluate(options => options.SetTypeConverter(e => new WrappingTypeConverter(e)));

        engine._engineOperatorOverloads.Should().NotBeNull().And.HaveCount(1,
            "this engine's converter answers overload scoring's last rule, so its resolution is its own");

        // and it really is a cache: a second evaluation of the same pair adds nothing
        engine.Evaluate("a + b").AsString().Should().Be("operator");
        engine._engineOperatorOverloads.Should().HaveCount(1);
    }

    [Fact]
    public void SwappingTheConverterDropsWhatTheOldOneDecided()
    {
        var engine = Evaluate(options => options.SetTypeConverter(e => new WrappingTypeConverter(e)));
        engine._engineOperatorOverloads.Should().HaveCount(1);

        engine.TypeConverter = new WrappingTypeConverter(engine);

        engine._engineOperatorOverloads.Should().BeEmpty(
            "every entry was scored against the converter that has just been replaced");
    }
}
