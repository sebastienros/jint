#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// <see href="https://github.com/sebastienros/jint/issues/4129">#4129</see>: naming a bound function used
/// to flatten the target's whole <c>"bound bound … f"</c> name into a fresh CLR string, so a chain of N
/// binds cost O(N²) in both time and retained memory.
/// </summary>
public class BoundFunctionNameTests
{
    /// <summary>
    /// The depth the issue measured: 37.3 s and roughly 30 GB of live text before the fix, which is what
    /// shut the 7 GB Linux CI runners down mid-test.
    /// </summary>
    private const int ChainDepth = 100_000;

    /// <summary>
    /// The wedge ceiling, and deliberately not the assertion — no duration is asserted anywhere here.
    /// The whole script allocates about 53 MB once each level's name is a node over the level below; a
    /// per-level copy allocates about 6·N² bytes, needs about 30 GB for the whole chain, and trips this
    /// around the 6,500th level, so the quadratic is reported as a failed test rather than as an exhausted
    /// machine — and a run against the defect never holds more than this much live text while it does.
    /// </summary>
    private const long AllocationCeiling = 256L * 1024 * 1024;

    [Fact]
    public void ADeepChainOfBoundFunctionsDoesNotCopyTheNameAtEveryLevel()
    {
        var engine = new Engine(options => options.LimitMemory(AllocationCeiling));

        // Reading the full name at the end is legitimately O(N) once — it is 600,006 characters — and
        // comparing it against the same text built in script is what pins the bytes as unchanged.
        var result = engine.Evaluate($$"""
            function target(x) { return x; }
            var f = target;
            var shallow;
            for (var i = 0; i < {{ChainDepth}}; i++) {
                f = f.bind(null);
                if (i === 99) { shallow = f; }
            }
            var name = f.name;
            [
                name.length,
                name === 'bound '.repeat({{ChainDepth}}) + 'target',
                name.slice(0, 12),
                name.slice(-12),
                f.length,
                typeof f,
                shallow(42)
            ].join('|');
            """).AsString();

        result.Should().Be("600006|true|bound bound |bound target|1|function|42");
    }

    /// <summary>
    /// The premise of the row above, answered without running the chain: a bound name long enough to be
    /// worth deferring is a node over the level below rather than a copy of it. On this branch a bound
    /// function is not a <see cref="Jint.Native.Function.Function"/>, so <c>bind</c> names it through
    /// <see cref="Jint.Native.Object.ObjectInstance"/>'s overload of SetFunctionName rather than
    /// <see cref="Jint.Native.Function.Function"/>'s, and both have to defer.
    /// </summary>
    [Fact]
    public void ABoundNameLongEnoughToDeferIsANodeOverTheLevelBelow()
    {
        var engine = new Engine();
        engine.Execute("var f = function target() {}; for (var i = 0; i < 100; i++) { f = f.bind(null); }");

        var name = engine.Evaluate("Object.getOwnPropertyDescriptor(f, 'name').value");

        name.Should().BeOfType<JsString.RopeString>();
        name.AsString().Should().Be(string.Concat(Enumerable.Repeat("bound ", 100)) + "target");
    }

    [Theory]
    [InlineData("(function f() {}).bind(null).name", "bound f")]
    [InlineData("(function f() {}).bind(null).bind(null).bind(null).name", "bound bound bound f")]
    [InlineData("(function () {}).bind(null).name", "bound ")]
    [InlineData("Object.getOwnPropertyDescriptor({ get x() {} }, 'x').get.name", "get x")]
    [InlineData("Object.getOwnPropertyDescriptor({ set x(v) {} }, 'x').set.name", "set x")]
    [InlineData("Object.getOwnPropertyDescriptor({ get [Symbol.iterator]() {} }, Symbol.iterator).get.name", "get [Symbol.iterator]")]
    public void APrefixedNameIsTheSameTextItAlwaysWas(string source, string expected)
    {
        new Engine().Evaluate(source).AsString().Should().Be(expected);
    }
}
