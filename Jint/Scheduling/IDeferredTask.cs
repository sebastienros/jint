using System;

namespace Jint.Scheduling
{
    public interface IDeferredTask : IDisposable
    {
        void Invoke(Action action);

        void Cancel();
    }
}
