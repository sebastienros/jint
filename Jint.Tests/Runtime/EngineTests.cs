using System;
using System.IO;
using System.Reflection;
using Jint.Runtime;
using Xunit;
using Xunit.Extensions;

namespace Jint.Tests.Runtime
{
    public class EngineTests
    {
        private Engine RunTest(string source)
        {
            var engine = new Engine(cfg => cfg
                .WithDelegate("log", new Action<object>(Console.WriteLine))
                .WithDelegate("assert", new Action<bool>(Assert.True))
            );

            engine.Execute(source);

            return engine;
        }

        [Theory]
        [InlineData("Scratch.js")]
        public void ShouldInterpretScriptFile(string file)
        {
            const string prefix = "Jint.Tests.Runtime.Scripts.";

            var assembly = Assembly.GetExecutingAssembly();
            var scriptPath = prefix + file;

            using (var stream = assembly.GetManifestResourceStream(scriptPath))
                if (stream != null)
                    using (var sr = new StreamReader(stream))
                    {
                        var source = sr.ReadToEnd();
                        RunTest(source);
                    }
        }


        [Theory]
        [InlineData(42d, "42")]
        [InlineData("Hello", "'Hello'")]
        public void ShouldInterpretLiterals(object expected, string source)
        {
            var interpreter = new Engine();
            interpreter.Execute(source);
            
            Assert.Equal(expected, interpreter.Result);
        }

        [Fact]
        public void ShouldInterpretVariableDeclaration()
        {
            var interpreter = new Engine();
            interpreter.Execute("var foo = 'bar'; foo;");

            Assert.Equal("bar", interpreter.Result);
        }

        [Theory]
        [InlineData(4d, "1 + 3")]
        [InlineData(-2d, "1 - 3")]
        [InlineData(3d, "1 * 3")]
        [InlineData(2d, "6 / 3")]
        public void ShouldInterpretBinaryExpression(double expected, string source)
        {
            var interpreter = new Engine();
            interpreter.Execute(source);

            Assert.Equal(expected, interpreter.Result);
        }

        [Fact]
        public void ShouldEvaluateHasOwnProperty()
        {
            var engine = RunTest(@"
                var x = {};
                x.Bar = 42;
                assert(x.hasOwnProperty('Bar'));
            ");
        }

        [Fact]
        public void FunctionConstructorsShouldCreateNewObjects()
        {
            var engine = RunTest(@"
                var Vehicle = function () {};
                var vehicle = new Vehicle();
                assert(vehicle != undefined);
            ");
        }

        [Fact]
        public void NewObjectsInheritFunctionConstructorProperties()
        {
            var engine = RunTest(@"
                var Vehicle = function () {};
                var vehicle = new Vehicle();
                Vehicle.prototype.wheelCount = 4;
                assert(vehicle.wheelCount == 4);
                assert((new Vehicle()).wheelCount == 4);
            ");
        }

        [Fact]
        public void NewObjectsShouldUsePrivateProperties()
        {
            var engine = RunTest(@"
                var Vehicle = function (color) {
                    this.color = color;
                };
                var vehicle = new Vehicle('tan');
                assert(vehicle.color == 'tan');
            ");
        }

        [Fact]
        public void FunctionConstructorsShouldDefinePrototypeChain()
        {
            var engine = RunTest(@"
                function Vehicle() {};
                var vehicle = new Vehicle();
                assert(vehicle.hasOwnProperty('constructor') == false);
            ");
        }

        [Fact]
        public void NewObjectsConstructorIsObject()
        {
            var engine = RunTest(@"
                var o = new Object();
                assert(o instanceof Object);
                assert(o.constructor == Object);
            ");
        }

        [Fact]
        public void NewObjectsConstructorShouldBeConstructorObject()
        {
            var engine = RunTest(@"
                var Vehicle = function () {};
                var vehicle = new Vehicle();
                assert(vehicle.constructor == Vehicle);
            ");
        }

        [Fact]
        public void NewObjectsIntanceOfConstructorObject()
        {
            var engine = RunTest(@"
                var Vehicle = function () {};
                var vehicle = new Vehicle();
                assert(vehicle instanceof Vehicle);
            ");
        }

        [Fact]
        public void ShouldEvaluateForLoops()
        {
            var engine = RunTest(@"
                var foo = 0;
                for (var i = 0; i < 5; i++) {
                    foo += i;
                }
                assert(foo == 10);
            ");
        }

        [Fact]
        public void ShouldEvaluateRecursiveFunctions()
        {
            var engine = RunTest(@"
                function fib(n) {
                    if (n < 2) {
                        return n;
                    }
                    return fib(n - 1) + fib(n - 2);
                }
                var result = fib(6);
                assert(result == 8);
            ");
        }

        [Fact]
        public void ShouldAccessObjectProperties()
        {
            var engine = RunTest(@"
                var o = {};
                o.Foo = 'bar';
                o.Baz = 42;
                o.Blah = o.Foo + o.Baz;
                assert(o.Blah == 'bar42');
            ");
        }


        [Fact]
        public void ShouldConstructArray()
        {
            var engine = RunTest(@"
                var o = [];
                assert(o.length == 0);
            ");
        }

        [Fact]
        public void ArrayPushShouldIncrementLength()
        {
            var engine = RunTest(@"
                var o = [];
                o.push(1);
                assert(o.length == 1);
            ");
        }

        [Fact]
        public void ArrayConstructor()
        {
            var engine = RunTest(@"
                var o = [];
                assert(o.constructor == Array);
                assert(o.hasOwnProperty('constructor') == false);
            ");
        }
        /*
                        [Fact]
                        public void ()
                        {
                            var engine = RunTest(@"
                            ");
                        }
                */

    }
}
