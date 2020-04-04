using Jint.Runtime;
using System.Threading;

namespace Jint.Constraints
{
    public sealed class CancellationConstraint : IConstraint
    {
        private CancellationToken _cancellationToken;

        public CancellationConstraint(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public void Check()
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                ExceptionHelper.ThrowStatementsCountOverflowException();
            }
        }

        public void Reset(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public void Reset()
        {
        }
    }
}
