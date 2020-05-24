﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class AsyncTests
    {
        public class TestMethods
        {
            public class TestClass
            {
                public string Name { get; set; }
            }

            public class TestStruct
            {
                public string Name { get; set; }
            }

            public static Dictionary<string, object> ExpectedMethodResults = new Dictionary<string, object>
            {
                { nameof(GetAsyncObject), nameof(GetAsyncObject) },
                { nameof(GetAsyncVoid), null },
                { nameof(GetSynchronousObject), nameof(GetSynchronousObject) },
                { nameof(GetSynchronousVoid), null },
                { nameof(GetAsyncDouble), 42d },
                { nameof(GetAsyncTestClass), new TestClass { Name = "MyTestClass" } },
                { nameof(GetAsyncTestStruct), new TestStruct { Name = "MyTestStruct" } },
                { nameof(GetAsyncDate), DateTime.Parse("1111-11-11 11:11:11").ToUniversalTime() },
                { nameof(GetAsyncEcho), "Echo..." }
            };

            private const int _delay = 50;

            public string MethodInvoked { get; private set; }

            public async Task<DateTime> GetAsyncDate()
            {
                await Task.Delay(_delay).ConfigureAwait(true);
                MethodInvoked = nameof(GetAsyncDate);
                return (DateTime)ExpectedMethodResults[MethodInvoked];
            }

            public async Task<double> GetAsyncDouble()
            {
                await Task.Delay(_delay).ConfigureAwait(true);
                MethodInvoked = nameof(GetAsyncDouble);
                return (double)ExpectedMethodResults[MethodInvoked];
            }

            public async Task<string> GetAsyncEcho(string echo = "Echo...")
            {
                await Task.Delay(_delay).ConfigureAwait(true);
                MethodInvoked = nameof(GetAsyncEcho);
                return (string)ExpectedMethodResults[MethodInvoked];
            }

            public async Task<object> GetAsyncObject()
            {
                await Task.Delay(_delay).ConfigureAwait(true);
                MethodInvoked = nameof(GetAsyncObject);
                return (object)ExpectedMethodResults[MethodInvoked];
            }

            public async Task<TestClass> GetAsyncTestClass()
            {
                await Task.Delay(_delay).ConfigureAwait(true);
                MethodInvoked = nameof(GetAsyncTestClass);
                return (TestClass)ExpectedMethodResults[MethodInvoked];
            }

            public async Task<TestStruct> GetAsyncTestStruct()
            {
                await Task.Delay(_delay).ConfigureAwait(true);
                MethodInvoked = nameof(GetAsyncTestStruct);
                return (TestStruct)ExpectedMethodResults[MethodInvoked];
            }

            public async Task GetAsyncVoid()
            {
                await Task.Delay(_delay);
                MethodInvoked = nameof(GetAsyncVoid);
            }

            public object GetSynchronousObject()
            {
                Task.Delay(_delay).Wait();
                MethodInvoked = nameof(GetSynchronousObject);
                return ExpectedMethodResults[MethodInvoked];
            }

            public void GetSynchronousVoid()
            {
                Task.Delay(_delay).Wait();
                MethodInvoked = nameof(GetSynchronousVoid);
            }
        }

        [Fact]
        public async void ClrMethodHandlesImmediateExceptions()
        {
            var engine = new Engine();

            var testMethods = new TestMethods();
            engine.SetValue("badMethod", new Func<Task>(() => throw new Exception("MyException")));

            var script = "badMethod();";

            await Assert.ThrowsAsync<Exception>(async () =>
            {
                var result = (await engine.ExecuteAsync(script))
                   .GetCompletionValue().ToObject();
            });
        }

        [Fact]
        public async void ClrMethodHandlesLateExceptions()
        {
            var engine = new Engine();

            var testMethods = new TestMethods();
            engine.SetValue("badMethod", new Func<Task>(async () =>
            {
                await Task.Delay(200);
                throw new Exception("MyException");
            }));

            var script = "badMethod();";

            await Assert.ThrowsAsync<Exception>(async () =>
            {
                var result = (await engine.ExecuteAsync(script))
                   .GetCompletionValue().ToObject();
            });
        }

        [Fact]
        public async void ClrMethodAreAwaited()
        {
            var engine = new Engine();

            var testMethods = new TestMethods();
            engine.SetValue("testMethods", testMethods);

            var methodNames = TestMethods.ExpectedMethodResults.Keys;

            foreach (var methodName in methodNames)
            {
                var script = $"testMethods.{methodName}();";
                var result = (await engine.ExecuteAsync(script))
                    .GetCompletionValue().ToObject();

                var expected = TestMethods.ExpectedMethodResults[methodName];
                Assert.Equal(expected, result);
                Assert.Equal(methodName, testMethods.MethodInvoked);
            }
        }

        [Fact]
        public async void ClrMethodAssignmentsAreAwaited()
        {
            var engine = new Engine();

            var testMethods = new TestMethods();
            engine.SetValue("testMethods", testMethods);

            var methodNames = TestMethods.ExpectedMethodResults.Keys;

            foreach (var methodName in methodNames)
            {
                var script = $"var result = testMethods.{methodName}(); return result;";
                var result = (await engine.ExecuteAsync(script))
                    .GetCompletionValue().ToObject();

                var expected = TestMethods.ExpectedMethodResults[methodName];
                Assert.Equal(expected, result);
                Assert.Equal(methodName, testMethods.MethodInvoked);
            }
        }

        [Fact]
        public async void ClrMethodAssignToIdentifierIsAwaited()
        {
            var engine = new Engine();

            var testMethods = new TestMethods();
            engine.SetValue("testMethods", testMethods);

            var methodNames = TestMethods.ExpectedMethodResults.Keys;

            foreach (var methodName in methodNames)
            {
                var script = $@"
                    var result = null;
                    result = testMethods.{methodName}();
                    return result;
                ";
                var result = (await engine.ExecuteAsync(script))
                    .GetCompletionValue().ToObject();

                var expected = TestMethods.ExpectedMethodResults[methodName];
                Assert.Equal(expected, result);
                Assert.Equal(methodName, testMethods.MethodInvoked);
            }
        }

        [Fact]
        public async void ClrMethodAsFunctionArgumentIsAwaited()
        {
            var engine = new Engine();

            var testMethods = new TestMethods();
            engine.SetValue("testMethods", testMethods);

            var methodNames = TestMethods.ExpectedMethodResults.Keys;

            foreach (var methodName in methodNames)
            {
                var script = $@"
                    function echo(arg) {{
                        return arg;
                    }}
                    var result = echo(testMethods.{methodName}());
                    return result;
                ";
                var result = (await engine.ExecuteAsync(script))
                    .GetCompletionValue().ToObject();

                var expected = TestMethods.ExpectedMethodResults[methodName];
                Assert.Equal(expected, result);
                Assert.Equal(methodName, testMethods.MethodInvoked);
            }
        }

        [Fact]
        public async void DelegatesAreAwaited()
        {
            var engine = new Engine();

            var testMethods = new TestMethods();
            engine.SetValue(nameof(TestMethods.GetAsyncDouble), new Func<Task<double>>(async () => await testMethods.GetAsyncDouble()))
                .SetValue(nameof(TestMethods.GetAsyncObject), new Func<Task<object>>(async () => await testMethods.GetAsyncObject()))
                .SetValue(nameof(TestMethods.GetAsyncVoid), new Func<Task>(async () => await testMethods.GetAsyncVoid()))
                .SetValue(nameof(TestMethods.GetSynchronousObject), new Func<object>(() => testMethods.GetSynchronousObject()))
                .SetValue(nameof(TestMethods.GetSynchronousVoid), new Action(() => testMethods.GetSynchronousVoid()))
                .SetValue(nameof(TestMethods.GetAsyncTestClass), new Func<Task<TestMethods.TestClass>>(async () => await testMethods.GetAsyncTestClass()))
                .SetValue(nameof(TestMethods.GetAsyncTestStruct), new Func<Task<TestMethods.TestStruct>>(async () => await testMethods.GetAsyncTestStruct()))
                .SetValue(nameof(TestMethods.GetAsyncDate), new Func<Task<DateTime>>(async () => await testMethods.GetAsyncDate()))
                .SetValue(nameof(TestMethods.GetAsyncEcho), new Func<string, Task<string>>(async s => await testMethods.GetAsyncEcho(s)));

            var methodNames = TestMethods.ExpectedMethodResults.Keys;

            foreach (var methodName in methodNames)
            {
                var script = $"{methodName}()";
                var result = (await engine.ExecuteAsync(script))
                    .GetCompletionValue().ToObject();

                var expected = TestMethods.ExpectedMethodResults[methodName];
                Assert.Equal(expected, result);
                Assert.Equal(methodName, testMethods.MethodInvoked);
            }
        }
    }
}