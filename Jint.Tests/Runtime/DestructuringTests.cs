namespace Jint.Tests.Runtime;

public class DestructuringTests
{
    private readonly Engine _engine;

    public DestructuringTests()
    {
        _engine = new Engine()
            .SetValue("equal", new Action<object, object>(Assert.Equal));
    }

    [Fact]
    public void WithParameterStrings()
    {
        const string Script = @"
            return function([a, b, c]) {
              equal('a', a);
              equal('b', b);
              return c === void undefined;
            }('ab');";

        Assert.True(_engine.Evaluate(Script).AsBoolean());
    }

    [Fact]
    public void WithParameterObjectPrimitives()
    {
        const string Script = @"
            return function({toFixed}, {slice}) {
              equal(Number.prototype.toFixed, toFixed);
              equal(String.prototype.slice, slice);
              return true;
            }(2,'');";

        Assert.True(_engine.Evaluate(Script).AsBoolean());
    }

    [Fact]
    public void WithParameterComputedProperties()
    {
        const string Script = @"
            var qux = 'corge';
            return function({ [qux]: grault }) {
              equal('garply', grault);
            }({ corge: 'garply' });";

        _engine.Execute(Script);
    }

    [Fact]
    public void WithParameterFunctionLengthProperty()
    {
        _engine.Execute("equal(0, ((x = 42, y) => {}).length);");
        _engine.Execute("equal(1, ((x, y = 42, z) => {}).length);");
        _engine.Execute("equal(1, ((a, b = 39,) => {}).length);");
        _engine.Execute("equal(2, function({a, b}, [c, d]){}.length);");
    }

    [Fact]
    public void WithNestedRest()
    {
        _engine.Execute("return function([x, ...[y, ...z]]) { equal(1, x); equal(2, y); equal('3,4', z + ''); }([1, 2, 3, 4]);");
    }

    [Fact]
    public void EmptyRest()
    {
        _engine.Execute("function test({ ...props }){}; test({});");
    }
}
