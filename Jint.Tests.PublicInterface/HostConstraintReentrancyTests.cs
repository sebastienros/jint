#nullable enable

using Jint.Constraints;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A host-supplied <see cref="Constraint"/> is host code, and the engine calls it from inside built-ins that
/// are still part-way through their own work. A constraint that runs script — on a second engine, or on the
/// same one — therefore re-enters the engine at points no script could reach on its own, and anything the
/// interrupted built-in was holding across that call has to survive it. These pin the cases that did not.
/// </summary>
public class HostConstraintReentrancyTests
{
    /// <summary>
    /// Runs a script of its own the first few times it is asked, then stands down so the outer script can
    /// finish. Counted rather than unconditional because the nested evaluation checks constraints too.
    /// </summary>
    private sealed class ReenteringConstraint : Constraint
    {
        private readonly Engine _other = new();
        private int _fired;

        public int Fired => _fired;

        public override void Check()
        {
            if (_fired++ >= 4)
            {
                return;
            }

            _other.Evaluate("'x,y,z'.split(',').length");
        }

        public override void Reset()
        {
        }
    }

    [Test]
    public void SplitKeepsItsSegmentsAcrossAConstraintThatSplitsOnAnotherEngine()
    {
        // The split fast path checks constraints every 10,000 segments, so the source has to be long enough
        // to reach one; the second engine's split then runs on the same thread, inside the first one's loop.
        var constraint = new ReenteringConstraint();
        var engine = new Engine(options => options.AddConstraint(constraint));

        engine.Evaluate("'a,'.repeat(30000).split(',').length").AsNumber().Should().Be(30001);
        constraint.Fired.Should().BeGreaterThan(0, "the constraint has to have re-entered for this to prove anything");
    }

    [Test]
    public void SplitIsUnaffectedWhenNoConstraintReenters()
    {
        var engine = new Engine();

        engine.Evaluate("'a,'.repeat(30000).split(',').length").AsNumber().Should().Be(30001);
        engine.Evaluate("'a,b,c'.split(',').join('|')").AsString().Should().Be("a|b|c");
    }
}
