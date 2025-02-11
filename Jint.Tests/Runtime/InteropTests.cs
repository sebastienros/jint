using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Number;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Jint.Tests.Runtime.Converters;
using Jint.Tests.Runtime.Domain;
using Jint.Tests.Runtime.TestClasses;
using MongoDB.Bson;
using Shapes;

namespace Jint.Tests.Runtime;

public partial class InteropTests : IDisposable
{
    private readonly Engine _engine;

    public InteropTests()
    {
        _engine = new Engine(cfg => cfg.AllowClr(
                    typeof(Shape).GetTypeInfo().Assembly,
                    typeof(Console).GetTypeInfo().Assembly,
                    typeof(File).GetTypeInfo().Assembly))
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

    public class Foo
    {
        public static Bar GetBar()
        {
            return new Bar();
        }
    }

    public class Bar
    {
        public string Test { get; set; } = "123";
    }

    [Fact]
    public void ShouldStringifyNetObjects()
    {
        _engine.SetValue("foo", typeof(Foo));
        var json = _engine.Evaluate("JSON.stringify(foo.GetBar())").AsString();
        Assert.Equal("{\"Test\":\"123\"}", json);
    }


    [Fact]
    public void EngineShouldStringifyADictionary()
    {
        var engine = new Engine();

        var d = new Hashtable();
        d["Values"] = 1;
        engine.SetValue("d", d);

        Assert.Equal("{\"Values\":1}", engine.Evaluate($"JSON.stringify(d)").AsString());
    }

    [Fact]
    public void EngineShouldStringifyADictionaryOfStringAndObjectCorrectly()
    {
        var engine = new Engine();

        var dictionary = new Dictionary<string, object>
        {
            { "foo", 5 },
            { "bar", "A string" }
        };
        engine.SetValue(nameof(dictionary), dictionary);

        var result = engine.Evaluate($"JSON.stringify({nameof(dictionary)})").AsString();
        Assert.Equal("{\"foo\":5,\"bar\":\"A string\"}", result);
    }

    [Fact]
    public void EngineShouldRoundtripParsedJSONBackToStringCorrectly()
    {
        var engine = new Engine();

        const string json = "{\"foo\":5,\"bar\":\"A string\"}";
        var parsed = engine.Evaluate($"JSON.parse('{json}')").ToObject();
        engine.SetValue(nameof(parsed), parsed);

        var result = engine.Evaluate($"JSON.stringify({nameof(parsed)})").AsString();
        Assert.Equal(json, result);
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
    public void TypePropertyAccess()
    {
        var userClass = new Person();

        var result = new Engine()
            .SetValue("userclass", userClass)
            .Evaluate("userclass.TypeProperty.Name;")
            .AsString();

        Assert.Equal("Person", result);
    }

    [Fact]
    public void CanAccessMemberNamedItem()
    {
        _engine.Execute(@"
                    function item2(arg) {
                        return arg.item2
                    }
                    function item1(arg) {
                        return arg.item
                    }
                    function item3(arg) {
                        return arg.Item
                    }
            ");

        var argument = new Dictionary<string, object>
        {
            { "item2", "item2 value" },
            { "item", "item value" },
            { "Item", "Item value" }
        };

        Assert.Equal("item2 value", _engine.Invoke("item2", argument));
        Assert.Equal("item value", _engine.Invoke("item1", argument));
        Assert.Equal("Item value", _engine.Invoke("item3", argument));

        var company = new Company("Acme Ltd");
        _engine.SetValue("c", company);
        Assert.Equal("item thingie", _engine.Evaluate("c.Item"));
        Assert.Equal("item thingie", _engine.Evaluate("c.item"));
        Assert.Equal("value", _engine.Evaluate("c['key']"));
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

    class Example()
    {
        public T ExchangeGenericViaFunc<T>(Func<T> objViaFunc)
        {
            return objViaFunc();
        }

        public object ExchangeObjectViaFunc(Func<object> objViaFunc)
        {
            return objViaFunc();
        }

        public int ExchangeValueViaFunc(Func<int> objViaFunc)
        {
            return objViaFunc();
        }
    }

    [Fact]
    public void ExchangeGenericViaFunc()
    {
        _engine.SetValue("Example", new Example());

        RunTest(@"
            const result = Example.ExchangeGenericViaFunc(() => {
                return {
                    value: 42
                };
            });

            assert(result.value === 42);
        ");
    }

    [Fact]
    public void ExchangeObjectViaFunc()
    {
        _engine.SetValue("Example", new Example());

        RunTest(@"
            const result = Example.ExchangeObjectViaFunc(() => {
                return {
                    value: 42
                };
            });

            assert(result.value === 42);
        ");
    }

    [Fact]
    public void ExchangeValueViaFunc()
    {
        _engine.SetValue("Example", new Example());

        RunTest(@"
            const result = Example.ExchangeValueViaFunc(() => {
                return 42;
            });

            assert(result === 42);
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

    private class DoubleIndexedClass
    {
        public int this[int index] => index;

        public string this[string index] => index;
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
        _engine.Evaluate("list[1] = 'Donald Duck';");

        Assert.Equal("Donald Duck", _engine.Evaluate("list[1]").AsString());
        Assert.Equal("Mickey Mouse", list[0]);
        Assert.Equal("Donald Duck", list[1]);
    }

    [Fact]
    public void ShouldForOfOnLists()
    {
        _engine.SetValue("list", new List<string> { "a", "b" });

        var result = _engine.Evaluate("var l = ''; for (var x of list) l += x; return l;").AsString();

        Assert.Equal("ab", result);
    }

    [Fact]
    public void ShouldForOfOnArrays()
    {
        _engine.SetValue("arr", new[] { "a", "b" });

        var result = _engine.Evaluate("var l = ''; for (var x of arr) l += x; return l;").AsString();

        Assert.Equal("ab", result);
    }

    [Fact]
    public void ShouldForOfOnDictionaries()
    {
        _engine.SetValue("dict", new Dictionary<string, string> { { "a", "1" }, { "b", "2" } });

        var result = _engine.Evaluate("var l = ''; for (var x of dict) l += x; return l;").AsString();

        Assert.Equal("a,1b,2", result);
    }

    [Fact]
    public void ShouldForOfOnEnumerable()
    {
        _engine.SetValue("c", new Company("name"));

        var result = _engine.Evaluate("var l = ''; for (var x of c.getNameChars()) l += x + ','; return l;").AsString();

        Assert.Equal("n,a,m,e,", result);
    }

    [Fact]
    public void ShouldThrowWhenForOfOnObject()
    {
        // normal objects are not iterable in javascript
        var o = new { A = 1, B = 2 };
        _engine.SetValue("anonymous", o);

        var ex = Assert.Throws<JavaScriptException>(() => _engine.Evaluate("for (var x of anonymous) {}"));
        Assert.Equal("The value is not iterable", ex.Message);
    }

    [Fact]
    public void CanAccessAnonymousObject()
    {
        var p = new
        {
            Name = "Mickey Mouse"
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
            y = new JsString("string")
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
        var x = new JsObject(_engine);
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
            Name = "foo"
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
            Name = "foo"
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
        var p = new Person { Name = "Mickey Mouse " };
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
        public string String { get; set; }
        public int Int { get; set; }
        public int? NullableInt { get; set; }
        public DateTime? NullableDate { get; set; }
        public bool? NullableBool { get; set; }
        public bool Bool { get; set; }
        public TestEnumInt32? NullableEnum { get; set; }
        public TestStruct? NullableStruct { get; set; }

        public void SetBool(bool value)
        {
            Bool = value;
        }

        public void SetInt(int value)
        {
            Int = value;
        }

        public void SetString(string value)
        {
            String = value;
        }
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

        var name = e.Evaluate("o.values.filter(x => x.age == 12)[0].name").ToString();
        Assert.Equal("John", name);
    }


    [Fact]
    public void CanSetIsConcatSpreadableForArrays()
    {
        var engine = new Engine();

        engine
            .SetValue("list1", new List<string> { "A", "B", "C" })
            .SetValue("list2", new List<string> { "D", "E", "F" })
            .Execute("var array1 = ['A', 'B', 'C'];")
            .Execute("var array2 = ['D', 'E', 'F'];");

        Assert.True(engine.Evaluate("list1[Symbol.isConcatSpreadable] = true; list1[Symbol.isConcatSpreadable];").AsBoolean());
        Assert.True(engine.Evaluate("list2[Symbol.isConcatSpreadable] = true; list2[Symbol.isConcatSpreadable];").AsBoolean());

        Assert.Equal("[\"A\",\"B\",\"C\"]", engine.Evaluate("JSON.stringify(array1);"));
        Assert.Equal("[\"D\",\"E\",\"F\"]", engine.Evaluate("JSON.stringify(array2);"));
        Assert.Equal("[\"A\",\"B\",\"C\"]", engine.Evaluate("JSON.stringify(list1);"));
        Assert.Equal("[\"D\",\"E\",\"F\"]", engine.Evaluate("JSON.stringify(list2);"));

        const string Concatenated = "[\"A\",\"B\",\"C\",\"D\",\"E\",\"F\"]";
        Assert.Equal(Concatenated, engine.Evaluate("JSON.stringify(array1.concat(array2));"));
        Assert.Equal(Concatenated, engine.Evaluate("JSON.stringify(array1.concat(list2));"));
        Assert.Equal(Concatenated, engine.Evaluate("JSON.stringify(list1.concat(array2));"));
        Assert.Equal(Concatenated, engine.Evaluate("JSON.stringify(list1.concat(list2));"));

        Assert.False(engine.Evaluate("list1[Symbol.isConcatSpreadable] = false; list1[Symbol.isConcatSpreadable];").AsBoolean());
        Assert.False(engine.Evaluate("list2[Symbol.isConcatSpreadable] = false; list2[Symbol.isConcatSpreadable];").AsBoolean());

        Assert.Equal("[[\"A\",\"B\",\"C\"]]", engine.Evaluate("JSON.stringify([].concat(list1));"));
        Assert.Equal("[[\"A\",\"B\",\"C\"],[\"D\",\"E\",\"F\"]]", engine.Evaluate("JSON.stringify(list1.concat(list2));"));
    }

    [Fact]
    public void ShouldConvertArrayToArrayInstance()
    {
        var result = _engine
            .SetValue("values", new[] { 1, 2, 3, 4, 5, 6 })
            .Evaluate("values.filter(function(x){ return x % 2 == 0; })");

        var parts = result.ToObject();

        Assert.True(parts.GetType().IsArray);
        Assert.Equal(3, ((object[]) parts).Length);
        Assert.Equal(2d, ((object[]) parts)[0]);
        Assert.Equal(4d, ((object[]) parts)[1]);
        Assert.Equal(6d, ((object[]) parts)[2]);
    }

    [Fact]
    public void ShouldConvertListsToArrayInstance()
    {
        var result = _engine
            .SetValue("values", new List<object> { 1, 2, 3, 4, 5, 6 })
            .Evaluate("new Array(values).filter(function(x){ return x % 2 == 0; })");

        var parts = result.ToObject();

        Assert.True(parts.GetType().IsArray);
        Assert.Equal(3, ((object[]) parts).Length);
        Assert.Equal(2d, ((object[]) parts)[0]);
        Assert.Equal(4d, ((object[]) parts)[1]);
        Assert.Equal(6d, ((object[]) parts)[2]);
    }

    [Fact]
    public void ShouldConvertArrayInstanceToArray()
    {
        var parts = _engine.Evaluate("'foo@bar.com'.split('@');").ToObject();

        Assert.True(parts.GetType().IsArray);
        Assert.Equal(2, ((object[]) parts).Length);
        Assert.Equal("foo", ((object[]) parts)[0]);
        Assert.Equal("bar.com", ((object[]) parts)[1]);
    }

    [Fact]
    public void ShouldLoopWithNativeEnumerator()
    {
        JsValue adder(JsValue argValue)
        {
            var args = argValue.AsArray();
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
            .Evaluate("getSum([1,2,3]);");

        Assert.True(result == 6);
    }

    [Fact]
    public void ShouldConvertBooleanInstanceToBool()
    {
        var value = _engine.Evaluate("new Boolean(true)").ToObject();

        Assert.Equal(typeof(bool), value.GetType());
        Assert.Equal(true, value);
    }

    [Fact]
    public void ShouldAllowBooleanCoercion()
    {
        var engine = new Engine(options => { options.Interop.ValueCoercion = ValueCoercionType.Boolean; });

        engine.SetValue("o", new TestClass());
        Assert.True(engine.Evaluate("o.Bool = 1; return o.Bool;").AsBoolean());
        Assert.True(engine.Evaluate("o.Bool = 'dog'; return o.Bool;").AsBoolean());
        Assert.True(engine.Evaluate("o.Bool = {}; return o.Bool;").AsBoolean());
        Assert.False(engine.Evaluate("o.Bool = 0; return o.Bool;").AsBoolean());
        Assert.False(engine.Evaluate("o.Bool = ''; return o.Bool;").AsBoolean());
        Assert.False(engine.Evaluate("o.Bool = null; return o.Bool;").AsBoolean());
        Assert.False(engine.Evaluate("o.Bool = undefined; return o.Bool;").AsBoolean());

        engine.Evaluate("class MyClass { valueOf() { return 42; } }");
        Assert.Equal(true, engine.Evaluate("let obj = new MyClass(); o.Bool = obj; return o.Bool;").AsBoolean());

        engine.SetValue("func3", new Action<bool, bool, bool>((param1, param2, param3) =>
        {
            Assert.True(param1);
            Assert.True(param2);
            Assert.True(param3);
        }));
        engine.Evaluate("func3(true, obj, [ 1, 2, 3])");

        Assert.Equal(true, engine.Evaluate("o.SetBool(42); return o.Bool;").AsBoolean());
        Assert.Equal(true, engine.Evaluate("o.SetBool(obj); return o.Bool;").AsBoolean());
        Assert.Equal(true, engine.Evaluate("o.SetBool([ 1, 2, 3].length); return o.Bool;").AsBoolean());
    }

    [Fact]
    public void ShouldAllowNumberCoercion()
    {
        var engine = new Engine(options => { options.Interop.ValueCoercion = ValueCoercionType.Number; });

        engine.SetValue("o", new TestClass());
        Assert.Equal(1, engine.Evaluate("o.Int = true; return o.Int;").AsNumber());
        Assert.Equal(0, engine.Evaluate("o.Int = false; return o.Int;").AsNumber());

        engine.Evaluate("class MyClass { valueOf() { return 42; } }");
        Assert.Equal(42, engine.Evaluate("let obj = new MyClass(); o.Int = obj; return o.Int;").AsNumber());

        // but null and undefined should be injected as nulls to nullable objects
        Assert.True(engine.Evaluate("o.NullableInt = null; return o.NullableInt;").IsNull());
        Assert.True(engine.Evaluate("o.NullableInt = undefined; return o.NullableInt;").IsNull());

        engine.SetValue("func3", new Action<int, double, long>((param1, param2, param3) =>
        {
            Assert.Equal(1, param1);
            Assert.Equal(42, param2);
            Assert.Equal(3, param3);
        }));
        engine.Evaluate("func3(true, obj, [ 1, 2, 3].length)");

        Assert.Equal(1, engine.Evaluate("o.SetInt(true); return o.Int;").AsNumber());
        Assert.Equal(42, engine.Evaluate("o.SetInt(obj); return o.Int;").AsNumber());
        Assert.Equal(3, engine.Evaluate("o.SetInt([ 1, 2, 3].length); return o.Int;").AsNumber());
    }

    [Fact]
    public void ShouldAllowStringCoercion()
    {
        var engine = new Engine(options => { options.Interop.ValueCoercion = ValueCoercionType.String; });

        // basic premise, booleans in JS are lower-case, so should the the toString under interop
        Assert.Equal("true", engine.Evaluate("'' + true").AsString());

        engine.SetValue("o", new TestClass());
        Assert.Equal("false", engine.Evaluate("'' + o.Bool").AsString());

        Assert.Equal("true", engine.Evaluate("o.Bool = true; o.String = o.Bool; return o.String;").AsString());

        Assert.Equal("true", engine.Evaluate("o.String = true; return o.String;").AsString());

        engine.SetValue("func1", new Func<bool>(() => true));
        Assert.Equal("true", engine.Evaluate("'' + func1()").AsString());

        engine.SetValue("func2", new Func<JsValue>(() => JsBoolean.True));
        Assert.Equal("true", engine.Evaluate("'' + func2()").AsString());

        // but null and undefined should be injected as nulls to c# objects
        Assert.True(engine.Evaluate("o.String = null; return o.String;").IsNull());
        Assert.True(engine.Evaluate("o.String = undefined; return o.String;").IsNull());

        Assert.Equal("1,2,3", engine.Evaluate("o.String = [ 1, 2, 3 ]; return o.String;").AsString());

        engine.Evaluate("class MyClass { toString() { return 'hello world'; } }");
        Assert.Equal("hello world", engine.Evaluate("let obj = new MyClass(); o.String = obj; return o.String;").AsString());

        engine.SetValue("func3", new Action<string, string, string>((param1, param2, param3) =>
        {
            Assert.Equal("true", param1);
            Assert.Equal("hello world", param2);
            Assert.Equal("1,2,3", param3);
        }));
        engine.Evaluate("func3(true, obj, [ 1, 2, 3])");

        Assert.Equal("true", engine.Evaluate("o.SetString(true); return o.String;").AsString());
        Assert.Equal("hello world", engine.Evaluate("o.SetString(obj); return o.String;").AsString());
        Assert.Equal("1,2,3", engine.Evaluate("o.SetString([ 1, 2, 3]); return o.String;").AsString());
    }

    [Fact]
    public void ShouldConvertDateInstanceToDateTime()
    {
        var result = _engine.Evaluate("new Date(0)");
        var value = result.ToObject() is DateTime ? (DateTime) result.ToObject() : default;

        Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), value);
        Assert.Equal(DateTimeKind.Utc, value.Kind);
    }

    [Fact]
    public void ShouldConvertDateInstanceToLocalDateTime()
    {
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Helsinki");
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
        }

        var engine = new Engine(options =>
        {
            options.TimeZone = timeZone;
            options.Interop.DateTimeKind = DateTimeKind.Local;
        });

        var result = engine.Evaluate("new Date(0)");
        var value = result.ToObject() is DateTime ? (DateTime) result.ToObject() : default;

        Assert.Equal(new DateTime(1970, 1, 1, 2, 0, 0, DateTimeKind.Local), value);
        Assert.Equal(DateTimeKind.Local, value.Kind);
    }

    [Fact]
    public void ShouldConvertNumberInstanceToDouble()
    {
        var result = _engine.Evaluate("new Number(10)");
        var value = result.ToObject();

        Assert.Equal(typeof(double), value.GetType());
        Assert.Equal(10d, value);
    }

    [Fact]
    public void ShouldConvertStringInstanceToString()
    {
        var value = _engine.Evaluate("new String('foo')").ToObject();

        Assert.Equal(typeof(string), value.GetType());
        Assert.Equal("foo", value);
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
                var callCount = 0;
                var handler = function(sender, eventArgs) { callCount++; } ;
                collection.add_CollectionChanged(handler);
                collection.Add('test');
                collection.remove_CollectionChanged(handler);
                collection.Add('test');

                var json = JSON.stringify(Object.keys(handler));
            ");

        var callCount = (int) _engine.GetValue("callCount").AsNumber();
        Assert.Equal(1, callCount);
        Assert.Equal(2, collection.Count);

        // make sure our delegate holder is hidden
        Assert.Equal("[]", _engine.Evaluate("json"));
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
    public void ShouldImportEmptyNamespace()
    {
        RunTest("""
                    var nullSpace = importNamespace(null);
                    var c1 = new nullSpace.ShapeWithoutNameSpace();
                    assert(c1.Perimeter() === 42);
                    var undefinedSpace = importNamespace(undefined);
                    var c2 = new undefinedSpace.ShapeWithoutNameSpace();
                    assert(c2.Perimeter() === 42);
                    var defaultSpace = importNamespace();
                    var c3 = new defaultSpace.ShapeWithoutNameSpace();
                    assert(c3.Perimeter() === 42);
                """);
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

        Assert.Throws<JavaScriptException>(() => _engine.Invoke("x", 1, 2));
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
        Assert.True((bool) engine1.GetValue("result").ToObject());

        var engine2 = new Engine(o => o.AddObjectConverter(new NegateBoolConverter()));
        engine2.SetValue("p", new { Test = true });
        engine2.Execute("var result = p.Test;");
        Assert.False((bool) engine2.GetValue("result").ToObject());
    }

    [Fact]
    public void CanConvertEnumsToString()
    {
        var engine1 = new Engine(o => o.AddObjectConverter(new EnumsToStringConverter()))
            .SetValue("assert", new Action<bool>(Assert.True));
        engine1.SetValue("p", new { Comparison = StringComparison.CurrentCulture });
        engine1.Execute("assert(p.Comparison === 'CurrentCulture');");
        engine1.Execute("var result = p.Comparison;");
        Assert.Equal("CurrentCulture", (string) engine1.GetValue("result").ToObject());
    }

    [Fact]
    public void CanUserIncrementOperator()
    {
        var p = new Person
        {
            Age = 1
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
            Color = Colors.Red
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

    private enum TestEnumInt32 : int
    {
        None,
        One = 1,
        Min = int.MaxValue,
        Max = int.MaxValue
    }

    private enum TestEnumUInt32 : uint
    {
        None,
        One = 1,
        Min = uint.MaxValue,
        Max = uint.MaxValue
    }

    private enum TestEnumInt64 : long
    {
        None,
        One = 1,
        Min = long.MaxValue,
        Max = long.MaxValue
    }

    private enum TestEnumUInt64 : ulong
    {
        None,
        One = 1,
        Min = ulong.MaxValue,
        Max = ulong.MaxValue
    }

    private void TestEnum<T>(T enumValue)
    {
        var i = Convert.ChangeType(enumValue, Enum.GetUnderlyingType(typeof(T)));
        var s = Convert.ToString(i, CultureInfo.InvariantCulture);
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
            Color = Colors.Red
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
        Assert.Equal("ACME", _engine.Evaluate("c.Name"));
    }

    [Fact]
    public void ShouldUseExplicitIndexerPropertyGetter()
    {
        var company = new Company("ACME");
        ((ICompany) company)["Foo"] = "Bar";
        _engine.SetValue("c", company);
        Assert.Equal("Bar", _engine.Evaluate("c.Foo"));
    }

    [Fact]
    public void ShouldUseExplicitPropertySetter()
    {
        _engine.SetValue("c", new Company("ACME"));
        Assert.Equal("Foo", _engine.Evaluate("c.Name = 'Foo'; c.Name;"));
    }

    [Fact]
    public void ShouldUseExplicitIndexerPropertySetter()
    {
        var company = new Company("ACME");
        ((ICompany) company)["Foo"] = "Bar";
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
        _engine.SetValue("ud", new Dictionary<string, object> { { "foo", "bar" } });
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
    public void ShouldGetNestedTypeFromParentType()
    {
        RunTest(@"
                var Shapes = importNamespace('Shapes');
                var usages = Shapes.Circle.Meta.Usage;
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
        var exceptionMessage = "myExceptionMessage";

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

    [Fact]
    public void ShouldNotCatchClrFromApply()
    {
        var engine = new Engine(options =>
        {
            options.CatchClrExceptions(e =>
            {
                Assert.Fail("was called");
                return true;
            });
        });

        engine.Execute(@"
                function throwError() {
                    throw new Error();
                }

                // doesn't cause ExceptionDelegateHandler call
                try { throwError(); } catch {}

                // does cause ExceptionDelegateHandler call
                try { throwError.apply(); } catch {}
            ");
    }

    private class MemberExceptionTest
    {
        public MemberExceptionTest(bool throwOnCreate)
        {
            if (throwOnCreate)
            {
                throw new InvalidOperationException("thrown as requested");
            }
        }

        public JsValue ThrowingProperty1
        {
            get => throw new InvalidOperationException();
            set => throw new InvalidOperationException();
        }

        public object ThrowingProperty2
        {
            get => throw new InvalidOperationException();
            set => throw new InvalidOperationException();
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
        engine.SetValue("instance", new MemberExceptionTest(false));

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
        var exceptionMessage = "myExceptionMessage";

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
            new Person { Name = "Mike" },
            new Person { Name = "Mika" }
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
        var list = new[]
        {
            new Person { Name = "Mike" },
            new Person { Name = "Mika" }
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
        var enumerable = new[]
        {
            new Person { Name = "Mike" },
            new Person { Name = "Mika" }
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
        engine.Evaluate("P.Name = 'b';");
        engine.Evaluate("P.Name += 'c';");
        Assert.Equal("bc", p.Name);
    }

    [Fact]
    public void ShouldNotResolveToPrimitiveSymbol()
    {
        var engine = new Engine(options =>
            options.AllowClr(typeof(FloatIndexer).GetTypeInfo().Assembly));
        var c = engine.Evaluate(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                return new domain.FloatIndexer();
            ");

        Assert.NotNull(c.ToString());
        Assert.Equal((uint) 0, c.AsObject().GetLength());
    }

    private class DictionaryWrapper
    {
        public IDictionary<string, object> Values { get; set; }
    }

    private class DictionaryTest
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
        engine.Evaluate("dictionaryTest.test1({ a: 1 });");
    }

    [Fact]
    public void ShouldBeAbleToPassDictionaryInObjectToMethod()
    {
        var engine = new Engine();
        engine.SetValue("dictionaryTest", new DictionaryTest());
        engine.Evaluate("dictionaryTest.test2({ values: { a: 1 } });");
    }

    [Fact]
    public void ShouldSupportSpreadForDictionary()
    {
        var engine = new Engine();
        var state = new Dictionary<string, object>
        {
            { "invoice", new Dictionary<string, object> { ["number"] = "42" } }
        };
        engine.SetValue("state", state);

        var result = (IDictionary<string, object>) engine
            .Evaluate("({ supplier: 'S1', ...state.invoice })")
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
            { "invoice", new Dictionary<string, object> { ["number"] = "42" } }
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
            .Evaluate("({ supplier: 'S1', ...p })")
            .ToObject();

        Assert.Equal("S1", result["supplier"]);
        Assert.Equal("Mike", result["Name"]);
        Assert.Equal(20d, result["Age"]);
    }

    [Fact]
    public void ShouldBeAbleToJsonStringifyClrObjects()
    {
        var engine = new Engine();

        engine.Evaluate("var jsObj = { 'key1' :'value1', 'key2' : 'value2' }");

        engine.SetValue("netObj", new Dictionary<string, object>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        });

        var jsValue = engine.Evaluate("jsObj['key1']").AsString();
        var clrValue = engine.Evaluate("netObj['key1']").AsString();
        Assert.Equal(jsValue, clrValue);

        jsValue = engine.Evaluate("JSON.stringify(jsObj)").AsString();
        clrValue = engine.Evaluate("JSON.stringify(netObj)").AsString();
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
        jsValue = engine.Evaluate("showProps(jsObj, 'theObject')").AsString();
        clrValue = engine.Evaluate("showProps(jsObj, 'theObject')").AsString();
        Assert.Equal(jsValue, clrValue);
    }

    [Fact]
    public void SettingValueViaIntegerIndexer()
    {
        var engine = new Engine(cfg => cfg.AllowClr(typeof(FloatIndexer).GetTypeInfo().Assembly));
        engine.SetValue("log", new Action<object>(Console.WriteLine));
        engine.Execute(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                var fia = new domain.IntegerIndexer();
                log(fia[0]);
            ");

        Assert.Equal(123, engine.Evaluate("fia[0]").AsNumber());
        engine.Evaluate("fia[0] = 678;");
        Assert.Equal(678, engine.Evaluate("fia[0]").AsNumber());
    }

    [Fact]
    public void IndexingBsonProperties()
    {
        const string jsonAnimals = @" { ""Animals"": [ { ""Id"": 1, ""Type"": ""Cat"" } ] }";
        var bsonAnimals = BsonDocument.Parse(jsonAnimals);

        _engine.SetValue("animals", bsonAnimals["Animals"]);

        // weak equality does conversions from native types
        Assert.True(_engine.Evaluate("animals[0].Type == 'Cat'").AsBoolean());
        Assert.True(_engine.Evaluate("animals[0].Id == 1").AsBoolean());
    }

    [Fact]
    public void IntegerAndFloatInFunctionOverloads()
    {
        var engine = new Engine(options => options.AllowClr(GetType().Assembly));
        engine.SetValue("a", new OverLoading());
        Assert.Equal("int-val", engine.Evaluate("a.testFunc(123);").AsString());
        Assert.Equal("float-val", engine.Evaluate("a.testFunc(12.3);").AsString());
    }

    [Fact]
    public void TypeConversionWithTemporaryInvalidValuesShouldNotCache()
    {
        var engine = new Engine(options => options.AllowClr());
        engine.SetValue("IntValueInput", TypeReference.CreateTypeReference(engine, typeof(IntValueInput)));
        var ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate("new IntValueInput().testFunc(NaN);").AsString());
        Assert.Equal("No public methods with the specified arguments were found.", ex.Message);

        Assert.Equal(123, engine.Evaluate("new IntValueInput().testFunc(123);").AsNumber());
    }

    [Fact]
    public void CanConvertFloatingPointToIntegerWithoutError()
    {
        var engine = new Engine(options => options.AllowClr());
        engine.SetValue("IntValueInput", TypeReference.CreateTypeReference(engine, typeof(IntValueInput)));
        Assert.Equal(12, engine.Evaluate("new IntValueInput().testFunc(12.3);").AsNumber());
    }

    public class IntValueInput
    {
        public int TestFunc(int value) => value;
    }

    public class TestItem
    {
        public double Cost { get; set; }

        public double Age { get; set; }

        public string Name { get; set; }
    }

    public class TestItemList : List<TestItem>
    {
        public double Sum(Func<TestItem, double> calc)
        {
            double rc = 0;

            foreach (var item in this)
            {
                rc += calc(item);
            }

            return rc;
        }

        public TestItemList Where(Func<TestItem, bool> cond)
        {
            var rc = new TestItemList();

            foreach (var item in this)
            {
                if (cond(item))
                {
                    rc.Add(item);
                }
            }

            return rc;
        }
    }

    [Fact]
    public void DelegateCanReturnValue()
    {
        var engine = new Engine(options => options.AllowClr(GetType().Assembly));

        var lst = new TestItemList();

        lst.Add(new TestItem() { Name = "a", Cost = 1, Age = 10 });
        lst.Add(new TestItem() { Name = "a", Cost = 1, Age = 10 });
        lst.Add(new TestItem() { Name = "b", Cost = 1, Age = 10 });
        lst.Add(new TestItem() { Name = "b", Cost = 1, Age = 10 });
        lst.Add(new TestItem() { Name = "b", Cost = 1, Age = 10 });

        engine.SetValue("lst", lst);

        Assert.Equal(5, engine.Evaluate("lst.Sum(x => x.Cost);").AsNumber());
        Assert.Equal(50, engine.Evaluate("lst.Sum(x => x.Age);").AsNumber());
        Assert.Equal(3, engine.Evaluate("lst.Where(x => x.Name == 'b').Count;").AsNumber());
        Assert.Equal(30, engine.Evaluate("lst.Where(x => x.Name == 'b').Sum(x => x.Age);").AsNumber());
    }

    [Fact]
    public void ObjectWrapperOverridingEquality()
    {
        // equality same via name
        _engine.SetValue("a", new Person { Name = "Name" });
        _engine.SetValue("b", new Person { Name = "Name" });
        _engine.Evaluate("const arr = [ null, a, undefined ];");

        Assert.Equal(1, _engine.Evaluate("arr.filter(x => x == b).length").AsNumber());
        Assert.Equal(1, _engine.Evaluate("arr.filter(x => x === b).length").AsNumber());

        Assert.True(_engine.Evaluate("arr.find(x => x == b) === a").AsBoolean());
        Assert.True(_engine.Evaluate("arr.find(x => x === b) == a").AsBoolean());

        Assert.Equal(1, _engine.Evaluate("arr.findIndex(x => x == b)").AsNumber());
        Assert.Equal(1, _engine.Evaluate("arr.findIndex(x => x === b)").AsNumber());

        Assert.Equal(1, _engine.Evaluate("arr.indexOf(b)").AsNumber());
        Assert.True(_engine.Evaluate("arr.includes(b)").AsBoolean());
    }

    [Fact]
    public void ObjectWrapperWrappingDictionaryShouldNotBeArrayLike()
    {
        var wrapper = ObjectWrapper.Create(_engine, new Dictionary<string, object>());
        Assert.False(wrapper.IsArrayLike);
    }

    [Fact]
    public void ShouldHandleCyclicReferences()
    {
        var engine = new Engine();

        static void Test(string message, object value)
        {
            Console.WriteLine(message);
        }

        engine.Realm.GlobalObject.FastSetDataProperty("global", engine.Realm.GlobalObject);
        engine.Realm.GlobalObject.FastSetDataProperty("test", new DelegateWrapper(engine, (Action<string, object>) Test));

        {
            var ex = Assert.Throws<JavaScriptException>(() => engine.Realm.GlobalObject.ToObject());
            Assert.Equal("Cyclic reference detected.", ex.Message);
        }

        {
            var ex = Assert.Throws<JavaScriptException>(() =>
                engine.Execute(@"
                    var demo={};
                    demo.value=1;
                    test('Test 1', demo.value===1);
                    test('Test 2', demo.value);
                    demo.demo=demo;
                    test('Test 3', demo);
                    test('Test 4', global);"
                )
            );
            Assert.Equal("Cyclic reference detected.", ex.Message);
        }
    }

    [Fact]
    public void CanConfigurePropertyNameMatcher()
    {
        // defaults
        var e = new Engine();
        e.SetValue("a", new A());
        Assert.True(e.Evaluate("a.call1").IsObject());
        Assert.True(e.Evaluate("a.Call1").IsObject());
        Assert.True(e.Evaluate("a.CALL1").IsUndefined());

        e = new Engine(options =>
        {
            options.SetTypeResolver(new TypeResolver
            {
                MemberNameComparer = StringComparer.Ordinal
            });
        });
        e.SetValue("a", new A());
        Assert.True(e.Evaluate("a.call1").IsUndefined());
        Assert.True(e.Evaluate("a.Call1").IsObject());
        Assert.True(e.Evaluate("a.CALL1").IsUndefined());

        e = new Engine(options =>
        {
            options.SetTypeResolver(new TypeResolver
            {
                MemberNameComparer = StringComparer.OrdinalIgnoreCase
            });
        });
        e.SetValue("a", new A());
        Assert.True(e.Evaluate("a.call1").IsObject());
        Assert.True(e.Evaluate("a.Call1").IsObject());
        Assert.True(e.Evaluate("a.CALL1").IsObject());
    }

    [Fact]
    public void ShouldNotEnumerateClassMethods()
    {
        var engine = new Engine();

        var dictionary = new Dictionary<string, object>
        {
            { "foo", 5 },
            { "bar", "A string" }
        };
        engine.SetValue("dictionary", dictionary);

        var result = engine.Evaluate($"Object.keys(dictionary).join(',')").AsString();
        Assert.Equal("foo,bar", result);


        engine.Execute("dictionary.ContainsKey('foo')");
        result = engine.Evaluate($"Object.keys(dictionary).join(',')").AsString();
        Assert.Equal("foo,bar", result);
    }

    [Fact]
    public void ShouldNotEnumerateExtensionMethods()
    {
        var engine = new Engine(cfg => cfg.AddExtensionMethods(typeof(Enumerable)));

        var result = engine.Evaluate("Object.keys({ ...[1,2,3] }).join(',')").AsString();
        Assert.Equal("0,1,2", result);

        var script = @"
                var arr = [1,2,3];
                var keys = [];
                for(var index in arr) keys.push(index);
                keys.join(',');
            ";
        result = engine.Evaluate(script).ToString();
        Assert.Equal("0,1,2", result);
    }

    [Fact]
    public void CanCheckIfCallable()
    {
        var engine = new Engine();
        engine.Evaluate("var f = () => true;");

        var result = engine.GetValue("f");
        Assert.True(result.IsCallable);

        Assert.True(result.Call(Array.Empty<JsValue>()).AsBoolean());
        Assert.True(result.Call().AsBoolean());
    }

    [Fact]
    public void CanGiveCustomNameToInteropMembers()
    {
        static IEnumerable<string> MemberNameCreator(MemberInfo prop)
        {
            var attributes = prop.GetCustomAttributes(typeof(CustomNameAttribute), true);
            if (attributes.Length > 0)
            {
                foreach (CustomNameAttribute attribute in attributes)
                {
                    yield return attribute.Name;
                }
            }
            else
            {
                yield return prop.Name;
            }
        }

        var customTypeResolver = new TypeResolver
        {
            MemberNameCreator = MemberNameCreator
        };

        var engine = new Engine(options =>
        {
            options.SetTypeResolver(customTypeResolver);
            options.AddExtensionMethods(typeof(CustomNamedExtensions));
        });

        engine.SetValue("o", new CustomNamed());
        Assert.Equal("StringField", engine.Evaluate("o.jsStringField").AsString());
        Assert.Equal("StringField", engine.Evaluate("o.jsStringField2").AsString());
        Assert.Equal("StringProperty", engine.Evaluate("o.jsStringProperty").AsString());
        Assert.Equal("Method", engine.Evaluate("o.jsMethod()").AsString());
        Assert.Equal("InterfaceStringProperty", engine.Evaluate("o.jsInterfaceStringProperty").AsString());
        Assert.Equal("InterfaceMethod", engine.Evaluate("o.jsInterfaceMethod()").AsString());
        Assert.Equal("ExtensionMethod", engine.Evaluate("o.jsExtensionMethod()").AsString());

        // static methods are reported by default, unlike properties and fields
        Assert.Equal("StaticMethod", engine.Evaluate("o.jsStaticMethod()").AsString());

        engine.SetValue("CustomNamed", typeof(CustomNamed));
        Assert.Equal("StaticStringField", engine.Evaluate("CustomNamed.jsStaticStringField").AsString());
        Assert.Equal("StaticMethod", engine.Evaluate("CustomNamed.jsStaticMethod()").AsString());

        engine.SetValue("XmlHttpRequest", typeof(CustomNamedEnum));
        engine.Evaluate("o.jsEnumProperty = XmlHttpRequest.HEADERS_RECEIVED;");
        Assert.Equal((int) CustomNamedEnum.HeadersReceived, engine.Evaluate("o.jsEnumProperty").AsNumber());

        // can get static members with different configuration
        var engineWithStaticsReported = new Engine(options => options.Interop.ObjectWrapperReportedFieldBindingFlags |= BindingFlags.Static);
        engineWithStaticsReported.SetValue("o", new CustomNamed());
        Assert.Equal("StaticMethod", engineWithStaticsReported.Evaluate("o.staticMethod()").AsString());
        Assert.Equal("StaticStringField", engineWithStaticsReported.Evaluate("o.staticStringField").AsString());
    }

    [Fact]
    public void ShouldBeAbleToHandleInvalidClrConversionViaCatchClrExceptions()
    {
        var engine = new Engine(cfg => cfg.CatchClrExceptions());
        engine.SetValue("a", new Person());
        var ex = Assert.Throws<JavaScriptException>(() => engine.Execute("a.age = 'It will not work, but it is normal'"));
        Assert.Contains("input string ", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" was not in a correct format", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShouldLetNotSupportedExceptionBubble()
    {
        _engine.SetValue("profile", new Profile());
        var ex = Assert.Throws<NotSupportedException>(() => _engine.Evaluate("profile.AnyProperty"));
        Assert.Equal("NOT SUPPORTED", ex.Message);
    }

    [Fact]
    public void ShouldBeAbleToUseConvertibleStructAsMethodParameter()
    {
        _engine.SetValue("test", new DiscordTestClass());
        _engine.SetValue("id", new DiscordId("12345"));

        Assert.Equal("12345", _engine.Evaluate("String(id)").AsString());
        Assert.Equal("12345", _engine.Evaluate("test.echo('12345')").AsString());
        Assert.Equal("12345", _engine.Evaluate("test.create(12345)").AsString());
    }

    [Fact]
    public void ShouldGetIteratorForListAndDictionary()
    {
        const string Script = @"
                var it = collection[Symbol.iterator]();
                var result = it.next();
                var str = """";
                while (!result.done) {
                    str += result.value;
                    result = it.next();
                }
                return str;";

        _engine.SetValue("collection", new List<string> { "a", "b", "c" });
        Assert.Equal("abc", _engine.Evaluate(Script));

        _engine.SetValue("collection", new Dictionary<string, object> { { "a", 1 }, { "b", 2 }, { "c", 3 } });
        Assert.Equal("a,1b,2c,3", _engine.Evaluate(Script));
    }

    [Fact]
    public void ShouldNotIntroduceNewPropertiesWhenTraversing()
    {
        _engine.SetValue("x", new Dictionary<string, int> { { "First", 1 }, { "Second", 2 } });

        Assert.Equal("[\"First\",\"Second\"]", _engine.Evaluate("JSON.stringify(Object.keys(x))"));

        Assert.Equal("x['First']: 1", _engine.Evaluate("\"x['First']: \" + x['First']"));
        Assert.Equal("[\"First\",\"Second\"]", _engine.Evaluate("JSON.stringify(Object.keys(x))"));

        Assert.Equal("x['Third']: undefined", _engine.Evaluate("\"x['Third']: \" + x['Third']"));
        Assert.Equal("[\"First\",\"Second\"]", _engine.Evaluate("JSON.stringify(Object.keys(x))"));

        Assert.Equal(JsValue.Undefined, _engine.Evaluate("x.length"));
        Assert.Equal("[\"First\",\"Second\"]", _engine.Evaluate("JSON.stringify(Object.keys(x))"));

        Assert.Equal(2, _engine.Evaluate("x.Count").AsNumber());
        Assert.Equal("[\"First\",\"Second\"]", _engine.Evaluate("JSON.stringify(Object.keys(x))"));

        _engine.Evaluate("x.Clear();");

        Assert.Equal("[]", _engine.Evaluate("JSON.stringify(Object.keys(x))"));

        _engine.Evaluate("x['Fourth'] = 4;");
        Assert.Equal("[\"Fourth\"]", _engine.Evaluate("JSON.stringify(Object.keys(x))"));

        Assert.False(_engine.Evaluate("Object.prototype.hasOwnProperty.call(x, 'Third')").AsBoolean());
    }

    [Fact]
    public void CanConfigureCustomObjectTypeForJsToClrConversion()
    {
        var engine = new Engine(options =>
        {
            options.Interop.CreateClrObject = oi => new Dictionary<string, object>();
        });

        object capture = null;
        var callback = (object value) => capture = value;
        engine.SetValue("callback", callback);
        engine.Evaluate("callback(({'a': 'b'}));");

        Assert.IsType<Dictionary<string, object>>(capture);
        var dictionary = (Dictionary<string, object>) capture;
        Assert.Equal("b", dictionary["a"]);
    }

    [Fact]
    public void ArrayPrototypeIndexOfWithInteropList()
    {
        var engine = new Jint.Engine();

        engine.SetValue("list", new List<string> { "A", "B", "C" });

        Assert.Equal(1, engine.Evaluate("list.indexOf('B')"));
        Assert.Equal(1, engine.Evaluate("list.lastIndexOf('B')"));

        Assert.Equal(1, engine.Evaluate("Array.prototype.indexOf.call(list, 'B')"));
        Assert.Equal(1, engine.Evaluate("Array.prototype.lastIndexOf.call(list, 'B')"));
    }

    [Fact]
    public void ArrayPrototypeFindWithInteropList()
    {
        var engine = new Jint.Engine();
        var list = new List<string> { "A", "B", "C" };

        engine.SetValue("list", list);

        Assert.Equal(1, engine.Evaluate("list.findIndex((x) => x === 'B')"));
        Assert.Equal('B', engine.Evaluate("list.find((x) => x === 'B')"));
    }

    [Fact]
    public void ArrayPrototypePushWithInteropList()
    {
        var engine = new Jint.Engine();

        var list = new List<string> { "A", "B", "C" };

        engine.SetValue("list", list);

        engine.Evaluate("list.push('D')");
        Assert.Equal(4, list.Count);
        Assert.Equal("D", list[3]);
        Assert.Equal(3, engine.Evaluate("list.lastIndexOf('D')"));
    }

    [Fact]
    public void ArrayPrototypePopWithInteropList()
    {
        var engine = new Jint.Engine();

        var list = new List<string> { "A", "B", "C" };
        engine.SetValue("list", list);

        Assert.Equal(2, engine.Evaluate("list.lastIndexOf('C')"));
        Assert.Equal(3, list.Count);
        Assert.Equal("C", engine.Evaluate("list.pop()"));
        Assert.Equal(2, list.Count);
        Assert.Equal(-1, engine.Evaluate("list.lastIndexOf('C')"));
    }

    [Fact]
    public void ShouldBeJavaScriptException()
    {
        var engine = new Engine(cfg => cfg.AllowClr().AllowOperatorOverloading().CatchClrExceptions());
        engine.SetValue("Dimensional", typeof(Dimensional));

        engine.Execute(@"	
				function Eval(param0, param1)
				{ 
					var result = param0 + param1;
					return result;
				}");
        // checking working custom type
        Assert.Equal(new Dimensional("kg", 90), (new Dimensional("kg", 30) + new Dimensional("kg", 60)));
        Assert.Equal(new Dimensional("kg", 90), engine.Invoke("Eval", new object[] { new Dimensional("kg", 30), new Dimensional("kg", 60) }).ToObject());
        Assert.Throws<InvalidOperationException>(() => new Dimensional("kg", 30) + new Dimensional("piece", 70));

        // checking throwing exception in override operator
        string errorMsg = string.Empty;
        errorMsg = Assert.Throws<JavaScriptException>(() => engine.Invoke("Eval", new object[] { new Dimensional("kg", 30), new Dimensional("piece", 70) })).Message;
        Assert.Equal("Dimensionals with different measure types are non-summable", errorMsg);
    }

    private class Profile
    {
        public int AnyProperty => throw new NotSupportedException("NOT SUPPORTED");
    }

    [Fact]
    public void GenericParameterResolutionShouldWorkWithNulls()
    {
        var result = new Engine()
            .SetValue("JintCommon", new JintCommon())
            .Evaluate("JintCommon.sum(1, null)")
            .AsNumber();

        Assert.Equal(2, result);
    }

    public class JintCommon
    {
        public int Sum(int a, int? b) => a + b.GetValueOrDefault(1);
    }

    private delegate void ParamsTestDelegate(params Action[] callbacks);

    [Fact]
    public void CanUseParamsActions()
    {
        var engine = new Engine();
        engine.SetValue("print", new Action<string>(_ => { }));
        engine.SetValue("callAll", new DelegateWrapper(engine, new ParamsTestDelegate(ParamsTest)));

        engine.Execute(@"
                callAll(
                    function() { print('a'); },
                    function() { print('b'); },
                    function() { print('c'); }
                );
            ");
    }

    private static void ParamsTest(params Action[] callbacks)
    {
        foreach (var callback in callbacks)
        {
            callback.Invoke();
        }
    }

    [Fact]
    public void ObjectWrapperIdentityIsMaintained()
    {
        // run in separate method so stack won't keep reference
        var reference = RunWeakReferenceTest();

        GC.Collect();

        // make sure no dangling reference is left
        Assert.False(reference.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RunWeakReferenceTest()
    {
        var o = new object();

        var engine = new Engine()
            .SetValue("o", o);

        var wrapper1 = (ObjectWrapper) engine.GetValue("o");
        var reference = new WeakReference(wrapper1);

        Assert.Same(wrapper1, engine.GetValue("o"));
        Assert.Same(o, wrapper1.Target);

        // reset
        engine.Realm.GlobalObject.RemoveOwnProperty("o");
        return reference;
    }

    [Fact]
    public void CanUseClrFunction()
    {
        var engine = new Engine();
        engine.SetValue("fn", new ClrFunction(engine, "fn", (_, args) => (JsValue) (args[0].AsInteger() + 1)));

        var result = engine.Evaluate("fn(1)");

        Assert.Equal(2, result);
    }

    [Fact]
    public void ShouldAllowClrExceptionsThrough()
    {
        var engine = new Engine(opts => opts.CatchClrExceptions(exc => false));
        engine.SetValue("fn", new ClrFunction(engine, "fn", (_, _) => throw new InvalidOperationException("This is a C# error")));
        const string Source = @"
function wrap() {
  fn();
}
wrap();
";

        Assert.Throws<InvalidOperationException>(() => engine.Execute(Source));
    }

    [Fact]
    public void ShouldConvertClrExceptionsToErrors()
    {
        var engine = new Engine(opts => opts.CatchClrExceptions(exc => exc is InvalidOperationException));
        engine.SetValue("fn", new ClrFunction(engine, "fn", (_, _) => throw new InvalidOperationException("This is a C# error")));
        const string Source = @"
function wrap() {
  fn();
}
wrap();
";

        var exc = Assert.Throws<JavaScriptException>(() => engine.Execute(Source));
        Assert.Equal(exc.Message, "This is a C# error");
    }

    [Fact]
    public void ShouldAllowCatchingConvertedClrExceptions()
    {
        var engine = new Engine(opts => opts.CatchClrExceptions(exc => exc is InvalidOperationException));
        engine.SetValue("fn", new ClrFunction(engine, "fn", (_, _) => throw new InvalidOperationException("This is a C# error")));
        const string Source = @"
try {
  fn();
} catch (e) {
  throw new Error('Caught: ' + e.message);
}
";

        var exc = Assert.Throws<JavaScriptException>(() => engine.Execute(Source));
        Assert.Equal(exc.Message, "Caught: This is a C# error");
    }

    class Baz
    {
        public int DisposeCalls { get; private set; }
        public IEnumerable<int> Enumerator
        {
            get
            {
                try
                {
                    for (int i = 0; i < 10; i++) yield return i;
                }
                finally     // finally clause is translated into IDisposable.Dispose in underlying IEnumerator
                {
                    ++DisposeCalls;
                }
            }
        }
    }

    [Fact]
    public void ShouldCallEnumeratorDisposeOnNormalTermination()
    {
        var engine = new Engine();
        var baz = new Baz();
        engine.SetValue("baz", baz);
        const string Source = @"
for (let i of baz.Enumerator) {
}";
        engine.Execute(Source);
        Assert.Equal(1, baz.DisposeCalls);
    }

    [Fact]
    public void ShouldCallEnumeratorDisposeOnBreak()
    {
        var engine = new Engine();
        var baz = new Baz();
        engine.SetValue("baz", baz);
        const string Source = @"
for (let i of baz.Enumerator) {
  if (i == 2) break;
}";
        engine.Execute(Source);
        Assert.Equal(1, baz.DisposeCalls);
    }

    [Fact]
    public void ShouldCallEnumeratorDisposeOnException()
    {
        var engine = new Engine();
        var baz = new Baz();
        engine.SetValue("baz", baz);
        const string Source = @"
try {
  for (let i of baz.Enumerator) {
    if (i == 2) throw 'exception';
  }
} catch (e) {
}";
        engine.Execute(Source);
        Assert.Equal(1, baz.DisposeCalls);
    }

    public class PropertyTestClass
    {
        public object Value;
    }

    [Fact]
    public void PropertiesOfJsObjectPassedToClrShouldBeReadable()
    {
        _engine.SetValue("MyClass", typeof(PropertyTestClass));
        RunTest(@"
                var obj = new MyClass();
                obj.Value = { foo: 'bar' };
                equal('bar', obj.Value.foo);
            ");
    }

    [Fact]
    public void ShouldBeAbleToDeleteDictionaryEntries()
    {
        var engine = new Engine(options => options.Strict());

        var dictionary = new Dictionary<string, int>
        {
            { "a", 1 },
            { "b", 2 }
        };

        engine.SetValue("data", dictionary);

        Assert.True(engine.Evaluate("Object.hasOwn(data, 'a')").AsBoolean());
        Assert.True(engine.Evaluate("data['a'] === 1").AsBoolean());

        engine.Evaluate("data['a'] = 42");
        Assert.True(engine.Evaluate("data['a'] === 42").AsBoolean());

        Assert.Equal(42, dictionary["a"]);

        engine.Execute("delete data['a'];");

        Assert.False(engine.Evaluate("Object.hasOwn(data, 'a')").AsBoolean());
        Assert.False(engine.Evaluate("data['a'] === 42").AsBoolean());

        Assert.False(dictionary.ContainsKey("a"));

        var engineNoWrite = new Engine(options => options.Strict().AllowClrWrite(false));

        dictionary = new Dictionary<string, int>
        {
            { "a", 1 },
            { "b", 2 }
        };

        engineNoWrite.SetValue("data", dictionary);

        var ex1 = Assert.Throws<JavaScriptException>(() => engineNoWrite.Evaluate("data['a'] = 42"));
        Assert.Equal("Cannot assign to read only property 'a' of System.Collections.Generic.Dictionary`2[System.String,System.Int32]", ex1.Message);

        // no changes
        Assert.True(engineNoWrite.Evaluate("data['a'] === 1").AsBoolean());

        var ex2 = Assert.Throws<JavaScriptException>(() => engineNoWrite.Execute("delete data['a'];"));
        Assert.Equal("Cannot delete property 'a' of System.Collections.Generic.Dictionary`2[System.String,System.Int32]", ex2.Message);
    }

    public record RecordTestClass(object Value = null);

    public class RecordTestClassContext
    {
        public object Method(RecordTestClass recordTest)
        {
            return recordTest.Value;
        }
    }

    private class ClassWithIndexerAndProperty
    {
        public string MyProp { get; } = "from property";

        public string this[string name] => name != nameof(MyProp) ? "from indexer" : null;
    }

    [Fact]
    public void CanToStringObjectWithoutToPrimitiveSymbol()
    {
        var engine = new Engine();

        engine.SetValue("obj", new ClassWithIndexerAndProperty());
        Assert.Equal("Jint.Tests.Runtime.InteropTests+ClassWithIndexerAndProperty", engine.Evaluate("obj + ''").AsString());

        engine.SetValue("obj", new Company("name"));
        Assert.Equal("Jint.Tests.Runtime.Domain.Company", engine.Evaluate("obj + ''").AsString());
    }

    [Fact]
    public void CanConstructOptionalRecordClass()
    {
        _engine.SetValue("Context", new RecordTestClassContext());
        Assert.Equal(null, _engine.Evaluate("Context.method({});").ToObject());
        Assert.Equal(5, _engine.Evaluate("Context.method({ value: 5 });").AsInteger());
    }

    [Fact]
    public void CanPassDateTimeMinAndMaxViaInterop()
    {
        var engine = new Engine(cfg => cfg.AllowClrWrite());

        var dt = DateTime.UtcNow;
        engine.SetValue("capture", new Action<object>(o => dt = (DateTime) o));

        engine.SetValue("minDate", DateTime.MinValue);
        engine.Execute("capture(minDate);");
        Assert.Equal(DateTime.MinValue, dt);

        engine.SetValue("maxDate", DateTime.MaxValue);
        engine.Execute("capture(maxDate);");
        Assert.Equal(DateTime.MaxValue, dt);
    }

    private class Container
    {
        private readonly Child _child = new();
        public Child Child => _child;
        public BaseClass Get() => _child;
    }

    private class BaseClass
    {
    }

    private class Child : BaseClass
    {
    }

    [Fact]
    public void AccessingBaseTypeShouldBeEqualToAccessingDerivedType()
    {
        var engine = new Engine().SetValue("container", new Container());
        var res = engine.Evaluate("container.Child === container.Get()"); // These two should be the same object. But this PR makes `container.Get()` return a different object

        Assert.True(res.AsBoolean());
    }

    public interface IIndexer<out T>
    {
        T this[int index] { get; }
    }

    public interface ICountable<out T>
    {
        int Count { get; }
    }

    public interface IStringCollection : IIndexer<string>, ICountable<string>
    {
        string this[string name] { get; }
    }

    public class Strings : IStringCollection
    {
        private readonly string[] _strings;

        public Strings(string[] strings)
        {
            _strings = strings;
        }

        public string this[string name] => null;
        public string this[int index] => _strings[index];
        public int Count => _strings.Length;
    }

    public class Utils
    {
        public IStringCollection GetStrings() => new Strings(new[] { "a", "b", "c" });
    }

    [Fact]
    public void AccessingInterfaceShouldContainExtendedInterfaces()
    {
        var engine = new Engine();
        engine.SetValue("Utils", new Utils());
        var result = engine.Evaluate("const strings = Utils.GetStrings(); strings.Count;").AsNumber();
        Assert.Equal(3, result);
    }

    [Fact]
    public void IntegerIndexerIfPreferredOverStringIndexerWhenFound()
    {
        var engine = new Engine();
        engine.SetValue("Utils", new Utils());
        var result = engine.Evaluate("const strings = Utils.GetStrings(); strings[2];");
        Assert.Equal("c", result);
    }

    [Fact]
    public void CanDestructureInteropTargetMethod()
    {
        var engine = new Engine();
        engine.SetValue("test", new Utils());
        var result = engine.Evaluate("const { getStrings } = test; getStrings().Count;");
        Assert.Equal(3, result);
    }

    private class MetadataWrapper : IDictionary<string, object>
    {
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => throw new NotImplementedException();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(KeyValuePair<string, object> item) => throw new NotImplementedException();
        public void Clear() => throw new NotImplementedException();
        public bool Contains(KeyValuePair<string, object> item) => throw new NotImplementedException();
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => throw new NotImplementedException();
        public bool Remove(KeyValuePair<string, object> item) => throw new NotImplementedException();
        public int Count { get; set; }
        public bool IsReadOnly { get; set; }
        public bool ContainsKey(string key) => throw new NotImplementedException();
        public void Add(string key, object value) => throw new NotImplementedException();
        public bool Remove(string key) => throw new NotImplementedException();

        public bool TryGetValue(string key, out object value)
        {
            value = "from-wrapper";
            return true;
        }

        public object this[string key]
        {
            get => "from-wrapper";
            set
            {
            }
        }

        public ICollection<string> Keys { get; set; }
        public ICollection<object> Values { get; set; }
    }

    private class ShadowedGetter : IReadOnlyDictionary<string, object>
    {
        private Dictionary<string, object> _dictionary = new();

        public void SetInitial(object value, string key)
        {
            _dictionary[key] = value;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => throw new NotImplementedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count { get; }
        public bool ContainsKey(string key) => _dictionary.ContainsKey(key);

        public bool TryGetValue(string key, out object value) => _dictionary.TryGetValue(key, out value);

        public object this[string key]
        {
            get
            {
                _dictionary.TryGetValue(key, out var value);
                return value;
            }
        }

        public IEnumerable<string> Keys { get; set; }
        public IEnumerable<object> Values { get; set; }
    }

    private class ShadowingSetter : ShadowedGetter
    {
        public Dictionary<string, int> Metadata
        {
            set
            {
                SetInitial(new MetadataWrapper(), "metadata");
            }
        }
    }

    [Fact]
    public void CanSelectShadowedPropertiesBasedOnReadableAndWritable()
    {
        var engine = new Engine();
        engine.SetValue("test", new ShadowingSetter
        {
            Metadata = null
        });

        engine.Evaluate("test.metadata['abc'] = 123");
        var result = engine.Evaluate("test.metadata['abc']");
        Assert.Equal("from-wrapper", result);
    }

    [Fact]
    public void ShouldRespectConcreteGenericReturnTypes()
    {
        var engine = new Engine(opt =>
        {
            opt.AddExtensionMethods(typeof(Enumerable)); // Allow LINQ extension methods.
        });

        var result = new List<string>();

        void Debug(object o)
        {
            result.Add($"{o?.GetType().Name ?? "null"}: {o ?? "null"}");
        }

        engine.SetValue("debug", Debug);
        engine.SetValue("dict", new Dictionary<string, string> { ["test"] = "val" });

        engine.Execute("var t = dict.last(kvp => { debug(kvp); debug(kvp.key); return kvp.key != null; } );");
        engine.Execute("debug(t); debug(t.key);");

        Assert.Equal(4, result.Count);
        Assert.Equal("KeyValuePair`2: [test, val]", result[0]);
        Assert.Equal("String: test", result[1]);
        Assert.Equal("KeyValuePair`2: [test, val]", result[2]);
        Assert.Equal("String: test", result[3]);
    }

    private class ClrMembersVisibilityTestClass
    {
        public string Field = "field";

        public int Property { get; set; } = 10;

        public int Method()
        {
            return 4;
        }

        public string Extras { get; set; }
    }

    [Fact]
    public void PropertiesShouldNotSeeReportMethodsWhenMemberTypesActive()
    {
        var engine = new Engine(opt =>
        {
            opt.Interop.ObjectWrapperReportedMemberTypes = MemberTypes.Field | MemberTypes.Property;
        });

        engine.SetValue("clrInstance", new ClrMembersVisibilityTestClass());

        var val = engine.GetValue("clrInstance");

        var obj = val.AsObject();
        var props = obj.GetOwnProperties().Select(x => x.Key.ToString()).ToList();

        props.Should().BeEquivalentTo("Property", "Extras", "Field");
    }

    [Fact]
    public void PropertyKeysShouldReportMethods()
    {
        var engine = new Engine();

        engine.SetValue("clrInstance", new ClrMembersVisibilityTestClass());

        var val = engine.GetValue("clrInstance");
        var obj = val.AsObject();
        var props = obj.GetOwnProperties().Select(x => x.Key.ToString()).ToList();

        props.Should().BeEquivalentTo("Property", "Extras", "Field", "Method");
    }

    [Fact]
    public void PropertyKeysShouldObeyMemberFilter()
    {
        var engine = new Engine(options =>
        {
            options.SetTypeResolver(new TypeResolver
            {
                MemberFilter = member => member.Name == "Extras"
            });
        });

        engine.SetValue("clrInstance", new ClrMembersVisibilityTestClass());

        var val = engine.GetValue("clrInstance");
        var obj = val.AsObject();
        var props = obj.GetOwnProperties().Select(x => x.Key.ToString()).ToList();

        props.Should().BeEquivalentTo("Extras");
    }

    private class ClrMembersVisibilityTestClass2
    {
        public int Get_A { get; set; } = 5;
    }

    [Fact]
    public void ShouldSeeClrMethods2()
    {
        var engine = new Engine();

        engine.SetValue("clrInstance", new ClrMembersVisibilityTestClass2());

        var val = engine.GetValue("clrInstance");

        var obj = val.AsObject();
        var props = obj.GetOwnProperties().Select(x => x.Key.ToString()).ToList();

        props.Should().BeEquivalentTo("Get_A");
    }

    [Fact]
    public void ShouldNotThrowOnInspectingClrFunction()
    {
        var engine = new Engine();

        engine.SetValue("clrDelegate", () => 4);

        var val = engine.GetValue("clrDelegate");

        var fn = val as Function;
        var decl = fn!.FunctionDeclaration;

        decl.Should().BeNull();
    }

    private class ShouldNotThrowOnInspectingClrFunctionTestClass
    {
        public int MyInt()
        {
            return 4;
        }
    }

    [Fact]
    public void ShouldNotThrowOnInspectingClrClassFunction()
    {
        var engine = new Engine();

        engine.SetValue("clrCls", new ShouldNotThrowOnInspectingClrFunctionTestClass());

        var val = engine.GetValue("clrCls");
        var clrFn = val.Get("MyInt");

        var fn = clrFn as Function;
        var decl = fn!.FunctionDeclaration;

        decl.Should().BeNull();
    }

    [Fact]
    public void StringifyShouldIncludeInheritedFieldsAndProperties()
    {
        var engine = new Engine();
        engine.SetValue("c", new Circle(12.34));
        engine.Evaluate("JSON.stringify(c)").ToString().Should().Be("{\"Radius\":12.34,\"Color\":0,\"Id\":123}");
    }

    public class Animal
    {
        public virtual string name { get; set; } = "animal";
    }

    public class Elephant : Animal
    {
        public override string name { get; set; } = "elephant";
        public int earSize = 5;
    }

    public class Lion : Animal
    {
        public override string name { get; set; } = "lion";
        public int maneLength = 10;
    }

    public class Zoo
    {
        public Animal king { get => (new Animal[] { new Lion() })[0]; }
        public Animal[] animals { get => new Animal[] { new Lion(), new Elephant() }; }
    }

    [Fact]
    public void CanFindDerivedPropertiesFail() // Fails in 4.01 but success in 2.11
    {
        var engine = new Engine();
        engine.SetValue("zoo", new Zoo());
        var kingManeLength = engine.Evaluate("zoo.King.maneLength");
        Assert.Equal(10, kingManeLength.AsNumber());
    }

    [Fact]
    public void CanFindDerivedPropertiesSucceed() // Similar case that continues to succeed
    {
        var engine = new Engine();
        engine.SetValue("zoo", new Zoo());
        var lionManeLength = engine.Evaluate("zoo.animals[0].maneLength");
        Assert.Equal(10, lionManeLength.AsNumber());
    }

    [Fact]
    public void StaticFieldsShouldFollowJsSemantics()
    {
        _engine.Evaluate("Number.MAX_SAFE_INTEGER").AsNumber().Should().Be(NumberConstructor.MaxSafeInteger);
        _engine.Evaluate("new Number().MAX_SAFE_INTEGER").Should().Be(JsValue.Undefined);

        _engine.Execute("class MyJsClass { static MAX_SAFE_INTEGER = Number.MAX_SAFE_INTEGER; }");
        _engine.Evaluate("MyJsClass.MAX_SAFE_INTEGER").AsNumber().Should().Be(NumberConstructor.MaxSafeInteger);
        _engine.Evaluate("new MyJsClass().MAX_SAFE_INTEGER").Should().Be(JsValue.Undefined);

        _engine.SetValue("MyCsClass", typeof(MyClass));
        _engine.Evaluate("MyCsClass.MAX_SAFE_INTEGER").AsNumber().Should().Be(NumberConstructor.MaxSafeInteger);
        _engine.Evaluate("new MyCsClass().MAX_SAFE_INTEGER").Should().Be(JsValue.Undefined);
    }

    private class MyClass
    {
        public static JsNumber MAX_SAFE_INTEGER = new JsNumber(NumberConstructor.MaxSafeInteger);
    }

    [Fact]
    public void ShouldFindShortOverload()
    {
        _engine.SetValue("target", new ShortOverloadWithBoolean());
        _engine.Evaluate("target.method(42)").AsString().Should().Be("short");
    }

    private class ShortOverloadWithBoolean
    {
        public string Method(short s, bool b = true)
        {
            return "short";
        }

        public string Method(bool b)
        {
            return "boolean";
        }
    }
}
