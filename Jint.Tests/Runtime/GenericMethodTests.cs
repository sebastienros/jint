using Jint.Native;
using System;
using System.Dynamic;
using Jint.Runtime.Interop;
using Xunit;

using Xunit.Abstractions;
using System.IO;
using System.Text;

namespace Jint.Tests.Runtime
{
    public class GenericMethodTests : IDisposable
    {
        public GenericMethodTests()
        {
        }

        void IDisposable.Dispose()
        {
        }

        private void RunTest(string source)
        {
        }


        [Fact]
        public void TestGeneric()
        {
            var engine = new Engine(cfg => cfg.AllowOperatorOverloading());
            //engine.SetValue("Class1", TypeReference.CreateTypeReference<Class1>(engine));
            engine.SetValue("TestGenericBaseClass", typeof(TestGenericBaseClass<>));
            engine.SetValue("TestGenericClass", typeof(TestGenericClass));

            engine.Execute(@"
                var testGeneric = new TestGenericClass();
                testGeneric.Bar('testing testing 1 2 3');
                testGeneric.Foo('hello world');
                testGeneric.Add('blah');
            ");

            Assert.Equal(true, TestGenericClass.BarInvoked);
            Assert.Equal(true, TestGenericClass.FooInvoked);
        }

        [Fact]
        public void TestGeneric2()
        {
            var engine = new Engine(cfg => cfg.AllowOperatorOverloading());
            var testGenericObj = new TestGenericClass();
            engine.SetValue("testGenericObj", testGenericObj);

            engine.Execute(@"
                testGenericObj.Bar('testing testing 1 2 3');
                testGenericObj.Foo('hello world');
                testGenericObj.Add('blah');
            ");

            Assert.Equal(1, testGenericObj.Count);
        }

        [Fact]
        public void TestFancyGenericPass()
        {
            var engine = new Engine(cfg => cfg.AllowOperatorOverloading());
            var testGenericObj = new TestGenericClass();
            engine.SetValue("testGenericObj", testGenericObj);

            engine.Execute(@"
                testGenericObj.Fancy('test', 42, 'foo');
            ");

            Assert.Equal(true, testGenericObj.FancyInvoked);
        }

        [Fact]
        public void TestFancyGenericFail()
        {
            var engine = new Engine(cfg => cfg.AllowOperatorOverloading());
            var testGenericObj = new TestGenericClass();
            engine.SetValue("testGenericObj", testGenericObj);

            var argException = Assert.Throws< System.ArgumentException>(() =>
            {
                engine.Execute(@"
                    testGenericObj.Fancy('test', 'foo', 42);
                ");
            });

            Assert.Equal("Object of type 'System.String' cannot be converted to type 'System.Double'.", argException.Message);
        }

        public class TestGenericBaseClass<T>
		{
            private System.Collections.Generic.List<T> _list = new System.Collections.Generic.List<T>();

            public int Count
			{
                get { return _list.Count; }
			}

            public void Add(T t)
			{
                _list.Add(t);
			}
		}

        public class TestGenericClass : TestGenericBaseClass<string>
        {
            public static bool BarInvoked
            {
                get;
                private set;
            }
            public static bool FooInvoked
            {
                get;
                private set;
            }
            public bool FancyInvoked
            {
                get;
                private set;
            }

            public TestGenericClass()
			{
                BarInvoked = false;
                FooInvoked = false;
                FancyInvoked = false;

            }

            public void Bar(string text)
            {
                System.Console.WriteLine("TestGenericClass: Bar: text: " + text);
                BarInvoked = true;
            }

            public void Foo<T>(T t)
            {
                System.Console.WriteLine("TestGenericClass: Foo: t: " + t);
                FooInvoked = true;
            }

            public void Fancy<T,U>(T t1, U u, T t2)
			{
                System.Console.WriteLine("TestGenericClass: FancyInvoked: t1: " + t1 + "u: " + u + " t2: " + t2);
                FancyInvoked = true;
            }
        }
    }
}
