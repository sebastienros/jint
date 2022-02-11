using System;
using System.Threading.Tasks;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class SchedulingTests
    {
        class Context
        {
            public string Result { get; set; }
        }

        [Fact]
        public async Task CanRunAsyncCode()
        {
            var engine = new Engine();

            var ctx = new Context();

            engine.SetValue("ctx", ctx);
            engine.SetValue("setTimeout", new Action<Action, int>(async (callback, delay) =>
            {
                using (var task = engine.CreateTask())
                {
                    await Task.Delay(delay);

                    task.Invoke(callback);
                }
            }));

            var result = await engine.EvaluateAsync(@"
                setTimeout(function () {
                    ctx.Result = 'Hello World';
                }, 100);
            ");

            Assert.Equal("Hello World", ctx.Result);
        }
        [Fact]
        public async Task CanRunNestedAsyncCode()
        {
            var engine = new Engine();

            var ctx = new Context();

            engine.SetValue("ctx", ctx);
            engine.SetValue("setTimeout", new Action<Action, int>(async (callback, delay) =>
            {
                using (var task = engine.CreateTask())
                {
                    await Task.Delay(delay);

                    task.Invoke(callback);
                }
            }));

            var result = await engine.EvaluateAsync(@"
                setTimeout(function () {
                    setTimeout(function () {
                        setTimeout(function () {
                            ctx.Result = 'Hello World';
                        }, 100);
                    }, 100);
                }, 100);
            ");

            Assert.Equal("Hello World", ctx.Result);
        }

        [Fact]
        public async Task CanRunSyncCode()
        {
            var engine = new Engine();

            var ctx = new Context();

            engine.SetValue("ctx", ctx);
            engine.SetValue("setTimeout", new Action<Action, int>((callback, delay) =>
            {
                using (var task = engine.CreateTask())
                {
                    task.Invoke(callback);
                }
            }));

            var result = await engine.EvaluateAsync(@"
                setTimeout(function () {
                    ctx.Result = 'Hello World';
                }, 100);
            ");

            Assert.Equal("Hello World", ctx.Result);
        }
    }
}
