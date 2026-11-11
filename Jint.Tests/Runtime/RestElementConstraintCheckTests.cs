#nullable enable

using Jint.Constraints;

namespace Jint.Tests.Runtime;

/// <summary>
/// A rest element collects every value an iterator has left in one CLR loop. Over a built-in iterator - a typed array's,
/// a string's - the steps cross no statement boundary, so nothing in the per-statement checks runs while it does, and a
/// host's memory limit, timeout or cancellation would be inert for its duration. Each rest loop checks the constraints
/// every <see cref="Engine.ConstraintCheckInterval"/> steps, as <c>IteratorProtocol.Execute</c> does
/// (sebastienros/jint#2487): a parameter's pattern, a destructuring assignment or binding, and the resumable lane a
/// pattern that may suspend takes.
/// </summary>
public class RestElementConstraintCheckTests
{
    private const int Length = 100_000;

    [TestCase("(function ([...r]) { })(source);")]
    [TestCase("(function ([a, , ...r]) { })(source);")]
    [TestCase("var r; [...r] = source;")]
    [TestCase("var [a, ...r] = source;")]
    [TestCase("(async function () { const [a = await 0, ...r] = source; })();")]
    public void ARestElementOverABuiltInIteratorChecksTheConstraintsAsItCollects(string script)
    {
        var constraint = new CountingConstraint();
        var engine = new Engine(options => options.AddConstraint(constraint));
        engine.Execute($"var small = new Uint8Array(1); var big = new Uint8Array({Length});");

        constraint.Count = 0;
        engine.Execute("var source = small; " + script);
        var baseline = constraint.Count;

        constraint.Count = 0;
        engine.Execute("var source = big; " + script);

        // The same statements ran either way; what the larger source adds is the rest loop's own checks.
        (constraint.Count - baseline).Should().BeGreaterThanOrEqualTo(Length / Engine.ConstraintCheckInterval - 1);
    }

    /// <summary>
    /// A pattern over an array reads the array by index, as its iterator would, instead of stepping an iterator object:
    /// a parameter's (#4559), a declaration's and an assignment's. An array whose elements cannot be copied in one pass -
    /// one with holes, a frozen one - is collected a step at a time, and that loop checks the constraints too.
    /// </summary>
    [TestCase("new Array({0})", "(function ([...r]) { })(source);")]
    [TestCase("new Array({0})", "(function ([a, , ...r]) { })(source);")]
    [TestCase("Object.freeze(Array.from({{ length: {0} }}))", "(function ([...r]) { })(source);")]
    [TestCase("Object.freeze(Array.from({{ length: {0} }}))", "(function ([a, , ...r]) { })(source);")]
    [TestCase("new Array({0})", "var [...r] = source;")]
    [TestCase("new Array({0})", "var a, r; [a, , ...r] = source;")]
    [TestCase("Object.freeze(Array.from({{ length: {0} }}))", "const [...r] = source;")]
    [TestCase("Object.freeze(Array.from({{ length: {0} }}))", "var r; for ([...r] of [source]) { }")]
    public void ARestOverAnArrayCollectedStepByStepChecksTheConstraints(string source, string script)
    {
        var constraint = new CountingConstraint();
        var engine = new Engine(options => options.AddConstraint(constraint));
        engine.Execute($"var small = {string.Format(source, 1)}; var big = {string.Format(source, Length)};");

        constraint.Count = 0;
        engine.Execute("var source = small; " + script);
        var baseline = constraint.Count;

        constraint.Count = 0;
        engine.Execute("var source = big; " + script);

        (constraint.Count - baseline).Should().BeGreaterThanOrEqualTo(Length / Engine.ConstraintCheckInterval - 1);
    }

    private sealed class CountingConstraint : Constraint
    {
        public int Count;

        public override void Check() => Count++;

        public override void Reset()
        {
        }
    }
}
