using Jint.Native;
using Jint.Native.Function;

namespace Jint.Tests.PublicInterface;

public class PublicInterfaceTests
{
    [Fact]
    public void CanCallEval()
    {
        var engine = new Engine();
        var value = engine.Intrinsics.Eval.Call("1 + 1");
        Assert.Equal(2, value);
    }

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

        // Process queued callbacks after Execute() completes to avoid
        // concurrent access to the Engine (which is not thread-safe).
        emulator.ProcessQueue();
    }

    [Fact]
    public void JsArgumentsIsPublic()
    {
        // debuggers might want to access the information
        var obj = new Engine().Execute("function f() { return arguments; }").Evaluate("f('a', 'b', 'c');");
        var arguments = Assert.IsType<JsArguments>(obj);
        Assert.Equal((uint) 3, arguments.Length);
    }

    [Fact]
    public void IsCallableReturnsTrueForFunctions()
    {
        var engine = new Engine();
        var func = engine.Evaluate("(function() {})");
        Assert.True(func.IsCallable());

        var arrow = engine.Evaluate("(() => {})");
        Assert.True(arrow.IsCallable());
    }

    [Fact]
    public void IsCallableReturnsFalseForNonFunctions()
    {
        var engine = new Engine();
        Assert.False(JsValue.Undefined.IsCallable());
        Assert.False(JsValue.Null.IsCallable());
        Assert.False(JsNumber.Create(42).IsCallable());
        Assert.False(new JsString("hello").IsCallable());
        Assert.False(engine.Evaluate("({})").IsCallable());
    }

    [Fact]
    public void IsConstructorReturnsTrueForConstructors()
    {
        var engine = new Engine();
        var ctor = engine.Evaluate("(class Foo {})");
        Assert.True(ctor.IsConstructor());

        var func = engine.Evaluate("(function Bar() {})");
        Assert.True(func.IsConstructor());
    }

    [Fact]
    public void IsConstructorReturnsFalseForNonConstructors()
    {
        var engine = new Engine();
        Assert.False(JsValue.Undefined.IsConstructor());
        Assert.False(JsValue.Null.IsConstructor());
        Assert.False(JsNumber.Create(42).IsConstructor());

        var arrow = engine.Evaluate("(() => {})");
        Assert.False(arrow.IsConstructor());
    }

    private sealed class SetTimeoutEmulator : IDisposable
    {
        private readonly Engine _engine;
        private readonly Queue<JsValue> _queue = new();

        public SetTimeoutEmulator(Engine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public void SetTimeout(JsValue script, object timeout)
        {
            _queue.Enqueue(script);
        }

        /// <summary>
        /// Process all queued callbacks. Must be called when the engine is not in use
        /// (Engine is not thread-safe).
        /// </summary>
        public void ProcessQueue()
        {
            while (_queue.Count > 0)
            {
                var queueEntry = _queue.Dequeue();
                if (queueEntry is Function fi)
                {
                    _engine.Invoke(fi);
                }
                else if (queueEntry is BindFunction bfi)
                {
                    _engine.Invoke(bfi);
                }
                else
                {
                    _engine.Execute(queueEntry.ToString());
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
