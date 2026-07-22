using System.Reflection;
using Jint.Native;
using Jint.Runtime;
using Jint.Tests.Runtime.TestClasses;

namespace Jint.Tests.Runtime;

public class ErrorTests
{
    [Fact]
    public void CanReturnCorrectErrorMessageAndLocation1()
    {
        const string script = @"
var a = {};

var b = a.user.name;
";

        var engine = new Engine();
        var e = Invoking(() => engine.Execute(script)).Should().ThrowExactly<JavaScriptException>().Which;
        e.Message.Should().Be("Cannot read properties of undefined (reading 'name')");
        e.Location.Start.Line.Should().Be(4);
        e.Location.Start.Column.Should().Be(15);
    }

    [Fact]
    public void CanReturnCorrectErrorMessageAndLocation1WithoutReferencedName()
    {
        const string script = @"
var c = a(b().Length);
";

        var engine = new Engine();
        engine.SetValue("a", new Action<string>((_) => { }));
        engine.SetValue("b", new Func<string>(() => null));
        var e = Invoking(() => engine.Execute(script)).Should().ThrowExactly<JavaScriptException>().Which;
        e.Message.Should().Be("Cannot read properties of null (reading 'Length')");
        e.Location.Start.Line.Should().Be(2);
        e.Location.Start.Column.Should().Be(14);
    }

    [Fact]
    public void CanReturnCorrectErrorMessageAndLocation2()
    {
        const string script = @"
 test();
";

        var engine = new Engine();
        var e = Invoking(() => engine.Execute(script)).Should().ThrowExactly<JavaScriptException>().Which;
        e.Message.Should().Be("test is not defined");
        e.Location.Start.Line.Should().Be(2);
        e.Location.Start.Column.Should().Be(1);
    }

