#nullable enable

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
    /// The whole chain allocates a few tens of megabytes once each level's name is a node over the level
    /// below; a per-level copy needs about 30 GB and trips this inside the first few thousand levels, so
    /// the quadratic is reported as a failed test rather than as an exhausted machine. Raising this by an
    /// order of magnitude would still catch it.
    /// </summary>
    private const long AllocationCeiling = 768L * 1024 * 1024;

    [Test]
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

    [TestCase("(function f() {}).bind(null).name", "bound f")]
    [TestCase("(function f() {}).bind(null).bind(null).bind(null).name", "bound bound bound f")]
    [TestCase("(function () {}).bind(null).name", "bound ")]
    [TestCase("Object.getOwnPropertyDescriptor({ get x() {} }, 'x').get.name", "get x")]
    [TestCase("Object.getOwnPropertyDescriptor({ set x(v) {} }, 'x').set.name", "set x")]
    [TestCase("Object.getOwnPropertyDescriptor({ get [Symbol.iterator]() {} }, Symbol.iterator).get.name", "get [Symbol.iterator]")]
    public void APrefixedNameIsTheSameTextItAlwaysWas(string source, string expected)
    {
        new Engine().Evaluate(source).AsString().Should().Be(expected);
    }
}
