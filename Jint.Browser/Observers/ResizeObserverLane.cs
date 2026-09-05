using Jint.Browser.Runtime;

namespace Jint.Browser.Observers;

/// <summary>Observes flat-box changes at page-turn boundaries and delivers them in a later rendering task.</summary>
/// <remarks>
/// The active list keeps observers alive only while they observe targets, as
/// https://drafts.csswg.org/resize-observer/#lifetime requires. No mutation observer is installed: CSSOM
/// writes and viewport changes need no DOM mutation, so a mutation-only signal would miss size changes.
/// Unlike a layout engine's depth-limited rendering loop, callback-caused changes wait for the next task.
/// </remarks>
internal sealed class ResizeObserverLane(PageRuntime runtime)
{
    private readonly List<JsResizeObserver> _observers = [];
    private bool _scheduled;

    internal void Enlist(JsResizeObserver observer)
    {
        if (!_observers.Contains(observer))
        {
            _observers.Add(observer);
        }

        Schedule();
    }

    internal void Withdraw(JsResizeObserver observer) => _observers.Remove(observer);

    internal void CheckForChanges()
    {
        if (_scheduled || _observers.Count == 0)
        {
            return;
        }

        var layout = runtime.Layout.Current();
        foreach (var observer in _observers)
        {
            if (observer.HasChanges(layout))
            {
                Schedule();
                return;
            }
        }
    }

    private void Schedule()
    {
        if (_scheduled)
        {
            return;
        }

        ObserverTask.Post(runtime, Deliver);
        _scheduled = true;
    }

    private void Deliver()
    {
        _scheduled = false;
        if (_observers.Count == 0)
        {
            return;
        }

        var batch = _observers.ToArray();
        var layout = runtime.Layout.Current();
        foreach (var observer in batch)
        {
            observer.Deliver(layout);
        }
    }
}
