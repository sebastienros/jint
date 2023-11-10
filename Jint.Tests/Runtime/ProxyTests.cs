using Jint.Native.Error;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class ProxyTests
{
    private readonly Engine _engine;

    public ProxyTests()
    {
        _engine = new Engine()
            .SetValue("equal", new Action<object, object>(Assert.Equal));
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
    public void ProxyToStringUseTarget()
    {
        _engine.Execute(@"
            const targetWithToString = {toString: () => 'target'}
        ");
        Assert.Equal("target", _engine.Evaluate("new Proxy(targetWithToString, {}).toString()").AsString());
        Assert.Equal("target", _engine.Evaluate("`${new Proxy(targetWithToString, {})}`").AsString());
    }

    [Fact]
    public void ProxyToStringUseHandler()
    {
        _engine.Execute(@"
            const handler = { get: (target, prop, receiver) => prop === 'toString' ? () => 'handler' : Reflect.get(target, prop, receiver) }
            const targetWithToString = {toString: () => 'target'}
        ");

        Assert.Equal("handler", _engine.Evaluate("new Proxy({}, handler).toString()").AsString());
        Assert.Equal("handler", _engine.Evaluate("new Proxy(targetWithToString, handler).toString()").AsString());
        Assert.Equal("handler", _engine.Evaluate("`${new Proxy({}, handler)}`").AsString());
        Assert.Equal("handler", _engine.Evaluate("`${new Proxy(targetWithToString, handler)}`").AsString());
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

        Assert.Equal("enumerable,configurable,value,writable,get,set", _engine.Evaluate(Script));
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

        Assert.Equal("foo,bar", _engine.Evaluate(Script));
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

        Assert.True(_engine.Evaluate(Script).AsBoolean());
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

        Assert.True(_engine.Evaluate(Script).AsBoolean());
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

        Assert.True(_engine.Evaluate(Script).AsBoolean());
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
        Assert.Equal(TestClass.Instance.StringValue, result.AsString());
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
        Assert.Equal(TestClass.Instance.IntValue, result.AsInteger());
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
        Assert.Same(TypeErrorPrototype(engine), ex.Error.AsObject().Prototype);
        Assert.Equal(msg, ex.Message);
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
        var ex = Assert.Throws<JavaScriptException>(() => _engine.Evaluate("p.value"));
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
        var ex = Assert.Throws<JavaScriptException>(() => _engine.Evaluate("p.value"));
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
        var ex = Assert.Throws<JavaScriptException>(() => _engine.Evaluate("p.value = 32"));
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
        var ex = Assert.Throws<JavaScriptException>(() => _engine.Evaluate("p.value = 42"));
        AssertJsTypeError(_engine, ex, "'set' on proxy: trap returned truish for property 'value' which exists in the proxy target as a non-configurable and non-writable accessor property without a setter");
    }

    // https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Proxy/Proxy/set#invariants
    // In strict mode, a false return value from the set() handler
    // will throw a TypeError exception.
    [Fact]
    public void ProxyHandlerSetInvariantsReturnsFalseInStrictMode()
    {
        var ex = Assert.Throws<JavaScriptException>(() => _engine.Evaluate("""
            'use strict';
            let p = new Proxy({}, { set: () => false });
            p.value = 42;
        """));
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

        Assert.Equal(1, TestClass.Instance.PropertySideEffect); // first call to PropertySideEffect
        Assert.Equal(2, _engine.Evaluate("p.PropertySideEffect").AsInteger()); // no call to PropertySideEffect
        Assert.Equal(2, TestClass.Instance.PropertySideEffect); // second call to PropertySideEffect
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
}
