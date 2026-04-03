#nullable enable

using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Debugger;

namespace Jint.Tests.Runtime.Debugger;

public class ExceptionThrownTests
{
    [Fact]
    public void ExplicitThrowFiresEvent()
    {
        var engine = new Engine(options => options.DebugMode());

        ExceptionThrownEventArgs? received = null;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            received = args;
        };

        Assert.Throws<Jint.Runtime.JavaScriptException>(() => engine.Execute("throw new Error('test error');"));

        Assert.NotNull(received);
        Assert.True(received.ThrownValue is ObjectInstance);
    }

    [Fact]
    public void CaughtExceptionStillFiresEvent()
    {
        var engine = new Engine(options => options.DebugMode());

        var thrownValues = new List<JsValue>();
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            thrownValues.Add(args.ThrownValue);
        };

        engine.Execute(@"
            try {
                throw new Error('caught error');
            } catch (e) {
                // swallowed
            }
        ");

        Assert.Single(thrownValues);
    }

    [Fact]
    public void ImplicitTypeErrorFiresEvent()
    {
        var engine = new Engine(options => options.DebugMode());

        ExceptionThrownEventArgs? received = null;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            received = args;
        };

        engine.Execute(@"
            try {
                undefined.foo;
            } catch (e) {
            }
        ");

        Assert.NotNull(received);
    }

    [Fact]
    public void ImplicitReferenceErrorFiresEvent()
    {
        var engine = new Engine(options => options.DebugMode());

        ExceptionThrownEventArgs? received = null;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            received = args;
        };

        engine.Execute(@"
            try {
                undeclaredVariable;
            } catch (e) {
            }
        ");

        Assert.NotNull(received);
    }

    [Fact]
    public void EventNotFiredWhenDebugModeDisabled()
    {
        var engine = new Engine(); // no debug mode

        var count = 0;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            count++;
        };

        engine.Execute(@"
            try {
                throw new Error('test');
            } catch (e) {
            }
        ");

        Assert.Equal(0, count);
    }

    [Fact]
    public void RethrowFiresEventTwice()
    {
        var engine = new Engine(options => options.DebugMode());

        var count = 0;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            count++;
        };

        engine.Execute(@"
            try {
                try {
                    throw new Error('original');
                } catch (e) {
                    throw e;
                }
            } catch (e) {
            }
        ");

        Assert.Equal(2, count);
    }

    [Fact]
    public void ThrowPrimitiveValueFiresEvent()
    {
        var engine = new Engine(options => options.DebugMode());

        ExceptionThrownEventArgs? received = null;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            received = args;
        };

        engine.Execute(@"
            try {
                throw 42;
            } catch (e) {
            }
        ");

        Assert.NotNull(received);
        Assert.Equal(42, received.ThrownValue.AsNumber());
    }

    [Fact]
    public void CallStackIsAvailable()
    {
        var engine = new Engine(options => options.DebugMode());

        DebugCallStack? callStack = null;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            callStack = args.CallStack;
        };

        engine.Execute(@"
            function inner() { throw new Error('deep'); }
            function outer() { inner(); }
            try {
                outer();
            } catch (e) {
            }
        ");

        Assert.NotNull(callStack);
        Assert.True(callStack.Count >= 1, $"Expected call stack frames, got {callStack.Count}");
    }

    [Fact]
    public void LocationIsAvailable()
    {
        var engine = new Engine(options => options.DebugMode());

        SourceLocation? location = null;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            location = args.Location;
        };

        engine.Execute(@"
            try {
                throw new Error('located');
            } catch (e) {
            }
        ");

        Assert.NotNull(location);
        Assert.True(location.Value.Start.Line > 0);
    }

    [Fact]
    public void MultipleExceptionsEachFireEvent()
    {
        var engine = new Engine(options => options.DebugMode());

        var count = 0;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            count++;
        };

        engine.Execute(@"
            try { throw 1; } catch (e) {}
            try { throw 2; } catch (e) {}
            try { throw 3; } catch (e) {}
        ");

        Assert.Equal(3, count);
    }
}
