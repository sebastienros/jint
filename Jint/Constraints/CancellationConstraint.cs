using Jint.Runtime;
using System.Threading;

namespace Jint.Constraints;

public sealed class CancellationConstraint : Constraint
{
    private CancellationToken _cancellationToken;

    internal CancellationConstraint(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
    }

    public override void Check()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            Throw.ExecutionCanceledException();
        }
    }

    public void Reset(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
    }

    public override void Reset()
    {
    }
}
