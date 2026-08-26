#nullable enable

using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Debugger;

namespace Jint.Tests.Runtime.Debugger;

public class ExceptionThrownTests
{
    [Test]
    public void ExplicitThrowFiresEvent()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

        ExceptionThrownEventArgs? received = null;
        engine.Debugger.ExceptionThrown += (sender, args) =>
        {
            received = args;
        };

        Invoking(() => engine.Execute("throw new Error('test error');")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();

        received.Should().NotBeNull();
        received.ThrownValue.Should().BeAssignableTo<ObjectInstance>();
    }

    [Test]
    public void CaughtExceptionStillFiresEvent()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

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

        thrownValues.Should().ContainSingle();
    }

    [Test]
    public void ImplicitTypeErrorFiresEvent()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

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

        received.Should().NotBeNull();
    }

    [Test]
    public void ImplicitReferenceErrorFiresEvent()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

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

        received.Should().NotBeNull();
    }

    [Test]
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

        count.Should().Be(0);
    }

    [Test]
    public void RethrowFiresEventTwice()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

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

        count.Should().Be(2);
    }

    [Test]
    public void ThrowPrimitiveValueFiresEvent()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

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

        received.Should().NotBeNull();
        received.ThrownValue.AsNumber().Should().Be(42);
    }

    [Test]
    public void CallStackIsAvailable()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

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

        callStack.Should().NotBeNull();
        callStack.Count.Should().BeGreaterThanOrEqualTo(1, $"Expected call stack frames, got {callStack.Count}");
    }

    [Test]
    public void LocationIsAvailable()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

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

        location.Should().NotBeNull();
        location.Value.Start.Line.Should().BeGreaterThan(0);
    }

    [Test]
    public void MultipleExceptionsEachFireEvent()
    {
        var engine = new Engine(options => options.Debugger.Enabled = true);

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

        count.Should().Be(3);
    }
}