    [Fact]
    public void CanProduceCorrectStackTraceForInternalError()
    {
        var engine = new Engine();

        engine.Execute(@"
var a = function(v) {
  return v.xxx.yyy;
}

var b = function(v) {
  return a(v);
}
            ", "custom.js");

        var e = Invoking(() => engine.Execute("var x = b(7);", "main.js")).Should().ThrowExactly<JavaScriptException>().Which;
        e.Message.Should().Be("Cannot read properties of undefined (reading 'yyy')");
        e.Location.Start.Line.Should().Be(3);
        e.Location.Start.Column.Should().Be(15);
        e.Location.SourceFile.Should().Be("custom.js");

        var stack = e.JavaScriptStackTrace;
        EqualIgnoringNewLineDifferences(@"    at a (custom.js:3:16)
    at b (custom.js:7:10)
    at main.js:1:9", stack);
    }

    [Fact]
    public void CanProduceCorrectStackTraceForScriptError()
    {
        var engine = new Engine();

        engine.Execute(@"
var a = function(v) {
  throw new Error('Error thrown from script');
}

var b = function(v) {
  return a(v);
}
            ", "custom.js");

        var e = Invoking(() => engine.Execute("var x = b(7);", "main.js")).Should().ThrowExactly<JavaScriptException>().Which;
        e.Message.Should().Be("Error thrown from script");
        e.Location.Start.Line.Should().Be(3);
        e.Location.Start.Column.Should().Be(8);
        e.Location.SourceFile.Should().Be("custom.js");

        var stack = e.JavaScriptStackTrace;
        EqualIgnoringNewLineDifferences(@"    at a (custom.js:3:9)
    at b (custom.js:7:10)
    at main.js:1:9", stack);
    }

    [Fact]
    public void ErrorObjectHasTheStackTraceImmediately()
    {
        var engine = new Engine();

        engine.Execute(@"
var a = function(v) {
  return Error().stack;
}

var b = function(v) {
  return a(v);
}
            ", "custom.js");

        var e = engine.Evaluate(@"b(7)", "main.js").AsString();

        var stack = e;
        EqualIgnoringNewLineDifferences(@"    at a (custom.js:3:10)
    at b (custom.js:7:10)
    at main.js:1:1", stack);
    }

    [Fact]
    public void ThrownErrorObjectHasStackTraceInCatch()
    {
        var engine = new Engine();

        engine.Execute(@"
var a = function(v) {
  try {
    throw Error();
  } catch(err) {
    return err.stack;
  }
}

var b = function(v) {
  return a(v);
}
            ", "custom.js");

        var e = engine.Evaluate(@"b(7)", "main.js").AsString();

        var stack = e;
        EqualIgnoringNewLineDifferences(@"    at a (custom.js:4:11)
    at b (custom.js:11:10)
    at main.js:1:1", stack);
    }


    [Fact]
    public void GeneratedErrorHasStackTraceInCatch()
    {
        var engine = new Engine();

        engine.Execute(@"
var a = function(v) {
  try {
    var a = ''.xyz();
  } catch(err) {
    return err.stack;
  }
}

var b = function(v) {
  return a(v);
}
            ", "custom.js");

        var e = engine.Evaluate(@"b(7)", "main.js").AsString();

        var stack = e;
        EqualIgnoringNewLineDifferences(@"    at a (custom.js:4:13)
    at b (custom.js:11:10)
    at main.js:1:1", stack);
    }

    [Fact]
    public void EscapedErrorRendersDeferredStackAndIsStableAcrossReads()
    {
        // The stack string is captured lazily at construction and rendered on first read. Here the error
        // object escapes ALL of its construction frames (returned to the top level) before .stack is ever
        // read, so the deferred render must reproduce the construction-time frames, not the (now empty)
        // live stack. Reading twice must return the identical, cached string.
        var engine = new Engine();
        engine.Execute(@"
var makeError = function(v) {
  return Error('boom');
}
var wrap = function(v) {
  return makeError(v);
}", "custom.js");

        engine.Execute("var e = wrap(7);", "main.js");

        var first = engine.Evaluate("e.stack").AsString();
        var second = engine.Evaluate("e.stack").AsString();

        second.Should().Be(first);
        EqualIgnoringNewLineDifferences(@"    at makeError (custom.js:3:10)
    at wrap (custom.js:6:10)
    at main.js:1:9", first);
    }

    [Fact]
    public void ErrorObjectHasStackAccessorOnPrototype()
    {
        var engine = new Engine();

        // Per the error-stack-accessor proposal, "stack" is an accessor property on Error.prototype,
        // not an own data property of the instance.
        engine.Evaluate(@"Error().hasOwnProperty('stack')").AsBoolean().Should().BeFalse();

        engine.Evaluate(@"Error.prototype.hasOwnProperty('stack')").AsBoolean().Should().BeTrue();

        var descriptorType = engine.Evaluate(
            @"typeof Object.getOwnPropertyDescriptor(Error.prototype, 'stack').get").AsString();
        descriptorType.Should().Be("function");

        // The inherited accessor still produces a string for an error instance.
        engine.Evaluate(@"typeof Error().stack").AsString().Should().Be("string");
    }

    private class Folder
    {
        public Folder Parent { get; set; }
        public string Name { get; set; }
    }

    [Fact]
    public void CallStackBuildingShouldSkipResolvingFromEngine()
    {
        var engine = new Engine(o => o.LimitRecursion(200));
        var recordedFolderTraversalOrder = new List<string>();
        engine.SetValue("log", new Action<object>(o => recordedFolderTraversalOrder.Add(o.ToString())));

        var folder = new Folder
        {
            Name = "SubFolder2",
            Parent = new Folder
            {
                Name = "SubFolder1",
                Parent = new Folder
                {
                    Name = "Root",
                    Parent = null,
                }
            }
        };

        engine.SetValue("folder", folder);

        var javaScriptException = Invoking(() =>
            engine.Execute(@"
                var Test = {
                    recursive: function(folderInstance) {
                        // Enabling the guard here corrects the problem, but hides the hard fault
                        // if (folderInstance==null) return null;
                        log(folderInstance.Name);
                    if (folderInstance==null) return null;
                        return this.recursive(folderInstance.parent);
                    }
                }

                Test.recursive(folder);"
            )).Should().ThrowExactly<JavaScriptException>().Which;

        javaScriptException.Message.Should().Be("Cannot read properties of null (reading 'Name')");
        EqualIgnoringNewLineDifferences(@"    at recursive (<anonymous>:6:44)
    at recursive (<anonymous>:8:32)
    at recursive (<anonymous>:8:32)
    at recursive (<anonymous>:8:32)
    at <anonymous>:12:17", javaScriptException.JavaScriptStackTrace);

        var expected = new List<string>
        {
            "SubFolder2", "SubFolder1", "Root"
        };
        recordedFolderTraversalOrder.Should().Equal(expected);
    }

    [Fact]
    public void StackTraceCollectedOnThreeLevels()
    {
        var engine = new Engine();
        const string script = @"var a = function(v) {
    return v.xxx.yyy;
}

var b = function(v) {
    return a(v);
}

var x = b(7);";

        var ex = Invoking(() => engine.Execute(script)).Should().ThrowExactly<JavaScriptException>().Which;

        const string expected = @"Error: Cannot read properties of undefined (reading 'yyy')
    at a (<anonymous>:2:18)
    at b (<anonymous>:6:12)
    at <anonymous>:9:9";

        EqualIgnoringNewLineDifferences(expected, ex.GetJavaScriptErrorString());
        ex.Location.Start.Line.Should().Be(2);
        ex.Location.Start.Column.Should().Be(17);
    }

    [Fact]
    public void StackTraceCollectedForImmediatelyInvokedFunctionExpression()
    {
        var engine = new Engine();
        const string script = @"function getItem(items, itemIndex) {
    var item = items[itemIndex];

    return item;
}

(function (getItem) {
    var items = null,
        item = getItem(items, 5)
        ;

    return item;
})(getItem);";

        var parsingOptions = new ScriptParsingOptions
        {
            CompileRegex = false,
            Tolerant = true
        };
        var ex = Invoking(() => engine.Execute(script, "get-item.js", parsingOptions)).Should().ThrowExactly<JavaScriptException>().Which;

        const string expected = @"Error: Cannot read properties of null (reading '5')
    at getItem (get-item.js:2:22)
    at (anonymous) (get-item.js:9:16)
    at get-item.js:13:2";

        EqualIgnoringNewLineDifferences(expected, ex.GetJavaScriptErrorString());

        ex.Location.Start.Line.Should().Be(2);
        ex.Location.Start.Column.Should().Be(21);
    }

    // Verify #1202
    [Fact]
    public void StackIsUnwoundWhenExceptionHandledByInteropCode()
    {
        var engine = new Engine()
            .SetValue("handle", new Action<Action>(Handler));

        const string Script = @"
function throwIt(message) {
    throw new Error(message);
}

handle(() => throwIt('e1'));
handle(() => throwIt('e2'));
handle(() => throwIt('e3'));
    
try {
    throwIt('e4');
} catch(x){
    x.stack; // return stack trace string
}
";
        var stack = engine.Evaluate(Script).AsString();
        EqualIgnoringNewLineDifferences(@"    at throwIt (<anonymous>:3:11)
    at <anonymous>:11:5", stack);

        static void Handler(Action callback)
        {
            try
            {
                callback();
            }
            catch (JavaScriptException)
            {
                // handle JS error
            }
        }
    }

    [Fact]
    public void StackTraceIsForOriginalException()
    {
        var engine = new Engine();
        engine.SetValue("HelloWorld", new HelloWorld());
        const string script = @"HelloWorld.ThrowException();";

        var ex = Invoking(() => engine.Execute(script)).Should().ThrowExactly<DivideByZeroException>().Which;

        const string expected = "HelloWorld";

        ContainsIgnoringNewLineDifferences(expected, ex.ToString());
    }

    [Fact]
    public void BubbledClrExceptionShouldExposeJavaScriptLocation()
    {
        var engine = new Engine();
        engine.SetValue("HelloWorld", new HelloWorld());
        const string script = @"// line 1
// line 2
HelloWorld.ThrowException();";

        var ex = Invoking(() => engine.Execute(script)).Should().ThrowExactly<DivideByZeroException>().Which;

        JintException.TryGetJavaScriptLocation(ex, out var location).Should().BeTrue();
        location.Start.Line.Should().Be(3);

        JintException.TryGetJavaScriptCallStack(ex, out var callStack).Should().BeTrue();
        callStack.Should().Contain("3:");

        // Original CLR identity preserved.
        ContainsIgnoringNewLineDifferences("HelloWorld", ex.ToString());
    }

    [Fact]
    public void BubbledClrExceptionLocationSurvivesNestedCalls()
    {
        var engine = new Engine();
        engine.SetValue("Thrower", typeof(Domain.Thrower));

        const string script = @"
function inner() {
    new Thrower().ThrowNotSupportedException();
}
function outer() { inner(); }
outer();";

        var ex = Invoking(() => engine.Execute(script)).Should().ThrowExactly<NotSupportedException>().Which;

        JintException.TryGetJavaScriptLocation(ex, out var location).Should().BeTrue();
        // Innermost JS site is the `new Thrower()...` call on line 3.
        location.Start.Line.Should().Be(3);
    }

    [Fact]
    public void BubbledClrExceptionFromConstructorExposesLocation()
    {
        var engine = new Engine();
        engine.SetValue("Ctor", typeof(ThrowingCtor));

        var ex = Invoking(() => engine.Execute(@"
new Ctor();")).Should().ThrowExactly<InvalidOperationException>().Which;

        JintException.TryGetJavaScriptLocation(ex, out var location).Should().BeTrue();
        location.Start.Line.Should().Be(2);
    }

    [Fact]
    public void BubbledClrExceptionFromGetterExposesLocation()
    {
        var engine = new Engine();
        engine.SetValue("instance", new ThrowingGetter());

        var ex = Invoking(() => engine.Execute(@"
var x = instance.Boom;")).Should().ThrowExactly<InvalidOperationException>().Which;

        JintException.TryGetJavaScriptLocation(ex, out var location).Should().BeTrue();
        location.Start.Line.Should().Be(2);
    }

    [Fact]
    public void TryGetJavaScriptLocationReturnsFalseForUnannotatedException()
    {
        var unrelated = new InvalidOperationException();
        JintException.TryGetJavaScriptLocation(unrelated, out _).Should().BeFalse();
        JintException.TryGetJavaScriptCallStack(unrelated, out _).Should().BeFalse();

        JintException.TryGetJavaScriptLocation(null, out _).Should().BeFalse();
        JintException.TryGetJavaScriptCallStack(null, out _).Should().BeFalse();
    }

    private sealed class ThrowingCtor
    {
        public ThrowingCtor()
        {
            throw new InvalidOperationException("ctor boom");
        }
    }

    private sealed class ThrowingGetter
    {
        public int Boom => throw new InvalidOperationException("getter boom");
    }

    [Theory]
    [InlineData("Error")]
    [InlineData("EvalError")]
    [InlineData("RangeError")]
    [InlineData("SyntaxError")]
    [InlineData("TypeError")]
    [InlineData("ReferenceError")]
    public void ErrorsHaveCorrectConstructor(string type)
    {
        var engine = new Engine();
        engine.Execute($"const o = new {type}();");
        engine.Evaluate($"o.constructor === {type}").AsBoolean().Should().BeTrue();
        engine.Evaluate("o.constructor.name").AsString().Should().Be(type);
    }

    [Fact]
    public void CallStackWorksWithRecursiveCalls()
    {
        static ScriptParsingOptions CreateParsingOptions()
        {
            return new ScriptParsingOptions
            {
                CompileRegex = false,
                Tolerant = true
            };
        }

        var e = Invoking(() =>
        {
            var engine = new Engine();

            engine.SetValue("executeFile", (Action<string>) (path =>
            {
                var content = path switch
                {
                    "first-file.js" => @"num = num * 3;
executeFile(""second-file.js"");",
                    "second-file.js" => @"// Intentionally making a mistake in the variable name
nuм -= 3;",
                    _ => throw new FileNotFoundException($"File '{path}' not exist.", path)
                };
                engine.Execute(content, path, CreateParsingOptions());
            }));
            engine.Execute(
                @"var num = 5;
executeFile(""first-file.js"");",
                "main-file.js",
                CreateParsingOptions()
            );
        }).Should().ThrowExactly<JavaScriptException>().Which;

        e.Message.Should().Be("nuм is not defined");

        const string Expected = @"    at delegate (second-file.js:2:1)
    at delegate (first-file.js:2:1)
    at main-file.js:2:1";
        EqualIgnoringNewLineDifferences(Expected, e.JavaScriptStackTrace);
    }

    [Fact]
    public void ShouldReportCorrectColumn()
    {
        var e = Invoking(() =>
        {
            var engine = new Engine();
            engine.Execute(@"var $variable1 = 611;
var _variable2 = 711;
var variable3 = 678;

$variable1 + -variable2 - variable3;");
        }).Should().ThrowExactly<JavaScriptException>().Which;

        e.Location.Start.Line.Should().Be(5);
        e.Location.Start.Column.Should().Be(14);
        e.JavaScriptStackTrace.Should().Be("    at <anonymous>:5:15");
    }

    [Fact]
    public void InvokingDelegateShouldContainJavascriptExceptionAsInnerException()
    {
        Delegate func = null;
        void SetFuncValue(Delegate scriptFunc) => func = scriptFunc;

        var engine = new Engine();
        engine.SetValue("SetFuncValue", SetFuncValue);
        engine.Execute("SetFuncValue(() => { foo.bar });");

        var ex = Invoking(() => func?.DynamicInvoke(JsValue.Undefined, Array.Empty<JsValue>())).Should().ThrowExactly<TargetInvocationException>().Which;

        var exception = ex.InnerException.Should().BeOfType<JavaScriptException>().Which;
        exception.Message.Should().Be("foo is not defined");
    }

    [Fact]
    public void JavaScriptExceptionLocationOnModuleShouldBeRight()
    {
        var engine = new Engine();
        engine.Modules.Add("my_module", @"
function throw_error(){
    throw Error(""custom error"")
}

throw_error();
            ");

        var ex= Invoking(() => engine.Modules.Import("my_module")).Should().ThrowExactly<JavaScriptException>().Which;
        3.Should().Be(ex.Location.Start.Line);
        10.Should().Be(ex.Location.Start.Column);
    }

    [Fact]
    public void ShouldApplySourceOffsetToErrorLocation()
    {
        var parsingOptions = ScriptParsingOptions.Default with
        {
            SourceOffset = Position.From(234, 36)
        };

        var e = Invoking(() =>
        {
            var engine = new Engine();
            engine.Execute("undeclaredVariable.property", "mapping-spec.json", parsingOptions);
        }).Should().ThrowExactly<JavaScriptException>().Which;

        e.Location.Start.Line.Should().Be(234);
        e.Location.SourceFile.Should().Be("mapping-spec.json");
    }

    [Fact]
    public void ShouldApplySourceOffsetToStackTrace()
    {
        var parsingOptions = ScriptParsingOptions.Default with
        {
            SourceOffset = Position.From(10, 5)
        };

        var e = Invoking(() =>
        {
            var engine = new Engine();
            engine.Execute("throw new Error('test error');", "test.json", parsingOptions);
        }).Should().ThrowExactly<JavaScriptException>().Which;

        e.Location.Start.Line.Should().Be(10);
        e.Location.Start.Column.Should().Be(11); // 5 (column offset) + 6 (position of 'new Error' after 'throw ')
        e.Location.SourceFile.Should().Be("test.json");
        ContainsIgnoringNewLineDifferences("test.json:10:", e.JavaScriptStackTrace!);
    }

    [Fact]
    public void ShouldApplySourceOffsetWithEvaluate()
    {
        var parsingOptions = ScriptParsingOptions.Default with
        {
            SourceOffset = Position.From(100, 0)
        };

        var e = Invoking(() =>
        {
            var engine = new Engine();
            engine.Evaluate("undeclaredVariable.property", "test.json", parsingOptions);
        }).Should().ThrowExactly<JavaScriptException>().Which;

        e.Location.Start.Line.Should().Be(100);
        e.Location.SourceFile.Should().Be("test.json");
    }

    [Fact]
    public void ShouldApplySourceOffsetWithPreparedScript()
    {
        var preparedScript = Engine.PrepareScript(
            "undeclaredVariable.property",
            source: "mapping-spec.json",
            options: new ScriptPreparationOptions
            {
                ParsingOptions = ScriptParsingOptions.Default with
                {
                    SourceOffset = Position.From(50, 10)
                }
            });

        var e = Invoking(() =>
        {
            var engine = new Engine();
            engine.Execute(preparedScript);
        }).Should().ThrowExactly<JavaScriptException>().Which;

        e.Location.Start.Line.Should().Be(50);
        e.Location.SourceFile.Should().Be("mapping-spec.json");
    }

    private static void EqualIgnoringNewLineDifferences(string expected, string actual)
    {
        expected = expected.Replace("\r\n", "\n");
        actual = actual.Replace("\r\n", "\n");
        actual.Should().Be(expected);
    }

    private static void ContainsIgnoringNewLineDifferences(string expectedSubstring, string actualString)
    {
        expectedSubstring = expectedSubstring.Replace("\r\n", "\n");
        actualString = actualString.Replace("\r\n", "\n");
        actualString.Should().Contain(expectedSubstring);
    }
}
