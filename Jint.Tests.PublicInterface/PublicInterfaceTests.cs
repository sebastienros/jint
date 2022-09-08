using System.Collections.Concurrent;
using Jint.Native;
using Jint.Native.Function;

namespace Jint.Tests.PublicInterface
{
    public class PublicInterfaceTests
    {

        [Fact]
        public void BindFunctionInstancesArePublic()
        {
            var engine = new Engine(options =>
            {
                options.AllowClr();
            });

            using var emulator = new SetTimeoutEmulator(engine);

            engine.SetValue("emulator", emulator);
            engine.Execute(@"
var coolingObject = {
    coolDownTime: 1000,
    cooledDown: false
}

        emulator.SetTimeout(function() {
    coolingObject.cooledDown = true;
    }.bind(coolingObject), coolingObject.coolDownTime);

");

        }

        private class SetTimeoutEmulator : IDisposable
        {
            private readonly Engine _engine;
            private readonly ConcurrentQueue<JsValue> _queue = new();
            private readonly Task _queueProcessor;
            private readonly CancellationTokenSource _quit = new();
            private bool _disposedValue;

            public SetTimeoutEmulator(Engine engine)
            {
                _engine = engine ?? throw new ArgumentNullException(nameof(engine));

                _queueProcessor = Task.Run(() =>
                {

                    while (!_quit.IsCancellationRequested)
                    {
                        while (_queue.TryDequeue(out var queueEntry))
                        {
                            if (queueEntry is FunctionInstance fi)
                            {
                                _engine.Invoke(fi);
                            }
                            else if (queueEntry is BindFunctionInstance bfi)
                            {
                                _engine.Invoke(bfi);
                            }
                            else
                            {
                                _engine.Execute(queueEntry.ToString());
                            }
                        }
                    }
                });
            }

            public void SetTimeout(JsValue script, object timeout)
            {
                _queue.Enqueue(script);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!_disposedValue)
                {
                    if (disposing)
                    {
                        // TODO: dispose managed state (managed objects)
                        _quit.Cancel();
                        _queueProcessor.Wait();
                    }

                    _disposedValue = true;
                }
            }

            public void Dispose()
            {
                // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }
    }
}
