namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the observable behavior of the virtual <c>message</c> own property on error instances. The message
/// is stored in a field until an operation forces real property storage; the optimization must be invisible
/// to script (descriptor shape, enumeration order, delete/redefine/freeze, subtypes and coercion).
/// </summary>
public class ErrorMessageSlotTests
{
    private static Engine E() => new Engine();

    [Fact]
    public void MessageIsWritableNonEnumerableConfigurableDataProperty()
    {
        var engine = E();
        Assert.Equal("hello", engine.Evaluate("new Error('hello').message").AsString());

        engine.Execute("var d = Object.getOwnPropertyDescriptor(new Error('hello'), 'message');");
        Assert.Equal("hello", engine.Evaluate("d.value").AsString());
        Assert.True(engine.Evaluate("d.writable").AsBoolean());
        Assert.False(engine.Evaluate("d.enumerable").AsBoolean());
        Assert.True(engine.Evaluate("d.configurable").AsBoolean());
    }

    [Fact]
    public void HasOwnAndInReflectMessagePresence()
    {
        var engine = E();
        Assert.True(engine.Evaluate("new Error('x').hasOwnProperty('message')").AsBoolean());
        Assert.True(engine.Evaluate("'message' in new Error('x')").AsBoolean());
        // stack stays an accessor on the prototype, not an own property.
        Assert.False(engine.Evaluate("new Error('x').hasOwnProperty('stack')").AsBoolean());
    }

    [Fact]
    public void NoArgumentErrorInheritsEmptyMessage()
    {
        var engine = E();
        Assert.Equal("", engine.Evaluate("new Error().message").AsString());
        Assert.False(engine.Evaluate("new Error().hasOwnProperty('message')").AsBoolean());
    }

    [Fact]
    public void WritingMessageKeepsItAWritableDataProperty()
    {
        var engine = E();
        Assert.Equal("b", engine.Evaluate("var e = new Error('a'); e.message = 'b'; e.message").AsString());
        engine.Execute("var d = Object.getOwnPropertyDescriptor(e, 'message');");
        Assert.True(engine.Evaluate("d.writable && !d.enumerable && d.configurable").AsBoolean());
    }

    [Fact]
    public void DeletingMessageMakesItInheritEmptyString()
    {
        var engine = E();
        Assert.True(engine.Evaluate("var e = new Error('x'); delete e.message").AsBoolean());
        Assert.Equal("", engine.Evaluate("e.message").AsString());
        Assert.False(engine.Evaluate("e.hasOwnProperty('message')").AsBoolean());
    }

    [Fact]
    public void OwnPropertyNamesKeepMessageFirstThenAddedKeys()
    {
        var engine = E();
        var names = engine.Evaluate("var e = new Error('m'); e.foo = 1; e.bar = 2; Object.getOwnPropertyNames(e).join(',')").AsString();
        Assert.Equal("message,foo,bar", names);
    }

    [Fact]
    public void NonEnumerableMessageIsSkippedByKeysAndJsonStringify()
    {
        var engine = E();
        Assert.Equal("foo", engine.Evaluate("var e = new Error('m'); e.foo = 1; Object.keys(e).join(',')").AsString());
        Assert.Equal("{}", engine.Evaluate("JSON.stringify(new Error('secret'))").AsString());
    }

    [Fact]
    public void RedefiningMessageDeoptsToDefinedDescriptor()
    {
        var engine = E();
        engine.Execute("var e = new Error('orig'); Object.defineProperty(e, 'message', { value: 'redef', enumerable: true });");
        Assert.Equal("redef", engine.Evaluate("e.message").AsString());
        Assert.True(engine.Evaluate("var d = Object.getOwnPropertyDescriptor(e, 'message'); d.enumerable && d.writable && d.configurable").AsBoolean());
        Assert.Equal("message", engine.Evaluate("Object.keys(e).join(',')").AsString());
    }

    [Fact]
    public void CauseOptionMaterializesMessageThenCause()
    {
        var engine = E();
        engine.Execute("var e = new Error('withcause', { cause: 42 });");
        Assert.Equal(42, engine.Evaluate("e.cause").AsNumber());
        Assert.Equal("withcause", engine.Evaluate("e.message").AsString());
        Assert.Equal("message,cause", engine.Evaluate("Object.getOwnPropertyNames(e).join(',')").AsString());
    }

    [Fact]
    public void FrozenErrorHasNonWritableNonConfigurableMessage()
    {
        var engine = E();
        engine.Execute("var e = Object.freeze(new Error('frozen'));");
        Assert.Equal("frozen", engine.Evaluate("e.message").AsString());
        Assert.True(engine.Evaluate("Object.isFrozen(e)").AsBoolean());
        Assert.True(engine.Evaluate("var d = Object.getOwnPropertyDescriptor(e, 'message'); !d.writable && !d.configurable").AsBoolean());
        // sloppy-mode write to a frozen property is silently ignored
        Assert.Equal("frozen", engine.Evaluate("e.message = 'nope'; e.message").AsString());
    }

    [Fact]
    public void PreventExtensionsKeepsMessageReadableAndEnumerated()
    {
        var engine = E();
        engine.Execute("var e = Object.preventExtensions(new Error('m'));");
        Assert.Equal("m", engine.Evaluate("e.message").AsString());
        Assert.Equal("message", engine.Evaluate("Object.getOwnPropertyNames(e).join(',')").AsString());
        Assert.False(engine.Evaluate("Object.isFrozen(e)").AsBoolean());
    }

    [Theory]
    [InlineData("Error")]
    [InlineData("TypeError")]
    [InlineData("RangeError")]
    [InlineData("EvalError")]
    [InlineData("ReferenceError")]
    [InlineData("SyntaxError")]
    [InlineData("URIError")]
    public void SubtypesShareTheVirtualMessageSlot(string type)
    {
        var engine = E();
        Assert.Equal("boom", engine.Evaluate($"new {type}('boom').message").AsString());
        Assert.Equal($"{type}: boom", engine.Evaluate($"new {type}('boom').toString()").AsString());
        Assert.True(engine.Evaluate($"new {type}('boom').hasOwnProperty('message')").AsBoolean());
    }

    [Fact]
    public void MessageArgumentIsCoercedToString()
    {
        var engine = E();
        Assert.Equal("42", engine.Evaluate("new Error(42).message").AsString());
    }

    [Fact]
    public void GeneratedRuntimeErrorHasReadableMessageAndOwnMessageKey()
    {
        var engine = E();
        var result = engine.Evaluate(@"
try { null.foo; }
catch (e) { e.message + '|' + e.hasOwnProperty('message') + '|' + Object.getOwnPropertyNames(e).indexOf('message'); }").AsString();
        // message present, own, and first own key
        Assert.StartsWith("Cannot read", result);
        Assert.EndsWith("|true|0", result);
    }

    [Fact]
    public void AssignAndSpreadSkipNonEnumerableMessage()
    {
        var engine = E();
        Assert.True(engine.Evaluate("Object.assign({}, new Error('m')).message === undefined").AsBoolean());
        Assert.True(engine.Evaluate("({ ...new Error('m') }).message === undefined").AsBoolean());
    }

    [Fact]
    public void JavaScriptExceptionMessageReflectsVirtualMessage()
    {
        var engine = E();
        var ex = Assert.Throws<Jint.Runtime.JavaScriptException>(() => engine.Execute("throw new Error('surfaced');"));
        Assert.Equal("surfaced", ex.Message);
    }
}
