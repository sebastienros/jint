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

    public override void Check()
    {
        if (MaxStatements > 0 && ++_statementsCount > MaxStatements)
        {
            ExceptionHelper.ThrowStatementsCountOverflowException();
        }
    }

    public override void Reset()
    {
        _statementsCount = 0;
    }
}
