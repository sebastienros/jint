using Jint.Native;
using Jint.Native.Global;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;
using Jint.Tests.TestClasses;

namespace Jint.Tests.Runtime;

public class PropertyDescriptorTests
{
    public class TestClass
    {
        public static readonly TestClass Instance = new TestClass();
        public string Method() => "Method";
        public class NestedType { }

        public readonly int fieldReadOnly = 8;
        public int field = 42;

        public string PropertyReadOnly => "PropertyReadOnly";
        public string PropertyWriteOnly { set { } }
        public string PropertyReadWrite { get; set; } = "PropertyReadWrite";

        public IndexedPropertyReadOnly<int, int> IndexerReadOnly { get; }
            = new((idx) => 42);
        public IndexedPropertyWriteOnly<int, int> IndexerWriteOnly { get; }
            = new((idx, v) => { });
        public IndexedProperty<int, int> IndexerReadWrite { get; }
            = new((idx) => 42, (idx, v) => { });
    }

    private readonly Engine _engine;

    private readonly bool checkType = true;

    public PropertyDescriptorTests()
    {
        _engine = new Engine(cfg => cfg.AllowClr(
                    typeof(TestClass).Assembly,
                    typeof(Console).Assembly,
                    typeof(File).Assembly))
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
                .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())))
                .SetValue("testClass", TestClass.Instance)
        ;
    }

    [Fact]
    public void PropertyDescriptorReadOnly()
    {
        var pd = _engine.Evaluate("""
            Object.defineProperty({}, 'value', {
              value: 42,
              writable: false
            })
        """).AsObject().GetOwnProperty("value");
        pd.IsAccessorDescriptor().Should().BeFalse();
        pd.IsDataDescriptor().Should().BeTrue();
        pd.Writable.Should().BeFalse();
        pd.Get.Should().BeNull();
        pd.Set.Should().BeNull();
    }

    [Fact]
    public void PropertyDescriptorReadWrite()
    {
        var pd = _engine.Evaluate("""
            Object.defineProperty({}, 'value', {
              value: 42,
              writable: true
            })
        """).AsObject().GetOwnProperty("value");
        pd.IsAccessorDescriptor().Should().BeFalse();
        pd.IsDataDescriptor().Should().BeTrue();
        pd.Writable.Should().BeTrue();
        pd.Get.Should().BeNull();
        pd.Set.Should().BeNull();
    }

    [Fact]
    public void UndefinedPropertyDescriptor()
    {
        var pd = PropertyDescriptor.Undefined;
        // PropertyDescriptor.UndefinedPropertyDescriptor is private
        //if (checkType) pd.Should().BeOfType<PropertyDescriptor.UndefinedPropertyDescriptor>();
        pd.IsAccessorDescriptor().Should().BeFalse();
        pd.IsDataDescriptor().Should().BeFalse();
    }

    [Fact]
    public void AllForbiddenDescriptor()
    {
        var pd = _engine.Evaluate("Object.getPrototypeOf('s')").AsObject().GetOwnProperty("length");
        if (checkType) pd.Should().BeOfType<PropertyDescriptor.AllForbiddenDescriptor>();
        pd.IsAccessorDescriptor().Should().BeFalse();
        pd.IsDataDescriptor().Should().BeTrue();
    }

    [Fact]
    public void FastSetPropertyIsVisibleOnBuiltinShapedHost()
    {
        // Math uses builtin-shape storage; a raw property store of a non-shape name joins the
        // hybrid side dictionary, which every read/enumeration surface must consult — without
        // that, the property silently doesn't exist.
        _engine.Evaluate("Math.floor(4.7)").AsNumber().Should().Be(4); // initialize the shaped host
        var math = _engine.Evaluate("Math").AsObject();
        math.FastSetProperty("custom", new PropertyDescriptor(42, PropertyFlag.ConfigurableEnumerableWritable));

        _engine.Evaluate("Math.custom").AsNumber().Should().Be(42);
        _engine.Evaluate("Object.getOwnPropertyNames(Math).includes('custom')").AsBoolean().Should().BeTrue();
        _engine.Evaluate("Math.floor(4.7)").AsNumber().Should().Be(4);
    }

    [Fact]
    public void FastSetPropertyBeforeLazyInitializationSurvivesOnDictionaryHost()
    {
        // a raw store before the host's first property access used to land in _properties and get
        // wiped when the read-triggered Initialize() replaced the bag; RegExp is a dictionary-path host
        var regExp = _engine.Evaluate("RegExp").AsObject();
        regExp.FastSetProperty("custom", new PropertyDescriptor(42, PropertyFlag.ConfigurableEnumerableWritable));

        _engine.Evaluate("RegExp.custom").AsNumber().Should().Be(42);
        _engine.Evaluate("typeof RegExp.escape === 'function'").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void FastSetPropertyBeforeLazyInitializationSurvivesOnBuiltinShapedHost()
    {
        // the builtin-shape sibling of the dictionary-host case above
        var math = _engine.Evaluate("Math").AsObject();
        math.FastSetProperty("custom", new PropertyDescriptor(42, PropertyFlag.ConfigurableEnumerableWritable));

        _engine.Evaluate("Math.custom").AsNumber().Should().Be(42);
        _engine.Evaluate("Object.getOwnPropertyNames(Math).includes('custom')").AsBoolean().Should().BeTrue();
        _engine.Evaluate("Math.floor(4.7)").AsNumber().Should().Be(4);
    }

    [Fact]
    public void SymbolSetPropertyBeforeLazyInitializationSurvives()
    {
        // symbol stores have the same pre-initialization hazard: Initialize() replaces _symbols
        var math = _engine.Evaluate("Math").AsObject();
        math.SetProperty(GlobalSymbolRegistry.Iterator, new PropertyDescriptor(42, PropertyFlag.ConfigurableEnumerableWritable));

        _engine.Evaluate("Math[Symbol.iterator]").AsNumber().Should().Be(42);
        _engine.Evaluate("Math[Symbol.toStringTag]").AsString().Should().Be("Math");
    }

    [Fact]
    public void LazyPropertyDescriptor()
    {
        // the shaped global materializes a targeted slot on demand (other slots stay untouched)
        var pd = _engine.Evaluate("globalThis").AsObject().GetOwnProperty("decodeURI");
        if (checkType)
        {
            pd.Should().BeOfType<PropertyDescriptor>();
        }
        pd.IsAccessorDescriptor().Should().BeFalse();
        pd.IsDataDescriptor().Should().BeTrue();

        // parseInt is a host-filled instance slot carrying a LazyPropertyDescriptor
        var lazy = _engine.Evaluate("globalThis").AsObject().GetOwnProperty("parseInt");
        if (checkType)
        {
            lazy.Should().BeOfType<LazyPropertyDescriptor<GlobalObject>>();
        }
        lazy.IsDataDescriptor().Should().BeTrue();

        // deleting a shape name deopts the global; untouched function slots must survive
        // as lazy wrappers instead of eagerly instantiating every built-in dispatcher
        _engine.Evaluate("delete globalThis.escape");
        var afterDeopt = _engine.Evaluate("globalThis").AsObject().GetOwnProperty("encodeURI");
        if (checkType)
        {
            afterDeopt.Should().BeOfType<LazyBuiltinSlotDescriptor>();
        }
        afterDeopt.IsDataDescriptor().Should().BeTrue();
        _engine.Evaluate("encodeURI('test')").AsString().Should().Be("test");
    }

    [Fact]
    public void ThrowerPropertyDescriptor()
    {
        var pd = _engine.Evaluate("Object.getPrototypeOf(function() {})").AsObject().GetOwnProperty("arguments");
        if (checkType) pd.Should().BeOfType<GetSetPropertyDescriptor.ThrowerPropertyDescriptor>();
        pd.IsAccessorDescriptor().Should().BeTrue();
        pd.IsDataDescriptor().Should().BeFalse();
    }

    [Fact]
    public void GetSetPropertyDescriptorGetOnly()
    {
        var pd = _engine.Evaluate("""
            Object.defineProperty({}, 'value', {
              get() {}
            })
        """).AsObject().GetOwnProperty("value");
        if (checkType) pd.Should().BeOfType<GetSetPropertyDescriptor>();
        pd.IsAccessorDescriptor().Should().BeTrue();
        pd.IsDataDescriptor().Should().BeFalse();
        pd.Get.Should().NotBeNull();
        pd.Set.Should().BeNull();
    }

    [Fact]
    public void GetSetPropertyDescriptorSetOnly()
    {
        var pd = _engine.Evaluate("""
            Object.defineProperty({}, 'value', {
              set() {}
            })
        """).AsObject().GetOwnProperty("value");
        if (checkType) pd.Should().BeOfType<GetSetPropertyDescriptor>();
        pd.IsAccessorDescriptor().Should().BeTrue();
        pd.IsDataDescriptor().Should().BeFalse();
        pd.Get.Should().BeNull();
        pd.Set.Should().NotBeNull();
    }

    [Fact]
    public void GetSetPropertyDescriptorGetSet()
    {
        var pd = _engine.Evaluate("""
            Object.defineProperty({}, 'value', {
              get() {},
              set() {}
            })
        """).AsObject().GetOwnProperty("value");
        if (checkType) pd.Should().BeOfType<GetSetPropertyDescriptor>();
        pd.IsAccessorDescriptor().Should().BeTrue();
        pd.IsDataDescriptor().Should().BeFalse();
        pd.Get.Should().NotBeNull();
        pd.Set.Should().NotBeNull();
    }

    [Fact]
    public void ClrAccessDescriptor()
    {
        JsValue ExtractClrAccessDescriptor(JsValue jsArugments)
        {
            var pd = ((JsArguments) jsArugments).ParameterMap.GetOwnProperty("0");
            return ObjectWrapper.Create(_engine, pd);
        }
        _engine.SetValue("ExtractClrAccessDescriptor", ExtractClrAccessDescriptor);
        var pdobj = _engine.Evaluate("""
            (function(a) {
              return ExtractClrAccessDescriptor(arguments);
            })(42)
        """);
        var pd = (PropertyDescriptor) ((ObjectWrapper) pdobj).Target;
        if (checkType) pd.Should().BeOfType<ClrAccessDescriptor>();
        pd.IsAccessorDescriptor().Should().BeTrue();
        pd.IsDataDescriptor().Should().BeFalse();
    }

    [Fact]
    public void PropertyDescriptorMethod()
    {
        var pdMethod = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass, 'Method')");
        CheckPropertyDescriptor(jsPropertyDescriptor: pdMethod, configurable: true, enumerable: false, writable: false, hasValue: true, hasGet: false, hasSet: false);

        var pd = _engine.Evaluate("testClass").AsObject().GetOwnProperty("Method");
        // use PropertyDescriptor to wrap method directly
        //if (checkType) pd.Should().BeOfType<PropertyDescriptor>();
        pd.IsAccessorDescriptor().Should().BeFalse();
        pd.IsDataDescriptor().Should().BeTrue();
    }

    [Fact]
    public void PropertyDescriptorNestedType()
    {
        var pdMethod = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass, 'NestedType')");
        CheckPropertyDescriptor(pdMethod, false, false, false, true, false, false);

        var pd = _engine.Evaluate("testClass").AsObject().GetOwnProperty("NestedType");
        // use PropertyDescriptor to wrap nested type directly
        //if (checkType) pd.Should().BeOfType<PropertyDescriptor>();
        pd.IsAccessorDescriptor().Should().BeFalse();
        pd.IsDataDescriptor().Should().BeTrue();
    }

    [Fact]
    public void ReflectionDescriptorFieldReadOnly()
    {
        var pdField = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass, 'fieldReadOnly')");
        CheckPropertyDescriptor(pdField, false, true, false, false, true, false);

        var pd = _engine.Evaluate("testClass").AsObject().GetOwnProperty("fieldReadOnly");
        if (checkType) pd.Should().BeOfType<ReflectionDescriptor>();
        pd.IsAccessorDescriptor().Should().BeTrue();
        pd.IsDataDescriptor().Should().BeFalse();
    }

    [Fact]
    public void ReflectionDescriptorField()
    {
        var pdField = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass, 'field')");
        CheckPropertyDescriptor(pdField, false, true, true, false, true, true);

        var pd = _engine.Evaluate("testClass").AsObject().GetOwnProperty("field");
        if (checkType) pd.Should().BeOfType<ReflectionDescriptor>();
        pd.IsAccessorDescriptor().Should().BeTrue();
        pd.IsDataDescriptor().Should().BeFalse();
    }

    [Fact]
    public void ReflectionDescriptorPropertyReadOnly()
    {
        var pdPropertyReadOnly = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass, 'PropertyReadOnly')");
        CheckPropertyDescriptor(pdPropertyReadOnly, false, true, false, false, true, false);

        var pd = _engine.Evaluate("testClass").AsObject().GetOwnProperty("PropertyReadOnly");
        if (checkType) pd.Should().BeOfType<ReflectionDescriptor>();
        pd.IsAccessorDescriptor().Should().BeTrue();
        pd.IsDataDescriptor().Should().BeFalse();
    }

    [Fact]
    public void ReflectionDescriptorPropertyWriteOnly()
    {
        var pdPropertyWriteOnly = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass, 'PropertyWriteOnly')");
        CheckPropertyDescriptor(pdPropertyWriteOnly, false, true, true, false, false, true);

        var pd = _engine.Evaluate("testClass").AsObject().GetOwnProperty("PropertyWriteOnly");
        if (checkType) pd.Should().BeOfType<ReflectionDescriptor>();
        pd.IsAccessorDescriptor().Should().BeTrue();
        pd.IsDataDescriptor().Should().BeFalse();
    }

    [Fact]
    public void ReflectionDescriptorPropertyReadWrite()
    {
        var pdPropertyReadWrite = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass, 'PropertyReadWrite')");
        CheckPropertyDescriptor(pdPropertyReadWrite, false, true, true, false, true, true);

        var pd = _engine.Evaluate("testClass").AsObject().GetOwnProperty("PropertyReadWrite");
        if (checkType) pd.Should().BeOfType<ReflectionDescriptor>();
        pd.IsAccessorDescriptor().Should().BeTrue();
        pd.IsDataDescriptor().Should().BeFalse();
    }

    [Fact]
    public void ReflectionDescriptorIndexerReadOnly()
    {
        var pdIndexerReadOnly = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass.IndexerReadOnly, '1')");
        CheckPropertyDescriptor(pdIndexerReadOnly, false, true, false, false, true, false);

        var pd1 = _engine.Evaluate("testClass.IndexerReadOnly");
        var pd = pd1.AsObject().GetOwnProperty("1");
        if (checkType) pd.Should().BeOfType<ReflectionDescriptor>();
        pd.IsAccessorDescriptor().Should().BeTrue();
        pd.IsDataDescriptor().Should().BeFalse();
    }

    [Fact]
    public void ReflectionDescriptorIndexerWriteOnly()
    {
        var pdIndexerWriteOnly = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass.IndexerWriteOnly, '1')");
        CheckPropertyDescriptor(pdIndexerWriteOnly, false, true, true, false, false, true);

        var pd = _engine.Evaluate("testClass.IndexerWriteOnly").AsObject().GetOwnProperty("1");
        if (checkType) pd.Should().BeOfType<ReflectionDescriptor>();
        pd.IsAccessorDescriptor().Should().BeTrue();
        pd.IsDataDescriptor().Should().BeFalse();
    }

    [Fact]
    public void ReflectionDescriptorIndexerReadWrite()
    {
        var pdIndexerReadWrite = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass.IndexerReadWrite, 1)");
        CheckPropertyDescriptor(pdIndexerReadWrite, false, true, true, false, true, true);

        var pd = _engine.Evaluate("testClass.IndexerReadWrite").AsObject().GetOwnProperty("1");
        if (checkType) pd.Should().BeOfType<ReflectionDescriptor>();
        pd.IsAccessorDescriptor().Should().BeTrue();
        pd.IsDataDescriptor().Should().BeFalse();
    }

    private void CheckPropertyDescriptor(
        JsValue jsPropertyDescriptor,
        bool configurable,
        bool enumerable,
        bool writable,
        bool hasValue,
        bool hasGet,
        bool hasSet
    )
    {
        var pd = jsPropertyDescriptor.AsObject();

        pd["configurable"].AsBoolean().Should().Be(configurable);
        pd["enumerable"].AsBoolean().Should().Be(enumerable);
        if (writable)
        {
            var writableActual = pd["writable"];
            if (!writableActual.IsUndefined())
            {
                writableActual.AsBoolean().Should().BeTrue();
            }
        }

        (!pd["value"].IsUndefined()).Should().Be(hasValue);
        (!pd["get"].IsUndefined()).Should().Be(hasGet);
        (!pd["set"].IsUndefined()).Should().Be(hasSet);
    }

    [Fact]
    public void DefinePropertyFromAccesorToData()
    {
        var pd = _engine.Evaluate("""
            let o = {};
            Object.defineProperty(o, 'foo', {
              get() { return 1; },
              configurable: true
            });
            Object.defineProperty(o, 'foo', {
              value: 101
            });
            return Object.getOwnPropertyDescriptor(o, 'foo');
        """);
        pd.AsObject().Get("value").AsInteger().Should().Be(101);
        CheckPropertyDescriptor(pd, true, false, false, true, false, false);
    }
}
