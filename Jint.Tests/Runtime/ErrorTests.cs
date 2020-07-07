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
            var script = @"
var a = {};

var b = a.user.name;
";

            var engine = new Engine();
            var e = Assert.Throws<JavaScriptException>(() => engine.Execute(script));
            Assert.Equal("Cannot read property 'name' of undefined", e.Message);
            Assert.Equal(4, e.Location.Start.Line);
            Assert.Equal(8, e.Location.Start.Column);
        }
        [Fact]
        public void CanReturnCorrectErrorMessageAndLocation1WithoutReferencedName()
        {
            var script = @"
var c = a(b().Length);
";

            var engine = new Engine();
            engine.SetValue("a", new Action<string>((a) =>
            {

            }));
            engine.SetValue("b", new Func<string>(() =>
            {
                return null;
            }));
            var e = Assert.Throws<JavaScriptException>(() => engine.Execute(script));
            Assert.Equal("Cannot read property 'Length' of null", e.Message);
            Assert.Equal(2, e.Location.Start.Line);
            Assert.Equal(10, e.Location.Start.Column);
        }

        [Fact]
        public void CanReturnCorrectErrorMessageAndLocation2()
        {
            var script = @"
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
            var engine = new Engine(options => options.LimitRecursion(1000));

            engine.Execute(@"var a = function(v) {
	return v.xxx.yyy;
}

var b = function(v) {
	return a(v);
}", new ParserOptions("custom.js") { Loc = true });

            var e = Assert.Throws<JavaScriptException>(() => engine.Execute("var x = b(7);", new ParserOptions("main.js") { Loc = true } ));
            Assert.Equal("Cannot read property 'yyy' of undefined", e.Message);
            Assert.Equal(2, e.Location.Start.Line);
            Assert.Equal(8, e.Location.Start.Column);
            Assert.Equal("custom.js", e.Location.Source);

            var stack = e.CallStack;
            Assert.Equal(@" at a(v) @ custom.js 8:6
 at b(7) @ main.js 8:1
".Replace("\r\n", "\n"), stack.Replace("\r\n", "\n"));
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
            Assert.Equal(@" at recursive(folderInstance.parent) @  31:8
 at recursive(folderInstance.parent) @  31:8
 at recursive(folderInstance.parent) @  31:8
 at recursive(folder) @  16:12
", javaScriptException.CallStack);

            var expected = new List<string>
            {
                "SubFolder2", "SubFolder1", "Root"
            };
            Assert.Equal(expected, recordedFolderTraversalOrder);
        }
    }
}