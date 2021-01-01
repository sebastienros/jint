using Esprima;
using Jint.Runtime;
using System;
using System.Collections.Generic;
using Xunit;

namespace Jint.Tests.Runtime
{
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
        public void CanProduceCorrectStackTrace()
        {
            var engine = new Engine();

            engine.Execute(@"var a = function(v) {
    return v.xxx.yyy;
}

var b = function(v) {
	return a(v);
}", new ParserOptions("custom.js") { Loc = true });

            var e = Assert.Throws<JavaScriptException>(() => engine.Execute("var x = b(7);", new ParserOptions("main.js") { Loc = true } ));
            Assert.Equal("Cannot read property 'yyy' of undefined", e.Message);
            Assert.Equal(2, e.Location.Start.Line);
            Assert.Equal(17, e.Location.Start.Column);
            Assert.Equal("custom.js", e.Location.Source);

            var stack = e.StackTrace;
            EqualIgnoringNewLineDifferences(@"   at a (v) custom.js:2:18
   at b (v) custom.js:6:9
   at main.js:1:9", stack);
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
                        Name = "Root", Parent = null,
                    }
                }
            };

            engine.SetValue("folder", folder);

            var javaScriptException =  Assert.Throws<JavaScriptException>(() =>
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
   at <anonymous>:12:17", javaScriptException.StackTrace);

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

            const string expected = @"Jint.Runtime.JavaScriptException: Cannot read property 'yyy' of undefined
   at a (v) <anonymous>:2:18
   at b (v) <anonymous>:6:12
   at <anonymous>:9:9";
            
            EqualIgnoringNewLineDifferences(expected, ex.ToString());
        }

        private static void EqualIgnoringNewLineDifferences(string expected, string actual)
        {
            expected = expected.Replace("\r\n", "\n");
            actual = actual.Replace("\r\n", "\n");
            Assert.Equal(expected, actual);
        }
    }
}