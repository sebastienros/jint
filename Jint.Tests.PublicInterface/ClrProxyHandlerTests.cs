#nullable enable

using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

public class ClrProxyHandlerTests
{
    private sealed class DelegatingProxyHandler : ProxyHandler
    {
        public Func<ObjectInstance, JsValue, JsValue, JsValue?>? OnGet;
        public Func<ObjectInstance, JsValue, JsValue, JsValue, bool?>? OnSet;
        public Func<ObjectInstance, JsValue, bool?>? OnHas;
        public Func<ObjectInstance, JsValue, bool?>? OnDeleteProperty;
        public Func<ObjectInstance, JsValue, PropertyDescriptor?>? OnGetOwnPropertyDescriptor;
        public Func<ObjectInstance, JsValue, PropertyDescriptor, bool?>? OnDefineProperty;
        public Func<ObjectInstance, IReadOnlyList<JsValue>?>? OnOwnKeys;
        public Func<ObjectInstance, JsValue, JsValue[], JsValue?>? OnApply;
        public Func<ObjectInstance, JsValue[], JsValue, ObjectInstance?>? OnConstruct;
        public Func<ObjectInstance, JsValue?>? OnGetPrototypeOf;
        public Func<ObjectInstance, JsValue, bool?>? OnSetPrototypeOf;
        public Func<ObjectInstance, bool?>? OnIsExtensible;
        public Func<ObjectInstance, bool?>? OnPreventExtensions;

        public override JsValue? Get(ObjectInstance target, JsValue property, JsValue receiver) => OnGet?.Invoke(target, property, receiver);
        public override bool? Set(ObjectInstance target, JsValue property, JsValue value, JsValue receiver) => OnSet?.Invoke(target, property, value, receiver);
        public override bool? Has(ObjectInstance target, JsValue property) => OnHas?.Invoke(target, property);
        public override bool? DeleteProperty(ObjectInstance target, JsValue property) => OnDeleteProperty?.Invoke(target, property);
        public override PropertyDescriptor? GetOwnPropertyDescriptor(ObjectInstance target, JsValue property) => OnGetOwnPropertyDescriptor?.Invoke(target, property);
        public override bool? DefineProperty(ObjectInstance target, JsValue property, PropertyDescriptor descriptor) => OnDefineProperty?.Invoke(target, property, descriptor);
        public override IReadOnlyList<JsValue>? OwnKeys(ObjectInstance target) => OnOwnKeys?.Invoke(target);
        public override JsValue? Apply(ObjectInstance target, JsValue thisObject, JsValue[] arguments) => OnApply?.Invoke(target, thisObject, arguments);
        public override ObjectInstance? Construct(ObjectInstance target, JsValue[] arguments, JsValue newTarget) => OnConstruct?.Invoke(target, arguments, newTarget);
        public override JsValue? GetPrototypeOf(ObjectInstance target) => OnGetPrototypeOf?.Invoke(target);
        public override bool? SetPrototypeOf(ObjectInstance target, JsValue prototype) => OnSetPrototypeOf?.Invoke(target, prototype);
        public override bool? IsExtensible(ObjectInstance target) => OnIsExtensible?.Invoke(target);
        public override bool? PreventExtensions(ObjectInstance target) => OnPreventExtensions?.Invoke(target);
    }

    public class Calculator
    {
        public int Offset { get; set; }

        public int Add(int a, int b) => Offset + a + b;
    }

    public class Person
    {
        public string Name { get; set; } = "";
    }

    private static JsObject CreateTarget(Engine engine, params (string Key, JsValue Value)[] properties)
    {
        var target = new JsObject(engine);
        foreach (var (key, value) in properties)
        {
            target.Set(key, value);
        }
        return target;
    }

    [Test]
    public void GetTrapInterceptsPropertyRead()
    {
        var engine = new Engine();
        var target = CreateTarget(engine, ("x", 1), ("y", 2));
        var handler = new DelegatingProxyHandler
        {
            OnGet = (_, property, _) => property.ToString() == "x" ? JsNumber.Create(42) : null
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));

