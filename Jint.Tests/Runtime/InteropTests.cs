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
        _engine = new Engine(cfg => cfg
                .AllowClr(
                    typeof(Shape).GetTypeInfo().Assembly,
                    typeof(Console).GetTypeInfo().Assembly,
                    typeof(File).GetTypeInfo().Assembly)
                .Interop.AllowWrite = true)
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
                .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())))
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

    [Test]
    public void ShouldStringifyNetObjects()
    {
        _engine.SetValue("foo", typeof(Foo));
        var json = _engine.Evaluate("JSON.stringify(foo.GetBar())").AsString();
        json.Should().Be("{\"Test\":\"123\"}");
    }


    [Test]
    public void EngineShouldStringifyADictionary()
    {
        var engine = new Engine();

        var d = new Hashtable();
        d["Values"] = 1;
        engine.SetValue("d", d);

        engine.Evaluate($"JSON.stringify(d)").AsString().Should().Be("{\"Values\":1}");
    }

    [Test]
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
        result.Should().Be("{\"foo\":5,\"bar\":\"A string\"}");
    }

    [Test]
    public void ReadOnlyDictionaryShouldNotBeTreatedAsArrayLike()
    {
        var engine = new Engine();

        var dictionary = new ReadOnlyDictionary(new Dictionary<string, object>
        {
            { "foo", 5 },
            { "bar", "A string" }
        });
        engine.SetValue(nameof(dictionary), dictionary);

        var result = engine.Evaluate($"JSON.stringify({nameof(dictionary)})").AsString();
        result.Should().Be("{\"foo\":5,\"bar\":\"A string\"}");

        var keys = engine.Evaluate($"Object.keys({nameof(dictionary)})").AsArray();
        keys.Length.Should().Be((uint) 2);
    }

    [Test]
    public void EngineShouldRoundtripParsedJSONBackToStringCorrectly()
    {
        var engine = new Engine();

        const string json = "{\"foo\":5,\"bar\":\"A string\"}";
        var parsed = engine.Evaluate($"JSON.parse('{json}')").ToObject();
        engine.SetValue(nameof(parsed), parsed);

        var result = engine.Evaluate($"JSON.stringify({nameof(parsed)})").AsString();
        result.Should().Be(json);
    }

    [Test]
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

    [Test]
    public void TypePropertyAccess()
    {
        var userClass = new Person();

        var result = new Engine()
            .SetValue("userclass", userClass)
            .Evaluate("userclass.TypeProperty.Name;")
            .AsString();

        result.Should().Be("Person");
    }

    [Test]
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

        _engine.Invoke("item2", argument).Should().Be("item2 value");
        _engine.Invoke("item1", argument).Should().Be("item value");
        _engine.Invoke("item3", argument).Should().Be("Item value");

        var company = new Company("Acme Ltd");
        _engine.SetValue("c", company);
        _engine.Evaluate("c.Item").Should().Be("item thingie");
        _engine.Evaluate("c.item").Should().Be("item thingie");
        _engine.Evaluate("c['key']").Should().Be("value");
    }

    [Test]
    public void DelegatesCanBeSet()
    {
        _engine.SetValue("square", new Func<double, double>(x => x * x));

        RunTest(@"
                assert(square(10) === 100);
            ");
    }

    [Test]
    public void DelegateWithNullableParameterCanBePassedANull()
    {
        _engine.SetValue("isnull", new Func<double?, bool>(x => x == null));

        RunTest(@"
                assert(isnull(null) === true);
            ");
    }

    [Test]
    public void DelegateWithObjectParameterCanBePassedANull()
    {
        _engine.SetValue("isnull", new Func<object, bool>(x => x == null));

        RunTest(@"
                assert(isnull(null) === true);
            ");
    }

    [Test]
    public void DelegateWithNullableParameterCanBePassedAnUndefined()
    {
        _engine.SetValue("isnull", new Func<double?, bool>(x => x == null));

        RunTest(@"
                assert(isnull(undefined) === true);
            ");
    }

    [Test]
    public void DelegateWithObjectParameterCanBePassedAnUndefined()
    {
        _engine.SetValue("isnull", new Func<object, bool>(x => x == null));

        RunTest(@"
                assert(isnull(undefined) === true);
            ");
    }

    [Test]
    public void DelegateWithNullableParameterCanBeExcluded()
    {
        _engine.SetValue("isnull", new Func<double?, bool>(x => x == null));

        RunTest(@"
                assert(isnull() === true);
            ");
    }

    [Test]
    public void DelegateWithObjectParameterCanBeExcluded()
    {
        _engine.SetValue("isnull", new Func<object, bool>(x => x == null));

        RunTest(@"
                assert(isnull() === true);
            ");
    }

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

        p.Name.Should().Be("Donald Duck");
    }

    [Test]
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

    [Test]
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

        dictionary["person2"].Name.Should().Be("Donald Duck");
    }

    [Test]
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

    [Test]
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

        dictionary[1].Should().Be("Mickey Mouse");
        dictionary[2].Should().Be("Donald Duck");
    }

    private class DoubleIndexedClass
    {
        public int this[int index] => index;

        public string this[string index] => index;
    }

    [Test]
    public void CanGetIndexUsingBothIntAndStringIndex()
    {
        var dictionary = new DoubleIndexedClass();

        _engine.SetValue("dictionary", dictionary);

        RunTest(@"
                assert(dictionary[1] === 1);
                assert(dictionary['test'] === 'test');
            ");
    }

    [Test]
    public void CanUseGenericMethods()
    {
        var dictionary = new Dictionary<int, string>();
        dictionary.Add(1, "Mickey Mouse");


        _engine.SetValue("dictionary", dictionary);

        RunTest(@"
                dictionary.Add(2, 'Goofy');
                assert(dictionary[2] === 'Goofy');
            ");

        dictionary[1].Should().Be("Mickey Mouse");
        dictionary[2].Should().Be("Goofy");
    }

    [Test]
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

    public class DictionaryKeyModel
    {
        public string Value { get; set; } = "test";
    }

    public class DictionaryKeyDerivedModel : DictionaryKeyModel
    {
    }

    public enum DictionaryKeyEnum
    {
        Foo,
        Bar,
    }

    [Test]
    public void CanGetIndexUsingObjectKey()
    {
        // repro from https://github.com/sebastienros/jint/issues/2441
        var model = new DictionaryKeyModel();
        var dictionary = new Dictionary<DictionaryKeyModel, string>
        {
            [model] = "value1",
        };
        _engine.SetValue("obj", dictionary);
        _engine.SetValue("model", model);

        var result = _engine.Evaluate("'' + obj[model]");
        result.AsString().Should().Be("value1");
    }

    [Test]
    public void CanSetIndexUsingObjectKey()
    {
        var model = new DictionaryKeyModel();
        var dictionary = new Dictionary<DictionaryKeyModel, string>
        {
            [model] = "value1",
        };
        _engine.SetValue("obj", dictionary);
        _engine.SetValue("model", model);

        _engine.Execute("obj[model] = 'updated';");
        dictionary[model].Should().Be("updated");
    }

    [Test]
    public void ObjectKeyDictionary_MissingKeyReturnsUndefined()
    {
        var model = new DictionaryKeyModel();
        var other = new DictionaryKeyModel();
        var dictionary = new Dictionary<DictionaryKeyModel, string>
        {
            [model] = "value1",
        };
        _engine.SetValue("obj", dictionary);
        _engine.SetValue("present", model);
        _engine.SetValue("absent", other);

        _engine.Evaluate("obj[present]").AsString().Should().Be("value1");
        _engine.Evaluate("obj[absent] === undefined").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ObjectKeyDictionary_HasReturnsTrueForPresentKey()
    {
        var present = new DictionaryKeyModel();
        var absent = new DictionaryKeyModel();
        var dictionary = new Dictionary<DictionaryKeyModel, string>
        {
            [present] = "value1",
        };
        _engine.SetValue("obj", dictionary);
        _engine.SetValue("present", present);
        _engine.SetValue("absent", absent);

        _engine.Evaluate("present in obj").AsBoolean().Should().BeTrue();
        _engine.Evaluate("absent in obj").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void ObjectKeyDictionary_DeleteRemovesEntry()
    {
        var model = new DictionaryKeyModel();
        var dictionary = new Dictionary<DictionaryKeyModel, string>
        {
            [model] = "value1",
        };
        _engine.SetValue("obj", dictionary);
        _engine.SetValue("model", model);

        _engine.Execute("delete obj[model];");
        dictionary.ContainsKey(model).Should().BeFalse();
    }

    [Test]
    public void ReadOnlyDictionary_WithObjectKey_AllowsRead()
    {
        var model = new DictionaryKeyModel();
        var dictionary = new Dictionary<DictionaryKeyModel, string>
        {
            [model] = "value1",
        };
        IReadOnlyDictionary<DictionaryKeyModel, string> readOnly = dictionary;
        _engine.SetValue("obj", readOnly);
        _engine.SetValue("model", model);

        _engine.Evaluate("'' + obj[model]").AsString().Should().Be("value1");
    }

    [Test]
    public void ReadOnlyDictionary_WithObjectKey_HasReturnsTrueForPresentKey()
    {
        var present = new DictionaryKeyModel();
        var absent = new DictionaryKeyModel();
        IReadOnlyDictionary<DictionaryKeyModel, string> readOnly = new Dictionary<DictionaryKeyModel, string>
        {
            [present] = "value1",
        };
        _engine.SetValue("obj", readOnly);
        _engine.SetValue("present", present);
        _engine.SetValue("absent", absent);

        _engine.Evaluate("present in obj").AsBoolean().Should().BeTrue();
        _engine.Evaluate("absent in obj").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void ObjectKeyDictionary_DerivedKeyTypeWorks()
    {
        var derived = new DictionaryKeyDerivedModel();
        var dictionary = new Dictionary<DictionaryKeyModel, string>
        {
            [derived] = "fromDerived",
        };
        _engine.SetValue("obj", dictionary);
        _engine.SetValue("model", derived);

        _engine.Evaluate("'' + obj[model]").AsString().Should().Be("fromDerived");
    }

    [Test]
    public void ObjectKeyDictionary_StructKey()
    {
        var key = Guid.NewGuid();
        var dictionary = new Dictionary<Guid, string>
        {
            [key] = "valueForGuid",
        };
        _engine.SetValue("obj", dictionary);
        _engine.SetValue("key", key);

        _engine.Evaluate("'' + obj[key]").AsString().Should().Be("valueForGuid");
    }

    [Test]
    public void ObjectKeyDictionary_SetWithIncompatibleValueType_SloppyMode_NoOp()
    {
        var model = new DictionaryKeyModel();
        var dictionary = new Dictionary<DictionaryKeyModel, int>
        {
            [model] = 42,
        };
        _engine.SetValue("obj", dictionary);
        _engine.SetValue("model", model);

        // assigning a non-numeric string to an int-valued dictionary must not throw and must not corrupt the entry
        _engine.Execute("obj[model] = 'not an int';");
        dictionary[model].Should().Be(42);
    }

    [Test]
    public void ObjectKeyDictionary_EnumKey()
    {
        var dictionary = new Dictionary<DictionaryKeyEnum, string>
        {
            [DictionaryKeyEnum.Foo] = "fooValue",
            [DictionaryKeyEnum.Bar] = "barValue",
        };
        _engine.SetValue("obj", dictionary);
        _engine.SetValue("foo", DictionaryKeyEnum.Foo);
        _engine.SetValue("bar", DictionaryKeyEnum.Bar);

        _engine.Evaluate("'' + obj[foo]").AsString().Should().Be("fooValue");
        _engine.Evaluate("'' + obj[bar]").AsString().Should().Be("barValue");
    }

    [Test]
    public void ObjectKeyDictionary_IntegerKeyReturnsUndefined()
    {
        // a JS number key against an object-keyed dictionary should return undefined cleanly,
        // not throw, and not match by coincidence
        var model = new DictionaryKeyModel();
        var dictionary = new Dictionary<DictionaryKeyModel, string>
        {
            [model] = "value1",
        };
        _engine.SetValue("obj", dictionary);

        _engine.Evaluate("obj[42] === undefined").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void IntegerKeyedDictionary_ResolvesByNumericKey()
    {
        // Dictionary<int, T> went through the IsStringKeyedGenericDictionary path before this
        // change (which stringified the int and likely failed); now it routes through the
        // non-string-keyed path and resolves directly via the int key.
        var dictionary = new Dictionary<int, string>
        {
            [1] = "one",
            [2] = "two",
        };
        _engine.SetValue("obj", dictionary);

        _engine.Evaluate("'' + obj[1]").AsString().Should().Be("one");
        _engine.Evaluate("'' + obj[2]").AsString().Should().Be("two");
        _engine.Evaluate("obj[3] === undefined").AsBoolean().Should().BeTrue();
        _engine.Evaluate("1 in obj").AsBoolean().Should().BeTrue();
        _engine.Evaluate("3 in obj").AsBoolean().Should().BeFalse();

        _engine.Execute("obj[3] = 'three';");
        dictionary[3].Should().Be("three");

        _engine.Execute("delete obj[1];");
        dictionary.ContainsKey(1).Should().BeFalse();
    }

    [Test]
    public void ObjectKeyDictionary_SymbolKeyHandledByBase()
    {
        // symbol keys must not be hijacked by the new non-string-keyed dict branches —
        // Symbol.iterator should still resolve to the iterator function (Dictionary<,> is enumerable)
        var model = new DictionaryKeyModel();
        var dictionary = new Dictionary<DictionaryKeyModel, string>
        {
            [model] = "value1",
        };
        _engine.SetValue("obj", dictionary);

        _engine.Evaluate("typeof obj[Symbol.iterator]").AsString().Should().Be("function");
    }

    [Test]
    public void ObjectKeyDictionary_NullKeyReturnsUndefined()
    {
        // null/undefined JS keys must short-circuit cleanly, not crash with ArgumentNullException
        // when reflectively invoking Dictionary<TKey, TValue>.TryGetValue/ContainsKey/Remove.
        var dictionary = new Dictionary<DictionaryKeyModel, string>
        {
            [new DictionaryKeyModel()] = "value1",
        };
        _engine.SetValue("obj", dictionary);

        _engine.Evaluate("obj[null] === undefined").AsBoolean().Should().BeTrue();
        _engine.Evaluate("obj[undefined] === undefined").AsBoolean().Should().BeTrue();
        _engine.Evaluate("null in obj").AsBoolean().Should().BeFalse();
        _engine.Evaluate("delete obj[null]; null in obj").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void ObjectKeyDictionary_NullKeyAssignment_SloppyMode_NoOp()
    {
        // mirror of the read-side null guard: assigning to obj[null] must not crash with
        // ArgumentNullException when reflectively invoking the indexer setter, and must not
        // pollute the dictionary with a null key.
        var dictionary = new Dictionary<DictionaryKeyModel, string>
        {
            [new DictionaryKeyModel()] = "value1",
        };
        _engine.SetValue("obj", dictionary);

        _engine.Execute("obj[null] = 'should not stick';");
        dictionary.Should().ContainSingle();
        dictionary.Values.Should().NotContain("should not stick");
    }

    [Test]
    public void ObjectKeyDictionary_ObjectValuedDictionary_StoresUnwrappedClrValue()
    {
        // Dictionary<TKey, object> must receive the unwrapped CLR value, not the raw JsValue —
        // otherwise C# callers reading the dictionary back get JsString/JsNumber surprises.
        var model = new DictionaryKeyModel();
        var dictionary = new Dictionary<DictionaryKeyModel, object>();
        _engine.SetValue("obj", dictionary);
        _engine.SetValue("model", model);

        _engine.Execute("obj[model] = 'hello';");
        dictionary[model].Should().Be("hello");
        dictionary[model].Should().BeOfType<string>();
    }

    [Test]
    public void ObjectKeyDictionary_SetWithIncompatibleValueType_StrictMode_Throws()
    {
        // strict mode must escalate the [[Set]] failure to a TypeError
        var model = new DictionaryKeyModel();
        var dictionary = new Dictionary<DictionaryKeyModel, int>
        {
            [model] = 42,
        };
        _engine.SetValue("obj", dictionary);
        _engine.SetValue("model", model);

        var ex = Invoking(() => _engine.Execute("'use strict'; obj[model] = 'not an int';")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().ContainEquivalentOf("Cannot assign");
        dictionary[model].Should().Be(42);
    }

    [Test]
    public void ObjectKeyDictionary_FrozenWrapper_BlocksWrites()
    {
        // Object.freeze on the wrapper sets Extensible=false, which the [[Set]] path treats as
        // a blanket write block (matching the existing string-keyed dict behavior). Lock in the
        // contract so a future change to relax this is a deliberate decision, not a regression.
        var model = new DictionaryKeyModel();
        var dictionary = new Dictionary<DictionaryKeyModel, string>
        {
            [model] = "value1",
        };
        _engine.SetValue("obj", dictionary);
        _engine.SetValue("model", model);

        _engine.Execute("Object.freeze(obj);");

        // sloppy mode: silent failure, value unchanged
        _engine.Execute("obj[model] = 'updated';");
        dictionary[model].Should().Be("value1");

        // strict mode: TypeError, value unchanged
        Invoking(() => _engine.Execute("'use strict'; obj[model] = 'updated';")).Should().ThrowExactly<JavaScriptException>();
        dictionary[model].Should().Be("value1");
    }

    [Test]
    public void NonWritableArrayElement_ThrowsOnIndexWrite_StrictMode()
    {
        // https://github.com/sebastienros/jint/issues/2541
        // The CLR object[] nested in the wrapped dictionary is exposed as a native JS array. Once its
        // element descriptors are made non-writable and the array is made non-extensible, assigning to
        // an element must throw a TypeError in strict mode. Regression: it previously neither threw nor
        // assigned (the dense-write fast path bypassed the writability check).
        // This scenario is about a copied JS array's per-element descriptors, so it pins Copy mode
        var engine = new Engine(x =>
        {
            x.Strict = true;
            x.Interop.ArrayConversion = ArrayConversionMode.Copy;
        });

        var context = new Dictionary<string, object>
        {
            ["property"] = new object[1],
        };

        var contextValue = JsValue.FromObjectWithType(engine, context, typeof(IReadOnlyDictionary<string, object>));

        // Lock the context value using the same pattern as the issue reporter:
        // iterate properties, set writable=false, recurse into values, and call DefineOwnPropertyUnchecked
        LockDescriptorHelper(contextValue, []);

        var contextDescriptor = new Jint.Runtime.Descriptors.PropertyDescriptor
        {
            Value = contextValue,
            Writable = false,
            Enumerable = true,
            Configurable = false,
        };

        engine.Global.DefineOwnProperty("context", contextDescriptor);

        var before = engine.Evaluate("context.property[0]");

        var ex = Invoking(() => engine.Evaluate("context.property[0] = {};")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().ContainEquivalentOf("Cannot assign");

        // the element must be left untouched (the issue's other symptom was a silent overwrite)
        engine.Evaluate("context.property[0]").Should().Be(before);
    }

    [Test]
    public void NonWritableArrayElement_SilentlyIgnoresIndexWrite_NonStrict()
    {
        // Non-strict counterpart of https://github.com/sebastienros/jint/issues/2541:
        // the write to the non-writable element must be silently ignored, not applied.
        // Pinned to Copy mode for the same reason as the strict-mode counterpart above.
        var engine = new Engine(x => x.Interop.ArrayConversion = ArrayConversionMode.Copy);

        var context = new Dictionary<string, object>
        {
            ["property"] = new object[1],
        };

        var contextValue = JsValue.FromObjectWithType(engine, context, typeof(IReadOnlyDictionary<string, object>));

        LockDescriptorHelper(contextValue, []);

        engine.Global.DefineOwnProperty("context", new Jint.Runtime.Descriptors.PropertyDescriptor
        {
            Value = contextValue,
            Writable = false,
            Enumerable = true,
            Configurable = false,
        });

        var before = engine.Evaluate("context.property[0]");

        // no throw in sloppy mode
        engine.Evaluate("context.property[0] = {};");

        // ...but the assignment must not have taken effect
        engine.Evaluate("context.property[0]").Should().Be(before);
    }

    private static void LockDescriptorHelper(JsValue jsValue, HashSet<JsValue> visited)
    {
        if (!visited.Add(jsValue))
        {
            return;
        }

        if (jsValue.IsObject())
        {
            var obj = jsValue.AsObject();

            foreach (var property in obj.GetOwnProperties())
            {
                property.Value.Writable = false;
                property.Value.Enumerable = true;
                property.Value.Configurable = false;

                LockDescriptorHelper(property.Value.Value, visited);

                obj.DefineOwnPropertyUnchecked(property.Key, property.Value);
            }

            obj.PreventExtensions();
        }
    }

    [Test]
    public void FrozenListWrapper_BlocksIndexWrite_StrictMode()
    {
        // https://github.com/sebastienros/jint/issues/2541 (interop-wrapper variant)
        // A frozen IList<T> wrapper must reject element assignment in strict mode rather than letting the
        // base SetSlow path silently "succeed". Wrappers don't track per-element writability, so the
        // non-extensible (frozen) state is what blocks the write.
        var engine = new Engine(x => { x.Strict = true; x.Interop.AllowWrite = true; });
        var list = new List<object> { "a", "b" };
        engine.SetValue("list", list);
        engine.Execute("Object.freeze(list);");

        var ex = Invoking(() => engine.Evaluate("list[0] = 'changed';")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().ContainEquivalentOf("Cannot assign");
        list[0].Should().Be("a"); // not mutated
    }

    [Test]
    public void FrozenListWrapper_SilentlyIgnoresIndexWrite_NonStrict()
    {
        var engine = new Engine(options => options.Interop.AllowWrite = true);
        var list = new List<object> { "a", "b" };
        engine.SetValue("list", list);
        engine.Execute("Object.freeze(list);");

        engine.Evaluate("list[0] = 'changed';"); // no throw in sloppy mode
        list[0].Should().Be("a"); // ...but silently ignored, not mutated
    }

    [Test]
    public void WritableListWrapper_AllowsIndexWrite()
    {
        // The frozen guard must not regress ordinary writable element assignment through the wrapper.
        var engine = new Engine(x => { x.Strict = true; x.Interop.AllowWrite = true; });
        var list = new List<object> { "a", "b" };
        engine.SetValue("list", list);

        engine.Evaluate("list[0] = 'changed';");
        list[0].Should().Be("changed");
    }

    [Test]
    public void ReadOnlyCollectionWrapper_RejectsIndexWriteCleanly()
    {
        // A runtime read-only IList<T> (e.g. List<T>.AsReadOnly()) reaches GenericListWrapper, whose
        // backing indexer throws. The element write must be rejected through the normal [[Set]] path
        // (TypeError in strict, silent no-op in non-strict), NOT surface a raw NotSupportedException
        // from the underlying collection. This engine has AllowWrite off, so CanWrite refuses either
        // way; the wrapper's own ICollection<T>.IsReadOnly refuses it with AllowWrite on (#3382).
        var readOnly = new List<int> { 1, 2 }.AsReadOnly();

        var strict = new Engine(x => x.Strict = true);
        strict.SetValue("a", readOnly);
        var ex = Invoking(() => strict.Evaluate("a[0] = 9;")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().ContainEquivalentOf("Cannot assign");
        readOnly[0].Should().Be(1);

        var sloppy = new Engine();
        sloppy.SetValue("a", readOnly);
        sloppy.Evaluate("a[0] = 9;"); // silently ignored, no throw
        readOnly[0].Should().Be(1);
    }

    [Test]
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

        collection[0].Should().Be("Mickey Mouse");
        collection[1].Should().Be("Donald Duck");
    }

    [Test]
    public void CanUseIndexOnList()
    {
        var list = new List<object>(2);
        list.Add("Mickey Mouse");
        list.Add("Goofy");

        _engine.SetValue("list", list);
        _engine.Evaluate("list[1] = 'Donald Duck';");

        _engine.Evaluate("list[1]").AsString().Should().Be("Donald Duck");
        list[0].Should().Be("Mickey Mouse");
        list[1].Should().Be("Donald Duck");
    }

    [Test]
    public void ShouldForOfOnLists()
    {
        _engine.SetValue("list", new List<string> { "a", "b" });

        var result = _engine.Evaluate("var l = ''; for (var x of list) l += x; return l;").AsString();

        result.Should().Be("ab");
    }

    [Test]
    public void ShouldForOfOnArrays()
    {
        _engine.SetValue("arr", new[] { "a", "b" });

        var result = _engine.Evaluate("var l = ''; for (var x of arr) l += x; return l;").AsString();

        result.Should().Be("ab");
    }

    [Test]
    public void ShouldForOfOnDictionaries()
    {
        _engine.SetValue("dict", new Dictionary<string, string> { { "a", "1" }, { "b", "2" } });

        var result = _engine.Evaluate("var l = ''; for (var x of dict) l += x; return l;").AsString();

        result.Should().Be("a,1b,2");
    }

    [Test]
    public void ShouldForOfOnEnumerable()
    {
        _engine.SetValue("c", new Company("name"));

        var result = _engine.Evaluate("var l = ''; for (var x of c.getNameChars()) l += x + ','; return l;").AsString();

        result.Should().Be("n,a,m,e,");
    }

    [Test]
    public void ShouldThrowWhenForOfOnObject()
    {
        // normal objects are not iterable in javascript
        var o = new { A = 1, B = 2 };
        _engine.SetValue("anonymous", o);

        var ex = Invoking(() => _engine.Evaluate("for (var x of anonymous) {}")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("The value is not iterable");
    }

    [Test]
    public void ShouldForOfOnProxiedList()
    {
        _engine.SetValue("list", new List<string> { "a", "b" });

        var result = _engine.Evaluate("var l = ''; for (var x of new Proxy(list, {})) l += x; return l;").AsString();
        result.Should().Be("ab");

        // nested proxies unwrap all the way down to the wrapper
        result = _engine.Evaluate("var l = ''; for (var x of new Proxy(new Proxy(list, {}), {})) l += x; return l;").AsString();
        result.Should().Be("ab");
    }

    [Test]
    public void ExtractedClrIteratorCalledWithForeignObjectThisThrowsTypeError()
    {
        _engine.SetValue("list", new List<string> { "a", "b" });

        var ex = Invoking(() => _engine.Evaluate("var f = list[Symbol.iterator]; f.call({});")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Method '[Symbol.iterator]' called on incompatible receiver");

        // the error is a proper TypeError catchable from script
        var result = _engine.Evaluate("var f = list[Symbol.iterator]; try { f.call({}); 'no error' } catch (e) { e instanceof TypeError }");
        result.AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ExtractedClrIteratorCalledWithPrimitiveThisThrowsTypeError()
    {
        _engine.SetValue("list", new List<string> { "a", "b" });

        // primitive receiver has no engine attached, error is materialized as TypeError by the interpreter
        var result = _engine.Evaluate("var f = list[Symbol.iterator]; try { f.call('x'); 'no error' } catch (e) { e instanceof TypeError }");
        result.AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ExtractedClrLengthGetterCalledWithForeignThisThrowsTypeError()
    {
        _engine.SetValue("list", new List<string> { "a", "b" });

        var ex = Invoking(() => _engine.Evaluate("Object.getOwnPropertyDescriptor(list, 'length').get.call({});")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Method 'length' called on incompatible receiver");

        var result = _engine.Evaluate("var g = Object.getOwnPropertyDescriptor(list, 'length').get; try { g.call('x'); 'no error' } catch (e) { e instanceof TypeError }");
        result.AsBoolean().Should().BeTrue();
    }

    [Test]
    public void LazilyMaterializedLengthPropertyBehavesLikeEagerOne()
    {
        _engine.SetValue("list", new List<string> { "a", "b" });
        _engine.SetValue("array", new[] { 1, 2, 3 });

        // plain reads (served by the ICollection fast path) and own-property reflection agree
        _engine.Evaluate("list.length").AsNumber().Should().Be(2);
        _engine.Evaluate("array.length").AsNumber().Should().Be(3);
        _engine.Evaluate("'length' in list").AsBoolean().Should().BeTrue();
        _engine.Evaluate("list.hasOwnProperty('length')").AsBoolean().Should().BeTrue();
        _engine.Evaluate("array.hasOwnProperty('length')").AsBoolean().Should().BeTrue();

        // the accessor descriptor keeps its eager-era shape
        _engine.Evaluate("typeof Object.getOwnPropertyDescriptor(list, 'length').get").AsString().Should().Be("function");
        _engine.Evaluate("Object.getOwnPropertyDescriptor(list, 'length').configurable").AsBoolean().Should().BeTrue();
        _engine.Evaluate("Object.getOwnPropertyDescriptor(list, 'length').set === undefined").AsBoolean().Should().BeTrue();

        // deleting the forwarder removes the own property for good
        _engine.Evaluate("delete list.length").AsBoolean().Should().BeTrue();
        _engine.Evaluate("list.hasOwnProperty('length')").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void ForOfOverRevokedProxyOfClrListThrowsTypeError()
    {
        _engine.SetValue("list", new List<string> { "a", "b" });

        // the proxy's own [[Get]] rejects the revoked proxy before the iterator helper runs,
        // actual message: "Cannot perform 'get' on a proxy that has been revoked" (matches V8/node)
        var ex = Invoking(() => _engine.Evaluate("""
            var r = Proxy.revocable(list, {});
            r.revoke();
            for (var x of r.proxy) {}
        """)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Error.AsObject().Prototype.Should().BeSameAs(_engine.Realm.Intrinsics.TypeError.PrototypeObject);
    }

    [Test]
    public void ExtractedClrIteratorCalledOnRevokedProxyThrowsTypeError()
    {
        _engine.SetValue("list", new List<string> { "a", "b" });

        // extract while alive, revoke, then re-invoke with the revoked proxy as receiver;
        // this bypasses the proxy [[Get]] guard and exercises the iterator helper directly
        var ex = Invoking(() => _engine.Evaluate("""
            var r = Proxy.revocable(list, {});
            var f = r.proxy[Symbol.iterator];
            r.revoke();
            f.call(r.proxy);
        """)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Cannot perform '[Symbol.iterator]' on a proxy that has been revoked");
        ex.Error.AsObject().Prototype.Should().BeSameAs(_engine.Realm.Intrinsics.TypeError.PrototypeObject);
    }

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

        p.Name.Should().Be("bar");
        p.Age.Should().Be(10);
    }

    [Test]
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

        p.Name.Should().Be("10");
        p.Age.Should().Be(20);
    }

    [Test]
    public void ShouldCallInstanceMethodWithoutArgument()
    {
        _engine.SetValue("a", new A());

        RunTest(@"
                assert(a.Call1() === 0);
            ");
    }

    [Test]
    public void ShouldCallInstanceMethodOverloadArgument()
    {
        _engine.SetValue("a", new A());

        RunTest(@"
                assert(a.Call1(1) === 1);
            ");
    }

    [Test]
    public void ShouldCallInstanceMethodWithString()
    {
        var p = new Person();
        _engine.SetValue("a", new A());
        _engine.SetValue("p", p);

        RunTest(@"
                p.Name = a.Call2('foo');
                assert(p.Name === 'foo');
            ");

        p.Name.Should().Be("foo");
    }

    [Test]
    public void CanUseTrim()
    {
        var p = new Person { Name = "Mickey Mouse " };
        _engine.SetValue("p", p);

        RunTest(@"
                assert(p.Name === 'Mickey Mouse ');
                p.Name = p.Name.trim();
                assert(p.Name === 'Mickey Mouse');
            ");

        p.Name.Should().Be("Mickey Mouse");
    }

    [Test]
    public void CanUseMathFloor()
    {
        var p = new Person();
        _engine.SetValue("p", p);

        RunTest(@"
                p.Age = Math.floor(1.6);p
                assert(p.Age === 1);
            ");

        p.Age.Should().Be(1);
    }

    [Test]
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

    [Test]
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

    [Test]
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
        name.Should().Be("John");
    }


    [Test]
    public void CanSetIsConcatSpreadableForArrays()
    {
        var engine = new Engine(options => options.Interop.AllowWrite = true);

        engine
            .SetValue("list1", new List<string> { "A", "B", "C" })
            .SetValue("list2", new List<string> { "D", "E", "F" })
            .Execute("var array1 = ['A', 'B', 'C'];")
            .Execute("var array2 = ['D', 'E', 'F'];");

        engine.Evaluate("list1[Symbol.isConcatSpreadable] = true; list1[Symbol.isConcatSpreadable];").AsBoolean().Should().BeTrue();
        engine.Evaluate("list2[Symbol.isConcatSpreadable] = true; list2[Symbol.isConcatSpreadable];").AsBoolean().Should().BeTrue();

        engine.Evaluate("JSON.stringify(array1);").Should().Be("[\"A\",\"B\",\"C\"]");
        engine.Evaluate("JSON.stringify(array2);").Should().Be("[\"D\",\"E\",\"F\"]");
        engine.Evaluate("JSON.stringify(list1);").Should().Be("[\"A\",\"B\",\"C\"]");
        engine.Evaluate("JSON.stringify(list2);").Should().Be("[\"D\",\"E\",\"F\"]");

        const string Concatenated = "[\"A\",\"B\",\"C\",\"D\",\"E\",\"F\"]";
        engine.Evaluate("JSON.stringify(array1.concat(array2));").Should().Be(Concatenated);
        engine.Evaluate("JSON.stringify(array1.concat(list2));").Should().Be(Concatenated);
        engine.Evaluate("JSON.stringify(list1.concat(array2));").Should().Be(Concatenated);
        engine.Evaluate("JSON.stringify(list1.concat(list2));").Should().Be(Concatenated);

        engine.Evaluate("list1[Symbol.isConcatSpreadable] = false; list1[Symbol.isConcatSpreadable];").AsBoolean().Should().BeFalse();
        engine.Evaluate("list2[Symbol.isConcatSpreadable] = false; list2[Symbol.isConcatSpreadable];").AsBoolean().Should().BeFalse();

        engine.Evaluate("JSON.stringify([].concat(list1));").Should().Be("[[\"A\",\"B\",\"C\"]]");
        engine.Evaluate("JSON.stringify(list1.concat(list2));").Should().Be("[[\"A\",\"B\",\"C\"],[\"D\",\"E\",\"F\"]]");
    }

    [Test]
    public void ShouldConvertArrayToArrayInstance()
    {
        var result = _engine
            .SetValue("values", new[] { 1, 2, 3, 4, 5, 6 })
            .Evaluate("values.filter(function(x){ return x % 2 == 0; })");

        var parts = result.ToObject();

        parts.GetType().IsArray.Should().BeTrue();
        ((object[]) parts).Length.Should().Be(3);
        ((object[]) parts)[0].Should().Be(2d);
        ((object[]) parts)[1].Should().Be(4d);
        ((object[]) parts)[2].Should().Be(6d);
    }

    [Test]
    public void ShouldConvertListsToArrayInstance()
    {
        var result = _engine
            .SetValue("values", new List<object> { 1, 2, 3, 4, 5, 6 })
            .Evaluate("new Array(values).filter(function(x){ return x % 2 == 0; })");

        var parts = result.ToObject();

        parts.GetType().IsArray.Should().BeTrue();
        ((object[]) parts).Length.Should().Be(3);
        ((object[]) parts)[0].Should().Be(2d);
        ((object[]) parts)[1].Should().Be(4d);
        ((object[]) parts)[2].Should().Be(6d);
    }

    [Test]
    public void ShouldConvertArrayInstanceToArray()
    {
        var parts = _engine.Evaluate("'foo@bar.com'.split('@');").ToObject();

        parts.GetType().IsArray.Should().BeTrue();
        ((object[]) parts).Length.Should().Be(2);
        ((object[]) parts)[0].Should().Be("foo");
        ((object[]) parts)[1].Should().Be("bar.com");
    }

    [Test]
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

        result.Should().Be(6);
    }

    [Test]
    public void ShouldConvertBooleanInstanceToBool()
    {
        var value = _engine.Evaluate("new Boolean(true)").ToObject();

        value.GetType().Should().Be(typeof(bool));
        value.Should().Be(true);
    }

    [Test]
    public void ShouldAllowBooleanCoercion()
    {
        var engine = new Engine(options =>
        {
            options.Interop.AllowWrite = true;
            options.Interop.ValueCoercion = ValueCoercionType.Boolean;
        });

        engine.SetValue("o", new TestClass());
        engine.Evaluate("o.Bool = 1; return o.Bool;").AsBoolean().Should().BeTrue();
        engine.Evaluate("o.Bool = 'dog'; return o.Bool;").AsBoolean().Should().BeTrue();
        engine.Evaluate("o.Bool = {}; return o.Bool;").AsBoolean().Should().BeTrue();
        engine.Evaluate("o.Bool = 0; return o.Bool;").AsBoolean().Should().BeFalse();
        engine.Evaluate("o.Bool = ''; return o.Bool;").AsBoolean().Should().BeFalse();
        engine.Evaluate("o.Bool = null; return o.Bool;").AsBoolean().Should().BeFalse();
        engine.Evaluate("o.Bool = undefined; return o.Bool;").AsBoolean().Should().BeFalse();

        engine.Evaluate("class MyClass { valueOf() { return 42; } }");
        engine.Evaluate("let obj = new MyClass(); o.Bool = obj; return o.Bool;").AsBoolean().Should().BeTrue();

        engine.SetValue("func3", new Action<bool, bool, bool>((param1, param2, param3) =>
        {
            param1.Should().BeTrue();
            param2.Should().BeTrue();
            param3.Should().BeTrue();
        }));
        engine.Evaluate("func3(true, obj, [ 1, 2, 3])");

        engine.Evaluate("o.SetBool(42); return o.Bool;").AsBoolean().Should().BeTrue();
        engine.Evaluate("o.SetBool(obj); return o.Bool;").AsBoolean().Should().BeTrue();
        engine.Evaluate("o.SetBool([ 1, 2, 3].length); return o.Bool;").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ShouldAllowNumberCoercion()
    {
        var engine = new Engine(options =>
        {
            options.Interop.AllowWrite = true;
            options.Interop.ValueCoercion = ValueCoercionType.Number;
        });

        engine.SetValue("o", new TestClass());
        engine.Evaluate("o.Int = true; return o.Int;").AsNumber().Should().Be(1);
        engine.Evaluate("o.Int = false; return o.Int;").AsNumber().Should().Be(0);

        engine.Evaluate("class MyClass { valueOf() { return 42; } }");
        engine.Evaluate("let obj = new MyClass(); o.Int = obj; return o.Int;").AsNumber().Should().Be(42);

        // but null and undefined should be injected as nulls to nullable objects
        engine.Evaluate("o.NullableInt = null; return o.NullableInt;").IsNull().Should().BeTrue();
        engine.Evaluate("o.NullableInt = undefined; return o.NullableInt;").IsNull().Should().BeTrue();

        engine.SetValue("func3", new Action<int, double, long>((param1, param2, param3) =>
        {
            param1.Should().Be(1);
            param2.Should().Be(42);
            param3.Should().Be(3);
        }));
        engine.Evaluate("func3(true, obj, [ 1, 2, 3].length)");

        engine.Evaluate("o.SetInt(true); return o.Int;").AsNumber().Should().Be(1);
        engine.Evaluate("o.SetInt(obj); return o.Int;").AsNumber().Should().Be(42);
        engine.Evaluate("o.SetInt([ 1, 2, 3].length); return o.Int;").AsNumber().Should().Be(3);
    }

    [Test]
    public void ShouldAllowStringCoercion()
    {
        var engine = new Engine(options =>
        {
            options.Interop.AllowWrite = true;
            options.Interop.ValueCoercion = ValueCoercionType.String;
        });

        // basic premise, booleans in JS are lower-case, so should the the toString under interop
        engine.Evaluate("'' + true").AsString().Should().Be("true");

        engine.SetValue("o", new TestClass());
        engine.Evaluate("'' + o.Bool").AsString().Should().Be("false");

        engine.Evaluate("o.Bool = true; o.String = o.Bool; return o.String;").AsString().Should().Be("true");

        engine.Evaluate("o.String = true; return o.String;").AsString().Should().Be("true");

        engine.SetValue("func1", new Func<bool>(() => true));
        engine.Evaluate("'' + func1()").AsString().Should().Be("true");

        engine.SetValue("func2", new Func<JsValue>(() => JsBoolean.True));
        engine.Evaluate("'' + func2()").AsString().Should().Be("true");

        // but null and undefined should be injected as nulls to c# objects
        engine.Evaluate("o.String = null; return o.String;").IsNull().Should().BeTrue();
        engine.Evaluate("o.String = undefined; return o.String;").IsNull().Should().BeTrue();

        engine.Evaluate("o.String = [ 1, 2, 3 ]; return o.String;").AsString().Should().Be("1,2,3");

        engine.Evaluate("class MyClass { toString() { return 'hello world'; } }");
        engine.Evaluate("let obj = new MyClass(); o.String = obj; return o.String;").AsString().Should().Be("hello world");

        engine.SetValue("func3", new Action<string, string, string>((param1, param2, param3) =>
        {
            param1.Should().Be("true");
            param2.Should().Be("hello world");
            param3.Should().Be("1,2,3");
        }));
        engine.Evaluate("func3(true, obj, [ 1, 2, 3])");

        engine.Evaluate("o.SetString(true); return o.String;").AsString().Should().Be("true");
        engine.Evaluate("o.SetString(obj); return o.String;").AsString().Should().Be("hello world");
        engine.Evaluate("o.SetString([ 1, 2, 3]); return o.String;").AsString().Should().Be("1,2,3");
    }

    [Test]
    public void ShouldConvertDateInstanceToDateTime()
    {
        var result = _engine.Evaluate("new Date(0)");
        var value = result.ToObject() is DateTime ? (DateTime) result.ToObject() : default;

        value.Should().Be(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
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

        value.Should().Be(new DateTime(1970, 1, 1, 2, 0, 0, DateTimeKind.Local));
        value.Kind.Should().Be(DateTimeKind.Local);
    }

    [Test]
    public void ShouldConvertNumberInstanceToDouble()
    {
        var result = _engine.Evaluate("new Number(10)");
        var value = result.ToObject();

        value.GetType().Should().Be(typeof(double));
        value.Should().Be(10d);
    }

    [Test]
    public void ShouldConvertStringInstanceToString()
    {
        var value = _engine.Evaluate("new String('foo')").ToObject();

        value.GetType().Should().Be(typeof(string));
        value.Should().Be("foo");
    }

    [Test]
    public void ShouldNotTryToConvertCompatibleTypes()
    {
        _engine.SetValue("a", new A());

        RunTest(@"
                assert(a.Call3('foo') === 'foo');
                assert(a.Call3(1) === '1');
            ");
    }

    [Test]
    public void ShouldNotTryToConvertDerivedTypes()
    {
        _engine.SetValue("a", new A());
        _engine.SetValue("p", new Person { Name = "Mickey" });

        RunTest(@"
                assert(a.Call4(p) === 'Mickey');
            ");
    }

    [Test]
    public void ShouldExecuteFunctionCallBackAsDelegate()
    {
        _engine.SetValue("a", new A());

        RunTest(@"
                assert(a.Call5(function(a,b){ return a+b }) === '1foo');
            ");
    }

    [Test]
    public void ShouldExecuteFunctionCallBackAsFuncAndThisCanBeAssigned()
    {
        _engine.SetValue("a", new A());

        RunTest(@"
                assert(a.Call6(function(a,b){ return this+a+b }) === 'bar1foo');
            ");
    }

    [Test]
    public void ShouldExecuteFunctionCallBackAsPredicate()
    {
        _engine.SetValue("a", new A());

        // Func<>
        RunTest(@"
                assert(a.Call8(function(){ return 'foo'; }) === 'foo');
            ");
    }

    [Test]
    public void ShouldExecuteFunctionWithParameterCallBackAsPredicate()
    {
        _engine.SetValue("a", new A());

        // Func<,>
        RunTest(@"
                assert(a.Call7('foo', function(a){ return a === 'foo'; }) === true);
            ");
    }

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
    public void ShouldExecuteFunc()
    {
        _engine.SetValue("a", new A());

        // Func<int, int>
        RunTest(@"
                var result = a.Call12(42, function(a){ return a + a; });
                assert(result === 84);
            ");
    }

    [Test]
    public void ShouldExecuteActionCallbackOnEventChanged()
    {
        var collection = new System.Collections.ObjectModel.ObservableCollection<string>();
        collection.Should().HaveCount(0);

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
        callCount.Should().Be(1);
        collection.Should().HaveCount(2);

        // make sure our delegate holder is hidden
        _engine.Evaluate("json").Should().Be("[]");
    }

    [Test]
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

    [Test]
    public void ShouldImportNamespace()
    {
        RunTest(@"
                var Shapes = importNamespace('Shapes');
                var circle = new Shapes.Circle();
                assert(circle.Radius === 0);
                assert(circle.Perimeter() === 0);
            ");
    }

    [Test]
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

    [Test]
    public void ShouldConstructReferenceTypeWithParameters()
    {
        RunTest(@"
                var Shapes = importNamespace('Shapes');
                var circle = new Shapes.Circle(1);
                assert(circle.Radius === 1);
                assert(circle.Perimeter() === Math.PI);
            ");
    }

    [Test]
    public void ShouldConstructValueTypeWithoutParameters()
    {
        RunTest(@"
                var guid = new System.Guid();
                assert('00000000-0000-0000-0000-000000000000' === guid.ToString());
            ");
    }

    [Test]
    public void ShouldInvokeAFunctionByName()
    {
        RunTest(@"
                function add(x, y) { return x + y; }
            ");

        _engine.Invoke("add", 1, 2).Should().Be(3);
    }

    [Test]
    public void ShouldNotInvokeNonFunctionValue()
    {
        RunTest(@"
                var x= 10;
            ");

        Invoking(() => _engine.Invoke("x", 1, 2)).Should().ThrowExactly<JavaScriptException>();
    }

    [Test]
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

    [Test]
    public void CanSetField()
    {
        var o = new ClassWithField();

        _engine.SetValue("o", o);

        RunTest(@"
                o.Field = 'Mickey Mouse';
                assert(o.Field === 'Mickey Mouse');
            ");

        o.Field.Should().Be("Mickey Mouse");
    }

    [Test]
    public void CanGetStaticField()
    {
        RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                var statics = domain.ClassWithStaticFields;
                assert(statics.Get == 'Get');
            ");
    }

    [Test]
    public void CanSetStaticField()
    {
        RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                var statics = domain.ClassWithStaticFields;
                statics.Set = 'hello';
                assert(statics.Set == 'hello');
            ");

        "hello".Should().Be(ClassWithStaticFields.Set);
    }

    [Test]
    public void CanGetStaticAccessor()
    {
        RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                var statics = domain.ClassWithStaticFields;
                assert(statics.Getter == 'Getter');
            ");
    }

    [Test]
    public void CanSetStaticAccessor()
    {
        RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                var statics = domain.ClassWithStaticFields;
                statics.Setter = 'hello';
                assert(statics.Setter == 'hello');
            ");

        "hello".Should().Be(ClassWithStaticFields.Setter);
    }

    [Test]
    public void CantSetStaticReadonly()
    {
        RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                var statics = domain.ClassWithStaticFields;
                statics.Readonly = 'hello';
                assert(statics.Readonly == 'Readonly');
            ");

        "Readonly".Should().Be(ClassWithStaticFields.Readonly);
    }

    [Test]
    public void CanSetCustomConverters()
    {
        var engine1 = new Engine();
        engine1.SetValue("p", new { Test = true });
        engine1.Execute("var result = p.Test;");
        ((bool) engine1.GetValue("result").ToObject()).Should().BeTrue();

        var engine2 = new Engine(o => o.AddObjectConverter(new NegateBoolConverter()));
        engine2.SetValue("p", new { Test = true });
        engine2.Execute("var result = p.Test;");
        ((bool) engine2.GetValue("result").ToObject()).Should().BeFalse();
    }

    [Test]
    public void CanConvertEnumsToString()
    {
        var engine1 = new Engine(o => o.AddObjectConverter(new EnumsToStringConverter()))
            .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()));
        engine1.SetValue("p", new { Comparison = StringComparison.CurrentCulture });
        engine1.Execute("assert(p.Comparison === 'CurrentCulture');");
        engine1.Execute("var result = p.Comparison;");
        ((string) engine1.GetValue("result").ToObject()).Should().Be("CurrentCulture");
    }

    [Test]
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

        p.Age.Should().Be(2);
    }

    [Test]
    public void CanOverwriteValues()
    {
        _engine.SetValue("x", 3);
        _engine.SetValue("x", 4);

        RunTest(@"
                assert(x === 4);
            ");
    }

    [Test]
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

    [Test]
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
        _engine.SetValue("assertFalse", new Action<bool>(static value => value.Should().BeFalse()));

        RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                var colors = domain.Colors;
                assert(o.r === colors.Red);
                assert(o.g === colors.Green);
                assert(o.b === colors.Blue);
                assertFalse(o.b2 === colors.Blue);
            ");
    }

    [Test]
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

        s.Color.Should().Be(Colors.Blue | Colors.Green);
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

    [Test]
    public void ShouldWorkWithEnumInt32()
    {
        TestEnum(TestEnumInt32.None);
        TestEnum(TestEnumInt32.One);
        TestEnum(TestEnumInt32.Min);
        TestEnum(TestEnumInt32.Max);
    }

    [Test]
    public void ShouldWorkWithEnumUInt32()
    {
        TestEnum(TestEnumUInt32.None);
        TestEnum(TestEnumUInt32.One);
        TestEnum(TestEnumUInt32.Min);
        TestEnum(TestEnumUInt32.Max);
    }

    [Test]
    public void ShouldWorkWithEnumInt64()
    {
        TestEnum(TestEnumInt64.None);
        TestEnum(TestEnumInt64.One);
        TestEnum(TestEnumInt64.Min);
        TestEnum(TestEnumInt64.Max);
    }

    [Test]
    public void ShouldWorkWithEnumUInt64()
    {
        TestEnum(TestEnumUInt64.None);
        TestEnum(TestEnumUInt64.One);
        TestEnum(TestEnumUInt64.Min);
        TestEnum(TestEnumUInt64.Max);
    }

    [Test]
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

    [Test]
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

        s.Color.Should().Be(Colors.Blue | Colors.Green);
    }

    [Test]
    public void ShouldUseExplicitPropertyGetter()
    {
        _engine.SetValue("c", new Company("ACME"));
        _engine.Evaluate("c.Name").Should().Be("ACME");
    }

    [Test]
    public void ShouldUseExplicitIndexerPropertyGetter()
    {
        var company = new Company("ACME");
        ((ICompany) company)["Foo"] = "Bar";
        _engine.SetValue("c", company);
        _engine.Evaluate("c.Foo").Should().Be("Bar");
    }

    [Test]
    public void ShouldConvertIndexerHitByTheIndexersOwnType()
    {
        // The indexer is probed before the member itself, so it can answer for a name that a
        // declared member also carries. The value then has the indexer's type, not the member's,
        // and converting it as the member's type used to throw an InvalidCastException.
        _engine.SetValue("h", new IndexerShadowingMember());
        _engine.Evaluate("h.Value").Should().Be("from indexer");
    }

    [Test]
    public void ShouldFallBackToMemberWhenIndexerDoesNotAnswer()
    {
        // the same object, on a name the indexer returns null for: the declared member wins and is
        // still converted by its own type
        _engine.SetValue("h", new IndexerShadowingMember());
        _engine.Evaluate("h.Other").Should().Be(7);
    }

    private sealed class IndexerShadowingMember
    {
        public int Value { get; set; } = 1;
        public int Other { get; set; } = 7;
        public string this[string key] => key == "Value" ? "from indexer" : null;
    }

    [Test]
    public void ShouldUseExplicitPropertySetter()
    {
        _engine.SetValue("c", new Company("ACME"));
        _engine.Evaluate("c.Name = 'Foo'; c.Name;").Should().Be("Foo");
    }

    [Test]
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

    [Test]
    public void ShouldUseExplicitMethod()
    {
        _engine.SetValue("c", new Company("ACME"));

        RunTest(@"
                assert(0 === c.CompareTo(c));
            ");
    }

    [Test]
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

    [Test]
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

    [Test]
    public void NullValueAsArgumentShouldWork()
    {
        _engine.SetValue("a", new A());

        RunTest(@"
                var x = a.Call2(null);
                assert(x === null);
            ");
    }

    [Test]
    public void ShouldSetPropertyToNull()
    {
        var p = new Person { Name = "Mickey" };
        _engine.SetValue("p", p);

        RunTest(@"
                assert(p.Name != null);
                p.Name = null;
                assert(p.Name == null);
            ");

        p.Name.Should().Be(null);
    }

    [Test]
    public void ShouldCallMethodWithNull()
    {
        _engine.SetValue("a", new A());

        RunTest(@"
                a.Call15(null);
                var result = a.Call2(null);
                assert(result == null);
            ");
    }

    [Test]
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

    [Test]
    public void ShouldPropagateIndexerExceptions()
    {
        var engine = new Engine();
        engine.Execute(@"function f2(obj) { return obj[1]; }");

        var failingObject = new FailingObject2();
        Invoking(() => engine.Invoke("f2", failingObject)).Should().ThrowExactly<ArgumentException>();
    }

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
    public void ShouldGetNestedNestedProp()
    {
        RunTest(@"
                var meta = importNamespace('Shapes.Circle');
                var m = new meta.Meta();
                assert(m.Description === 'descp');
            ");
    }

    [Test]
    public void ShouldSetNestedNestedProp()
    {
        RunTest(@"
                var meta = importNamespace('Shapes.Circle');
                var m = new meta.Meta();
                m.Description = 'hello';
                assert(m.Description === 'hello');
            ");
    }

    [Test]
    public void CanGetStaticNestedField()
    {
        RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain.Nested');
                var statics = domain.ClassWithStaticFields;
                assert(statics.Get == 'Get');
            ");
    }

    [Test]
    public void CanSetStaticNestedField()
    {
        RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain.Nested');
                var statics = domain.ClassWithStaticFields;
                statics.Set = 'hello';
                assert(statics.Set == 'hello');
            ");

        "hello".Should().Be(Nested.ClassWithStaticFields.Set);
    }

    [Test]
    public void CanGetStaticNestedAccessor()
    {
        RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain.Nested');
                var statics = domain.ClassWithStaticFields;
                assert(statics.Getter == 'Getter');
            ");
    }

    [Test]
    public void CanSetStaticNestedAccessor()
    {
        RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain.Nested');
                var statics = domain.ClassWithStaticFields;
                statics.Setter = 'hello';
                assert(statics.Setter == 'hello');
            ");

        "hello".Should().Be(Nested.ClassWithStaticFields.Setter);
    }

    [Test]
    public void CantSetStaticNestedReadonly()
    {
        RunTest(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain.Nested');
                var statics = domain.ClassWithStaticFields;
                statics.Readonly = 'hello';
                assert(statics.Readonly == 'Readonly');
            ");

        "Readonly".Should().Be(Nested.ClassWithStaticFields.Readonly);
    }

    [Test]
    public void ShouldExecuteFunctionWithValueTypeParameterCorrectly()
    {
        _engine.SetValue("a", new A());
        // Func<int, int>
        RunTest(@"
                assert(a.Call17(function(value){ return value; }) === 17);
            ");
    }

    [Test]
    public void ShouldExecuteActionWithValueTypeParameterCorrectly()
    {
        _engine.SetValue("a", new A());
        // Action<int>
        RunTest(@"
                a.Call18(function(value){ assert(value === 18); });
            ");
    }

    [Test]
    public void ShouldConvertToJsValue()
    {
        RunTest(@"
                var now = System.DateTime.Now;
                assert(new String(now) == now.toString());

                var zero = System.Int32.MaxValue;
                assert(new String(zero) == zero.toString());
            ");
    }

    [Test]
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

        Invoking(() => engine.Invoke("throwException1")).Should().Throw<NotSupportedException>();
        Invoking(() => engine.Invoke("throwException2")).Should().Throw<NotSupportedException>();
    }

    [Test]
    public void ShouldCatchAllClrExceptions()
    {
        var exceptionMessage = "myExceptionMessage";

        var engine = new Engine(o =>
            {
                o.CatchClrExceptions();
                o.Interop.ExposeDetailedExceptionMessages = true;
            })
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

        exceptionMessage.Should().Be(engine.Invoke("throwException1").AsString());
        exceptionMessage.Should().Be(engine.Invoke("throwException2").AsString());
    }

    [Test]
    public void CaughtClrExceptionShouldExposeJavaScriptLocation()
    {
        var engine = new Engine(o =>
            {
                o.CatchClrExceptions();
                o.Interop.ExposeDetailedExceptionMessages = true;
            })
            .SetValue("Thrower", typeof(Thrower));

        const string script = @"// line 1
// line 2
new Thrower().ThrowExceptionWithMessage('boom');";

        var ex = Invoking(() => engine.Execute(script)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("boom");
        ex.Location.Should().NotBe(default);
        ex.Location.Start.Line.Should().Be(3);
        ex.JavaScriptStackTrace.Should().NotBeNull();
        ex.JavaScriptStackTrace.Should().Contain("3:");
    }

    [Test]
    public void ShouldNotCatchClrFromApply()
    {
        var handlerCalled = false;
        var engine = new Engine(options =>
        {
            options.Interop.ExceptionHandler = e =>
            {
                handlerCalled = true;
                return true;
            };
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

        handlerCalled.Should().BeFalse();
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

    [Test]
    public void ShouldCatchClrMemberExceptions()
    {
        var engine = new Engine(cfg =>
        {
            cfg.AllowClr();
            cfg.CatchClrExceptions();
        });

        engine.SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()));
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

    [Test]
    public void ShouldCatchSomeExceptions()
    {
        var exceptionMessage = "myExceptionMessage";

        var engine = new Engine(o =>
            {
                o.Interop.ExceptionHandler = e => e is NotSupportedException;
                o.Interop.ExposeDetailedExceptionMessages = true;
            })
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

        exceptionMessage.Should().Be(engine.Invoke("throwException1").AsString());
        Invoking(() => engine.Invoke("throwException2")).Should().ThrowExactly<ArgumentNullException>();
        exceptionMessage.Should().Be(engine.Invoke("throwException3").AsString());
        Invoking(() => engine.Invoke("throwException4")).Should().ThrowExactly<ArgumentNullException>();
    }

    [Test]
    public void ShouldDecorateClrExceptionErrors()
    {
        var exceptionMessage = "Test exception";
        var decoratorCalled = false;
        Exception capturedOriginalException = null;

        var engine = new Engine(options =>
        {
            options.CatchClrExceptions();
            options.Interop.ClrExceptionErrorDecorator = (engine, error, clrException) =>
            {
                decoratorCalled = true;
                capturedOriginalException = clrException;
                
                // Add custom property
                error.Set("clrType", clrException.GetType().FullName);
                error.Set("customProperty", "decorated");
                
                // Modify existing property
                var currentMessage = error.Get("message").ToString();
                error.Set("message", $"[Decorated] {currentMessage}");
            };
        });

        engine.SetValue("throwException", new Action(() => { throw new InvalidOperationException(exceptionMessage); }));

        var result = engine.Evaluate(@"
            let caughtError;
            try {
                throwException();
            } catch(e) {
                caughtError = e;
            }
            caughtError;
        ");

        decoratorCalled.Should().BeTrue("Decorator should have been called");
        capturedOriginalException.Should().NotBeNull();
        capturedOriginalException.Should().BeOfType<InvalidOperationException>();
        capturedOriginalException.Message.Should().Be(exceptionMessage);
        
        var errorObject = result.AsObject();
        errorObject.Get("customProperty").AsString().Should().Be("decorated");
        errorObject.Get("clrType").AsString().Should().Be("System.InvalidOperationException");
        errorObject.Get("message").AsString().Should().Be("[Decorated] A host operation failed.");
    }

    [Test]
    public void ShouldDecorateClrExceptionErrorsFromMemberCalls()
    {
        var decoratorCallCount = 0;

        var engine = new Engine(options =>
        {
            options.AllowClr();
            options.CatchClrExceptions();
            options.Interop.ClrExceptionErrorDecorator = (engine, error, clrException) =>
            {
                decoratorCallCount++;
                error.Set("exceptionType", clrException.GetType().Name);
            };
        });

        engine.SetValue("instance", new MemberExceptionTest(false));

        engine.Execute(@"
            try {
                instance.ThrowingFunction();
            } catch(e) {
                if (e.exceptionType !== 'InvalidOperationException') {
                    throw new Error('Expected exceptionType to be InvalidOperationException');
                }
            }
        ");

        decoratorCallCount.Should().Be(1);
    }

    [Test]
    public void ShouldNotCallDecoratorWhenExceptionNotCaught()
    {
        var decoratorCalled = false;

        var engine = new Engine(options =>
        {
            options.Interop.ExceptionHandler = e => e is NotSupportedException;
            options.Interop.ClrExceptionErrorDecorator = (engine, error, clrException) =>
            {
                decoratorCalled = true;
            };
        });

        engine.SetValue("throwException", new Action(() => { throw new InvalidOperationException(); }));

        // Should throw because InvalidOperationException is not caught
        Invoking(() => engine.Evaluate("throwException()")).Should().ThrowExactly<InvalidOperationException>();
        decoratorCalled.Should().BeFalse("Decorator should not be called when exception is not caught");
    }

    [Test]
    public void DecoratorCanAccessEngineContext()
    {
        var engine = new Engine(options =>
        {
            options.CatchClrExceptions();
            options.Interop.ClrExceptionErrorDecorator = (engine, error, clrException) =>
            {
                // Decorator can access engine and add context from it
                error.Set("hasRealm", engine.Realm != null);
                error.Set("timestamp", DateTime.UtcNow.ToString("o"));
            };
        });

        engine.SetValue("throwException", new Action(() => { throw new Exception("test"); }));

        var result = engine.Evaluate(@"
            try {
                throwException();
            } catch(e) {
                return { hasRealm: e.hasRealm, hasTimestamp: e.timestamp !== undefined };
            }
        ").AsObject();

        result.Get("hasRealm").AsBoolean().Should().BeTrue();
        result.Get("hasTimestamp").AsBoolean().Should().BeTrue();
    }

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
    public void ShouldBeAbleToPlusAssignStringProperty()
    {
        var p = new Person();
        var engine = new Engine(options => options.Interop.AllowWrite = true);
        engine.SetValue("P", p);
        engine.Evaluate("P.Name = 'b';");
        engine.Evaluate("P.Name += 'c';");
        p.Name.Should().Be("bc");
    }

    [Test]
    public void ShouldNotResolveToPrimitiveSymbol()
    {
        var engine = new Engine(options =>
            options.AllowClr(typeof(FloatIndexer).GetTypeInfo().Assembly));
        var c = engine.Evaluate(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                return new domain.FloatIndexer();
            ");

        c.ToString().Should().NotBeNull();
        c.AsObject().GetLength().Should().Be((uint) 0);
    }

    private class DictionaryWrapper
    {
        public IDictionary<string, object> Values { get; set; }
    }

    private class DictionaryTest
    {
        public void Test1(IDictionary<string, object> values)
        {
            Convert.ToInt32(values["a"]).Should().Be(1);
        }

        public void Test2(DictionaryWrapper dictionaryObject)
        {
            Convert.ToInt32(dictionaryObject.Values["a"]).Should().Be(1);
        }

        public void Test3(Dictionary<string, object> values)
        {
            Convert.ToInt32(values["a"]).Should().Be(1);
        }

        public void Test4(Dictionary<string, object> values = null)
        {
            values.Should().NotBeNull();
            values["value"].Should().Be("world");
        }
    }

    [Test]
    public void ShouldBeAbleToPassDictionaryToMethod()
    {
        var engine = new Engine();
        engine.SetValue("dictionaryTest", new DictionaryTest());
        engine.Evaluate("dictionaryTest.test1({ a: 1 });");
    }

    [Test]
    public void ShouldBeAbleToPassDictionaryInObjectToMethod()
    {
        var engine = new Engine();
        engine.SetValue("dictionaryTest", new DictionaryTest());
        engine.Evaluate("dictionaryTest.test2({ values: { a: 1 } });");
    }

    [Test]
    public void ShouldBeAbleToPassConcreteDictionaryToMethod()
    {
        var engine = new Engine();
        engine.SetValue("dictionaryTest", new DictionaryTest());
        engine.Evaluate("dictionaryTest.test3({ a: 1 });");
    }

    [Test]
    public void ShouldBeAbleToPassConcreteDictionaryToOptionalParameter()
    {
        var engine = new Engine();
        engine.SetValue("dictionaryTest", new DictionaryTest());
        engine.Evaluate("dictionaryTest.test4({ value: 'world' });");
    }

    [Test]
    public void ShouldNotChangeFunctionArgumentsWhenFunctionStoredInDictionary()
    {
        var engine = new Engine(options => options.Interop.AllowWrite = true);
        engine.SetValue("globalScope", new Dictionary<string, object>());

        engine.Execute("""
            globalScope.fuzzyScore = function (pattern, text) {
                return pattern + ":" + text;
            };
            """);

        var result = engine.Evaluate("globalScope.fuzzyScore('abc', 'xyz');");
        result.AsString().Should().Be("abc:xyz");
    }

    [Test]
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

        result["supplier"].Should().Be("S1");
        result["number"].Should().Be("42");
    }

    [Test]
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

        result["supplier"].Should().Be("S1");
        result["number"].Should().Be("42");
    }

    [Test]
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

        result["supplier"].Should().Be("S1");
        result["Name"].Should().Be("Mike");
        result["Age"].Should().Be(20d);
    }

    [Test]
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
        clrValue.Should().Be(jsValue);

        jsValue = engine.Evaluate("JSON.stringify(jsObj)").AsString();
        clrValue = engine.Evaluate("JSON.stringify(netObj)").AsString();
        clrValue.Should().Be(jsValue);

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
        clrValue.Should().Be(jsValue);
    }

    [Test]
    public void SettingValueViaIntegerIndexer()
    {
        var engine = new Engine(cfg => cfg
            .AllowClr(typeof(FloatIndexer).GetTypeInfo().Assembly)
            .Interop.AllowWrite = true);
        engine.SetValue("log", new Action<object>(Console.WriteLine));
        engine.Execute(@"
                var domain = importNamespace('Jint.Tests.Runtime.Domain');
                var fia = new domain.IntegerIndexer();
                log(fia[0]);
            ");

        engine.Evaluate("fia[0]").AsNumber().Should().Be(123);
        engine.Evaluate("fia[0] = 678;");
        engine.Evaluate("fia[0]").AsNumber().Should().Be(678);
    }

    [Test]
    public void IndexingBsonProperties()
    {
        const string jsonAnimals = @" { ""Animals"": [ { ""Id"": 1, ""Type"": ""Cat"" } ] }";
        var bsonAnimals = BsonDocument.Parse(jsonAnimals);

        _engine.SetValue("animals", bsonAnimals["Animals"]);

        // weak equality does conversions from native types
        _engine.Evaluate("animals[0].Type == 'Cat'").AsBoolean().Should().BeTrue();
        _engine.Evaluate("animals[0].Id == 1").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void IntegerAndFloatInFunctionOverloads()
    {
        var engine = new Engine(options => options.AllowClr(GetType().Assembly));
        engine.SetValue("a", new OverLoading());
        engine.Evaluate("a.testFunc(123);").AsString().Should().Be("int-val");
        engine.Evaluate("a.testFunc(12.3);").AsString().Should().Be("float-val");
    }

    [Test]
    public void TypeConversionWithTemporaryInvalidValuesShouldNotCache()
    {
        var engine = new Engine(options => options.AllowClr());
        engine.SetValue("IntValueInput", TypeReference.CreateTypeReference(engine, typeof(IntValueInput)));
        var ex = Invoking(() => engine.Evaluate("new IntValueInput().testFunc(NaN);").AsString()).Should().ThrowExactly<JavaScriptException>().Which;
        // detailed resolution errors are off by default, so the terse message is used
        ex.Message.Should().Be("No public methods with the specified arguments were found.");

        engine.Evaluate("new IntValueInput().testFunc(123);").AsNumber().Should().Be(123);
    }

    [Test]
    public void CanConvertFloatingPointToIntegerWithoutError()
    {
        var engine = new Engine(options => options.AllowClr());
        engine.SetValue("IntValueInput", TypeReference.CreateTypeReference(engine, typeof(IntValueInput)));
        engine.Evaluate("new IntValueInput().testFunc(12.3);").AsNumber().Should().Be(12);
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

    [Test]
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

        engine.Evaluate("lst.Sum(x => x.Cost);").AsNumber().Should().Be(5);
        engine.Evaluate("lst.Sum(x => x.Age);").AsNumber().Should().Be(50);
        engine.Evaluate("lst.Where(x => x.Name == 'b').Count;").AsNumber().Should().Be(3);
        engine.Evaluate("lst.Where(x => x.Name == 'b').Sum(x => x.Age);").AsNumber().Should().Be(30);
    }

    [Test]
    public void ObjectWrapperOverridingEquality()
    {
        // equality same via name
        _engine.SetValue("a", new Person { Name = "Name" });
        _engine.SetValue("b", new Person { Name = "Name" });
        _engine.Evaluate("const arr = [ null, a, undefined ];");

        _engine.Evaluate("arr.filter(x => x == b).length").AsNumber().Should().Be(1);
        _engine.Evaluate("arr.filter(x => x === b).length").AsNumber().Should().Be(1);

        _engine.Evaluate("arr.find(x => x == b) === a").AsBoolean().Should().BeTrue();
        _engine.Evaluate("arr.find(x => x === b) == a").AsBoolean().Should().BeTrue();

        _engine.Evaluate("arr.findIndex(x => x == b)").AsNumber().Should().Be(1);
        _engine.Evaluate("arr.findIndex(x => x === b)").AsNumber().Should().Be(1);

        _engine.Evaluate("arr.indexOf(b)").AsNumber().Should().Be(1);
        _engine.Evaluate("arr.includes(b)").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ObjectWrapperWrappingDictionaryShouldNotBeArrayLike()
    {
        var wrapper = ObjectWrapper.Create(_engine, new Dictionary<string, object>());
        wrapper.IsArrayLike.Should().BeFalse();
    }

    [TestCase("result")]
    [TestCase("result1", "result2")]
    [TestCase("result1", "result2", "result3")]
    [TestCase("result1", "result2", "result3", "result4")]
    public void ObjectWrapperFrozenDictionaryShouldPreventDelete(params string[] names)
    {
        var access = string.Join(".", names);

        var engine = new Engine(cfg => cfg.Strict = true);

        var context = new Dictionary<string, object>();
        var temp = context;

        for (var i = 0; i < names.Length - 1; i++)
        {
            var newStore = new Dictionary<string, object>();
            temp[names[i]] = newStore;
            temp = newStore;
        }

        temp[names[^1]] = "value";

        engine.SetValue("context", context);

        engine.Execute(
            """
            function freeze(obj) {
              Object.freeze(obj);
              Object.keys(obj).forEach(key => {
                if (typeof obj[key] === 'object' && obj[key] !== null) {
                  freeze(obj[key]);
                }
              });
            }
            freeze(context);
            """
        );

        var ex = Invoking(() => engine.Execute($"delete context.{access}")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().StartWith($"Cannot delete property '{names[^1]}'");
    }

    [Test]
    public void ShouldHandleCyclicReferences()
    {
        var engine = new Engine();

        static void Test(string message, object value)
        {
            Console.WriteLine(message);
        }

        engine.Realm.GlobalObject.DefineOwnDataPropertyUnchecked("global", engine.Realm.GlobalObject);
        engine.Realm.GlobalObject.DefineOwnDataPropertyUnchecked("test", new DelegateWrapper(engine, (Action<string, object>) Test));

        {
            var ex = Invoking(() => engine.Realm.GlobalObject.ToObject()).Should().ThrowExactly<JavaScriptException>().Which;
            ex.Message.Should().Be("Cyclic reference detected.");
        }

        {
            var ex = Invoking(() =>
                engine.Execute(@"
                    var demo={};
                    demo.value=1;
                    test('Test 1', demo.value===1);
                    test('Test 2', demo.value);
                    demo.demo=demo;
                    test('Test 3', demo);
                    test('Test 4', global);"
                )).Should().ThrowExactly<JavaScriptException>().Which;
            ex.Message.Should().Be("Cyclic reference detected.");
        }
    }

    [Test]
    public void CanConfigurePropertyNameMatcher()
    {
        // defaults
        var e = new Engine();
        e.SetValue("a", new A());
        e.Evaluate("a.call1").IsObject().Should().BeTrue();
        e.Evaluate("a.Call1").IsObject().Should().BeTrue();
        e.Evaluate("a.CALL1").IsUndefined().Should().BeTrue();

        e = new Engine(options =>
        {
            options.Interop.TypeResolver = new TypeResolver
            {
                MemberNameComparer = StringComparer.Ordinal
            };
        });
        e.SetValue("a", new A());
        e.Evaluate("a.call1").IsUndefined().Should().BeTrue();
        e.Evaluate("a.Call1").IsObject().Should().BeTrue();
        e.Evaluate("a.CALL1").IsUndefined().Should().BeTrue();

        e = new Engine(options =>
        {
            options.Interop.TypeResolver = new TypeResolver
            {
                MemberNameComparer = StringComparer.OrdinalIgnoreCase
            };
        });
        e.SetValue("a", new A());
        e.Evaluate("a.call1").IsObject().Should().BeTrue();
        e.Evaluate("a.Call1").IsObject().Should().BeTrue();
        e.Evaluate("a.CALL1").IsObject().Should().BeTrue();
    }

    [Test]
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
        result.Should().Be("foo,bar");


        engine.Execute("dictionary.ContainsKey('foo')");
        result = engine.Evaluate($"Object.keys(dictionary).join(',')").AsString();
        result.Should().Be("foo,bar");
    }

    [Test]
    public void ShouldNotEnumerateExtensionMethods()
    {
        var engine = new Engine(cfg => cfg.AddExtensionMethods(typeof(Enumerable)));

        var result = engine.Evaluate("Object.keys({ ...[1,2,3] }).join(',')").AsString();
        result.Should().Be("0,1,2");

        var script = @"
                var arr = [1,2,3];
                var keys = [];
                for(var index in arr) keys.push(index);
                keys.join(',');
            ";
        result = engine.Evaluate(script).ToString();
        result.Should().Be("0,1,2");
    }

    [Test]
    public void ForInEnumeratesClrObjectUsingReportedPropertyKeys()
    {
        // https://github.com/sebastienros/jint/discussions/2513
        var people = new ReportedKeysPeople(new[]
        {
            new ReportedKeysPerson("person1"),
            new ReportedKeysPerson("person2"),
        });

        var engine = new Engine(cfg => cfg.Interop.ObjectWrapperReportedPropertyKeys =
            static (_, target) => target is ReportedKeysPeople p ? p.Names.Select(n => (JsValue) n) : null);
        engine.SetValue("people", people);

        // indexer access keeps working
        engine.Evaluate("people['person1'].Name").AsString().Should().Be("person1");

        // for...in now enumerates the reported keys and the body resolves values via the indexer
        var forIn = engine.Evaluate(@"
            var result = '';
            for (var p in people) { result += people[p].Name + ' '; }
            result.trim();
        ").AsString();
        forIn.Should().Be("person1 person2");

        // Object.keys shares the same enumeration path
        engine.Evaluate("Object.keys(people).join(',')").AsString().Should().Be("person1,person2");

        // regular CLR members remain accessible alongside the reported keys
        engine.Evaluate("people.Count").AsNumber().Should().Be(2d);
    }

    [Test]
    public void ReportedPropertyKeysDefaultsToExistingEnumeration()
    {
        var engine = new Engine();
        engine.SetValue("dictionary", new Dictionary<string, object>
        {
            { "foo", 1 },
            { "bar", 2 },
        });

        // default delegate returns null, so dictionary enumeration is unchanged
        engine.Evaluate("Object.keys(dictionary).join(',')").AsString().Should().Be("foo,bar");

        var forIn = engine.Evaluate(@"
            var keys = [];
            for (var k in dictionary) keys.push(k);
            keys.join(',');
        ").AsString();
        forIn.Should().Be("foo,bar");
    }

    public sealed class ReportedKeysPerson(string name)
    {
        public string Name => name;
    }

    public sealed class ReportedKeysPeople(IEnumerable<ReportedKeysPerson> people)
    {
        private readonly List<ReportedKeysPerson> _people = people.ToList();

        public ReportedKeysPerson this[JsValue name] => _people.FirstOrDefault(p => p.Name == name.ToString());

        public int Count => _people.Count;

        public IEnumerable<string> Names => _people.Select(p => p.Name);
    }

    [Test]
    public void CanCheckIfCallable()
    {
        var engine = new Engine();
        engine.Evaluate("var f = () => true;");

        var result = engine.GetValue("f");
        result.IsCallable().Should().BeTrue();

        result.Call([]).AsBoolean().Should().BeTrue();
        result.Call().AsBoolean().Should().BeTrue();
    }

    [Test]
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
            options.Interop.AllowWrite = true;
            options.Interop.TypeResolver = customTypeResolver;
            options.AddExtensionMethods(typeof(CustomNamedExtensions));
        });

        engine.SetValue("o", new CustomNamed());
        engine.Evaluate("o.jsStringField").AsString().Should().Be("StringField");
        engine.Evaluate("o.jsStringField2").AsString().Should().Be("StringField");
        engine.Evaluate("o.jsStringProperty").AsString().Should().Be("StringProperty");
        engine.Evaluate("o.jsMethod()").AsString().Should().Be("Method");
        engine.Evaluate("o.jsInterfaceStringProperty").AsString().Should().Be("InterfaceStringProperty");
        engine.Evaluate("o.jsInterfaceMethod()").AsString().Should().Be("InterfaceMethod");
        engine.Evaluate("o.jsExtensionMethod()").AsString().Should().Be("ExtensionMethod");

        // static methods are reported by default, unlike properties and fields
        engine.Evaluate("o.jsStaticMethod()").AsString().Should().Be("StaticMethod");

        engine.SetValue("CustomNamed", typeof(CustomNamed));
        engine.Evaluate("CustomNamed.jsStaticStringField").AsString().Should().Be("StaticStringField");
        engine.Evaluate("CustomNamed.jsStaticMethod()").AsString().Should().Be("StaticMethod");

        engine.SetValue("XmlHttpRequest", typeof(CustomNamedEnum));
        engine.Evaluate("o.jsEnumProperty = XmlHttpRequest.HEADERS_RECEIVED;");
        engine.Evaluate("o.jsEnumProperty").AsNumber().Should().Be((int) CustomNamedEnum.HeadersReceived);

        // can get static members with different configuration
        var engineWithStaticsReported = new Engine(options => options.Interop.ObjectWrapperReportedFieldBindingFlags |= BindingFlags.Static);
        engineWithStaticsReported.SetValue("o", new CustomNamed());
        engineWithStaticsReported.Evaluate("o.staticMethod()").AsString().Should().Be("StaticMethod");
        engineWithStaticsReported.Evaluate("o.staticStringField").AsString().Should().Be("StaticStringField");
    }

    [Test]
    public void ShouldBeAbleToHandleInvalidClrConversionViaCatchClrExceptions()
    {
        var engine = new Engine(cfg => cfg.CatchClrExceptions().Interop.AllowWrite = true);
        engine.SetValue("a", new Person());
        var ex = Invoking(() => engine.Execute("a.age = 'It will not work, but it is normal'")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().ContainEquivalentOf("input string ");
        ex.Message.Should().ContainEquivalentOf(" was not in a correct format");
    }

    [Test]
    public void ShouldLetNotSupportedExceptionBubble()
    {
        _engine.SetValue("profile", new Profile());
        var ex = Invoking(() => _engine.Evaluate("profile.AnyProperty")).Should().ThrowExactly<NotSupportedException>().Which;
        ex.Message.Should().Be("NOT SUPPORTED");
    }

    [Test]
    public void ShouldBeAbleToUseConvertibleStructAsMethodParameter()
    {
        _engine.SetValue("test", new DiscordTestClass());
        _engine.SetValue("id", new DiscordId("12345"));

        _engine.Evaluate("String(id)").AsString().Should().Be("12345");
        _engine.Evaluate("test.echo('12345')").AsString().Should().Be("12345");
        _engine.Evaluate("test.create(12345)").AsString().Should().Be("12345");
    }

    [Test]
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
        _engine.Evaluate(Script).Should().Be("abc");

        _engine.SetValue("collection", new Dictionary<string, object> { { "a", 1 }, { "b", 2 }, { "c", 3 } });
        _engine.Evaluate(Script).Should().Be("a,1b,2c,3");
    }

    [Test]
    public void ShouldNotIntroduceNewPropertiesWhenTraversing()
    {
        _engine.SetValue("x", new Dictionary<string, int> { { "First", 1 }, { "Second", 2 } });

        _engine.Evaluate("JSON.stringify(Object.keys(x))").Should().Be("[\"First\",\"Second\"]");

        _engine.Evaluate("\"x['First']: \" + x['First']").Should().Be("x['First']: 1");
        _engine.Evaluate("JSON.stringify(Object.keys(x))").Should().Be("[\"First\",\"Second\"]");

        _engine.Evaluate("\"x['Third']: \" + x['Third']").Should().Be("x['Third']: undefined");
        _engine.Evaluate("JSON.stringify(Object.keys(x))").Should().Be("[\"First\",\"Second\"]");

        _engine.Evaluate("x.length").Should().BeUndefined();
        _engine.Evaluate("JSON.stringify(Object.keys(x))").Should().Be("[\"First\",\"Second\"]");

        _engine.Evaluate("x.Count").AsNumber().Should().Be(2);
        _engine.Evaluate("JSON.stringify(Object.keys(x))").Should().Be("[\"First\",\"Second\"]");

        _engine.Evaluate("x.Clear();");

        _engine.Evaluate("JSON.stringify(Object.keys(x))").Should().Be("[]");

        _engine.Evaluate("x['Fourth'] = 4;");
        _engine.Evaluate("JSON.stringify(Object.keys(x))").Should().Be("[\"Fourth\"]");

        _engine.Evaluate("Object.prototype.hasOwnProperty.call(x, 'Third')").AsBoolean().Should().BeFalse();
    }

    [Test]
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

        capture.Should().BeOfType<Dictionary<string, object>>();
        var dictionary = (Dictionary<string, object>) capture;
        dictionary["a"].Should().Be("b");
    }

    [Test]
    public void ArrayPrototypeIndexOfWithInteropList()
    {
        var engine = new Jint.Engine();

        engine.SetValue("list", new List<string> { "A", "B", "C" });

        engine.Evaluate("list.indexOf('B')").Should().Be(1);
        engine.Evaluate("list.lastIndexOf('B')").Should().Be(1);

        engine.Evaluate("Array.prototype.indexOf.call(list, 'B')").Should().Be(1);
        engine.Evaluate("Array.prototype.lastIndexOf.call(list, 'B')").Should().Be(1);
    }

    [Test]
    public void ArrayPrototypeFindWithInteropList()
    {
        var engine = new Jint.Engine();
        var list = new List<string> { "A", "B", "C" };

        engine.SetValue("list", list);

        engine.Evaluate("list.findIndex((x) => x === 'B')").Should().Be(1);
        engine.Evaluate("list.find((x) => x === 'B')").Should().Be('B');
    }

    [Test]
    public void ArrayPrototypePushWithInteropList()
    {
        var engine = new Jint.Engine(options => options.Interop.AllowWrite = true);

        var list = new List<string> { "A", "B", "C" };

        engine.SetValue("list", list);

        engine.Evaluate("list.push('D')");
        list.Should().HaveCount(4);
        list[3].Should().Be("D");
        engine.Evaluate("list.lastIndexOf('D')").Should().Be(3);
    }

    [Test]
    public void ArrayPrototypePopWithInteropList()
    {
        var engine = new Jint.Engine(options => options.Interop.AllowWrite = true);

        var list = new List<string> { "A", "B", "C" };
        engine.SetValue("list", list);

        engine.Evaluate("list.lastIndexOf('C')").Should().Be(2);
        list.Should().HaveCount(3);
        engine.Evaluate("list.pop()").Should().Be("C");
        list.Should().HaveCount(2);
        engine.Evaluate("list.lastIndexOf('C')").Should().Be(-1);
    }

    [Test]
    public void ListReverseDefaultsToClrSemantics()
    {
        // Default: List<T>.Reverse() (void) wins over Array.prototype.reverse — locks in current behavior.
        // CLR void is exposed to JS as null (not undefined), and the list is reversed in place.
        var engine = new Jint.Engine();
        var list = new List<int> { 1, 2, 3 };
        engine.SetValue("list", list);

        var result = engine.Evaluate("list.reverse()");
        result.IsNull().Should().BeTrue();
        list.Should().Equal(new[] { 3, 2, 1 });
    }

    [Test]
    public void PreferJsPrototypeMethodsMakesArrayReverseWin()
    {
        var engine = new Jint.Engine(cfg => { cfg.Interop.PreferJsPrototypeMethods = true; cfg.Interop.AllowWrite = true; });
        var list = new List<int> { 1, 2, 3 };
        engine.SetValue("list", list);

        engine.Evaluate("list.reverse() === list").AsBoolean().Should().BeTrue();
        list.Should().Equal(new[] { 3, 2, 1 });
    }

    [Test]
    public void PreferJsPrototypeMethodsMakesArraySortWin()
    {
        // Without the flag List<int>.Sort gives ascending int sort and returns void.
        // With the flag, Array.prototype.sort returns the array and uses JS string-compare semantics
        // ([10, 2, 1] -> [1, 10, 2] because "10" < "2" lexicographically).
        var engine = new Jint.Engine(cfg => { cfg.Interop.PreferJsPrototypeMethods = true; cfg.Interop.AllowWrite = true; });
        var list = new List<int> { 10, 2, 1 };
        engine.SetValue("list", list);

        engine.Evaluate("JSON.stringify(list.sort())").AsString().Should().Be("[1,10,2]");
    }

    [Test]
    public void PreferJsPrototypeMethodsLeavesNonClashingClrMethodsAlone()
    {
        // Methods without an Array.prototype counterpart must still resolve to CLR.
        var engine = new Jint.Engine(cfg => cfg.Interop.PreferJsPrototypeMethods = true);
        var list = new List<string> { "A", "B" };
        engine.SetValue("list", list);

        engine.Evaluate("list.Add('C')");
        list.Should().HaveCount(3);
        list[2].Should().Be("C");

        engine.Evaluate("list.RemoveAt(0)");
        list.Should().Equal(new[] { "B", "C" });
    }

    [Test]
    public void PreferJsPrototypeMethodsKeepsLengthMappedToCount()
    {
        // length is served by the fast path in ObjectWrapper.Get — must be unaffected.
        var engine = new Jint.Engine(cfg => cfg.Interop.PreferJsPrototypeMethods = true);
        var list = new List<int> { 10, 20, 30, 40 };
        engine.SetValue("list", list);

        engine.Evaluate("list.length").AsNumber().Should().Be(4);
    }

    [Test]
    public void PreferJsPrototypeMethodsDoesNotAffectPlainObjectWrapper()
    {
        // POCOs get Object.prototype, which the check explicitly skips, so CLR ToString still wins.
        var engine = new Jint.Engine(cfg => cfg.Interop.PreferJsPrototypeMethods = true);
        engine.SetValue("obj", new ClassWithToString());

        engine.Evaluate("obj.toString()").AsString().Should().Be("Test");
    }

    [Test]
    public void ListReverseCanBeFixedTodayWithMemberFilter()
    {
        // Documents the workaround that works without the new flag, on existing Jint versions.
        var engine = new Jint.Engine(options =>
        {
            options.Interop.AllowWrite = true;
            options.Interop.TypeResolver = new TypeResolver
            {
                MemberFilter = m =>
                {
                    if (m is System.Reflection.MethodInfo mi
                        && mi.DeclaringType is { } dt
                        && typeof(System.Collections.IList).IsAssignableFrom(dt))
                    {
                        return mi.Name is not ("Reverse" or "Sort");
                    }
                    return true;
                }
            };
        });

        var list = new List<int> { 1, 2, 3 };
        engine.SetValue("list", list);

        engine.Evaluate("list.reverse() === list").AsBoolean().Should().BeTrue();
        list.Should().Equal(new[] { 3, 2, 1 });
    }

    [Test]
    public void ShouldBeJavaScriptException()
    {
        var engine = new Engine(cfg =>
        {
            cfg.AllowClr().CatchClrExceptions();
            cfg.Interop.AllowOperatorOverloading = true;
            cfg.Interop.ExposeDetailedExceptionMessages = true;
        });
        engine.SetValue("Dimensional", typeof(Dimensional));

        engine.Execute(@"	
				function Eval(param0, param1)
				{ 
					var result = param0 + param1;
					return result;
				}");
        // checking working custom type
        (new Dimensional("kg", 30) + new Dimensional("kg", 60)).Should().Be(new Dimensional("kg", 90));
        engine.Invoke("Eval", new object[] { new Dimensional("kg", 30), new Dimensional("kg", 60) }).ToObject().Should().Be(new Dimensional("kg", 90));
        Invoking(() => new Dimensional("kg", 30) + new Dimensional("piece", 70)).Should().ThrowExactly<InvalidOperationException>();

        // checking throwing exception in override operator
        string errorMsg = string.Empty;
        errorMsg = Invoking(() => engine.Invoke("Eval", new object[] { new Dimensional("kg", 30), new Dimensional("piece", 70) })).Should().ThrowExactly<JavaScriptException>().Which.Message;
        errorMsg.Should().Be("Dimensionals with different measure types are non-summable");
    }

    private class Profile
    {
        public int AnyProperty => throw new NotSupportedException("NOT SUPPORTED");
    }

    [Test]
    public void GenericParameterResolutionShouldWorkWithNulls()
    {
        var result = new Engine()
            .SetValue("JintCommon", new JintCommon())
            .Evaluate("JintCommon.sum(1, null)")
            .AsNumber();

        result.Should().Be(2);
    }

    public class JintCommon
    {
        public int Sum(int a, int? b) => a + b.GetValueOrDefault(1);
    }

    private delegate void ParamsTestDelegate(params Action[] callbacks);

    [Test]
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

    [Test]
    public void ObjectWrapperIdentityIsMaintained()
    {
        // run in separate method so stack won't keep reference
        var reference = RunWeakReferenceTest();

        GC.Collect();

        // make sure no dangling reference is left
        reference.IsAlive.Should().BeFalse();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RunWeakReferenceTest()
    {
        var o = new object();

        var engine = new Engine()
            .SetValue("o", o);

        var wrapper1 = (ObjectWrapper) engine.GetValue("o");
        var reference = new WeakReference(wrapper1);

        engine.GetValue("o").Should().BeSameAs(wrapper1);
        wrapper1.Target.Should().BeSameAs(o);

        // reset
        engine.Realm.GlobalObject.RemoveOwnProperty("o");
        return reference;
    }

    [Test]
    public void CanUseClrFunction()
    {
        var engine = new Engine();
        engine.SetValue("fn", new ClrFunction(engine, "fn", (_, args) => (JsValue) (args[0].AsInteger() + 1)));

        var result = engine.Evaluate("fn(1)");

        result.Should().Be(2);
    }

    [Test]
    public void ShouldAllowClrExceptionsThrough()
    {
        var engine = new Engine(opts => opts.Interop.ExceptionHandler = exc => false);
        engine.SetValue("fn", new ClrFunction(engine, "fn", (_, _) => throw new InvalidOperationException("This is a C# error")));
        const string Source = @"
function wrap() {
  fn();
}
wrap();
";

        Invoking(() => engine.Execute(Source)).Should().ThrowExactly<InvalidOperationException>();
    }

    [Test]
    public void ShouldConvertClrExceptionsToErrors()
    {
        var engine = new Engine(opts =>
        {
            opts.Interop.ExceptionHandler = exc => exc is InvalidOperationException;
            opts.Interop.ExposeDetailedExceptionMessages = true;
        });
        engine.SetValue("fn", new ClrFunction(engine, "fn", (_, _) => throw new InvalidOperationException("This is a C# error")));
        const string Source = @"
function wrap() {
  fn();
}
wrap();
";

        var exc = Invoking(() => engine.Execute(Source)).Should().ThrowExactly<JavaScriptException>().Which;
        "This is a C# error".Should().Be(exc.Message);
    }

    [Test]
    public void ShouldAllowCatchingConvertedClrExceptions()
    {
        var engine = new Engine(opts =>
        {
            opts.Interop.ExceptionHandler = exc => exc is InvalidOperationException;
            opts.Interop.ExposeDetailedExceptionMessages = true;
        });
        engine.SetValue("fn", new ClrFunction(engine, "fn", (_, _) => throw new InvalidOperationException("This is a C# error")));
        const string Source = @"
try {
  fn();
} catch (e) {
  throw new Error('Caught: ' + e.message);
}
";

        var exc = Invoking(() => engine.Execute(Source)).Should().ThrowExactly<JavaScriptException>().Which;
        "Caught: This is a C# error".Should().Be(exc.Message);
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

    [Test]
    public void ShouldCallEnumeratorDisposeOnNormalTermination()
    {
        var engine = new Engine();
        var baz = new Baz();
        engine.SetValue("baz", baz);
        const string Source = @"
for (let i of baz.Enumerator) {
}";
        engine.Execute(Source);
        baz.DisposeCalls.Should().Be(1);
    }

    [Test]
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
        baz.DisposeCalls.Should().Be(1);
    }

    [Test]
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
        baz.DisposeCalls.Should().Be(1);
    }

    public class PropertyTestClass
    {
        public object Value;
    }

    [Test]
    public void PropertiesOfJsObjectPassedToClrShouldBeReadable()
    {
        _engine.SetValue("MyClass", typeof(PropertyTestClass));
        RunTest(@"
                var obj = new MyClass();
                obj.Value = { foo: 'bar' };
                equal('bar', obj.Value.foo);
            ");
    }

    [Test]
    public void ShouldBeAbleToDeleteDictionaryEntries()
    {
        var engine = new Engine(options => { options.Strict = true; options.Interop.AllowWrite = true; });

        var dictionary = new Dictionary<string, int>
        {
            { "a", 1 },
            { "b", 2 }
        };

        engine.SetValue("data", dictionary);

        engine.Evaluate("Object.hasOwn(data, 'a')").AsBoolean().Should().BeTrue();
        engine.Evaluate("data['a'] === 1").AsBoolean().Should().BeTrue();

        engine.Evaluate("data['a'] = 42");
        engine.Evaluate("data['a'] === 42").AsBoolean().Should().BeTrue();

        dictionary["a"].Should().Be(42);

        engine.Execute("delete data['a'];");

        engine.Evaluate("Object.hasOwn(data, 'a')").AsBoolean().Should().BeFalse();
        engine.Evaluate("data['a'] === 42").AsBoolean().Should().BeFalse();

        dictionary.ContainsKey("a").Should().BeFalse();

        var engineNoWrite = new Engine(options => { options.Strict = true; options.Interop.AllowWrite = false; });

        dictionary = new Dictionary<string, int>
        {
            { "a", 1 },
            { "b", 2 }
        };

        engineNoWrite.SetValue("data", dictionary);

        var ex1 = Invoking(() => engineNoWrite.Evaluate("data['a'] = 42")).Should().ThrowExactly<JavaScriptException>().Which;
        ex1.Message.Should().Be("Cannot assign to read only property 'a' of Object");

        // no changes
        engineNoWrite.Evaluate("data['a'] === 1").AsBoolean().Should().BeTrue();

        var ex2 = Invoking(() => engineNoWrite.Execute("delete data['a'];")).Should().ThrowExactly<JavaScriptException>().Which;
        ex2.Message.Should().Be("Cannot delete property 'a' of Object");
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

    [Test]
    public void CanToStringObjectWithoutToPrimitiveSymbol()
    {
        var engine = new Engine();

        engine.SetValue("obj", new ClassWithIndexerAndProperty());
        engine.Evaluate("obj + ''").AsString().Should().Be("Jint.Tests.Runtime.InteropTests+ClassWithIndexerAndProperty");

        engine.SetValue("obj", new Company("name"));
        engine.Evaluate("obj + ''").AsString().Should().Be("Jint.Tests.Runtime.Domain.Company");
    }

    [Test]
    public void CanConstructOptionalRecordClass()
    {
        _engine.SetValue("Context", new RecordTestClassContext());
        _engine.Evaluate("Context.method({});").ToObject().Should().BeNull();
        _engine.Evaluate("Context.method({ value: 5 });").AsInteger().Should().Be(5);
    }

    [Test]
    public void CanPassDateTimeMinAndMaxViaInterop()
    {
        var engine = new Engine(cfg => cfg.Interop.AllowWrite = true);

        var dt = DateTime.UtcNow;
        engine.SetValue("capture", new Action<object>(o => dt = (DateTime) o));

        engine.SetValue("minDate", DateTime.MinValue);
        engine.Execute("capture(minDate);");
        dt.Should().Be(DateTime.MinValue);

        engine.SetValue("maxDate", DateTime.MaxValue);
        engine.Execute("capture(maxDate);");
        dt.Should().Be(DateTime.MaxValue);
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

    [Test]
    public void AccessingBaseTypeShouldBeEqualToAccessingDerivedType()
    {
        var engine = new Engine().SetValue("container", new Container());
        var res = engine.Evaluate("container.Child === container.Get()"); // These two should be the same object. But this PR makes `container.Get()` return a different object

        res.AsBoolean().Should().BeTrue();
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
        public IStringCollection GetStrings() => new Strings(["a", "b", "c"]);
    }

    [Test]
    public void AccessingInterfaceShouldContainExtendedInterfaces()
    {
        var engine = new Engine();
        engine.SetValue("Utils", new Utils());
        var result = engine.Evaluate("const strings = Utils.GetStrings(); strings.Count;").AsNumber();
        result.Should().Be(3);
    }

    [Test]
    public void IntegerIndexerIfPreferredOverStringIndexerWhenFound()
    {
        var engine = new Engine();
        engine.SetValue("Utils", new Utils());
        var result = engine.Evaluate("const strings = Utils.GetStrings(); strings[2];");
        result.Should().Be("c");
    }

    [Test]
    public void CanDestructureInteropTargetMethod()
    {
        var engine = new Engine();
        engine.SetValue("test", new Utils());
        var result = engine.Evaluate("const { getStrings } = test; getStrings().Count;");
        result.Should().Be(3);
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

    /// <summary>
    /// A custom IReadOnlyDictionary that does NOT implement IDictionary, to verify it's treated as dictionary-like, not array-like.
    /// </summary>
    private class ReadOnlyDictionary : IReadOnlyDictionary<string, object>
    {
        private readonly Dictionary<string, object> _dictionary;

        public ReadOnlyDictionary(Dictionary<string, object> dictionary)
        {
            _dictionary = dictionary;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _dictionary.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int Count => _dictionary.Count;
        public bool ContainsKey(string key) => _dictionary.ContainsKey(key);
        public bool TryGetValue(string key, out object value) => _dictionary.TryGetValue(key, out value!);
        public object this[string key] => _dictionary[key];
        public IEnumerable<string> Keys => _dictionary.Keys;
        public IEnumerable<object> Values => _dictionary.Values;
    }

    [Test]
    public void CanSelectShadowedPropertiesBasedOnReadableAndWritable()
    {
        var engine = new Engine();
        engine.SetValue("test", new ShadowingSetter
        {
            Metadata = null
        });

        engine.Evaluate("test.metadata['abc'] = 123");
        var result = engine.Evaluate("test.metadata['abc']");
        result.Should().Be("from-wrapper");
    }

    [Test]
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

        result.Should().HaveCount(4);
        result[0].Should().Be("KeyValuePair`2: [test, val]");
        result[1].Should().Be("String: test");
        result[2].Should().Be("KeyValuePair`2: [test, val]");
        result[3].Should().Be("String: test");
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

    [Test]
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

    [Test]
    public void PropertyKeysShouldReportMethods()
    {
        var engine = new Engine();

        engine.SetValue("clrInstance", new ClrMembersVisibilityTestClass());

        var val = engine.GetValue("clrInstance");
        var obj = val.AsObject();
        var props = obj.GetOwnProperties().Select(x => x.Key.ToString()).ToList();

        props.Should().BeEquivalentTo("Property", "Extras", "Field", "Method");
    }

    [Test]
    public void PropertyKeysShouldObeyMemberFilter()
    {
        var engine = new Engine(options =>
        {
            options.Interop.TypeResolver = new TypeResolver
            {
                MemberFilter = member => member.Name == "Extras"
            };
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

    [Test]
    public void ShouldSeeClrMethods2()
    {
        var engine = new Engine();

        engine.SetValue("clrInstance", new ClrMembersVisibilityTestClass2());

        var val = engine.GetValue("clrInstance");

        var obj = val.AsObject();
        var props = obj.GetOwnProperties().Select(x => x.Key.ToString()).ToList();

        props.Should().BeEquivalentTo("Get_A");
    }

    [Test]
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

    [Test]
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

    [Test]
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
        public Animal[] animals { get => [new Lion(), new Elephant()]; }
    }

    [Test]
    public void CanFindDerivedPropertiesFail() // Fails in 4.01 but success in 2.11
    {
        var engine = new Engine();
        engine.SetValue("zoo", new Zoo());
        var kingManeLength = engine.Evaluate("zoo.King.maneLength");
        kingManeLength.AsNumber().Should().Be(10);
    }

    [Test]
    public void CanFindDerivedPropertiesSucceed() // Similar case that continues to succeed
    {
        var engine = new Engine();
        engine.SetValue("zoo", new Zoo());
        var lionManeLength = engine.Evaluate("zoo.animals[0].maneLength");
        lionManeLength.AsNumber().Should().Be(10);
    }

    [Test]
    public void StaticFieldsShouldFollowJsSemantics()
    {
        _engine.Evaluate("Number.MAX_SAFE_INTEGER").AsNumber().Should().Be(NumberConstructor.MaxSafeInteger);
        _engine.Evaluate("new Number().MAX_SAFE_INTEGER").Should().BeUndefined();

        _engine.Execute("class MyJsClass { static MAX_SAFE_INTEGER = Number.MAX_SAFE_INTEGER; }");
        _engine.Evaluate("MyJsClass.MAX_SAFE_INTEGER").AsNumber().Should().Be(NumberConstructor.MaxSafeInteger);
        _engine.Evaluate("new MyJsClass().MAX_SAFE_INTEGER").Should().BeUndefined();

        _engine.SetValue("MyCsClass", typeof(MyClass));
        _engine.Evaluate("MyCsClass.MAX_SAFE_INTEGER").AsNumber().Should().Be(NumberConstructor.MaxSafeInteger);
        _engine.Evaluate("new MyCsClass().MAX_SAFE_INTEGER").Should().BeUndefined();
    }

    private class MyClass
    {
        public static JsNumber MAX_SAFE_INTEGER = new JsNumber(NumberConstructor.MaxSafeInteger);
    }

    [Test]
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

    [Test]
    public void MultipleInteropCallsShouldNotCacheFunctionEnvironment()
    {
        var engine = new Engine();
        engine.Evaluate(
            """
            function findIt(array, kind) {           
                let found = array.find(function sub(x) {
                    return x.kind == kind;
                });
                return found;
            };
            """);
        var findIt = (ScriptFunction) engine.GetValue("findIt");
        var interop = (Func<JsValue, JsValue[], JsValue>) findIt.ToObject()!;

        var values = new List<object>
        {
            new { kind = 'a' },
            new { kind = 'b' }
        };

        var found1 = interop(
            JsValue.Undefined,
            [
                JsValue.FromObject(engine, values),
                JsValue.FromObject(engine, "a")
            ])
            .ToObject();

        var found2 = interop(
            JsValue.Undefined,
            [
                JsValue.FromObject(engine, values),
                JsValue.FromObject(engine, "b")
            ])
            .ToObject();

        found1.Should().Be(values[0]);
        found2.Should().Be(values[1]);
    }

    [Test]
    public void CanCallBoundJavascriptFunctionFromDotnet()
    {
        var ticker = new Ticker();
        _engine.SetValue("ticker", ticker);

        var counter = (double) _engine.Evaluate("""
            function tickHandler() {
                counter++;
            }

            let counter = 0;
            const dummyThisObject = {};

            // bind javascript function to new this-object
            const tickerHandlerBinding = tickHandler.bind(dummyThisObject);
            
            // register it with .NET
            ticker.add_Ticked(tickerHandlerBinding);
            ticker.Tick();

            // unregister it
            ticker.remove_Ticked(tickerHandlerBinding);
            ticker.Tick();

            // return counter as result
            counter;
            """).ToObject();

        ticker.Tick();
        counter.Should().Be(1);
    }

    internal class Ticker
    {
        public event EventHandler Ticked;

        public void Tick()
        {
            Ticked?.Invoke(this, EventArgs.Empty);
        }
    }

    [Test]
    public void ShouldBeAbleToWriteLengthOfListLike()
    {
        var list = new List<string> { "a", "b", "c" };
        _engine.SetValue("list", list);

        _engine.Evaluate("list.length = 2;");
        list.Should().HaveCount(2);
        list[0].Should().Be("a");
        list[1].Should().Be("b");

        _engine.Evaluate("list.length = 0;");
        list.Should().BeEmpty();

        var act = () => _engine.Evaluate("list.length = -1;");
        act.Should().Throw<JavaScriptException>().WithMessage("Invalid array length");

        _engine.Evaluate("list.length = 1;");
        list.Should().HaveCount(1);
        list[0].Should().Be(null);
    }

    // GitHub issue #2173 - Type resolution should use runtime type when declared type has indexer
    private class WrapperWithIndexer
    {
        private readonly Dictionary<string, object> _properties = new();

        public object this[string key]
        {
            get => _properties.TryGetValue(key, out var value) ? value : null!;
            set => _properties[key] = value;
        }
    }

    private class GeometryWrapperWithProperty : WrapperWithIndexer
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    private class FeatureWithBaseTypeProperty
    {
        public WrapperWithIndexer Geometry { get; set; } = new GeometryWrapperWithProperty { X = 10.5, Y = 20.5 };
    }

    [Test]
    public void ShouldAccessDerivedTypePropertyWhenDeclaredTypeHasIndexer()
    {
        // GitHub issue #2173: When a property is declared with a base type that has an indexer,
        // but the actual runtime value is a derived type with a property, the property should be accessible
        var engine = new Engine();
        var feature = new FeatureWithBaseTypeProperty();
        engine.SetValue("feature", feature);

        // Should access the X property from GeometryWrapperWithProperty, not the indexer from WrapperWithIndexer
        var result = engine.Evaluate("feature.Geometry.x").AsNumber();
        result.Should().Be(10.5);

        var resultY = engine.Evaluate("feature.Geometry.y").AsNumber();
        resultY.Should().Be(20.5);
    }

    [Test]
    public void ShouldStillAccessIndexerWhenPropertyDoesNotExist()
    {
        // Ensure the indexer still works when the property doesn't exist on the derived type
        var engine = new Engine();
        var feature = new FeatureWithBaseTypeProperty();
        ((GeometryWrapperWithProperty) feature.Geometry)["customKey"] = "customValue";
        engine.SetValue("feature", feature);

        var result = engine.Evaluate("feature.Geometry.customKey");
        result.AsString().Should().Be("customValue");
    }

    [Test]
    public void ShouldSetDerivedTypePropertyWhenDeclaredTypeHasIndexer()
    {
        var engine = new Engine(cfg => cfg.Interop.AllowWrite = true);
        var feature = new FeatureWithBaseTypeProperty();
        engine.SetValue("feature", feature);

        engine.Evaluate("feature.Geometry.x = 99.9");

        var geometry = (GeometryWrapperWithProperty) feature.Geometry;
        geometry.X.Should().Be(99.9);
    }

    public class TypeWithListConstructor
    {
        public TypeWithListConstructor(List<string> items)
        {
            Items = items;
        }

        public List<string> Items { get; }
    }

    public class TypeWithCollectionParameters
    {
        public IList<string> IListItems { get; set; } = [];
        public ICollection<string> ICollectionItems { get; set; } = [];
        public IEnumerable<string> IEnumerableItems { get; set; } = [];
        public IReadOnlyList<string> IReadOnlyListItems { get; set; } = [];
        public IReadOnlyCollection<string> IReadOnlyCollectionItems { get; set; } = [];

        public void SetIListItems(IList<string> items) => IListItems = items;
        public void SetICollectionItems(ICollection<string> items) => ICollectionItems = items;
        public void SetIEnumerableItems(IEnumerable<string> items) => IEnumerableItems = items;
        public void SetIReadOnlyListItems(IReadOnlyList<string> items) => IReadOnlyListItems = items;
        public void SetIReadOnlyCollectionItems(IReadOnlyCollection<string> items) => IReadOnlyCollectionItems = items;
    }

    [Test]
    public void ShouldConvertJsArrayToListWhenPassedToConstructor()
    {
        var engine = new Engine(options => options.AllowClr(GetType().Assembly));
        engine.SetValue("TypeWithListConstructor", TypeReference.CreateTypeReference(engine, typeof(TypeWithListConstructor)));

        var result = engine.Evaluate("new TypeWithListConstructor(['a', 'b', 'c'])");
        var obj = result.ToObject() as TypeWithListConstructor;

        obj.Should().NotBeNull();
        obj.Items.Should().HaveCount(3);
        obj.Items[0].Should().Be("a");
        obj.Items[1].Should().Be("b");
        obj.Items[2].Should().Be("c");
    }

    [Test]
    public void ShouldConvertJsArrayToEmptyListWhenPassedToConstructor()
    {
        var engine = new Engine(options => options.AllowClr(GetType().Assembly));
        engine.SetValue("TypeWithListConstructor", TypeReference.CreateTypeReference(engine, typeof(TypeWithListConstructor)));

        var result = engine.Evaluate("new TypeWithListConstructor([])");
        var obj = result.ToObject() as TypeWithListConstructor;

        obj.Should().NotBeNull();
        obj.Items.Should().BeEmpty();
    }

    [Test]
    public void ShouldConvertJsArrayToGenericCollectionTypes()
    {
        var engine = new Engine(options => options.AllowClr(GetType().Assembly));
        var target = new TypeWithCollectionParameters();
        engine.SetValue("target", target);

        engine.Evaluate("target.SetIListItems(['a', 'b'])");
        target.IListItems.Should().HaveCount(2);
        target.IListItems[0].Should().Be("a");

        engine.Evaluate("target.SetICollectionItems(['c', 'd'])");
        target.ICollectionItems.Should().HaveCount(2);

        engine.Evaluate("target.SetIEnumerableItems(['e', 'f'])");
        target.IEnumerableItems.Count().Should().Be(2);

        engine.Evaluate("target.SetIReadOnlyListItems(['g', 'h'])");
        target.IReadOnlyListItems.Should().HaveCount(2);
        target.IReadOnlyListItems[0].Should().Be("g");

        engine.Evaluate("target.SetIReadOnlyCollectionItems(['i', 'j'])");
        target.IReadOnlyCollectionItems.Should().HaveCount(2);
    }
}
