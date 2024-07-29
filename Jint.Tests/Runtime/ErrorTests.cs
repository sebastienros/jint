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
        var e = Assert.Throws<JavaScriptException>(() => engine.Execute(script));
        Assert.Equal("Cannot read property 'name' of undefined", e.Message);
        Assert.Equal(4, e.Location.Start.Line);
        Assert.Equal(15, e.Location.Start.Column);
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
        var e = Assert.Throws<JavaScriptException>(() => engine.Execute(script));
        Assert.Equal("Cannot read property 'Length' of null", e.Message);
        Assert.Equal(2, e.Location.Start.Line);
        Assert.Equal(14, e.Location.Start.Column);
    }

    [Fact]
    public void CanReturnCorrectErrorMessageAndLocation2()
    {
        const string script = @"
 test();
";

        var engine = new Engine();
        var e = Assert.Throws<JavaScriptException>(() => engine.Execute(script));
        Assert.Equal("test is not defined", e.Message);
        Assert.Equal(2, e.Location.Start.Line);
        Assert.Equal(1, e.Location.Start.Column);
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

        var e = Assert.Throws<JavaScriptException>(() => engine.Execute("var x = b(7);", "main.js"));
        Assert.Equal("Cannot read property 'yyy' of undefined", e.Message);
        Assert.Equal(3, e.Location.Start.Line);
        Assert.Equal(15, e.Location.Start.Column);
        Assert.Equal("custom.js", e.Location.SourceFile);

        var stack = e.JavaScriptStackTrace;
        EqualIgnoringNewLineDifferences(@"   at a (v) custom.js:3:16
   at b (v) custom.js:7:10
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

        var e = Assert.Throws<JavaScriptException>(() => engine.Execute("var x = b(7);", "main.js"));
        Assert.Equal("Error thrown from script", e.Message);
        Assert.Equal(3, e.Location.Start.Line);
        Assert.Equal(8, e.Location.Start.Column);
        Assert.Equal("custom.js", e.Location.SourceFile);

        var stack = e.JavaScriptStackTrace;
        EqualIgnoringNewLineDifferences(@"   at a (v) custom.js:3:9
   at b (v) custom.js:7:10
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
        EqualIgnoringNewLineDifferences(@"   at a (v) custom.js:3:10
   at b (v) custom.js:7:10
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
        EqualIgnoringNewLineDifferences(@"   at a (v) custom.js:4:11
   at b (v) custom.js:11:10
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
        EqualIgnoringNewLineDifferences(@"   at a (v) custom.js:4:13
   at b (v) custom.js:11:10
   at main.js:1:1", stack);
    }

    [Fact]
    public void ErrorObjectHasOwnPropertyStack()
    {
        var res = new Engine().Evaluate(@"Error().hasOwnProperty('stack')").AsBoolean();
        Assert.True(res);
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

        var javaScriptException = Assert.Throws<JavaScriptException>(() =>
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
            ));

        Assert.Equal("Cannot read property 'Name' of null", javaScriptException.Message);
        EqualIgnoringNewLineDifferences(@"   at recursive (folderInstance) <anonymous>:6:44
   at recursive (folderInstance) <anonymous>:8:32
   at recursive (folderInstance) <anonymous>:8:32
   at recursive (folderInstance) <anonymous>:8:32
   at <anonymous>:12:17", javaScriptException.JavaScriptStackTrace);

        var expected = new List<string>
        {
            "SubFolder2", "SubFolder1", "Root"
        };
        Assert.Equal(expected, recordedFolderTraversalOrder);
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

        var ex = Assert.Throws<JavaScriptException>(() => engine.Execute(script));

        const string expected = @"Error: Cannot read property 'yyy' of undefined
   at a (v) <anonymous>:2:18
   at b (v) <anonymous>:6:12
   at <anonymous>:9:9";

        EqualIgnoringNewLineDifferences(expected, ex.GetJavaScriptErrorString());
        Assert.Equal(2, ex.Location.Start.Line);
        Assert.Equal(17, ex.Location.Start.Column);
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
        var ex = Assert.Throws<JavaScriptException>(() => engine.Execute(script, "get-item.js", parsingOptions));

        const string expected = @"Error: Cannot read property '5' of null
   at getItem (items, itemIndex) get-item.js:2:22
   at (anonymous) (getItem) get-item.js:9:16
   at get-item.js:13:2";

        EqualIgnoringNewLineDifferences(expected, ex.GetJavaScriptErrorString());

        Assert.Equal(2, ex.Location.Start.Line);
        Assert.Equal(21, ex.Location.Start.Column);
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
        EqualIgnoringNewLineDifferences(@"   at throwIt (message) <anonymous>:3:11
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

        var ex = Assert.Throws<DivideByZeroException>(() => engine.Execute(script));

        const string expected = "HelloWorld";

        ContainsIgnoringNewLineDifferences(expected, ex.ToString());
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
        Assert.True(engine.Evaluate($"o.constructor === {type}").AsBoolean());
        Assert.Equal(type, engine.Evaluate("o.constructor.name").AsString());
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

        var e = Assert.Throws<JavaScriptException>(() =>
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
        });

        Assert.Equal("nuм is not defined", e.Message);

        const string Expected = @"   at delegate second-file.js:2:1
   at delegate first-file.js:2:1
   at main-file.js:2:1";
        EqualIgnoringNewLineDifferences(Expected, e.JavaScriptStackTrace);
    }

    [Fact]
    public void ShouldReportCorrectColumn()
    {
        var e = Assert.Throws<JavaScriptException>(() =>
        {
            var engine = new Engine();
            engine.Execute(@"var $variable1 = 611;
var _variable2 = 711;
var variable3 = 678;

$variable1 + -variable2 - variable3;");
        });

        Assert.Equal(5, e.Location.Start.Line);
        Assert.Equal(14, e.Location.Start.Column);
        Assert.Equal("   at <anonymous>:5:15", e.JavaScriptStackTrace);
    }

    [Fact]
    public void InvokingDelegateShouldContainJavascriptExceptionAsInnerException()
    {
        Delegate func = null;
        void SetFuncValue(Delegate scriptFunc) => func = scriptFunc;

        var engine = new Engine();
        engine.SetValue("SetFuncValue", SetFuncValue);
        engine.Execute("SetFuncValue(() => { foo.bar });");

        var ex = Assert.Throws<TargetInvocationException>(() => func?.DynamicInvoke(JsValue.Undefined, Array.Empty<JsValue>()));

        var exception = Assert.IsType<JavaScriptException>(ex.InnerException);
        Assert.Equal("foo is not defined", exception.Message);
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

        var ex= Assert.Throws<JavaScriptException>(() => engine.Modules.Import("my_module"));
        Assert.Equal(ex.Location.Start.Line, 3);
        Assert.Equal(ex.Location.Start.Column, 10);
    }

    private static void EqualIgnoringNewLineDifferences(string expected, string actual)
    {
        expected = expected.Replace("\r\n", "\n");
        actual = actual.Replace("\r\n", "\n");
        Assert.Equal(expected, actual);
    }

    private static void ContainsIgnoringNewLineDifferences(string expectedSubstring, string actualString)
    {
        expectedSubstring = expectedSubstring.Replace("\r\n", "\n");
        actualString = actualString.Replace("\r\n", "\n");
        Assert.Contains(expectedSubstring, actualString);
    }
}