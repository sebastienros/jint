using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Jint.Native;
using Jint.Native.Object;
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
                typeof(System.IO.File).GetTypeInfo().Assembly))
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("equal", new Action<object, object>(Assert.Equal))
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

        private class TestClass
        {
            public int? NullableInt { get; set; }
            public DateTime? NullableDate { get; set; }
            public bool? NullableBool { get; set; }
        }

        [Fact]
        public void CanSetNullablePropertiesOnPocos()
        {
            var instance = new TestClass();
            _engine.SetValue("instance", instance);

            RunTest(@"
                instance.NullableInt = 2;
                instance.NullableDate = new Date();
                instance.NullableBool = true;

                assert(instance.NullableInt===2);
                assert(instance.NullableDate!=null);
                assert(instance.NullableBool===true);
            ");
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
            _engine.SetValue("ud", new Dictionary<string, object>() { {"foo", "bar"} });
            _engine.SetValue("ul", new List<string>() { "foo", "bar" });

            RunTest(@"
                assert(!uo.undefinedProperty);
                assert(!ul[5]);
                assert(!ud.undefinedProperty);
            ");
        }

        [Fact]
        public void ShouldAutomaticallyConvertArraysToFindBestInteropResulution()
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
    }
}
