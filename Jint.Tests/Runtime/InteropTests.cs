using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Tests.Runtime.Converters;
using Jint.Tests.Runtime.Domain;
using Shapes;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class InteropTests : IDisposable
    {
        private readonly Engine _engine;

        public InteropTests()
        {
            _engine = new Engine(cfg => cfg.AllowClr(
                typeof(Shape).GetTypeInfo().Assembly,
                typeof(Console).GetTypeInfo().Assembly,
                typeof(System.IO.File).GetTypeInfo().Assembly))
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("equal", new Action<object, object>(Assert.Equal))
                ;
        }

        void IDisposable.Dispose()
        {
        }

        private async void RunTest(string source)
        {
            _engine.Execute(source);
            //await _engine.ExecuteAsync(source);
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
        public void DelegateWithNullableParameterCanBePassedANull()
        {
            _engine.SetValue("isnull", new Func<double?, bool>(x => x == null));

            RunTest(@"
                assert(isnull(null) === true);
            ");
        }

        [Fact]
        public void DelegateWithObjectParameterCanBePassedANull()
        {
            _engine.SetValue("isnull", new Func<object, bool>(x => x == null));

            RunTest(@"
                assert(isnull(null) === true);
            ");
        }

        [Fact]
        public void DelegateWithNullableParameterCanBePassedAnUndefined()
        {
            _engine.SetValue("isnull", new Func<double?, bool>(x => x == null));

            RunTest(@"
                assert(isnull(undefined) === true);
            ");
        }

        [Fact]
        public void DelegateWithObjectParameterCanBePassedAnUndefined()
        {
            _engine.SetValue("isnull", new Func<object, bool>(x => x == null));

            RunTest(@"
                assert(isnull(undefined) === true);
            ");
        }

        [Fact]
        public void DelegateWithNullableParameterCanBeExcluded()
        {
            _engine.SetValue("isnull", new Func<double?, bool>(x => x == null));

            RunTest(@"
                assert(isnull() === true);
            ");
        }

        [Fact]
        public void DelegateWithObjectParameterCanBeExcluded()
        {
            _engine.SetValue("isnull", new Func<object, bool>(x => x == null));

            RunTest(@"
                assert(isnull() === true);
            ");
        }

        [Fact]
        public void DynamicDelegateCanBeSet()
        {
#if NETFRAMEWORK
            var parameters = new[]
            {
                System.Linq.Expressions.Expression.Parameter(typeof(int)),
                System.Linq.Expressions.Expression.Parameter(typeof(int))
            };
            var exp = System.Linq.Expressions.Expression.Add(parameters[0], parameters[1]);
            var del = System.Linq.Expressions.Expression.Lambda(exp, parameters).Compile();

            _engine.SetValue("add", del);

            RunTest(@"
                assert(add(1,1) === 2);
            ");
#endif
        }

        [Fact]
        public void ExtraParametersAreIgnored()
        {
            _engine.SetValue("passNumber", new Func<int, int>(x => x));

            RunTest(@"
                assert(passNumber(123,'test',{},[],null) === 123);
            ");
        }

        private delegate string callParams(params object[] values);
        private delegate string callArgumentAndParams(string firstParam, params object[] values);

        [Fact]
        public void DelegatesWithParamsParameterCanBeInvoked()
        {
            var a = new A();
            _engine.SetValue("callParams", new callParams(a.Call13));
            _engine.SetValue("callArgumentAndParams", new callArgumentAndParams(a.Call14));

            RunTest(@"
                assert(callParams('1','2','3') === '1,2,3');
                assert(callParams('1') === '1');
                assert(callParams() === '');

                assert(callArgumentAndParams('a','1','2','3') === 'a:1,2,3');
                assert(callArgumentAndParams('a','1') === 'a:1');
                assert(callArgumentAndParams('a') === 'a:');
                assert(callArgumentAndParams() === ':');
            ");
        }

        [Fact]
        public void DelegateWithDefaultValueParametersCanBeInvoked()
        {
            var instance = new A();
            _engine.SetValue("Instance", instance);
            _engine.SetValue("Class", TypeReference.CreateTypeReference(_engine, typeof(A)));

            RunTest(@"
                assert(Instance.Call19() === 0);
                assert(Instance.Call19(1) === 1);
                assert(Instance.Call20(1) === 4);
                assert(Instance.Call20(1, 2) === 5);
                assert(Instance.Call20(1 , 2, 3) === 6);

                assert(Class.Call19Static() === 0);
                assert(Class.Call19Static(1) === 1);
                assert(Class.Call20Static(1) === 4);
                assert(Class.Call20Static(1, 2) === 5);
                assert(Class.Call20Static(1 , 2, 3) === 6);
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
        public void CanInvokeObjectMethodsWithPascalCase()
        {
            var p = new Person
            {
                Name = "Mickey Mouse"
            };

            _engine.SetValue("p", p);

            RunTest(@"
                assert(p.toString() === 'Mickey Mouse');
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
        public void CanGetIndexUsingStringKey()
        {
            var dictionary = new Dictionary<string, Person>();
            dictionary.Add("person1", new Person { Name = "Mickey Mouse" });
            dictionary.Add("person2", new Person { Name = "Goofy" });

            _engine.SetValue("dictionary", dictionary);

            RunTest(@"
                assert(dictionary['person1'].Name === 'Mickey Mouse');
                assert(dictionary['person2'].Name === 'Goofy');
            ");
        }

        [Fact]
        public void CanSetIndexUsingStringKey()
        {
            var dictionary = new Dictionary<string, Person>();
            dictionary.Add("person1", new Person { Name = "Mickey Mouse" });
            dictionary.Add("person2", new Person { Name = "Goofy" });

            _engine.SetValue("dictionary", dictionary);

            RunTest(@"
                dictionary['person2'].Name = 'Donald Duck';
                assert(dictionary['person2'].Name === 'Donald Duck');
            ");

            Assert.Equal("Donald Duck", dictionary["person2"].Name);
        }

        [Fact]
        public void CanGetIndexUsingIntegerKey()
        {
            var dictionary = new Dictionary<int, string>();
            dictionary.Add(1, "Mickey Mouse");
            dictionary.Add(2, "Goofy");

            _engine.SetValue("dictionary", dictionary);

            RunTest(@"
                assert(dictionary[1] === 'Mickey Mouse');
                assert(dictionary[2] === 'Goofy');
            ");
        }

        [Fact]
        public void CanSetIndexUsingIntegerKey()
        {
            var dictionary = new Dictionary<int, string>();
            dictionary.Add(1, "Mickey Mouse");
            dictionary.Add(2, "Goofy");

            _engine.SetValue("dictionary", dictionary);

            RunTest(@"
                dictionary[2] = 'Donald Duck';
                assert(dictionary[2] === 'Donald Duck');
            ");

            Assert.Equal("Mickey Mouse", dictionary[1]);
            Assert.Equal("Donald Duck", dictionary[2]);
        }

        private class DoubleIndexedClass
        {
            public int this[int index]
            {
                get { return index; }
            }

            public string this[string index]
            {
                get { return index; }
            }
        }

        [Fact]
        public void CanGetIndexUsingBothIntAndStringIndex()
        {
            var dictionary = new DoubleIndexedClass();

            _engine.SetValue("dictionary", dictionary);

            RunTest(@"
                assert(dictionary[1] === 1);
                assert(dictionary['test'] === 'test');
            ");
        }

        [Fact]
        public void CanUseGenericMethods()
        {
            var dictionary = new Dictionary<int, string>();
            dictionary.Add(1, "Mickey Mouse");


            _engine.SetValue("dictionary", dictionary);

            RunTest(@"
                dictionary.Add(2, 'Goofy');
                assert(dictionary[2] === 'Goofy');
            ");

            Assert.Equal("Mickey Mouse", dictionary[1]);
            Assert.Equal("Goofy", dictionary[2]);
        }

        [Fact]
        public void CanUseMultiGenericTypes()
        {

            RunTest(@"
                var type = System.Collections.Generic.Dictionary(System.Int32, System.String);
                var dictionary = new type();
                dictionary.Add(1, 'Mickey Mouse');
                dictionary.Add(2, 'Goofy');
                assert(dictionary[2] === 'Goofy');
            ");
        }

        [Fact]
        public void CanUseIndexOnCollection()
        {
            var collection = new System.Collections.ObjectModel.Collection<string>();
            collection.Add("Mickey Mouse");
            collection.Add("Goofy");

            _engine.SetValue("dictionary", collection);

            RunTest(@"
                dictionary[1] = 'Donald Duck';
                assert(dictionary[1] === 'Donald Duck');
            ");

            Assert.Equal("Mickey Mouse", collection[0]);
            Assert.Equal("Donald Duck", collection[1]);
        }

        [Fact]
        public void CanUseIndexOnList()
        {
            var list = new List<object>(2);
            list.Add("Mickey Mouse");
            list.Add("Goofy");

            _engine.SetValue("list", list);

            RunTest(@"
                list[1] = 'Donald Duck';
                assert(list[1] === 'Donald Duck');
            ");

            Assert.Equal("Mickey Mouse", list[0]);
            Assert.Equal("Donald Duck", list[1]);
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
                x = new JsNumber(1),
                y = new JsString("string"),
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
            var x = new ObjectInstance(_engine);
            x.Set("foo", new JsString("bar"));

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
        public void DateTimeOffsetIsConvertedToDate()
        {
            var o = new
            {
                z = new DateTimeOffset(1970, 1, 1, 0, 0, 0, new TimeSpan())
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

        private struct TestStruct
        {
            public int Value;

            public TestStruct(int value)
            {
                Value = value;
            }
        }

        private class TestClass
        {
            public int? NullableInt { get; set; }
            public DateTime? NullableDate { get; set; }
            public bool? NullableBool { get; set; }
            public TestEnumInt32? NullableEnum { get; set; }
            public TestStruct? NullableStruct { get; set; }
        }

        [Fact]
        public void CanSetNullablePropertiesOnPocos()
        {
            var instance = new TestClass();
            _engine.SetValue("instance", instance);
            _engine.SetValue("TestStruct", typeof(TestStruct));

            RunTest(@"
                instance.NullableInt = 2;
                instance.NullableDate = new Date();
                instance.NullableBool = true;
                instance.NullableEnum = 1;
                instance.NullableStruct = new TestStruct(5);

                assert(instance.NullableInt===2);
                assert(instance.NullableDate!=null);
                assert(instance.NullableBool===true);
                assert(instance.NullableEnum===1);
                assert(instance.NullableStruct.Value===5);
            ");
        }

        private class ReadOnlyList : IReadOnlyList<Person>
        {
            private readonly Person[] _data;

            public ReadOnlyList(params Person[] data)
            {
                _data = data;
            }

            public IEnumerator<Person> GetEnumerator()
            {
                return ((IEnumerable<Person>) _data).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _data.GetEnumerator();
            }

            public int Count => _data.Length;

            public Person this[int index] => _data[index];
        }
        
        [Fact]
        public void CanAddArrayPrototypeForArrayLikeClrObjects()
        {
            var e = new Engine(cfg => cfg
                .AllowClr(typeof(Person).Assembly)
                .SetWrapObjectHandler((engine, target) =>
                {
                    var instance = new ObjectWrapper(engine, target);
                    if (instance.IsArrayLike)
                    {
                        instance.SetPrototypeOf(engine.Array.PrototypeObject);
                    }
                    return instance;
                })
            );

            var person = new Person
            {
                Age = 12,
                Name = "John"
            };
            
            dynamic obj = new
            {
                values = new ReadOnlyList(person)
            };

            e.SetValue("o", obj);

            var name = e.Execute("o.values.filter(x => x.age == 12)[0].name").GetCompletionValue().ToString();
            Assert.Equal("John", name);
        }
        
        [Fact]
        public void CanAccessExpandoObject()
        {
            var engine = new Engine();
            dynamic expando = new ExpandoObject();
            expando.Name = "test";
            engine.SetValue("expando", expando);
            Assert.Equal("test", engine.Execute("expando.Name").GetCompletionValue().ToString());
        }

        [Fact]
        public void ShouldConvertArrayToArrayInstance()
        {
            var result = _engine
                .SetValue("values", new[] { 1, 2, 3, 4, 5, 6 })
                .Execute("values.filter(function(x){ return x % 2 == 0; })");

            var parts = result.GetCompletionValue().ToObject();

            Assert.True(parts.GetType().IsArray);
            Assert.Equal(3, ((object[])parts).Length);
            Assert.Equal(2d, ((object[])parts)[0]);
            Assert.Equal(4d, ((object[])parts)[1]);
            Assert.Equal(6d, ((object[])parts)[2]);
        }

        [Fact]
        public void ShouldConvertListsToArrayInstance()
        {
            var result = _engine
                .SetValue("values", new List<object> { 1, 2, 3, 4, 5, 6 })
                .Execute("new Array(values).filter(function(x){ return x % 2 == 0; })");

            var parts = result.GetCompletionValue().ToObject();

            Assert.True(parts.GetType().IsArray);
            Assert.Equal(3, ((object[])parts).Length);
            Assert.Equal(2d, ((object[])parts)[0]);
            Assert.Equal(4d, ((object[])parts)[1]);
            Assert.Equal(6d, ((object[])parts)[2]);
        }

        [Fact]
        public void ShouldConvertArrayInstanceToArray()
        {
            var result = _engine.Execute("'foo@bar.com'.split('@');");
            var parts = result.GetCompletionValue().ToObject();

            Assert.True(parts.GetType().IsArray);
            Assert.Equal(2, ((object[])parts).Length);
            Assert.Equal("foo", ((object[])parts)[0]);
            Assert.Equal("bar.com", ((object[])parts)[1]);
        }

        [Fact]
        public void ShouldLoopWithNativeEnumerator()
        {
            JsValue adder(JsValue argValue)
            {
                ArrayInstance args = argValue.AsArray();
                double sum = 0;
                foreach (var item in args)
                {
                    if (item.IsNumber())
                    {
                        sum += item.AsNumber();
                    }
                }
                return sum;
            }
            var result = _engine.SetValue("getSum", new Func<JsValue, JsValue>(adder))
                .Execute("getSum([1,2,3]);");

            Assert.True(result.GetCompletionValue() == 6);
        }

        [Fact]
        public void ShouldConvertBooleanInstanceToBool()
        {
            var result = _engine.Execute("new Boolean(true)");
            var value = result.GetCompletionValue().ToObject();

            Assert.Equal(typeof(bool), value.GetType());
            Assert.Equal(true, value);
        }

        [Fact]
        public void ShouldConvertDateInstanceToDateTime()
        {
            var result = _engine.Execute("new Date(0)");
            var value = result.GetCompletionValue().ToObject();

            Assert.Equal(typeof(DateTime), value.GetType());
            Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), value);
        }

        [Fact]
        public void ShouldConvertNumberInstanceToDouble()
        {
            var result = _engine.Execute("new Number(10)");
            var value = result.GetCompletionValue().ToObject();

            Assert.Equal(typeof(double), value.GetType());
            Assert.Equal(10d, value);
        }

        [Fact]
        public void ShouldConvertStringInstanceToString()
        {
            var result = _engine.Execute("new String('foo')");
            var value = result.GetCompletionValue().ToObject();

            Assert.Equal(typeof(string), value.GetType());
            Assert.Equal("foo", value);
        }

        [Fact]
        public void ShouldConvertObjectInstanceToExpando()
        {
            _engine.Execute("var o = {a: 1, b: 'foo'}");
            var result = _engine.GetValue("o");

            dynamic value = result.ToObject();

            Assert.Equal(1, value.a);
            Assert.Equal("foo", value.b);

            var dic = (IDictionary<string, object>)result.ToObject();

            Assert.Equal(1d, dic["a"]);
            Assert.Equal("foo", dic["b"]);

        }

        [Fact]
        public void ShouldNotTryToConvertCompatibleTypes()
        {
            _engine.SetValue("a", new A());

            RunTest(@"
                assert(a.Call3('foo') === 'foo');
                assert(a.Call3(1) === '1');
            ");
        }

        [Fact]
        public void ShouldNotTryToConvertDerivedTypes()
        {
            _engine.SetValue("a", new A());
            _engine.SetValue("p", new Person { Name = "Mickey" });

            RunTest(@"
                assert(a.Call4(p) === 'Mickey');
            ");
        }

        [Fact]
        public void ShouldExecuteFunctionCallBackAsDelegate()
        {
            _engine.SetValue("a", new A());

            RunTest(@"
                assert(a.Call5(function(a,b){ return a+b }) === '1foo');
            ");
        }

        [Fact]
        public void ShouldExecuteFunctionCallBackAsFuncAndThisCanBeAssigned()
        {
            _engine.SetValue("a", new A());

            RunTest(@"
                assert(a.Call6(function(a,b){ return this+a+b }) === 'bar1foo');
            ");
        }

        [Fact]
        public void ShouldExecuteFunctionCallBackAsPredicate()
        {
            _engine.SetValue("a", new A());

            // Func<>
            RunTest(@"
                assert(a.Call8(function(){ return 'foo'; }) === 'foo');
            ");
        }

        [Fact]
        public void ShouldExecuteFunctionWithParameterCallBackAsPredicate()
        {
            _engine.SetValue("a", new A());

            // Func<,>
            RunTest(@"
                assert(a.Call7('foo', function(a){ return a === 'foo'; }) === true);
            ");
        }

        [Fact]
        public void ShouldExecuteActionCallBackAsPredicate()
        {
            _engine.SetValue("a", new A());

            // Action
            RunTest(@"
                var value;
                a.Call9(function(){ value = 'foo'; });
                assert(value === 'foo');
            ");
        }

        [Fact]
        public void ShouldExecuteActionWithParameterCallBackAsPredicate()
        {
            _engine.SetValue("a", new A());

            // Action<>
            RunTest(@"
                var value;
                a.Call10('foo', function(b){ value = b; });
                assert(value === 'foo');
            ");
        }

        [Fact]
        public void ShouldExecuteActionWithMultipleParametersCallBackAsPredicate()
        {
            _engine.SetValue("a", new A());

            // Action<,>
            RunTest(@"
                var value;
                a.Call11('foo', 'bar', function(a,b){ value = a + b; });
                assert(value === 'foobar');
            ");
        }

        [Fact]
        public void ShouldExecuteFunc()
        {
            _engine.SetValue("a", new A());

            // Func<int, int>
            RunTest(@"
                var result = a.Call12(42, function(a){ return a + a; });
                assert(result === 84);
            ");
        }

        [Fact]
        public void ShouldExecuteActionCallbackOnEventChanged()
        {
            var collection = new System.Collections.ObjectModel.ObservableCollection<string>();
            Assert.True(collection.Count == 0);

            _engine.SetValue("collection", collection);

            RunTest(@"
                var eventAction;
                collection.add_CollectionChanged(function(sender, eventArgs) { eventAction = eventArgs.Action; } );
                collection.Add('test');
            ");

            var eventAction = _engine.GetValue("eventAction").AsNumber();
            Assert.True(eventAction == 0);
            Assert.True(collection.Count == 1);
        }

        [Fact]
        public void ShouldUseSystemIO()
        {
            RunTest(@"
                var filename = System.IO.Path.GetTempFileName();
                var sw = System.IO.File.CreateText(filename);
                sw.Write('Hello World');
                sw.Dispose();

                var content = System.IO.File.ReadAllText(filename);
                System.Console.WriteLine(content);

                assert(content === 'Hello World');
            ");
        }

        [Fact]
        public void ShouldBeInstanceOfTypeReferenceType()
        {
            _engine.SetValue("A", typeof(A));
            RunTest(@"
                var a = new A();
                assert(a instanceof A);
            ");
        }

        [Fact]
        public void ShouldImportNamespace()
        {
            RunTest(@"
                var Shapes = importNamespace('Shapes');
                var circle = new Shapes.Circle();
                assert(circle.Radius === 0);
                assert(circle.Perimeter() === 0);
            ");
        }

        [Fact]
        public void ShouldConstructReferenceTypeWithParameters()
        {
            RunTest(@"
                var Shapes = importNamespace('Shapes');
                var circle = new Shapes.Circle(1);
                assert(circle.Radius === 1);
                assert(circle.Perimeter() === Math.PI);
            ");
        }

        [Fact]
        public void ShouldConstructValueTypeWithoutParameters()
        {
            RunTest(@"
                var guid = new System.Guid();
                assert('00000000-0000-0000-0000-000000000000' === guid.ToString());
            ");
        }

        [Fact]
        public void ShouldInvokeAFunctionByName()
        {
            RunTest(@"
                function add(x, y) { return x + y; }
            ");

            Assert.Equal(3, _engine.Invoke("add", 1, 2));
        }

        [Fact]
        public void ShouldNotInvokeNonFunctionValue()
        {
            RunTest(@"
                var x= 10;
            ");

            Assert.Throws<ArgumentException>(() => _engine.Invoke("x", 1, 2));
        }

        [Fact]
        public void CanGetField()
        {
            var o = new ClassWithField
            {
                Field = "Mickey Mouse"
            };

            _engine.SetValue("o", o);

            RunTest(@"
                assert(o.Field === 'Mickey Mouse');
            ");
        }

        [Fact]
        public void CanSetField()
        {
            var o = new ClassWithField();

            _engine.SetValue("o", o);

            RunTest(@"
                o.Field = 'Mickey Mouse';
                assert(o.Field === 'Mickey Mouse');
            ");

            Assert.Equal("Mickey Mouse", o.Field);
        }

        [Fact]
        public void CanGetStaticField()
        {
            RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                var statics = domain.ClassWithStaticFields;
                assert(statics.Get == 'Get');
            ");
        }

        [Fact]
        public void CanSetStaticField()
        {
            RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                var statics = domain.ClassWithStaticFields;
                statics.Set = 'hello';
                assert(statics.Set == 'hello');
            ");

            Assert.Equal(ClassWithStaticFields.Set, "hello");
        }

        [Fact]
        public void CanGetStaticAccessor()
        {
            RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                var statics = domain.ClassWithStaticFields;
                assert(statics.Getter == 'Getter');
            ");
        }

        [Fact]
        public void CanSetStaticAccessor()
        {
            RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                var statics = domain.ClassWithStaticFields;
                statics.Setter = 'hello';
                assert(statics.Setter == 'hello');
            ");

            Assert.Equal(ClassWithStaticFields.Setter, "hello");
        }

        [Fact]
        public void CantSetStaticReadonly()
        {
            RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                var statics = domain.ClassWithStaticFields;
                statics.Readonly = 'hello';
                assert(statics.Readonly == 'Readonly');
            ");

            Assert.Equal(ClassWithStaticFields.Readonly, "Readonly");
        }

        [Fact]
        public void CanSetCustomConverters()
        {

            var engine1 = new Engine();
            engine1.SetValue("p", new { Test = true });
            engine1.Execute("var result = p.Test;");
            Assert.True((bool)engine1.GetValue("result").ToObject());

            var engine2 = new Engine(o => o.AddObjectConverter(new NegateBoolConverter()));
            engine2.SetValue("p", new { Test = true });
            engine2.Execute("var result = p.Test;");
            Assert.False((bool)engine2.GetValue("result").ToObject());

        }

        [Fact]
        public void CanConvertEnumsToString()
        {
            var engine1 = new Engine(o => o.AddObjectConverter(new EnumsToStringConverter()))
                .SetValue("assert", new Action<bool>(Assert.True));
            engine1.SetValue("p", new { Comparison = StringComparison.CurrentCulture });
            engine1.Execute("assert(p.Comparison === 'CurrentCulture');");
            engine1.Execute("var result = p.Comparison;");
            Assert.Equal("CurrentCulture", (string)engine1.GetValue("result").ToObject());
        }

        [Fact]
        public void CanUserIncrementOperator()
        {
            var p = new Person
            {
                Age = 1,
            };

            _engine.SetValue("p", p);

            RunTest(@"
                assert(++p.Age === 2);
            ");

            Assert.Equal(2, p.Age);
        }

        [Fact]
        public void CanOverwriteValues()
        {
            _engine.SetValue("x", 3);
            _engine.SetValue("x", 4);

            RunTest(@"
                assert(x === 4);
            ");
        }

        [Fact]
        public void ShouldCreateGenericType()
        {
            RunTest(@"
                var ListOfString = System.Collections.Generic.List(System.String);
                var list = new ListOfString();
                list.Add('foo');
                list.Add(1);
                assert(2 === list.Count);
            ");
        }

        [Fact]
        public void EnumComparesByName()
        {
            var o = new
            {
                r = Colors.Red,
                b = Colors.Blue,
                g = Colors.Green,
                b2 = Colors.Red
            };

            _engine.SetValue("o", o);
            _engine.SetValue("assertFalse", new Action<bool>(Assert.False));

            RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                var colors = domain.Colors;
                assert(o.r === colors.Red);
                assert(o.g === colors.Green);
                assert(o.b === colors.Blue);
                assertFalse(o.b2 === colors.Blue);
            ");
        }

        [Fact]
        public void ShouldSetEnumProperty()
        {
            var s = new Circle
            {
                Color = Colors.Red,
            };

            _engine.SetValue("s", s);

            RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                var colors = domain.Colors;

                s.Color = colors.Blue;
                assert(s.Color === colors.Blue);
            ");

            _engine.SetValue("s", s);

            RunTest(@"
                s.Color = colors.Blue | colors.Green;
                assert(s.Color === colors.Blue | colors.Green);
            ");

            Assert.Equal(Colors.Blue | Colors.Green, s.Color);
        }

        enum TestEnumInt32 : int
        {
            None,
            One = 1,
            Min = int.MaxValue,
            Max = int.MaxValue,
        }

        enum TestEnumUInt32 : uint
        {
            None,
            One = 1,
            Min = uint.MaxValue,
            Max = uint.MaxValue,
        }

        enum TestEnumInt64 : long
        {
            None,
            One = 1,
            Min = long.MaxValue,
            Max = long.MaxValue,
        }

        enum TestEnumUInt64 : ulong
        {
            None,
            One = 1,
            Min = ulong.MaxValue,
            Max = ulong.MaxValue,
        }

        void TestEnum<T>(T enumValue)
        {
            object i = Convert.ChangeType(enumValue, Enum.GetUnderlyingType(typeof(T)));
            string s = Convert.ToString(i, CultureInfo.InvariantCulture);
            var o = new Tuple<T>(enumValue);
            _engine.SetValue("o", o);
            RunTest("assert(o.Item1 === " + s + ");");
        }

        [Fact]
        public void ShouldWorkWithEnumInt32()
        {
            TestEnum(TestEnumInt32.None);
            TestEnum(TestEnumInt32.One);
            TestEnum(TestEnumInt32.Min);
            TestEnum(TestEnumInt32.Max);
        }

        [Fact]
        public void ShouldWorkWithEnumUInt32()
        {
            TestEnum(TestEnumUInt32.None);
            TestEnum(TestEnumUInt32.One);
            TestEnum(TestEnumUInt32.Min);
            TestEnum(TestEnumUInt32.Max);
        }

        [Fact]
        public void ShouldWorkWithEnumInt64()
        {
            TestEnum(TestEnumInt64.None);
            TestEnum(TestEnumInt64.One);
            TestEnum(TestEnumInt64.Min);
            TestEnum(TestEnumInt64.Max);
        }

        [Fact]
        public void ShouldWorkWithEnumUInt64()
        {
            TestEnum(TestEnumUInt64.None);
            TestEnum(TestEnumUInt64.One);
            TestEnum(TestEnumUInt64.Min);
            TestEnum(TestEnumUInt64.Max);
        }

        [Fact]
        public void EnumIsConvertedToNumber()
        {
            var o = new
            {
                r = Colors.Red,
                b = Colors.Blue,
                g = Colors.Green
            };

            _engine.SetValue("o", o);

            RunTest(@"
                assert(o.r === 0);
                assert(o.g === 1);
                assert(o.b === 10);
            ");
        }

        [Fact]
        public void ShouldConvertToEnum()
        {
            var s = new Circle
            {
                Color = Colors.Red,
            };

            _engine.SetValue("s", s);

            RunTest(@"
                assert(s.Color === 0);
                s.Color = 10;
                assert(s.Color === 10);
            ");

            _engine.SetValue("s", s);

            RunTest(@"
                s.Color = 11;
                assert(s.Color === 11);
            ");

            Assert.Equal(Colors.Blue | Colors.Green, s.Color);
        }

        [Fact]
        public void ShouldUseExplicitPropertyGetter()
        {
            _engine.SetValue("c", new Company("ACME"));

            RunTest(@"
                assert(c.Name === 'ACME');
            ");
        }

        [Fact]
        public void ShouldUseExplicitIndexerPropertyGetter()
        {
            var company = new Company("ACME");
            ((ICompany)company)["Foo"] = "Bar";
            _engine.SetValue("c", company);

            RunTest(@"
                assert(c.Foo === 'Bar');
            ");
        }

        [Fact]
        public void ShouldUseExplicitPropertySetter()
        {
            _engine.SetValue("c", new Company("ACME"));

            RunTest(@"
                c.Name = 'Foo';
                assert(c.Name === 'Foo');
            ");
        }

        [Fact]
        public void ShouldUseExplicitIndexerPropertySetter()
        {
            var company = new Company("ACME");
            ((ICompany)company)["Foo"] = "Bar";
            _engine.SetValue("c", company);

            RunTest(@"
                c.Foo = 'Baz';
                assert(c.Foo === 'Baz');
            ");
        }

        [Fact]
        public void ShouldUseExplicitMethod()
        {
            _engine.SetValue("c", new Company("ACME"));

            RunTest(@"
                assert(0 === c.CompareTo(c));
            ");
        }

        [Fact]
        public void ShouldCallInstanceMethodWithParams()
        {
            _engine.SetValue("a", new A());

            RunTest(@"
                assert(a.Call13('1','2','3') === '1,2,3');
                assert(a.Call13('1') === '1');
                assert(a.Call13(1) === '1');
                assert(a.Call13() === '');

                assert(a.Call14('a','1','2','3') === 'a:1,2,3');
                assert(a.Call14('a','1') === 'a:1');
                assert(a.Call14('a') === 'a:');

                function call13wrapper(){ return a.Call13.apply(a, Array.prototype.slice.call(arguments)); }
                assert(call13wrapper('1','2','3') === '1,2,3');

                assert(a.Call13('1','2','3') === a.Call13(['1','2','3']));
            ");
        }

        [Fact]
        public void ShouldCallInstanceMethodWithJsValueParams()
        {
            _engine.SetValue("a", new A());

            RunTest(@"
                assert(a.Call16('1','2','3') === '1,2,3');
                assert(a.Call16('1') === '1');
                assert(a.Call16(1) === '1');
                assert(a.Call16() === '');
                assert(a.Call16('1','2','3') === a.Call16(['1','2','3']));
            ");
        }

        [Fact]
        public void NullValueAsArgumentShouldWork()
        {
            _engine.SetValue("a", new A());

            RunTest(@"
                var x = a.Call2(null);
                assert(x === null);
            ");
        }

        [Fact]
        public void ShouldSetPropertyToNull()
        {
            var p = new Person { Name = "Mickey" };
            _engine.SetValue("p", p);

            RunTest(@"
                assert(p.Name != null);
                p.Name = null;
                assert(p.Name == null);
            ");

            Assert.True(p.Name == null);
        }

        [Fact]
        public void ShouldCallMethodWithNull()
        {
            _engine.SetValue("a", new A());

            RunTest(@"
                a.Call15(null);
                var result = a.Call2(null);
                assert(result == null);
            ");
        }

        [Fact]
        public void ShouldReturnUndefinedProperty()
        {
            _engine.SetValue("uo", new { foo = "bar" });
            _engine.SetValue("ud", new Dictionary<string, object> { {"foo", "bar"} });
            _engine.SetValue("ul", new List<string> { "foo", "bar" });

            RunTest(@"
                assert(!uo.undefinedProperty);
                assert(!ul[5]);
                assert(!ud.undefinedProperty);
            ");
        }

        private class FailingObject2
        {
            public int this[int index] => throw new ArgumentException("index is bad", nameof(index));
        }

        [Fact]
        public void ShouldPropagateIndexerExceptions()
        {
            var engine = new Engine();
            engine.Execute(@"function f2(obj) { return obj[1]; }");

            var failingObject = new FailingObject2();
            Assert.Throws<ArgumentException>(() => engine.Invoke("f2", failingObject));
        }

        [Fact]
        public void ShouldAutomaticallyConvertArraysToFindBestInteropResolution()
        {
            _engine.SetValue("a", new ArrayConverterTestClass());
            _engine.SetValue("item1", new ArrayConverterItem(1));
            _engine.SetValue("item2", new ArrayConverterItem(2));

            RunTest(@"
                assert(a.MethodAcceptsArrayOfInt([false, '1', 2]) === a.MethodAcceptsArrayOfInt([0, 1, 2]));
                assert(a.MethodAcceptsArrayOfStrings(['1', 2]) === a.MethodAcceptsArrayOfStrings([1, 2]));
                assert(a.MethodAcceptsArrayOfBool(['1', 0]) === a.MethodAcceptsArrayOfBool([true, false]));

                assert(a.MethodAcceptsArrayOfStrings([item1, item2]) === a.MethodAcceptsArrayOfStrings(['1', '2']));
                assert(a.MethodAcceptsArrayOfInt([item1, item2]) === a.MethodAcceptsArrayOfInt([1, 2]));
            ");
        }

        [Fact]
        public void ShouldImportNamespaceNestedType()
        {
            RunTest(@"
                var shapes = importNamespace('Shapes.Circle');
                var kinds = shapes.Kind;
                assert(kinds.Unit === 0);
                assert(kinds.Ellipse === 1);
                assert(kinds.Round === 5);
            ");
        }

        [Fact]
        public void ShouldImportNamespaceNestedNestedType()
        {
            RunTest(@"
                var meta = importNamespace('Shapes.Circle.Meta');
                var usages = meta.Usage;
                assert(usages.Public === 0);
                assert(usages.Private === 1);
                assert(usages.Internal === 11);
            ");
        }

        [Fact]
        public void ShouldGetNestedNestedProp()
        {
            RunTest(@"
                var meta = importNamespace('Shapes.Circle');
                var m = new meta.Meta();
                assert(m.Description === 'descp');
            ");
        }

        [Fact]
        public void ShouldSetNestedNestedProp()
        {
            RunTest(@"
                var meta = importNamespace('Shapes.Circle');
                var m = new meta.Meta();
                m.Description = 'hello';
                assert(m.Description === 'hello');
            ");
        }

        [Fact]
        public void CanGetStaticNestedField()
        {
            RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain.Nested');
                var statics = domain.ClassWithStaticFields;
                assert(statics.Get == 'Get');
            ");
        }

        [Fact]
        public void CanSetStaticNestedField()
        {
            RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain.Nested');
                var statics = domain.ClassWithStaticFields;
                statics.Set = 'hello';
                assert(statics.Set == 'hello');
            ");

            Assert.Equal(Nested.ClassWithStaticFields.Set, "hello");
        }

        [Fact]
        public void CanGetStaticNestedAccessor()
        {
            RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain.Nested');
                var statics = domain.ClassWithStaticFields;
                assert(statics.Getter == 'Getter');
            ");
        }

        [Fact]
        public void CanSetStaticNestedAccessor()
        {
            RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain.Nested');
                var statics = domain.ClassWithStaticFields;
                statics.Setter = 'hello';
                assert(statics.Setter == 'hello');
            ");

            Assert.Equal(Nested.ClassWithStaticFields.Setter, "hello");
        }

        [Fact]
        public void CantSetStaticNestedReadonly()
        {
            RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain.Nested');
                var statics = domain.ClassWithStaticFields;
                statics.Readonly = 'hello';
                assert(statics.Readonly == 'Readonly');
            ");

            Assert.Equal(Nested.ClassWithStaticFields.Readonly, "Readonly");
        }

        [Fact]
        public void ShouldExecuteFunctionWithValueTypeParameterCorrectly()
        {
            _engine.SetValue("a", new A());
            // Func<int, int>
            RunTest(@"
                assert(a.Call17(function(value){ return value; }) === 17);
            ");
        }

        [Fact]
        public void ShouldExecuteActionWithValueTypeParameterCorrectly()
        {
            _engine.SetValue("a", new A());
            // Action<int>
            RunTest(@"
                a.Call18(function(value){ assert(value === 18); });
            ");
        }

        [Fact]
        public void ShouldConvertToJsValue()
        {
            RunTest(@"
                var now = System.DateTime.Now;
                assert(new String(now) == now.toString());

                var zero = System.Int32.MaxValue;
                assert(new String(zero) == zero.toString());
            ");
        }

        [Fact]
        public void ShouldNotCatchClrExceptions()
        {
            var engine = new Engine()
                .SetValue("throwMyException", new Action(() => { throw new NotSupportedException(); }))
                .SetValue("Thrower", typeof(Thrower))
                .Execute(@"
                    function throwException1(){
                        try {
                            throwMyException();
                            return;
                        }
                        catch(e) {
                            return;
                        }
                    }

                    function throwException2(){
                        try {
                            new Thrower().ThrowNotSupportedException();
                            return;
                        }
                        catch(e) {
                            return;
                        }
                    }
                ");

            Assert.ThrowsAny<NotSupportedException>(() => engine.Invoke("throwException1"));
            Assert.ThrowsAny<NotSupportedException>(() => engine.Invoke("throwException2"));
        }

        [Fact]
        public void ShouldCatchAllClrExceptions()
        {
            string exceptionMessage = "myExceptionMessage";

            var engine = new Engine(o => o.CatchClrExceptions())
                .SetValue("throwMyException", new Action(() => { throw new Exception(exceptionMessage); }))
                .SetValue("Thrower", typeof(Thrower))
                .Execute(@"
                    function throwException1(){
                        try {
                            throwMyException();
                            return '';
                        }
                        catch(e) {
                            return e.message;
                        }
                    }

                    function throwException2(){
                        try {
                            new Thrower().ThrowExceptionWithMessage('myExceptionMessage');
                            return;
                        }
                        catch(e) {
                            return e.message;
                        }
                    }
                ");

            Assert.Equal(engine.Invoke("throwException1").AsString(), exceptionMessage);
            Assert.Equal(engine.Invoke("throwException2").AsString(), exceptionMessage);
        }

        class MemberExceptionTest
        {
            public MemberExceptionTest(bool throwOnCreate)
            {
                if (throwOnCreate)
                    throw new InvalidOperationException();
            }

            public JsValue ThrowingProperty1
            {
                get { throw new InvalidOperationException(); }
                set { throw new InvalidOperationException(); }
            }

            public object ThrowingProperty2
            {
                get { throw new InvalidOperationException(); }
                set { throw new InvalidOperationException(); }
            }

            public void ThrowingFunction()
            {
                throw new InvalidOperationException();
            }
        }

        [Fact]
        public void ShouldCatchClrMemberExceptions()
        {
            var engine = new Engine(cfg =>
            {
                cfg.AllowClr();
                cfg.CatchClrExceptions();
            });

            engine.SetValue("assert", new Action<bool>(Assert.True));
            engine.SetValue("log", new Action<object>(Console.WriteLine));
            engine.SetValue("create", typeof(MemberExceptionTest));
            engine.SetValue("instance", new MemberExceptionTest(throwOnCreate: false));

            // Test calling a constructor that throws an exception
            engine.Execute(@"
                try
                {
                    create(true);
                    assert(false);
                }
                catch (e)
                {
                    assert(true);
                }
            ");

            // Test calling a member function that throws an exception
            engine.Execute(@"
                try
                {
                    instance.ThrowingFunction();
                    assert(false);
                }
                catch (e)
                {
                    assert(true);
                }
            ");

            // Test using a property getter that throws an exception
            engine.Execute(@"
                try
                {
                    log(o.ThrowingProperty);
                    assert(false);
                }
                catch (e)
                {
                    assert(true);
                }
            ");

            // Test using a property setter that throws an exception
            engine.Execute(@"
                try
                {
                    instance.ThrowingProperty1 = 123;
                    assert(false);
                }
                catch (e)
                {
                    assert(true);
                }

                try
                {
                    instance.ThrowingProperty2 = 456;
                    assert(false);
                }
                catch (e)
                {
                    assert(true);
                }
            ");
        }

        [Fact]
        public void ShouldCatchSomeExceptions()
        {
            string exceptionMessage = "myExceptionMessage";

            var engine = new Engine(o => o.CatchClrExceptions(e => e is NotSupportedException))
                .SetValue("throwMyException1", new Action(() => { throw new NotSupportedException(exceptionMessage); }))
                .SetValue("throwMyException2", new Action(() => { throw new ArgumentNullException(); }))
                .SetValue("Thrower", typeof(Thrower))
                .Execute(@"
                    function throwException1(){
                        try {
                            throwMyException1();
                            return '';
                        }
                        catch(e) {
                            return e.message;
                        }
                    }

                    function throwException2(){
                        try {
                            throwMyException2();
                            return '';
                        }
                        catch(e) {
                            return e.message;
                        }
                    }

                    function throwException3(){
                        try {
                            new Thrower().ThrowNotSupportedExceptionWithMessage('myExceptionMessage');
                            return '';
                        }
                        catch(e) {
                            return e.message;
                        }
                    }

                    function throwException4(){
                        try {
                            new Thrower().ThrowArgumentNullException();
                            return '';
                        }
                        catch(e) {
                            return e.message;
                        }
                    }
                ");

            Assert.Equal(engine.Invoke("throwException1").AsString(), exceptionMessage);
            Assert.Throws<ArgumentNullException>(() => engine.Invoke("throwException2"));
            Assert.Equal(engine.Invoke("throwException3").AsString(), exceptionMessage);
            Assert.Throws<ArgumentNullException>(() => engine.Invoke("throwException4"));
        }

        [Fact]
        public void ArrayFromShouldConvertListToArrayLike()
        {
            var list = new List<Person>
            {
                new Person {Name = "Mike"},
                new Person {Name = "Mika"}
            };
            _engine.SetValue("a", list);

            RunTest(@"
                var arr = new Array(a);
                assert(arr.length === 2);
                assert(arr[0].Name === 'Mike');
                assert(arr[1].Name === 'Mika');
            ");

            RunTest(@"
                var arr = Array.from(a);
                assert(arr.length === 2);
                assert(arr[0].Name === 'Mike');
                assert(arr[1].Name === 'Mika');
            ");
        }

        [Fact]
        public void ArrayFromShouldConvertArrayToArrayLike()
        {
            var list = new []
            {
                new Person {Name = "Mike"},
                new Person {Name = "Mika"}
            };
            _engine.SetValue("a", list);

            RunTest(@"
                var arr = new Array(a);
                assert(arr.length === 2);
                assert(arr[0].Name === 'Mike');
                assert(arr[1].Name === 'Mika');
            ");

            RunTest(@"
                var arr = Array.from(a);
                assert(arr.length === 2);
                assert(arr[0].Name === 'Mike');
                assert(arr[1].Name === 'Mika');
            ");
        }

        [Fact]
        public void ArrayFromShouldConvertIEnumerable()
        {
            var enumerable = new []
            {
                new Person {Name = "Mike"},
                new Person {Name = "Mika"}
            }.Select(x => x);

            _engine.SetValue("a", enumerable);

            RunTest(@"
                var arr = new Array(a);
                assert(arr.length === 2);
                assert(arr[0].Name === 'Mike');
                assert(arr[1].Name === 'Mika');
            ");

            RunTest(@"
                var arr = Array.from(a);
                assert(arr.length === 2);
                assert(arr[0].Name === 'Mike');
                assert(arr[1].Name === 'Mika');
            ");
        }

        [Fact]
        public void ShouldBeAbleToPlusAssignStringProperty()
        {
            var p = new Person();
            var engine = new Engine();
            engine.SetValue("P", p);
            engine.Execute("P.Name = 'b';");
            engine.Execute("P.Name += 'c';");
            Assert.Equal("bc", p.Name);
        }

        [Fact]
        public void ShouldNotResolveToPrimitiveSymbol()
        {
            var engine = new Engine(options => 
                options.AllowClr(typeof(FloatIndexer).GetTypeInfo().Assembly));
            var c = engine.Execute(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                return new domain.FloatIndexer();
            ").GetCompletionValue();

            Assert.NotNull(c.ToString());
            Assert.Equal((uint)0, c.As<ObjectInstance>().Length);
        }

        class DictionaryWrapper
        {
            public IDictionary<string, object> Values { get; set; }
        }

        class DictionaryTest
        {
            public void Test1(IDictionary<string, object> values)
            {
                Assert.Equal(1, Convert.ToInt32(values["a"]));
            }

            public void Test2(DictionaryWrapper dictionaryObject)
            {
                Assert.Equal(1, Convert.ToInt32(dictionaryObject.Values["a"]));
            }
        }

        [Fact]
        public void ShouldBeAbleToPassDictionaryToMethod()
        {
            var engine = new Engine();
            engine.SetValue("dictionaryTest", new DictionaryTest());
            engine.Execute("dictionaryTest.test1({ a: 1 });");
        }

        [Fact]
        public void ShouldBeAbleToPassDictionaryInObjectToMethod()
        {
            var engine = new Engine();
            engine.SetValue("dictionaryTest", new DictionaryTest());
            engine.Execute("dictionaryTest.test2({ values: { a: 1 } });");
        }

        [Fact]
        public void ShouldSupportSpreadForDictionary()
        {
            var engine = new Engine();
            var state = new Dictionary<string, object>
            {
                {"invoice", new Dictionary<string, object> {["number"] = "42"}}
            };
            engine.SetValue("state", state);

            var result = (IDictionary<string, object>) engine
                .Execute("({ supplier: 'S1', ...state.invoice })")
                .GetCompletionValue()
                .ToObject();

            Assert.Equal("S1", result["supplier"]);
            Assert.Equal("42", result["number"]);            
        }
        
        [Fact]
        public void ShouldSupportSpreadForDictionary2()
        {
            var engine = new Engine();
            var state = new Dictionary<string, object>
            {
                {"invoice", new Dictionary<string, object> {["number"] = "42"}}
            };
            engine.SetValue("state", state);

            var result = (IDictionary<string, object>) engine
                .Execute("function getValue() { return {supplier: 'S1', ...state.invoice}; }")
                .Invoke("getValue")
                .ToObject();
            
            Assert.Equal("S1", result["supplier"]);
            Assert.Equal("42", result["number"]);    
        }        

        [Fact]
        public void ShouldSupportSpreadForObject()
        {
            var engine = new Engine();
            var person = new Person
            {
                Name = "Mike",
                Age = 20
            };
            engine.SetValue("p", person);

            var result = (IDictionary<string, object>) engine
                .Execute("({ supplier: 'S1', ...p })")
                .GetCompletionValue()
                .ToObject();

            Assert.Equal("S1", result["supplier"]);
            Assert.Equal("Mike", result["Name"]);         
            Assert.Equal(20d, result["Age"]);         
        }

        [Fact]
        public void ShouldBeAbleToJsonStringifyClrObjects()
        {
            var engine = new Engine();

            engine.Execute("var jsObj = { 'key1' :'value1', 'key2' : 'value2' }");

            engine.SetValue("netObj", new Dictionary<string, object>
            {
                {"key1", "value1"},
                {"key2", "value2"},
            });

            var jsValue = engine.Execute("jsObj['key1']").GetCompletionValue().AsString();
            var clrValue = engine.Execute("netObj['key1']").GetCompletionValue().AsString();
            Assert.Equal(jsValue, clrValue);

            jsValue = engine.Execute("JSON.stringify(jsObj)").GetCompletionValue().AsString();
            clrValue = engine.Execute("JSON.stringify(netObj)").GetCompletionValue().AsString();
            Assert.Equal(jsValue, clrValue);

            // Write properties on screen using showProps function defined on https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Working_with_Objects
            engine.Execute(@"function showProps(obj, objName) {
  var result = """";
  for (var i in obj) {
    if (obj.hasOwnProperty(i)) {
      result += objName + ""."" + i + "" = "" + obj[i] + ""\n"";
    }
    }
  return result;
}");
            jsValue = engine.Execute("showProps(jsObj, 'theObject')").GetCompletionValue().AsString();
            clrValue = engine.Execute("showProps(jsObj, 'theObject')").GetCompletionValue().AsString();
            Assert.Equal(jsValue, clrValue);
        }

        [Fact]
        public void ShouldHideSpecificMembers()
        {
            var engine = new Engine(options => options.SetMemberAccessor((e, target, member) =>
            {
                if (target is HiddenMembers)
                {
                    if (member == nameof(HiddenMembers.Member2) || member == nameof(HiddenMembers.Method2))
                    {
                        return JsValue.Undefined;
                    }
                }

                return null;
            }));

            engine.SetValue("m", new HiddenMembers());

            Assert.Equal("Member1", engine.Execute("m.Member1").GetCompletionValue().ToString());
            Assert.Equal("undefined", engine.Execute("m.Member2").GetCompletionValue().ToString());
            Assert.Equal("Method1", engine.Execute("m.Method1()").GetCompletionValue().ToString());
            // check the method itself, not its invokation as it would mean invoking "undefined"
            Assert.Equal("undefined", engine.Execute("m.Method2").GetCompletionValue().ToString());
        }

        [Fact]
        public void ShouldOverrideMembers()
        {
            var engine = new Engine(options => options.SetMemberAccessor((e, target, member) =>
            {
                if (target is HiddenMembers && member == nameof(HiddenMembers.Member1))
                {
                    return "Orange";
                }

                return null;
            }));
            
            engine.SetValue("m", new HiddenMembers());

            Assert.Equal("Orange", engine.Execute("m.Member1").GetCompletionValue().ToString());
        }
    }
}
