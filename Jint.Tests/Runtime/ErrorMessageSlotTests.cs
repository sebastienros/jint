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
        engine.Evaluate("new Error('hello').message").AsString().Should().Be("hello");

        engine.Execute("var d = Object.getOwnPropertyDescriptor(new Error('hello'), 'message');");
        engine.Evaluate("d.value").AsString().Should().Be("hello");
        engine.Evaluate("d.writable").AsBoolean().Should().BeTrue();
        engine.Evaluate("d.enumerable").AsBoolean().Should().BeFalse();
        engine.Evaluate("d.configurable").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void HasOwnAndInReflectMessagePresence()
    {
        var engine = E();
        engine.Evaluate("new Error('x').hasOwnProperty('message')").AsBoolean().Should().BeTrue();
        engine.Evaluate("'message' in new Error('x')").AsBoolean().Should().BeTrue();
        // stack stays an accessor on the prototype, not an own property.
        engine.Evaluate("new Error('x').hasOwnProperty('stack')").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void NoArgumentErrorInheritsEmptyMessage()
    {
        var engine = E();
        engine.Evaluate("new Error().message").AsString().Should().Be("");
        engine.Evaluate("new Error().hasOwnProperty('message')").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void WritingMessageKeepsItAWritableDataProperty()
    {
        var engine = E();
        engine.Evaluate("var e = new Error('a'); e.message = 'b'; e.message").AsString().Should().Be("b");
        engine.Execute("var d = Object.getOwnPropertyDescriptor(e, 'message');");
        engine.Evaluate("d.writable && !d.enumerable && d.configurable").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void DeletingMessageMakesItInheritEmptyString()
    {
        var engine = E();
        engine.Evaluate("var e = new Error('x'); delete e.message").AsBoolean().Should().BeTrue();
        engine.Evaluate("e.message").AsString().Should().Be("");
        engine.Evaluate("e.hasOwnProperty('message')").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void OwnPropertyNamesKeepMessageFirstThenAddedKeys()
    {
        var engine = E();
        var names = engine.Evaluate("var e = new Error('m'); e.foo = 1; e.bar = 2; Object.getOwnPropertyNames(e).join(',')").AsString();
        names.Should().Be("message,foo,bar");
    }

    [Fact]
    public void NonEnumerableMessageIsSkippedByKeysAndJsonStringify()
    {
        var engine = E();
        engine.Evaluate("var e = new Error('m'); e.foo = 1; Object.keys(e).join(',')").AsString().Should().Be("foo");
        engine.Evaluate("JSON.stringify(new Error('secret'))").AsString().Should().Be("{}");
    }

    [Fact]
    public void RedefiningMessageDeoptsToDefinedDescriptor()
    {
        var engine = E();
        engine.Execute("var e = new Error('orig'); Object.defineProperty(e, 'message', { value: 'redef', enumerable: true });");
        engine.Evaluate("e.message").AsString().Should().Be("redef");
        engine.Evaluate("var d = Object.getOwnPropertyDescriptor(e, 'message'); d.enumerable && d.writable && d.configurable").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.keys(e).join(',')").AsString().Should().Be("message");
    }

    [Fact]
    public void CauseOptionMaterializesMessageThenCause()
    {
        var engine = E();
        engine.Execute("var e = new Error('withcause', { cause: 42 });");
        engine.Evaluate("e.cause").AsNumber().Should().Be(42);
        engine.Evaluate("e.message").AsString().Should().Be("withcause");
        engine.Evaluate("Object.getOwnPropertyNames(e).join(',')").AsString().Should().Be("message,cause");
    }

    [Fact]
    public void FrozenErrorHasNonWritableNonConfigurableMessage()
    {
        var engine = E();
        engine.Execute("var e = Object.freeze(new Error('frozen'));");
        engine.Evaluate("e.message").AsString().Should().Be("frozen");
        engine.Evaluate("Object.isFrozen(e)").AsBoolean().Should().BeTrue();
        engine.Evaluate("var d = Object.getOwnPropertyDescriptor(e, 'message'); !d.writable && !d.configurable").AsBoolean().Should().BeTrue();
        // sloppy-mode write to a frozen property is silently ignored
        engine.Evaluate("e.message = 'nope'; e.message").AsString().Should().Be("frozen");
    }

    [Fact]
    public void PreventExtensionsKeepsMessageReadableAndEnumerated()
    {
        var engine = E();
        engine.Execute("var e = Object.preventExtensions(new Error('m'));");
        engine.Evaluate("e.message").AsString().Should().Be("m");
        engine.Evaluate("Object.getOwnPropertyNames(e).join(',')").AsString().Should().Be("message");
        engine.Evaluate("Object.isFrozen(e)").AsBoolean().Should().BeFalse();
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
        engine.Evaluate($"new {type}('boom').message").AsString().Should().Be("boom");
        engine.Evaluate($"new {type}('boom').toString()").AsString().Should().Be($"{type}: boom");
        engine.Evaluate($"new {type}('boom').hasOwnProperty('message')").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void MessageArgumentIsCoercedToString()
    {
        var engine = E();
        engine.Evaluate("new Error(42).message").AsString().Should().Be("42");
    }

    [Fact]
    public void GeneratedRuntimeErrorHasReadableMessageAndOwnMessageKey()
    {
        var engine = E();
        var result = engine.Evaluate(@"
try { null.foo; }
catch (e) { e.message + '|' + e.hasOwnProperty('message') + '|' + Object.getOwnPropertyNames(e).indexOf('message'); }").AsString();
        // message present, own, and first own key
        result.Should().StartWith("Cannot read");
        result.Should().EndWith("|true|0");
    }

    [Fact]
    public void AssignAndSpreadSkipNonEnumerableMessage()
    {
        var engine = E();
        engine.Evaluate("Object.assign({}, new Error('m')).message === undefined").AsBoolean().Should().BeTrue();
        engine.Evaluate("({ ...new Error('m') }).message === undefined").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void JavaScriptExceptionMessageReflectsVirtualMessage()
    {
        var engine = E();
        var ex = Invoking(() => engine.Execute("throw new Error('surfaced');")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>().Which;
        ex.Message.Should().Be("surfaced");
    }
}
