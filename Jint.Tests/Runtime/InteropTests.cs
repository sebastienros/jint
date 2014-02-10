using System;
using System.Runtime.Remoting.Messaging;
using Jint.Native;
using Jint.Native.Object;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class InteropTests : IDisposable
    {
        private readonly Engine _engine;

        public InteropTests()
        {
            _engine = new Engine()
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(Assert.True))
                ;
        }

        void IDisposable.Dispose()
        {
        }

        private void RunTest(string source)
        {
            _engine.Execute(source);
        }

        [Fact]
        public void PrimitiveTypesCanBeSet()
        {
            _engine.SetValue("x", 10);
            _engine.SetValue("y", true);
            _engine.SetValue("z", "foo");

            RunTest(@"
                assert(x === 10);
                assert(y === true);
                assert(z === 'foo');
            ");
        }

        [Fact]
        public void DelegatesCanBeSet()
        {
            _engine.SetValue("square", new Func<double, double>(x => x * x));

            RunTest(@"
                assert(square(10) === 100);
            ");
        }

        [Fact]
        public void CanGetObjectProperties()
        {
            var p = new Person
            {
                Name = "Mickey Mouse"
            };

            _engine.SetValue("p", p);

            RunTest(@"
                assert(p.Name === 'Mickey Mouse');
            ");
        }

        [Fact]
        public void CanInvokeObjectMethods()
        {
            var p = new Person
            {
                Name = "Mickey Mouse"
            };

            _engine.SetValue("p", p);

            RunTest(@"
                assert(p.ToString() === 'Mickey Mouse');
            ");
        }

        [Fact]
        public void CanSetObjectProperties()
        {
            var p = new Person
            {
                Name = "Mickey Mouse"
            };

            _engine.SetValue("p", p);

            RunTest(@"
                p.Name = 'Donald Duck';
                assert(p.Name === 'Donald Duck');
            ");

            Assert.Equal("Donald Duck", p.Name);
        }

        [Fact]
        public void CanAccessAnonymousObject()
        {
            var p = new
            {
                Name = "Mickey Mouse",
            };

            _engine.SetValue("p", p);

            RunTest(@"
                assert(p.Name === 'Mickey Mouse');
            ");
        }

        [Fact]
        public void CanAccessAnonymousObjectProperties()
        {
            var p = new
            {
                Address = new
                {
                    City = "Mouseton"
                }
            };

            _engine.SetValue("p", p);

            RunTest(@"
                assert(p.Address.City === 'Mouseton');
            ");
        }

        [Fact]
        public void PocosCanReturnJsValueDirectly()
        {
            var o = new
            {
                x = new JsValue(1),
                y = new JsValue("string"),
            };

            _engine.SetValue("o", o);

            RunTest(@"
                assert(o.x === 1);
                assert(o.y === 'string');
            ");
        }

        [Fact]
        public void PocosCanReturnObjectInstanceDirectly()
        {
            var x = new ObjectInstance(_engine) { Extensible = true};
            x.Put("foo", new JsValue("bar"), false);

            var o = new
            {
                x
            };

            _engine.SetValue("o", o);

            RunTest(@"
                assert(o.x.foo === 'bar');
            ");
        }

        [Fact]
        public void DateTimeIsConvertedToDate()
        {
            var o = new
            {
                z = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            _engine.SetValue("o", o);

            RunTest(@"
                assert(o.z.valueOf() === 0);
            ");
        }

        [Fact]
        public void EcmaValuesAreAutomaticallyConvertedWhenSetInPoco()
        {
            var p = new Person
            {
                Name = "foo",
            };

            _engine.SetValue("p", p);

            RunTest(@"
                assert(p.Name === 'foo');
                assert(p.Age === 0);
                p.Name = 'bar';
                p.Age = 10;
            ");

            Assert.Equal("bar", p.Name);
            Assert.Equal(10, p.Age);
        }

        [Fact]
        public void EcmaValuesAreAutomaticallyConvertedToBestMatchWhenSetInPoco()
        {
            var p = new Person
            {
                Name = "foo",
            };

            _engine.SetValue("p", p);

            RunTest(@"
                p.Name = 10;
                p.Age = '20';
            ");

            Assert.Equal("10", p.Name);
            Assert.Equal(20, p.Age);
        }

        [Fact]
        public void ShouldCallInstanceMethodWithoutArgument()
        {
            _engine.SetValue("a", new A());

            RunTest(@"
                assert(a.Call1() === 0);
            ");
        }

        [Fact]
        public void ShouldCallInstanceMethodOverloadArgument()
        {
            _engine.SetValue("a", new A());

            RunTest(@"
                assert(a.Call1(1) === 1);
            ");
        }

        [Fact]
        public void ShouldCallInstanceMethodWithString()
        {
            var p = new Person();
            _engine.SetValue("a", new A());
            _engine.SetValue("p", p);

            RunTest(@"
                p.Name = a.Call2('foo');
                assert(p.Name === 'foo');
            ");

            Assert.Equal("foo", p.Name);
        }

        [Fact]
        public void CanUseTrim()
        {
            var p = new Person { Name = "Mickey Mouse "};
            _engine.SetValue("p", p);

            RunTest(@"
                assert(p.Name === 'Mickey Mouse ');
                p.Name = p.Name.trim();
                assert(p.Name === 'Mickey Mouse');
            ");

            Assert.Equal("Mickey Mouse", p.Name);
        }

        [Fact]
        public void CanUseMathFloor()
        {
            var p = new Person();
            _engine.SetValue("p", p);

            RunTest(@"
                p.Age = Math.floor(1.6);p
                assert(p.Age === 1);
            ");

            Assert.Equal(1, p.Age);
        }

        [Fact]
        public void CanUseDelegateAsFunction()
        {
            var even = new Func<int, bool>(x => x % 2 == 0);
            _engine.SetValue("even", even);

            RunTest(@"
                assert(even(2) === true);
            ");
        }


        [Fact]
        public void ShouldConvertArrayInstanceToArray()
        {
            var result = _engine.Execute("'foo@bar.com'.split('@');");
            var parts = result.ToObject();
            
            Assert.True(parts.GetType().IsArray);
            Assert.Equal(2, ((object[])parts).Length);
            Assert.Equal("foo", ((object[])parts)[0]);
            Assert.Equal("bar.com", ((object[])parts)[1]);
        }

        [Fact]
        public void ShouldConvertBooleanInstanceToBool()
        {
            var result = _engine.Execute("new Boolean(true)");
            var value = result.ToObject();

            Assert.Equal(typeof(bool), value.GetType());
            Assert.Equal(true, value);
        }

        [Fact]
        public void ShouldConvertDateInstanceToDateTime()
        {
            var result = _engine.Execute("new Date(0)");
            var value = result.ToObject();

            Assert.Equal(typeof(DateTime), value.GetType());
            Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), value);
        }

        [Fact]
        public void ShouldConvertNumberInstanceToDouble()
        {
            var result = _engine.Execute("new Number(10)");
            var value = result.ToObject();

            Assert.Equal(typeof(double), value.GetType());
            Assert.Equal(10d, value);
        }

        [Fact]
        public void ShouldConvertStringInstanceToString()
        {
            var result = _engine.Execute("new String('foo')");
            var value = result.ToObject();

            Assert.Equal(typeof(string), value.GetType());
            Assert.Equal("foo", value);
        }


        public class Person
        {
            public string Name { get; set; }
            public int Age { get; set; }
            
            public override string ToString()
            {
                return Name;
            }
        }

        public class A
        {
            public int Call1()
            {
                return 0;
            }

            public int Call1(int x)
            {
                return x;
            }

            public string Call2(string x)
            {
                return x;
            }

        }
    }
}
