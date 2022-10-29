namespace Jint.Tests.Runtime;

public class ProxyTests
{
    [Fact]
    public void ProxyCanBeRevokedWithoutContext()
    {
        new Engine()
            .Execute(@"
                    var revocable = Proxy.revocable({}, {});
                    var revoke = revocable.revoke;
                    revoke.call(null);
                ");
    }

    [Fact]
    public void ProxyToStringUseTarget()
    {
        var engine = new Engine().Execute(@"
                const targetWithToString = {toString: () => 'target'}
            ");
        Assert.Equal("target", engine.Evaluate("new Proxy(targetWithToString, {}).toString()").AsString());
        Assert.Equal("target", engine.Evaluate("`${new Proxy(targetWithToString, {})}`").AsString());
    }

    [Fact]
    public void ProxyToStringUseHandler()
    {
        var engine = new Engine().Execute(@"
                const handler = { get: (target, prop, receiver) => prop === 'toString' ? () => 'handler' : Reflect.get(target, prop, receiver) }
                const targetWithToString = {toString: () => 'target'}
            ");

        Assert.Equal("handler", engine.Evaluate("new Proxy({}, handler).toString()").AsString());
        Assert.Equal("handler", engine.Evaluate("new Proxy(targetWithToString, handler).toString()").AsString());
        Assert.Equal("handler", engine.Evaluate("`${new Proxy({}, handler)}`").AsString());
        Assert.Equal("handler", engine.Evaluate("`${new Proxy(targetWithToString, handler)}`").AsString());
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

        var engine = new Engine();
        Assert.Equal("enumerable,configurable,value,writable,get,set", engine.Evaluate(Script));
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

        var engine = new Engine();
        Assert.Equal("foo,bar", engine.Evaluate(Script));
    }
}
