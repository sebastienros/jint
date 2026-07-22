using Jint.Native.Error;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class ProxyTests
{
    private readonly Engine _engine;

    public ProxyTests()
    {
        _engine = new Engine()
            .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())));
    }

    [Fact]
    public void ProxyCanBeRevokedWithoutContext()
    {
        _engine.Execute(@"
            var revocable = Proxy.revocable({}, {});
            var revoke = revocable.revoke;
            revoke.call(null);
        ");
    }

    [Fact]
    public void GetTrapIsCalledForPropertyNamedRevoke()
    {
        _engine.Evaluate("new Proxy({}, { get: () => 'trapped' }).revoke").AsString().Should().Be("trapped");
    }

    [Fact]
    public void AccessingPropertyOnRevokedProxyThrowsTypeError()
    {
        _engine.Execute(@"
            var revocable = Proxy.revocable({ foo: 1 }, { get: () => 'trapped' });
            var proxy = revocable.proxy;
            revocable.revoke();
        ");
        var ex = Invoking(() => _engine.Evaluate("proxy.foo")).Should().ThrowExactly<JavaScriptException>().Which;
        AssertJsTypeError(_engine, ex, "Cannot perform 'get' on a proxy that has been revoked");
    }

    [Fact]
    public void RevokingProxyInsideSetTrapValidatesAgainstOriginalTarget()
    {
        // the spec captures [[ProxyTarget]] before the trap runs; a trap revoking its own
        // proxy must not crash the post-trap invariant validation
        var result = _engine.Evaluate("""
            var r = Proxy.revocable({}, { set: (t, k, v) => { r.revoke(); return true; } });
            r.proxy.x = 1;
            'ok';
            """).AsString();
        result.Should().Be("ok");
    }

    [Fact]
    public void RevokingProxyInsideDeleteTrapValidatesAgainstOriginalTarget()
    {
        var result = _engine.Evaluate("""
            var r = Proxy.revocable({ a: 1 }, { deleteProperty: (t, k) => { r.revoke(); return true; } });
            delete r.proxy.a;
            """);
        result.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void RevokingProxyInsideHasTrapValidatesAgainstOriginalTarget()
    {
        var result = _engine.Evaluate("""
            var r = Proxy.revocable({ a: 1 }, { has: (t, k) => { r.revoke(); return false; } });
            'a' in r.proxy;
            """);
        result.AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void ConstructTrapReceivesCreateArrayFromListArguments()
    {
        var result = _engine.Evaluate("""
            var p = new Proxy(function () {}, { construct: (t, args) => ({ len: args.length, first: args[0] }) });
            var r = new p(42);
            r.len + ':' + r.first;
            """).AsString();
        result.Should().Be("1:42");
    }

    [Fact]
    public void ConstructTrapAcceptsSingleFractionalArgument()
    {
        var result = _engine.Evaluate("new (new Proxy(function () {}, { construct: (t, args) => ({ v: args[0] }) }))(1.5).v");
        result.AsNumber().Should().Be(1.5);
    }

    [Fact]
    public void ProxyInvariantViolationsHaveDescriptiveMessages()
    {
        _engine.Execute("""
            var frozen = Object.freeze({ a: 1 });
            var pHas = new Proxy(frozen, { has: () => false });
            var pOwnKeys = new Proxy(frozen, { ownKeys: () => ['b'] });
            var pPrevent = new Proxy({}, { preventExtensions: () => true });
            """);

        var ex = Invoking(() => _engine.Evaluate("'a' in pHas")).Should().ThrowExactly<JavaScriptException>().Which;
        AssertJsTypeError(_engine, ex, "'has' on proxy: trap returned falsish for property 'a' which exists in the proxy target as non-configurable");

        ex = Invoking(() => _engine.Evaluate("Object.keys(pOwnKeys)")).Should().ThrowExactly<JavaScriptException>().Which;
        AssertJsTypeError(_engine, ex, "'ownKeys' on proxy: trap result did not include non-configurable property 'a' of the proxy target");

        ex = Invoking(() => _engine.Evaluate("Object.preventExtensions(pPrevent)")).Should().ThrowExactly<JavaScriptException>().Which;
        AssertJsTypeError(_engine, ex, "'preventExtensions' on proxy: trap returned truish but the proxy target is extensible");

        ex = Invoking(() => _engine.Evaluate("new Proxy({}, { get: 1 }).x")).Should().ThrowExactly<JavaScriptException>().Which;
        AssertJsTypeError(_engine, ex, "'get' trap of proxy handler is not a function");
    }

    [Fact]
    public void ProxyToStringUseTarget()
    {
        _engine.Execute(@"
            const targetWithToString = {toString: () => 'target'}
        ");
        _engine.Evaluate("new Proxy(targetWithToString, {}).toString()").AsString().Should().Be("target");
        _engine.Evaluate("`${new Proxy(targetWithToString, {})}`").AsString().Should().Be("target");
    }

    [Fact]
    public void ProxyToStringUseHandler()
    {
        _engine.Execute(@"
            const handler = { get: (target, prop, receiver) => prop === 'toString' ? () => 'handler' : Reflect.get(target, prop, receiver) }
            const targetWithToString = {toString: () => 'target'}
        ");

        _engine.Evaluate("new Proxy({}, handler).toString()").AsString().Should().Be("handler");
        _engine.Evaluate("new Proxy(targetWithToString, handler).toString()").AsString().Should().Be("handler");
        _engine.Evaluate("`${new Proxy({}, handler)}`").AsString().Should().Be("handler");
        _engine.Evaluate("`${new Proxy(targetWithToString, handler)}`").AsString().Should().Be("handler");
    }

    [Fact]
    public void ToPropertyDescriptor()
    {
        const string Script = @"
            var get = [];
            var p = new Proxy({
                enumerable: true, configurable: true, value: true,
                writable: true, get: Function(), set: Function()
              }, { get: function(o, k) { get.push(k); return o[k]; }});
            try {
              // This will throw, since it will have true for both ""get"" and ""value"",
              // but not before performing a Get on every property.
              Object.defineProperty({}, ""foo"", p);
            } catch(e) {
              return get + '';
            }
            return 'did not fail as expected'";

        _engine.Evaluate(Script).Should().Be("enumerable,configurable,value,writable,get,set");
    }

    [Fact]
    public void DefineProperties()
    {
        const string Script = @"
            // Object.defineProperties -> Get -> [[Get]]
            var get = [];
            var p = new Proxy({foo:{}, bar:{}}, { get: function(o, k) { get.push(k); return o[k]; }});
            Object.defineProperties({}, p);
            return get + '';";

        _engine.Evaluate(Script).Should().Be("foo,bar");
    }

    [Fact]
    public void GetHandlerInstancesOfProxies()
    {
        const string Script = @"
            var proxied = { };
            var proxy = Object.create(new Proxy(proxied, {
              get: function (t, k, r) {
                equal(t, proxied); equal('foo', k); equal(proxy, r);
                return t === proxied && k === 'foo' && r === proxy && 5;
              }
            }));
            equal(5, proxy.foo);";

        _engine.Execute(Script);
    }

    [Fact]
    public void SetHandlerInvariants()
    {
        const string Script = @"
            var passed = false;
            var proxied = { };
            var proxy = new Proxy(proxied, {
              get: function () {
                passed = true;
                return 4;
              }
            });
            // The value reported for a property must be the same as the value of the corresponding
            // target object property if the target object property is a non-writable,
            // non-configurable own data property.
            Object.defineProperty(proxied, ""foo"", { value: 5, enumerable: true });
            try {
              proxy.foo;
              return false;
            }
            catch(e) {}
            // The value reported for a property must be undefined if the corresponding target
            // object property is a non-configurable own accessor property that has undefined
            // as its [[Get]] attribute.
            Object.defineProperty(proxied, ""bar"",
              { set: function(){}, enumerable: true });
            try {
              proxy.bar;
              return false;
            }
            catch(e) {}
            return passed;";

        _engine.Evaluate(Script).AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ApplyHandlerInvariant()
    {
        const string Script = @"
            var passed = false;
            new Proxy(function(){}, {
                apply: function () { passed = true; }
            })();
            // A Proxy exotic object only has a [[Call]] internal method if the
            // initial value of its [[ProxyTarget]] internal slot is an object
            // that has a [[Call]] internal method.
            try {
              new Proxy({}, {
                apply: function () {}
              })();
              return false;
            } catch(e) {}
            return passed;";

        _engine.Evaluate(Script).AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ConstructHandlerInvariant()
    {
        const string Script = @"
            var passed = false;
            new Proxy({},{});
            // A Proxy exotic object only has a [[Construct]] internal method if the
            // initial value of its [[ProxyTarget]] internal slot is an object
            // that has a [[Construct]] internal method.
            try {
              new new Proxy({}, {
                construct: function (t, args) {
                  return {};
                }
              })();
              return false;
            } catch(e) {}
            // The result of [[Construct]] must be an Object.
            try {
              new new Proxy(function(){}, {
                construct: function (t, args) {
                  passed = true;
                  return 5;
                }
              })();
              return false;
            } catch(e) {}
            return passed;";

        _engine.Evaluate(Script).AsBoolean().Should().BeTrue();
    }

    // https://tc39.es/ecma262/#sec-proxycreate
    // A proxy only has [[Construct]] if its target is a constructor at creation time;
    // a handler "construct" trap cannot confer constructability.
    [Fact]
    public void ProxyWithNonConstructorTargetIsNotConstructor()
    {
        var ex = Invoking(() => _engine.Evaluate("new (new Proxy(() => {}, { construct: () => ({}) }))()")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Error.AsObject().Prototype.Should().BeSameAs(TypeErrorPrototype(_engine));
    }

    [Fact]
    public void ProxyWithConstructorTargetUsesConstructTrap()
    {
        var result = _engine.Evaluate("new (new Proxy(function(){}, { construct: () => ({ x: 1 }) }))().x");
        result.AsInteger().Should().Be(1);
    }

    [Fact]
    public void ReflectConstructRequiresConstructorNewTarget()
    {
        var ex = Invoking(() => _engine.Evaluate("Reflect.construct(function(){ return; }, [], new Proxy(() => {}, { construct: () => ({}) }))")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Error.AsObject().Prototype.Should().BeSameAs(TypeErrorPrototype(_engine));
    }

    [Fact]
    public void RevokedConstructorProxyKeepsTypeofFunctionButConstructThrowsRevokedError()
    {
        _engine.Execute("var r = Proxy.revocable(function(){}, {}); r.revoke();");

        // typeof relies on the [[Call]] slot captured at creation, revocation does not remove it
        _engine.Evaluate("typeof r.proxy").AsString().Should().Be("function");

        var ex = Invoking(() => _engine.Evaluate("new r.proxy()")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Error.AsObject().Prototype.Should().BeSameAs(TypeErrorPrototype(_engine));
        ex.Message.Should().Contain("revoked");
    }

    [Fact]
    public void ProxyHandlerGetDataPropertyShouldNotUseReferenceEquals()
    {
        // There are two JsString which should be treat as same value,
        // but they are not ReferenceEquals.
        _engine.Execute("""
            let o = Object.defineProperty({}, 'value', {
              configurable: false,
              value: 'in',
            });
            const handler = {
              get(target, property, receiver) {
                return 'Jint'.substring(1,3);
              }
            };
            let p = new Proxy(o, handler);
            let pv = p.value;
        """);
    }

    [Fact]
    public void ProxyHandlerGetDataPropertyShouldNotCheckClrType()
    {
        // There are a JsString and a ConcatenatedString which should be treat as same value,
        // but they are different CLR Type.
        _engine.Execute("""
            let o = Object.defineProperty({}, 'value', {
              configurable: false,
              value: 'Jint',
            });
            const handler = {
              get(target, property, receiver) {
                return 'Ji'.concat('nt');
              }
            };
            let p = new Proxy(o, handler);
            let pv = p.value;
        """);
    }

    class TestClass
    {
        public static readonly TestClass Instance = new TestClass();
        public string StringValue => "StringValue";
        public int IntValue => 42424242; // avoid small numbers cache
        public TestClass ObjectWrapper => Instance;

        public List<int> IntList { get; } = [1, 2, 3];

        private int x = 1;
        public int PropertySideEffect => x++;

        public string Name => "My Name is Test";

        public void SayHello()
        {
        }

        public int Add(int a, int b)
        {
            return a + b;
        }
    }

    [Fact]
    public void ProxyClrPropertyPrimitiveString()
    {
        _engine.SetValue("testClass", TestClass.Instance);
        var result = _engine.Evaluate("""
            const handler = {
              get(target, property, receiver) {
                return Reflect.get(target, property, receiver);
              }
            };
            const p = new Proxy(testClass, handler);
            return p.StringValue;
        """);
        result.AsString().Should().Be(TestClass.Instance.StringValue);
    }

    [Fact]
    public void ProxyClrPropertyPrimitiveInt()
    {
        _engine.SetValue("testClass", TestClass.Instance);
        var result = _engine.Evaluate("""
            const handler = {
              get(target, property, receiver) {
                return Reflect.get(target, property, receiver);
              }
            };
            const p = new Proxy(testClass, handler);
            return p.IntValue;
        """);
        result.AsInteger().Should().Be(TestClass.Instance.IntValue);
    }

    [Fact]
    public void ProxyClrPropertyObjectWrapper()
    {
        _engine.SetValue("testClass", TestClass.Instance);
        var result = _engine.Evaluate("""
            const handler = {
              get(target, property, receiver) {
                return Reflect.get(target, property, receiver);
              }
            };
            const p = new Proxy(testClass, handler);
            return p.ObjectWrapper;
        """);
    }

    private static ErrorPrototype TypeErrorPrototype(Engine engine)
        => engine.Realm.Intrinsics.TypeError.PrototypeObject;

    private static void AssertJsTypeError(Engine engine, JavaScriptException ex, string msg)
    {
        ex.Error.AsObject().Prototype.Should().BeSameAs(TypeErrorPrototype(engine));
        ex.Message.Should().Be(msg);
    }

    // https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Proxy/Proxy/get#invariants
    // The value reported for a property must be the same as
    // the value ofthe corresponding target object property,
    // if the target object property is
    // a non-writable, non-configurable own data property.
    [Fact]
    public void ProxyHandlerGetInvariantsDataPropertyReturnsDifferentValue()
    {
        _engine.Execute("""
            let o = Object.defineProperty({}, 'value', {
              writable: false,
              configurable: false,
              value: 42,
            });
            const handler = {
              get(target, property, receiver) {
                return 32;
              }
            };
            let p = new Proxy(o, handler);
        """);
        var ex = Invoking(() => _engine.Evaluate("p.value")).Should().ThrowExactly<JavaScriptException>().Which;
        AssertJsTypeError(_engine, ex, "'get' on proxy: property 'value' is a read-only and non-configurable data property on the proxy target but the proxy did not return its actual value (expected '42' but got '32')");
    }

    // https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Proxy/Proxy/get#invariants
    // The value reported for a property must be undefined,
    // if the corresponding target object property is
    // a non-configurable own accessor property
    // that has undefined as its [[Get]] attribute.
    [Fact]
    public void ProxyHandlerGetInvariantsAccessorPropertyWithoutGetButReturnsValue()
    {
        _engine.Execute("""
            let o = Object.defineProperty({}, 'value', {
              configurable: false,
              set() {},
            });
            const handler = {
              get(target, property, receiver) {
                return 32;
              }
            };
            let p = new Proxy(o, handler);
        """);
        var ex = Invoking(() => _engine.Evaluate("p.value")).Should().ThrowExactly<JavaScriptException>().Which;
        AssertJsTypeError(_engine, ex, "'get' on proxy: property 'value' is a non-configurable accessor property on the proxy target and does not have a getter function, but the trap did not return 'undefined' (got '32')");
    }

    private const string ScriptProxyHandlerSetInvariantsDataPropertyImmutable = """
        let o = Object.defineProperty({}, 'value', {
          writable: false,
          configurable: false,
          value: 42,
        });
        const handler = {
          set(target, property, value, receiver) {
            return true;
          }
        };
        let p = new Proxy(o, handler);
    """;

    // https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Proxy/Proxy/set#invariants
    // Cannot change the value of a property to be different from
    // the value of the corresponding target object property,
    // if the corresponding target object property is
    // a non-writable, non-configurable data property.
    [Fact]
    public void ProxyHandlerSetInvariantsDataPropertyImmutableChangeValue()
    {
        _engine.Execute(ScriptProxyHandlerSetInvariantsDataPropertyImmutable);
        var ex = Invoking(() => _engine.Evaluate("p.value = 32")).Should().ThrowExactly<JavaScriptException>().Which;
        AssertJsTypeError(_engine, ex, "'set' on proxy: trap returned truish for property 'value' which exists in the proxy target as a non-configurable and non-writable data property with a different value");
    }

    [Fact]
    public void ProxyHandlerSetInvariantsDataPropertyImmutableSetSameValue()
    {
        _engine.Execute(ScriptProxyHandlerSetInvariantsDataPropertyImmutable);
        _engine.Evaluate("p.value = 42");
    }

    // https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Proxy/Proxy/set#invariants
    // Cannot set the value of a property, 
    // if the corresponding target object property is
    // a non-configurable accessor property
    // that has undefined as its [[Set]] attribute.
    [Fact]
    public void ProxyHandlerSetInvariantsAccessorPropertyWithoutSetChange()
    {
        _engine.Execute("""
            let o = Object.defineProperty({}, 'value', {
              configurable: false,
              get() { return 42; },
            });
            const handler = {
              set(target, property, value, receiver) {
                return true;
              }
            };
            let p = new Proxy(o, handler);
        """);
        var ex = Invoking(() => _engine.Evaluate("p.value = 42")).Should().ThrowExactly<JavaScriptException>().Which;
        AssertJsTypeError(_engine, ex, "'set' on proxy: trap returned truish for property 'value' which exists in the proxy target as a non-configurable and non-writable accessor property without a setter");
    }

    // https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Proxy/Proxy/set#invariants
    // In strict mode, a false return value from the set() handler
    // will throw a TypeError exception.
    [Fact]
    public void ProxyHandlerSetInvariantsReturnsFalseInStrictMode()
    {
        var ex = Invoking(() => _engine.Evaluate("""
            'use strict';
            let p = new Proxy({}, { set: () => false });
            p.value = 42;
        """)).Should().ThrowExactly<JavaScriptException>().Which;
        // V8: "'set' on proxy: trap returned falsish for property 'value'",
        AssertJsTypeError(_engine, ex, "Cannot assign to read only property 'value' of [object Object]");
    }

    [Fact]
    public void ProxyHandlerSetInvariantsReturnsFalseInNonStrictMode()
    {
        _engine.Evaluate("""
            // 'use strict';
            let p = new Proxy({}, { set: () => false });
            p.value = 42;
        """);
    }

    // https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-getprototypeof
    [Fact]
    public void ProxyHandlerGetPrototypeOfCanReturnNull()
    {
        _engine.Evaluate("Object.getPrototypeOf(new Proxy({}, { getPrototypeOf: () => null }))").IsNull().Should().BeTrue();
        _engine.Evaluate("Reflect.getPrototypeOf(new Proxy({}, { getPrototypeOf: () => null }))").IsNull().Should().BeTrue();
    }

    [Fact]
    public void ProxyHandlerGetPrototypeOfCanReturnNullForNonExtensibleTargetWithNullPrototype()
    {
        _engine.Evaluate("""
            const t = Object.create(null);
            Object.preventExtensions(t);
            Object.getPrototypeOf(new Proxy(t, { getPrototypeOf: () => null }));
        """).IsNull().Should().BeTrue();
    }

    // https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-setprototypeof-v
    [Fact]
    public void ProxyHandlerSetPrototypeOfCanSetNullOnNonExtensibleTargetWithNullPrototype()
    {
        _engine.Execute("""
            let t = Object.create(null);
            Object.preventExtensions(t);
        """);

        _engine.Evaluate("let p1 = new Proxy(t, {}); Object.setPrototypeOf(p1, null) === p1").AsBoolean().Should().BeTrue();
        _engine.Evaluate("let p2 = new Proxy(t, { setPrototypeOf: () => true }); Object.setPrototypeOf(p2, null) === p2").AsBoolean().Should().BeTrue();
        _engine.Evaluate("Reflect.setPrototypeOf(new Proxy(t, {}), null)").AsBoolean().Should().BeTrue();
        _engine.Evaluate("Reflect.setPrototypeOf(new Proxy(t, { setPrototypeOf: () => true }), null)").AsBoolean().Should().BeTrue();
    }

    // https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Proxy/Proxy/getPrototypeOf#invariants
    // If the target object is not extensible, the prototype reported
    // by the trap must be the same as the target object's prototype.
    [Fact]
    public void ProxyHandlerGetPrototypeOfInvariantsNonExtensibleTargetDifferentPrototype()
    {
        _engine.Execute("""
            let t = {};
            Object.preventExtensions(t);
            let p = new Proxy(t, { getPrototypeOf: () => ({}) });
        """);
        var ex = Invoking(() => _engine.Evaluate("Object.getPrototypeOf(p)")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Error.AsObject().Prototype.Should().BeSameAs(TypeErrorPrototype(_engine));
    }

    // https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Proxy/Proxy/setPrototypeOf#invariants
    // If the target object is not extensible, the prototype cannot be changed.
    [Fact]
    public void ProxyHandlerSetPrototypeOfInvariantsNonExtensibleTargetDifferentPrototype()
    {
        _engine.Execute("""
            let t = {};
            Object.preventExtensions(t);
            let p = new Proxy(t, { setPrototypeOf: () => true });
        """);
        var ex = Invoking(() => _engine.Evaluate("Object.setPrototypeOf(p, null)")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Error.AsObject().Prototype.Should().BeSameAs(TypeErrorPrototype(_engine));
    }

    [Fact]
    public void ClrPropertySideEffect()
    {
        _engine.SetValue("testClass", TestClass.Instance);
        _engine.Execute("""
            const handler = {
              get(target, property, receiver) {
                return 2;
              }
            };
            const p = new Proxy(testClass, handler);
        """);

        TestClass.Instance.PropertySideEffect.Should().Be(1); // first call to PropertySideEffect
        _engine.Evaluate("p.PropertySideEffect").AsInteger().Should().Be(2); // no call to PropertySideEffect
        TestClass.Instance.PropertySideEffect.Should().Be(2); // second call to PropertySideEffect
    }

    [Fact]
    public void ToObjectReturnsProxiedToObject()
    {
        _engine
            .SetValue("T", new TestClass())
            .Execute("""
                 const handler = {
                     get(target, property, receiver) {

                         if (!target[property]) {
                             return (...args) => "Not available";
                         }

                         // return Reflect.get(target, property, receiver);
                         return Reflect.get(...arguments);
                     }
                 };

                 const p = new Proxy(T, handler);
                 const name = p.Name;  // works
                 const s = p.GetX();   // works because method does NOT exist on clr object

                 p.SayHello();          // throws System.Reflection.TargetException: 'Object does not match target type.'
                 const t = p.Add(5,3);  // throws System.Reflection.TargetException: 'Object does not match target type.'
             """);

    }

    [Fact]
    public void ProxyIterateClrList()
    {
        var res = _engine
            .SetValue("obj", TestClass.Instance)
            .Evaluate("""
                //const obj = {
                //    IntList: [1, 2, 3]
                //};

                const objProxy = new Proxy(obj, {
                  get(target, prop, receiver) {
                    const targetValue = Reflect.get(target, prop, receiver);
                    if (prop == 'IntList') {
                        return new Proxy(targetValue, {
                            get(target, prop, receiver) {
                                return Reflect.get(target, prop, receiver);            
                            }
                        });
                    }

                    return targetValue;
                  }
                });

                const arr = []
                for (const item of objProxy.IntList)
                {
                    arr.push(item);
                }
                arr.push(objProxy.IntList.length)

                return arr;
            """);

        res.AsArray().AsEnumerable().Should().Equal(1, 2, 3, 3);
    }

    [Fact]
    public void ProxyClrObjectMethod()
    {
        var res = _engine
            .SetValue("T", new TestClass())
            .Evaluate("""
                 const handler = {
                     get(target, property, receiver) {

                         if (property == "Add") {
                             return function(...args) { return 42};
                         }

                         return Reflect.get(...arguments);
                     }
                 };

                 const p = new Proxy(T, handler);
                 p.Add(5,3); // throws 'get' on proxy: property 'Add' is a read-only and non-configurable data property
                             // on the proxy target but the proxy did not return its actual value
                             // (expected 'function Jint.Tests.Runtime.ProxyTests+TestClass.Add() { [native code] }' but got 'function () { [native code] }')
             """);

        res.AsInteger().Should().Be(42);
    }

    [Fact]
    public void ProxyClrObjectMethodWithDelegate()
    {
        var res = _engine
            .SetValue("T", new TestClass())
            .Evaluate("""
                 const handler = {
                     get(target, property, receiver) {

                         if (property == "Add") {
                             return (...args) => 42;
                         }

                         return Reflect.get(...arguments);
                     }
                 };

                 const p = new Proxy(T, handler);
                 p.Add(5,3); // throws 'get' on proxy: property 'Add' is a read-only and non-configurable data property
                             // on the proxy target but the proxy did not return its actual value
                             // (expected 'function Jint.Tests.Runtime.ProxyTests+TestClass.Add() { [native code] }' but got 'function () { [native code] }')
             """);

        res.AsInteger().Should().Be(42);
    }

    [Fact]
    public void ProxyClrObjectWithTmpObjectMethod()
    {
        var res = _engine
            .SetValue("T", new TestClass())
            .Evaluate("""
                 const handler = {
                     get(target, property, receiver) {

                         if (property == "Add") {
                             return (...args) => target.target[property](...args) + 34;
                         }

                         if (typeof target.target[property] === "function")
                            return (...args) => target.target[property](...args);

                        return Reflect.get(target.target, property, receiver)
                     }
                 };

                 const tmpObj = { target: T };
                 const p = new Proxy(tmpObj, handler);

                 const name = p.Name;
                 p.SayHello();
                 const res = p.Add(5,3); // works now

                 name + " " + res
             """);

        res.AsString().Should().Be("My Name is Test 42");
    }
}
