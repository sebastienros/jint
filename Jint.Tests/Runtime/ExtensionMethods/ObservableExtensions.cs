namespace Jint.Tests.Runtime.ExtensionMethods;

internal class Subscribe<T> : IObserver<T>
{
    private readonly Action<T> onNext;
    private readonly Action<Exception> onError;
    private readonly Action onCompleted;

    readonly int isStopped = 0;

    public Subscribe(Action<T> onNext, Action<Exception> onError, Action onCompleted)
    {
        this.onNext = onNext;
        this.onError = onError;
        this.onCompleted = onCompleted;
    }

    public void OnNext(T value)
    {
        if (isStopped == 0)
        {
            onNext(value);
        }
    }

    public void OnError(Exception error)
    {
        onError(error);
    }


    public void OnCompleted()
    {
        onCompleted();
    }
}

public static partial class ObservableExtensions
{
    public static void Subscribe<T>(this IObservable<T> source, Action<T> onNext)
    {
        var subs = new Subscribe<T>(onNext, null, null);
        source.Subscribe(subs);
    }

    public static TResult Select<T, TResult>(this IObservable<T> source, TResult result)
    {
        return result;
    }

    public static IObservable<T> Where<T>(this IObservable<T> source, Func<T, bool> predicate)
    {
        T t = default;
        predicate(t);
        return source;
    }

    public static IObservable<T> Where<T>(this IObservable<T> source, Func<T, int, bool> predicate)
    {
        T t = default;
        bool result = predicate(t, 42);
        return source;
    }
}

public class BaseObservable<T> : IObservable<T>
{
    private readonly List<IObserver<T>> observers = new List<IObserver<T>>();

    public T Last { get; private set; }

    public IDisposable Subscribe(IObserver<T> observer)
    {
        if (!observers.Contains(observer))
            observers.Add(observer);
        return new Unsubscriber(observers, observer);
    }

    private class Unsubscriber : IDisposable
    {
        private readonly List<IObserver<T>> _observers;
        private readonly IObserver<T> _observer;

        public Unsubscriber(List<IObserver<T>> observers, IObserver<T> observer)
        {
            this._observers = observers;
            this._observer = observer;
        }

        public void Dispose()
        {
            if (_observer != null && _observers.Contains(_observer))
                _observers.Remove(_observer);
        }
    }

    protected void BroadcastUpdate(T t)
    {
        foreach (var observer in observers)
        {
            observer.OnNext(t);
        }
    }

    public void Update(T t)
    {
        Last = t;
        BroadcastUpdate(t);
    }

    public void BroadcastCompleted()
    {
        foreach (var observer in observers.ToArray())
        {
            observer.OnCompleted();
        }
        observers.Clear();
    }
}

public class ObservableFactory
{
    public static BaseObservable<bool> GetBoolBaseObservable()
    {
        return new BaseObservable<bool>();
    }
}

public class NameObservable : BaseObservable<string>
{
    public void UpdateName(string name)
    {
        Update(name);
    }

    public void CommitName()
    {
        BroadcastCompleted();
    }
}