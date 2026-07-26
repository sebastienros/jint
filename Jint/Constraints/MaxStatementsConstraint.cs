using Jint.Runtime;

namespace Jint.Constraints;

public sealed class MaxStatementsConstraint : Constraint
{
    private int _statementsCount;

    internal MaxStatementsConstraint(int maxStatements)
    {
        MaxStatements = maxStatements;
    }

    /// <summary>
    /// The maximum configured amount of statements to allow during engine evaluation.
    /// </summary>
    public int MaxStatements { get; set; }

    /// <summary>
    /// Never amortizable: this constraint counts its own invocations, so the call frequency <em>is</em>
    /// what it measures. Skipping calls would not merely delay detection, it would change the count.
    /// </summary>
    public override bool IsAmortizable => false;

    public override void Check()
    {
        if (MaxStatements > 0 && ++_statementsCount > MaxStatements)
        {
            Throw.StatementsCountOverflowException();
        }
    }

    public override void Reset()
    {
        _statementsCount = 0;
    }
}
