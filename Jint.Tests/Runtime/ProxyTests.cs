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
}
