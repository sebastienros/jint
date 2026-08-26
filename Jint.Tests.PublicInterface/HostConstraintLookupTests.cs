#nullable enable

using Jint.Constraints;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="Engine.ConstraintOperations.Find{T}"/> — how a host gets back to a constraint it registered.
///
/// <para>
/// Through 4.16 the match was <c>constraint.GetType() == typeof(T)</c>, an exact type identity that nothing
/// documented. Two consequences a host could not work around: asking for a base type never matched, so
/// <c>Find&lt;Constraint&gt;()</c> answered <see langword="null"/> on an engine with constraints registered,
/// and a host that put its own hierarchy behind a base class had to name each leaf. The match is
/// <c>is T</c> in v5.
/// </para>
/// </summary>
public class HostConstraintLookupTests
{
    private abstract class HostBudget : Constraint
    {
        public int Checks { get; private set; }

        public override void Check() => Checks++;

        public override void Reset()
        {
        }
    }

    private sealed class HostStatementBudget : HostBudget
    {
    }

    [Test]
    public void AConstraintIsFoundByItsOwnType()
    {
        var engine = new Engine(options => options.LimitStatements(1000));

        engine.Constraints.Find<MaxStatementsConstraint>().Should().NotBeNull();
    }

    [Test]
    public void AHostConstraintIsFoundThroughItsBaseType()
    {
        var registered = new HostStatementBudget();
        var engine = new Engine(options => options.AddConstraint(registered));

        engine.Constraints.Find<HostStatementBudget>().Should().BeSameAs(registered);
        engine.Constraints.Find<HostBudget>().Should().BeSameAs(registered);
    }

    [Test]
    public void TheBaseConstraintTypeMatchesAnythingRegistered()
    {
        var engine = new Engine(options => options.LimitStatements(1000));

        engine.Constraints.Find<Constraint>().Should().NotBeNull();
    }

    [Test]
    public void NothingIsFoundOnAnEngineThatRegisteredNone()
    {
        var engine = new Engine();

        engine.Constraints.Find<Constraint>().Should().BeNull();
        engine.Constraints.Find<MaxStatementsConstraint>().Should().BeNull();
    }
}
