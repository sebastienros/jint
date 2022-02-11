using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jint.Scheduling
{
    internal sealed class Scheduler : IDisposable
    {
        private readonly TaskCompletionSource<object> _taskCompletionSource = new TaskCompletionSource<object>();
        private readonly HashSet<DeferredTask> _pendingTasks = new HashSet<DeferredTask>();
        private readonly Queue<Action> _inlinedTasks = new Queue<Action>();
        private bool _isRunning;
        private bool _isDisposed;
        private bool _isMainFlow = true;

        public Task Completion
        {
            get => _taskCompletionSource.Task;
        }

        public void Dispose()
        {
            _isDisposed = true;
        }

        public IDeferredTask CreateTask()
        {
            var task = new DeferredTask(this);

            _pendingTasks.Add(task);

            return task;
        }

        public void Invoke(DeferredTask task, Action action)
        {
            if (_isDisposed)
            {
                return;
            }

            _pendingTasks.Remove(task);

            if (_isRunning)
            {
                _inlinedTasks.Enqueue(action);
                return;
            }

            _isRunning = true;
            try
            {
                action();

                RunAvailableContinuations();

                TryComplete();
            }
            catch (Exception ex) when (!_isMainFlow)
            {
                _taskCompletionSource.TrySetException(ex);
            }
            finally
            {
                _isMainFlow = false;
                _isRunning = false;
            }
        }

        internal void Cancel(DeferredTask deferredTask)
        {
            _pendingTasks.Remove(deferredTask);

            TryComplete();
        }

        private void TryComplete()
        {
            if (_pendingTasks.Count == 0)
            {
                _taskCompletionSource.TrySetResult(true);
            }
        }

        private void RunAvailableContinuations()
        {
            var queue = _inlinedTasks;

            while (true)
            {
                if (queue.Count == 0)
                {
                    return;
                }

                var nextContinuation = queue.Dequeue();

                // note that continuation can enqueue new events
                nextContinuation();
            }
        }
    }
}
