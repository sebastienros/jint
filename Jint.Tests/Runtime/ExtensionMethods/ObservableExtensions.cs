using System;
using System.Collections.Generic;

namespace Jint.Tests.Runtime.ExtensionMethods
{
    internal class Subscribe<T> : IObserver<T>
    {
        private readonly Action<T> onNext;
        private readonly Action<Exception> onError;
        private readonly Action onCompleted;

        int isStopped = 0;

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
    }

    public class NameObservable : IObservable<string>
    {
        private List<IObserver<string>> observers = new List<IObserver<string>>();

        public string Last { get; private set; }

        public IDisposable Subscribe(IObserver<string> observer)
        {
            if (!observers.Contains(observer))
                observers.Add(observer);
            return new Unsubscriber(observers, observer);
        }

        private class Unsubscriber : IDisposable
        {
            private List<IObserver<string>> _observers;
            private IObserver<string> _observer;

            public Unsubscriber(List<IObserver<string>> observers, IObserver<string> observer)
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

        public void UpdateName(string name)
        {
            Last = name;
            foreach (var observer in observers)
            {
                observer.OnNext(name);
            }
        }

        public void CommitName()
        {
            foreach (var observer in observers.ToArray())
            {
                observer.OnCompleted();
            }

            observers.Clear();
        }
    }
}