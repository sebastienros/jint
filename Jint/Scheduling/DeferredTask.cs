using System;

namespace Jint.Scheduling
{
    internal class DeferredTask : IDeferredTask
    {
        private readonly Scheduler _scheduler;
        private bool _isCompleted;

        public DeferredTask(Scheduler scheduler)
        {
            _scheduler = scheduler;
        }

        public void Dispose()
        {
            Cancel();
        }

        public void Cancel()
        {
            if (_isCompleted)
            {
                return;
            }

            _isCompleted = true;

            _scheduler.Cancel(this);
        }

        public void Invoke(Action action)
        {
            if (_isCompleted)
            {
                return;
            }

            _isCompleted = true;

            _scheduler.Invoke(this, action);
        }
    }
}
