using System.Reflection;
using Jint.Native;
using Jint.Runtime;
using Jint.Tests.Runtime.TestClasses;

namespace Jint.Tests.Runtime;

public class ErrorTests
{
    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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


    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
    public void ThrowingErrorPrototypeDerivedObjectWithoutErrorDataSurfacesAsJavaScriptException()
    {
        // A hand-rolled subclass in the pre-ES6 style inherits the Error.prototype "stack" accessor but has
        // no [[ErrorData]], so the getter returns undefined. Building the host exception must not turn that
        // undefined into a CLR ArgumentException — it must fall through to the engine-built call stack.
        // (This is exactly how WPT's idlharness.js declares IdlHarnessError.)
        var engine = new Engine();
        engine.Execute(@"
function CustomError(message) { this.message = message; }
CustomError.prototype = Object.create(Error.prototype);
function thrower() { throw new CustomError('custom boom'); }
", "custom.js");

        var e = Invoking(() => engine.Execute("thrower();", "main.js")).Should().ThrowExactly<JavaScriptException>().Which;

        e.Message.Should().Be("custom boom");
        EqualIgnoringNewLineDifferences(@"    at thrower (custom.js:4:28)
    at main.js:1:1", e.JavaScriptStackTrace);

        // The engine-built stack is the host's alone: the thrown object has no [[ErrorData]], so the inherited
        // accessor still answers undefined, as V8 does for the same object (issue #4430).
        engine.Evaluate(@"
try { thrower(); } catch (err) { typeof err.stack; }").AsString().Should().Be("undefined");
    }

    [Test]
    public void ThrowingObjectWithNonStringStackPropertyKeepsAuthorValueAndDoesNotFault()
    {
        var engine = new Engine();

        var e = Invoking(() => engine.Execute("throw { message: 'plain', stack: 42 };", "main.js"))
            .Should().ThrowExactly<JavaScriptException>().Which;

        e.Message.Should().Be("plain");
        // The author's non-string "stack" is left untouched; the host side falls back to its own call stack.
        e.JavaScriptStackTrace.Should().Be("    at main.js:1:7");
        engine.Evaluate("(function(){ try { throw { stack: 42 }; } catch (x) { return x.stack; } })()").AsNumber().Should().Be(42);
    }

    /// <summary>
    /// A thrown value that is not an Error is left exactly as it was thrown, as V8 leaves it: no own "stack"
    /// appears on it, frozen or not, whether the throw is caught in script or crosses into the host. The call
    /// stack is the host's, through <see cref="JavaScriptException.JavaScriptStackTrace"/> (issue #4430).
    /// </summary>
    [TestCase("{ a: 1 }")]
    [TestCase("Object.freeze({ a: 1 })")]
    [TestCase("Object.seal({ a: 1 })")]
    [TestCase("Object.preventExtensions({ a: 1 })")]
    [TestCase("[1, 2]")]
    [TestCase("Object.create(Error.prototype)")]
    public void ThrownNonErrorObjectGainsNoStackProperty(string value)
    {
        var engine = new Engine();
        engine.Execute($"function thrower() {{ throw {value}; }}", "lib.js");

        engine.Evaluate("""
            (function () {
                try { thrower(); } catch (e) {
                    return JSON.stringify(Reflect.ownKeys(e)) + ' ' + (Object.getOwnPropertyDescriptor(e, 'stack') === undefined);
                }
            })()
            """).AsString().Should().Be(engine.Evaluate($"JSON.stringify(Reflect.ownKeys({value})) + ' true'").AsString());

        var e = Invoking(() => engine.Execute("thrower();", "main.js")).Should().ThrowExactly<JavaScriptException>().Which;
        e.Error.AsObject().HasOwnProperty("stack").Should().BeFalse();
        EqualIgnoringNewLineDifferences(@"    at thrower (lib.js:1:28)
    at main.js:1:1", e.JavaScriptStackTrace);
    }

    [Test]
    public void ThrownFrozenObjectStaysFrozenWithItsOwnKeysOnly()
    {
        var engine = new Engine();

        engine.Evaluate("""
            (function () {
                var o = Object.freeze({ a: 1 });
                function thrower() { throw o; }
                try { thrower(); } catch (e) {
                    return [e === o, Object.isFrozen(e), Reflect.ownKeys(e).join(), typeof e.stack].join(' ');
                }
            })()
            """).AsString().Should().Be("true true a undefined");
    }

    /// <summary>
    /// An error the engine raises itself carries its stack in [[ErrorData]], read through the inherited accessor
    /// rather than an own non-writable property, so a script can replace it as it can on any other error.
    /// </summary>
    [Test]
    public void EngineRaisedErrorKeepsItsStackInErrorDataAndTheScriptCanReplaceIt()
    {
        var engine = new Engine();

        engine.Execute("""
            var t;
            function read(o) { return o.x; }
            try { read(null); } catch (e) { t = e; }
            """, "main.js");

        engine.Evaluate("t instanceof TypeError").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getOwnPropertyDescriptor(t, 'stack') === undefined").AsBoolean().Should().BeTrue();
        engine.Evaluate("t.stack").AsString().Should().Contain("at read (main.js:2:");
        engine.Evaluate("'use strict'; t.stack = 'replaced'; t.stack").AsString().Should().Be("replaced");
    }

    [Test]
    public void ThrownErrorStillHasNoOwnStackProperty()
    {
        var engine = new Engine();

        engine.Evaluate("""
            (function () {
                function thrower() { throw new Error('boom'); }
                try { thrower(); } catch (e) {
                    return [typeof e.stack, Object.getOwnPropertyDescriptor(e, 'stack') === undefined, Reflect.ownKeys(e).join()].join(' ');
                }
            })()
            """).AsString().Should().Be("string true message");
    }

    /// <summary>
    /// The case the engine's weak table exists for: a plain object thrown through two function bodies, each of
    /// which raises a new exception on the way out, still reaches the host with the frames from where it was
    /// thrown, not only the outermost one.
    /// </summary>
    [Test]
    public void ThrownNonErrorObjectKeepsItsInnermostStackThroughTwoFunctions()
    {
        var engine = new Engine();
        engine.Execute("""
            function inner() { throw { a: 1 }; }
            function outer() { inner(); }
            """, "lib.js");

        var e = Invoking(() => engine.Execute("outer();", "main.js")).Should().ThrowExactly<JavaScriptException>().Which;

        EqualIgnoringNewLineDifferences(@"    at inner (lib.js:1:26)
    at outer (lib.js:2:20)
    at main.js:1:1", e.JavaScriptStackTrace);
    }

    /// <summary>
    /// An Error whose <c>stack</c> does not read as a string keeps the stack recorded where it was thrown, rather
    /// than rebuilding it at each function boundary with fewer frames each time.
    /// </summary>
    [TestCase("delete Error.prototype.stack; function make() { return new Error('boom'); }")]
    [TestCase("function make() { var e = new Error('boom'); Object.defineProperty(e, 'stack', { value: 42 }); return e; }")]
    [TestCase("function make() { return Object.setPrototypeOf(new Error('boom'), Object.prototype); }")]
    public void ErrorWithoutAStringStackKeepsItsInnermostStackThroughTwoFunctions(string make)
    {
        var engine = new Engine();
        engine.Execute(make, "make.js");
        engine.Execute("""
            function inner() { throw make(); }
            function outer() { inner(); }
            """, "lib.js");

        var e = Invoking(() => engine.Execute("outer();", "main.js")).Should().ThrowExactly<JavaScriptException>().Which;

        EqualIgnoringNewLineDifferences(@"    at inner (lib.js:1:26)
    at outer (lib.js:2:20)
    at main.js:1:1", e.JavaScriptStackTrace);
    }

    private class Folder
    {
        public Folder Parent { get; set; }
        public string Name { get; set; }
    }

    [Test]
    public void CallStackBuildingShouldSkipResolvingFromEngine()
    {
        var engine = new Engine(o => o.Constraints.MaxRecursionDepth = 200);
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

    [Test]
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

    [Test]
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
    [Test]
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

    [Test]
    public void StackTraceIsForOriginalException()
    {
        var engine = new Engine();
        engine.SetValue("HelloWorld", new HelloWorld());
        const string script = @"HelloWorld.ThrowException();";

        var ex = Invoking(() => engine.Execute(script)).Should().ThrowExactly<DivideByZeroException>().Which;

        const string expected = "HelloWorld";

        ContainsIgnoringNewLineDifferences(expected, ex.ToString());
    }

    [Test]
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

    [Test]
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

    [Test]
    public void BubbledClrExceptionFromConstructorExposesLocation()
    {
        var engine = new Engine();
        engine.SetValue("Ctor", typeof(ThrowingCtor));

        var ex = Invoking(() => engine.Execute(@"
new Ctor();")).Should().ThrowExactly<InvalidOperationException>().Which;

        JintException.TryGetJavaScriptLocation(ex, out var location).Should().BeTrue();
        location.Start.Line.Should().Be(2);
    }

    [Test]
    public void BubbledClrExceptionFromGetterExposesLocation()
    {
        var engine = new Engine();
        engine.SetValue("instance", new ThrowingGetter());

        var ex = Invoking(() => engine.Execute(@"
var x = instance.Boom;")).Should().ThrowExactly<InvalidOperationException>().Which;

        JintException.TryGetJavaScriptLocation(ex, out var location).Should().BeTrue();
        location.Start.Line.Should().Be(2);
    }

    [Test]
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

    [TestCase("Error")]
    [TestCase("EvalError")]
    [TestCase("RangeError")]
    [TestCase("SyntaxError")]
    [TestCase("TypeError")]
    [TestCase("ReferenceError")]
    public void ErrorsHaveCorrectConstructor(string type)
    {
        var engine = new Engine();
        engine.Execute($"const o = new {type}();");
        engine.Evaluate($"o.constructor === {type}").AsBoolean().Should().BeTrue();
        engine.Evaluate("o.constructor.name").AsString().Should().Be(type);
    }

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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