        engine.Evaluate("p.x").AsNumber().Should().Be(42);
        // non-trapped key forwards to the target
        engine.Evaluate("p.y").AsNumber().Should().Be(2);
    }

    [Test]
    public void SetTrapInterceptsPropertyWrite()
    {
        var engine = new Engine();
        var target = CreateTarget(engine);
        var writes = new List<string>();
        var handler = new DelegatingProxyHandler
        {
            OnSet = (_, property, value, _) =>
            {
                writes.Add($"{property}={value}");
                return true;
            }
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));
        engine.Evaluate("p.x = 42;");

        writes.Should().Equal(new[] { "x=42" });
        // the trap claimed the write, the target was not touched
        target.Get("x").IsUndefined().Should().BeTrue();
    }

    [Test]
    public void SetTrapReturningFalseThrowsInStrictMode()
    {
        var engine = new Engine();
        var handler = new DelegatingProxyHandler
        {
            OnSet = (_, _, _, _) => false
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(CreateTarget(engine), handler));

        Invoking(() => engine.Evaluate("'use strict'; p.x = 1;")).Should().ThrowExactly<JavaScriptException>();
    }

    [Test]
    public void HasTrapInterceptsInOperator()
    {
        var engine = new Engine();
        var target = CreateTarget(engine, ("real", 1));
        var handler = new DelegatingProxyHandler
        {
            OnHas = (_, property) => property.ToString() == "phantom" ? true : null
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));

        engine.Evaluate("'phantom' in p").AsBoolean().Should().BeTrue();
        // forwarded to the target
        engine.Evaluate("'real' in p").AsBoolean().Should().BeTrue();
        engine.Evaluate("'missing' in p").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void DeletePropertyTrapInterceptsDelete()
    {
        var engine = new Engine();
        var target = CreateTarget(engine, ("x", 1));
        var deleted = new List<string>();
        var handler = new DelegatingProxyHandler
        {
            OnDeleteProperty = (_, property) =>
            {
                deleted.Add(property.ToString());
                return true;
            }
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));

        engine.Evaluate("delete p.x").AsBoolean().Should().BeTrue();
        deleted.Should().Equal(new[] { "x" });
        // the trap claimed the delete without touching the target
        target.Get("x").AsNumber().Should().Be(1);
    }

    [Test]
    public void GetOwnPropertyDescriptorTrapReportsPhantomAndHiddenProperties()
    {
        var engine = new Engine();
        var target = CreateTarget(engine, ("hidden", 1), ("visible", 2));
        var handler = new DelegatingProxyHandler
        {
            OnGetOwnPropertyDescriptor = (_, property) => property.ToString() switch
            {
                "phantom" => new PropertyDescriptor(JsNumber.Create(7), writable: true, enumerable: true, configurable: true),
                "hidden" => PropertyDescriptor.Undefined, // report "no such property"
                _ => null // forward
            }
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));

        engine.Evaluate("Object.getOwnPropertyDescriptor(p, 'phantom').value").AsNumber().Should().Be(7);
        engine.Evaluate("Object.getOwnPropertyDescriptor(p, 'hidden') === undefined").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getOwnPropertyDescriptor(p, 'visible').value").AsNumber().Should().Be(2);
    }

    [Test]
    public void DefinePropertyTrapInterceptsDefinition()
    {
        var engine = new Engine();
        var target = CreateTarget(engine);
        var defined = new List<string>();
        var handler = new DelegatingProxyHandler
        {
            OnDefineProperty = (_, property, descriptor) =>
            {
                defined.Add($"{property}={descriptor.Value}");
                return true;
            }
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));
        engine.Evaluate("Object.defineProperty(p, 'y', { value: 42, configurable: true });");

        defined.Should().Equal(new[] { "y=42" });
        target.Get("y").IsUndefined().Should().BeTrue();
    }

    [Test]
    public void OwnKeysTrapFiltersKeys()
    {
        var engine = new Engine();
        var target = CreateTarget(engine, ("a", 1), ("b", 2), ("c", 3));
        var handler = new DelegatingProxyHandler
        {
            OnOwnKeys = _ => new JsValue[] { "a", "b" }
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));

        engine.Evaluate("Object.getOwnPropertyNames(p).join()").AsString().Should().Be("a,b");
    }

    [Test]
    public void ApplyTrapInterceptsCall()
    {
        var engine = new Engine();
        var target = engine.Evaluate("(function (a, b) { return a + b; })").AsObject();
        JsValue[]? capturedArguments = null;
        var handler = new DelegatingProxyHandler
        {
            OnApply = (_, _, arguments) =>
            {
                // the trap receives a private copy it may hold on to
                capturedArguments = arguments;
                return JsNumber.Create(100);
            }
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));

        engine.Evaluate("p(2, 3)").AsNumber().Should().Be(100);
        capturedArguments.Should().NotBeNull();
        capturedArguments.Length.Should().Be(2);
        capturedArguments[0].AsNumber().Should().Be(2);
        capturedArguments[1].AsNumber().Should().Be(3);
    }

    [Test]
    public void ApplyTrapForwardsWhenNotImplemented()
    {
        var engine = new Engine();
        var target = engine.Evaluate("(function (a, b) { return a + b; })").AsObject();
        var handler = new DelegatingProxyHandler();

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));

        engine.Evaluate("p(2, 3)").AsNumber().Should().Be(5);
    }

    [Test]
    public void ConstructTrapInterceptsNew()
    {
        var engine = new Engine();
        var target = engine.Evaluate("(function (a, b) { this.sum = a + b; })").AsObject();
        var handler = new DelegatingProxyHandler
        {
            OnConstruct = (_, arguments, _) =>
            {
                var result = new JsObject(engine);
                result.Set("marker", JsNumber.Create(arguments[0].AsNumber() * arguments[1].AsNumber()));
                return result;
            }
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));

        engine.Evaluate("new p(2, 3).marker").AsNumber().Should().Be(6);
        // trap not consulted for the forwarding check
        var forwardingHandler = new DelegatingProxyHandler();
        engine.SetValue("pf", engine.Advanced.CreateProxy(target, forwardingHandler));
        engine.Evaluate("new pf(2, 3).sum").AsNumber().Should().Be(5);
    }

    [Test]
    public void GetPrototypeOfTrapCanReportNullPrototype()
    {
        var engine = new Engine();
        var handler = new DelegatingProxyHandler
        {
            OnGetPrototypeOf = _ => JsValue.Null
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(CreateTarget(engine), handler));

        engine.Evaluate("Object.getPrototypeOf(p) === null").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void SetPrototypeOfTrapInterceptsPrototypeChange()
    {
        var engine = new Engine();
        var target = CreateTarget(engine);
        var invocations = 0;
        var handler = new DelegatingProxyHandler
        {
            OnSetPrototypeOf = (_, _) =>
            {
                invocations++;
                return true;
            }
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));
        engine.Evaluate("Object.setPrototypeOf(p, { a: 1 });");

        invocations.Should().Be(1);
        // the trap claimed success without changing the target's prototype
        engine.Evaluate("Object.getPrototypeOf(p) === Object.prototype").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void IsExtensibleTrapIsConsulted()
    {
        var engine = new Engine();
        var invocations = 0;
        var handler = new DelegatingProxyHandler
        {
            OnIsExtensible = _ =>
            {
                invocations++;
                return true;
            }
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(CreateTarget(engine), handler));

        engine.Evaluate("Object.isExtensible(p)").AsBoolean().Should().BeTrue();
        invocations.Should().Be(1);
    }

    [Test]
    public void PreventExtensionsTrapIsConsulted()
    {
        var engine = new Engine();
        var target = CreateTarget(engine);
        var handler = new DelegatingProxyHandler
        {
            OnPreventExtensions = t =>
            {
                // honor the invariant: the target must actually become non-extensible
                t.PreventExtensions();
                return true;
            }
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));
        engine.Evaluate("Object.preventExtensions(p);");

        engine.Evaluate("Object.isExtensible(p)").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void GetAndSetTrapsWorkAgainstObjectWrapperTarget()
    {
        var engine = new Engine(options => options.Interop.AllowWrite = true);
        var person = new Person { Name = "Jane" };
        var blockedWrites = new List<string>();
        var handler = new DelegatingProxyHandler
        {
            OnGet = (_, property, _) => property.ToString() == "custom" ? JsNumber.Create(42) : null,
            OnSet = (_, property, value, _) =>
            {
                if (property.ToString() == "blocked")
                {
                    blockedWrites.Add(value.ToString());
                    return true;
                }
                return null;
            }
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(ObjectWrapper.Create(engine, person), handler));

        engine.Evaluate("p.custom").AsNumber().Should().Be(42);
        engine.Evaluate("p.Name").AsString().Should().Be("Jane");

        engine.Evaluate("p.blocked = 'nope';");
        blockedWrites.Should().Equal(new[] { "nope" });

        // untrapped write forwards to the CLR object
        engine.Evaluate("p.Name = 'John';");
        person.Name.Should().Be("John");
    }

    [Test]
    public void ApplyTrapWorksAgainstClrFunctionTarget()
    {
        var engine = new Engine();
        var target = new ClrFunction(engine, "add", (_, arguments) => JsNumber.Create(arguments[0].AsNumber() + arguments[1].AsNumber()));
        var handler = new DelegatingProxyHandler
        {
            OnApply = (_, _, arguments) => JsNumber.Create(arguments[0].AsNumber() * arguments[1].AsNumber())
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));

        engine.Evaluate("p(2, 3)").AsNumber().Should().Be(6);
    }

    [Test]
    public void HandlerWithOnlyGetLetsWritesReachTarget()
    {
        var engine = new Engine();
        var target = CreateTarget(engine);
        var handler = new DelegatingProxyHandler
        {
            OnGet = (_, _, _) => null
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));
        engine.Evaluate("p.x = 1;");

        target.Get("x").AsNumber().Should().Be(1);
    }

    [Test]
    public void TrapReturningUndefinedIsARealResultNotForward()
    {
        var engine = new Engine();
        var target = CreateTarget(engine, ("x", 5));
        var handler = new DelegatingProxyHandler
        {
            OnGet = (_, property, _) => property.ToString() == "x" ? JsValue.Undefined : null
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));

        engine.Evaluate("p.x === undefined").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ConditionalForwardingLetsTargetValuesThrough()
    {
        var engine = new Engine();
        var target = CreateTarget(engine, ("normal", 1), ("special", 2));
        var handler = new DelegatingProxyHandler
        {
            OnGet = (_, property, _) => property.ToString() == "special" ? JsNumber.Create(99) : null
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));

        engine.Evaluate("p.normal").AsNumber().Should().Be(1);
        engine.Evaluate("p.special").AsNumber().Should().Be(99);
    }

    [Test]
    public void GetTrapLyingAboutFrozenPropertyThrowsTypeError()
    {
        var engine = new Engine();
        engine.Evaluate("var frozen = Object.freeze({ x: 1 });");
        var target = engine.Evaluate("frozen").AsObject();
        var handler = new DelegatingProxyHandler
        {
            OnGet = (_, _, _) => JsNumber.Create(42)
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));

        var ex = Invoking(() => engine.Evaluate("p.x")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Contain("'get' on proxy");
    }

    [Test]
    public void SetTrapLyingAboutFrozenPropertyThrowsTypeError()
    {
        var engine = new Engine();
        engine.Evaluate("var frozen = Object.freeze({ x: 1 });");
        var target = engine.Evaluate("frozen").AsObject();
        var handler = new DelegatingProxyHandler
        {
            OnSet = (_, _, _, _) => true
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));

        var ex = Invoking(() => engine.Evaluate("p.x = 2;")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Contain("'set' on proxy");
    }

    [Test]
    public void OwnKeysTrapOmittingNonConfigurableKeyThrowsTypeError()
    {
        var engine = new Engine();
        engine.Evaluate("var frozen = Object.freeze({ a: 1 });");
        var target = engine.Evaluate("frozen").AsObject();
        var handler = new DelegatingProxyHandler
        {
            OnOwnKeys = _ => []
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(target, handler));

        var ex = Invoking(() => engine.Evaluate("Object.keys(p)")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Contain("'ownKeys' on proxy");
    }

    private sealed class MethodLoggingHandler : ProxyHandler
    {
        private readonly Dictionary<string, JsValue> _wrappers = new(StringComparer.Ordinal);

        public List<string> Calls { get; } = [];

        public override JsValue? Get(ObjectInstance target, JsValue property, JsValue receiver)
        {
            var value = target.Get(property);
            if (value is Function)
            {
                var name = property.ToString();
                if (!_wrappers.TryGetValue(name, out var wrapper))
                {
                    // proxy.method(x) fires the get trap and then a plain call on the result;
                    // to intercept the call, return a wrapping function - memoized so that
                    // proxy.method === proxy.method still holds
                    var engine = target.Engine;
                    wrapper = new ClrFunction(engine, name, (thisObject, arguments) =>
                    {
                        Calls.Add(name);
                        return engine.Invoke(value, thisObject, arguments);
                    });
                    _wrappers[name] = wrapper;
                }

                return wrapper;
            }

            return null; // forward plain values to the target
        }
    }

    [Test]
    public void MethodCallsCanBeInterceptedByWrappingFunctionsInGetTrap()
    {
        var engine = new Engine();
        var calculator = new Calculator { Offset = 10 };
        var handler = new MethodLoggingHandler();

        engine.SetValue("p", engine.Advanced.CreateProxy(ObjectWrapper.Create(engine, calculator), handler));

        // thisObject passthrough: the bound CLR method sees the original Calculator instance
        engine.Evaluate("p.Add(2, 3)").AsNumber().Should().Be(15);
        handler.Calls.Should().Equal(new[] { "Add" });

        // memoization preserves function identity
        engine.Evaluate("p.Add === p.Add").AsBoolean().Should().BeTrue();
        handler.Calls.Should().Equal(new[] { "Add" });

        // plain (non-function) members still forward
        engine.Evaluate("p.Offset").AsNumber().Should().Be(10);
    }

    [Test]
    public void RevocableProxyThrowsAfterRevoke()
    {
        var engine = new Engine();
        var target = CreateTarget(engine, ("x", 1));
        var revocable = engine.Advanced.CreateRevocableProxy(target, new DelegatingProxyHandler());

        engine.SetValue("p", revocable.Proxy);
        engine.Evaluate("p.x").AsNumber().Should().Be(1);

        revocable.Revoke();

        var ex = Invoking(() => engine.Evaluate("p.x")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Contain("revoked");

        // idempotent
        revocable.Revoke();

        ex = Invoking(() => engine.Evaluate("'x' in p")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Contain("revoked");
    }

    [Test]
    public void RevokedCallableProxyKeepsTypeofFunction()
    {
        var engine = new Engine();
        var target = engine.Evaluate("(function () {})").AsObject();
        var revocable = engine.Advanced.CreateRevocableProxy(target, new DelegatingProxyHandler());

        engine.SetValue("p", revocable.Proxy);
        revocable.Revoke();

        engine.Evaluate("typeof p").AsString().Should().Be("function");
    }

    [Test]
    public void CanComposeWithWrapObjectHandler()
    {
        var handler = new DelegatingProxyHandler
        {
            OnGet = (_, property, _) => property.ToString() == "intercepted" ? JsNumber.Create(1) : null
        };

        var engine = new Engine(options =>
        {
            options.Interop.WrapObjectHandler = (e, obj, type) => obj is Calculator
                ? e.Advanced.CreateProxy(ObjectWrapper.Create(e, obj), handler)
                : ObjectWrapper.Create(e, obj, type);
        });

        var calculator = new Calculator();
        Calculator? received = null;
        engine.SetValue("calc", calculator);
        engine.SetValue("accept", new Action<Calculator>(c => received = c));

        // script sees trapped members
        engine.Evaluate("calc.intercepted").AsNumber().Should().Be(1);
        // untrapped members forward to the wrapped CLR object
        engine.Evaluate("calc.Add(2, 3)").AsNumber().Should().Be(5);

        // passing the proxied wrapper back into a CLR method parameter unwraps to the original object
        engine.Evaluate("accept(calc);");
        received.Should().BeSameAs(calculator);
    }

    [Test]
    public void ReflectGetPassesReceiverToTrap()
    {
        var engine = new Engine();
        JsValue? capturedReceiver = null;
        var proxy = engine.Advanced.CreateProxy(CreateTarget(engine), new DelegatingProxyHandler
        {
            OnGet = (_, _, receiver) =>
            {
                capturedReceiver = receiver;
                return JsNumber.Create(1);
            }
        });

        engine.SetValue("p", proxy);

        engine.Evaluate("var r = { marker: true }; Reflect.get(p, 'x', r);");
        capturedReceiver.Should().BeSameAs(engine.Evaluate("r"));

        // default receiver is the proxy itself
        engine.Evaluate("p.x");
        capturedReceiver.Should().BeSameAs(proxy);
    }

    [Test]
    public void TrapExceptionsBubbleToClrByDefault()
    {
        var engine = new Engine();
        var handler = new DelegatingProxyHandler
        {
            OnGet = (_, _, _) => throw new InvalidOperationException("boom")
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(CreateTarget(engine), handler));

        var ex = Invoking(() => engine.Evaluate("p.x")).Should().ThrowExactly<InvalidOperationException>().Which;
        ex.Message.Should().Be("boom");
    }

    [Test]
    public void TrapExceptionsAreCatchableInScriptWithCatchClrExceptions()
    {
        var engine = new Engine(options => options.CatchClrExceptions());
        var handler = new DelegatingProxyHandler
        {
            OnGet = (_, _, _) => throw new InvalidOperationException("boom")
        };

        engine.SetValue("p", engine.Advanced.CreateProxy(CreateTarget(engine), handler));

        var result = engine.Evaluate("(function () { try { p.x; return 'no-throw'; } catch (e) { return 'caught: ' + e.message; } })()").AsString();
        result.Should().Be("caught: A host operation failed.");
    }

    [Test]
    public void ParsingLimitFromAnyTrapRemainsFatal()
    {
        var engine = new Engine(options =>
        {
            options.CatchClrExceptions();
            options.Parsing.MaxSourceLength = 100;
        });
        var handler = new DelegatingProxyHandler
        {
            OnSet = (_, _, _, _) =>
            {
                engine.Evaluate(new string(' ', 110));
                return true;
            }
        };
        engine.SetValue("p", engine.Advanced.CreateProxy(CreateTarget(engine), handler));

        Invoking(() => engine.Evaluate("try { p.x = 1; } catch { globalThis.caught = true; }"))
            .Should().ThrowExactly<ParsingLimitException>();
        engine.GetValue("caught").Should().BeUndefined();
    }

    [Test]
    public void ResultLimitFromAnyTrapRemainsFatal()
    {
        var engine = new Engine(options => options.CatchClrExceptions());
        var value = engine.Evaluate("[1, 2]");
        var handler = new DelegatingProxyHandler
        {
            OnGet = (_, _, _) =>
            {
                engine.ConvertResult(value, new ResultLimits { MaxPropertyCount = 1 });
                return null;
            }
        };
        engine.SetValue("p", engine.Advanced.CreateProxy(CreateTarget(engine), handler));

        Invoking(() => engine.Evaluate("try { p.x; } catch { globalThis.caught = true; }"))
            .Should().ThrowExactly<ResultLimitExceededException>();
        engine.GetValue("caught").Should().BeUndefined();
    }

    [Test]
    public void FactoriesValidateArguments()
    {
        var engine = new Engine();
        var target = CreateTarget(engine);
        var handler = new DelegatingProxyHandler();

        Invoking(() => engine.Advanced.CreateProxy(null!, handler)).Should().ThrowExactly<ArgumentNullException>();
        Invoking(() => engine.Advanced.CreateProxy(target, null!)).Should().ThrowExactly<ArgumentNullException>();
        Invoking(() => engine.Advanced.CreateRevocableProxy(null!, handler)).Should().ThrowExactly<ArgumentNullException>();
        Invoking(() => engine.Advanced.CreateRevocableProxy(target, null!)).Should().ThrowExactly<ArgumentNullException>();
    }

    [Test]
    public void DefaultRevocableProxyThrowsInvalidOperationException()
    {
        var uninitialized = default(RevocableProxy);

        Invoking(() => uninitialized.Proxy).Should().ThrowExactly<InvalidOperationException>();
        Invoking(() => uninitialized.Revoke()).Should().ThrowExactly<InvalidOperationException>();
    }
}
