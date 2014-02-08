using System;
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
        public class Person
        {
            public string Name { get; set; }
            public int Age { get; set; }
            
            public override string ToString()
            {
                return Name;
            }
        }
    }
}
