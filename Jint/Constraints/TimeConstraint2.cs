using Jint.Runtime;
using System;
using System.Threading;

namespace Jint.Constraints
{
    public sealed class TimeConstraint2 : IConstraint
    {
        private readonly TimeSpan _timeout;
        private CancellationTokenSource cts;

        public TimeConstraint2(TimeSpan timeout)
        {
            _timeout = timeout;
        }

        public void Check()
        {
            if (cts.IsCancellationRequested)
            {
                ExceptionHelper.ThrowTimeoutException();
            }
        }

        public void Reset()
        {
            cts?.Dispose();
            cts = new CancellationTokenSource(_timeout);
        }
    }
}
