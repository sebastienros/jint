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

    /// <summary>
    /// The token this constraint observes. Exposed so that blocking waits outside the
    /// per-statement check loop (e.g. the module top-level-await event-loop drain) can
    /// observe the same cancellation.
    /// </summary>
    internal CancellationToken Token => _cancellationToken;

    /// <summary>
    /// A cancellation token is external state that <see cref="Check"/> only reads and never consumes, and
    /// it never un-cancels, so checking less often bounds how late cancellation is noticed rather than
    /// changing whether it is.
    /// </summary>
    public override bool IsAmortizable => true;

    public override void Check()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            BeforeFailure?.Invoke();
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
