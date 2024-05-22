using Jint.Native;
using Jint.Native.Global;
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
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("equal", new Action<object, object>(Assert.Equal))
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
        Assert.Equal(false, pd.IsAccessorDescriptor());
        Assert.Equal(true, pd.IsDataDescriptor());
        Assert.Equal(false, pd.Writable);
        Assert.Null(pd.Get);
        Assert.Null(pd.Set);
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
        Assert.Equal(false, pd.IsAccessorDescriptor());
        Assert.Equal(true, pd.IsDataDescriptor());
        Assert.Equal(true, pd.Writable);
        Assert.Null(pd.Get);
        Assert.Null(pd.Set);
    }

    [Fact]
    public void UndefinedPropertyDescriptor()
    {
        var pd = PropertyDescriptor.Undefined;
        // PropertyDescriptor.UndefinedPropertyDescriptor is private
        //if (checkType) Assert.IsType<PropertyDescriptor.UndefinedPropertyDescriptor>(pd);
        Assert.Equal(false, pd.IsAccessorDescriptor());
        Assert.Equal(false, pd.IsDataDescriptor());
    }

    [Fact]
    public void AllForbiddenDescriptor()
    {
        var pd = _engine.Evaluate("Object.getPrototypeOf('s')").AsObject().GetOwnProperty("length");
        if (checkType) Assert.IsType<PropertyDescriptor.AllForbiddenDescriptor>(pd);
        Assert.Equal(false, pd.IsAccessorDescriptor());
        Assert.Equal(true, pd.IsDataDescriptor());
    }

    [Fact]
    public void LazyPropertyDescriptor()
    {
        var pd = _engine.Evaluate("globalThis").AsObject().GetOwnProperty("decodeURI");
        if (checkType)
        {
            Assert.IsType<LazyPropertyDescriptor<GlobalObject>>(pd);
        }
        Assert.Equal(false, pd.IsAccessorDescriptor());
        Assert.Equal(true, pd.IsDataDescriptor());
    }

    [Fact]
    public void ThrowerPropertyDescriptor()
    {
        var pd = _engine.Evaluate("Object.getPrototypeOf(function() {})").AsObject().GetOwnProperty("arguments");
        if (checkType) Assert.IsType<GetSetPropertyDescriptor.ThrowerPropertyDescriptor>(pd);
        Assert.Equal(true, pd.IsAccessorDescriptor());
        Assert.Equal(false, pd.IsDataDescriptor());
    }

    [Fact]
    public void GetSetPropertyDescriptorGetOnly()
    {
        var pd = _engine.Evaluate("""
            Object.defineProperty({}, 'value', {
              get() {}
            })
        """).AsObject().GetOwnProperty("value");
        if (checkType) Assert.IsType<GetSetPropertyDescriptor>(pd);
        Assert.Equal(true, pd.IsAccessorDescriptor());
        Assert.Equal(false, pd.IsDataDescriptor());
        Assert.NotNull(pd.Get);
        Assert.Null(pd.Set);
    }

    [Fact]
    public void GetSetPropertyDescriptorSetOnly()
    {
        var pd = _engine.Evaluate("""
            Object.defineProperty({}, 'value', {
              set() {}
            })
        """).AsObject().GetOwnProperty("value");
        if (checkType) Assert.IsType<GetSetPropertyDescriptor>(pd);
        Assert.Equal(true, pd.IsAccessorDescriptor());
        Assert.Equal(false, pd.IsDataDescriptor());
        Assert.Null(pd.Get);
        Assert.NotNull(pd.Set);
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
        if (checkType) Assert.IsType<GetSetPropertyDescriptor>(pd);
        Assert.Equal(true, pd.IsAccessorDescriptor());
        Assert.Equal(false, pd.IsDataDescriptor());
        Assert.NotNull(pd.Get);
        Assert.NotNull(pd.Set);
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
        if (checkType) Assert.IsType<ClrAccessDescriptor>(pd);
        Assert.Equal(true, pd.IsAccessorDescriptor());
        Assert.Equal(false, pd.IsDataDescriptor());
    }

    [Fact]
    public void PropertyDescriptorMethod()
    {
        var pdMethod = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass, 'Method')");
        CheckPropertyDescriptor(pdMethod, false, false, false, true, false, false);

        var pd = _engine.Evaluate("testClass").AsObject().GetOwnProperty("Method");
        // use PropertyDescriptor to wrap method directly
        //if (checkType) Assert.IsType<PropertyDescriptor>(pd);
        Assert.Equal(false, pd.IsAccessorDescriptor());
        Assert.Equal(true, pd.IsDataDescriptor());
    }

    [Fact]
    public void PropertyDescriptorNestedType()
    {
        var pdMethod = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass, 'NestedType')");
        CheckPropertyDescriptor(pdMethod, false, false, false, true, false, false);

        var pd = _engine.Evaluate("testClass").AsObject().GetOwnProperty("NestedType");
        // use PropertyDescriptor to wrap nested type directly
        //if (checkType) Assert.IsType<PropertyDescriptor>(pd);
        Assert.Equal(false, pd.IsAccessorDescriptor());
        Assert.Equal(true, pd.IsDataDescriptor());
    }

    [Fact]
    public void ReflectionDescriptorFieldReadOnly()
    {
        var pdField = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass, 'fieldReadOnly')");
        CheckPropertyDescriptor(pdField, false, true, false, false, true, false);

        var pd = _engine.Evaluate("testClass").AsObject().GetOwnProperty("fieldReadOnly");
        if (checkType) Assert.IsType<ReflectionDescriptor>(pd);
        Assert.Equal(true, pd.IsAccessorDescriptor());
        Assert.Equal(false, pd.IsDataDescriptor());
    }

    [Fact]
    public void ReflectionDescriptorField()
    {
        var pdField = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass, 'field')");
        CheckPropertyDescriptor(pdField, false, true, true, false, true, true);

        var pd = _engine.Evaluate("testClass").AsObject().GetOwnProperty("field");
        if (checkType) Assert.IsType<ReflectionDescriptor>(pd);
        Assert.Equal(true, pd.IsAccessorDescriptor());
        Assert.Equal(false, pd.IsDataDescriptor());
    }

    [Fact]
    public void ReflectionDescriptorPropertyReadOnly()
    {
        var pdPropertyReadOnly = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass, 'PropertyReadOnly')");
        CheckPropertyDescriptor(pdPropertyReadOnly, false, true, false, false, true, false);

        var pd = _engine.Evaluate("testClass").AsObject().GetOwnProperty("PropertyReadOnly");
        if (checkType) Assert.IsType<ReflectionDescriptor>(pd);
        Assert.Equal(true, pd.IsAccessorDescriptor());
        Assert.Equal(false, pd.IsDataDescriptor());
    }

    [Fact]
    public void ReflectionDescriptorPropertyWriteOnly()
    {
        var pdPropertyWriteOnly = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass, 'PropertyWriteOnly')");
        CheckPropertyDescriptor(pdPropertyWriteOnly, false, true, true, false, false, true);

        var pd = _engine.Evaluate("testClass").AsObject().GetOwnProperty("PropertyWriteOnly");
        if (checkType) Assert.IsType<ReflectionDescriptor>(pd);
        Assert.Equal(true, pd.IsAccessorDescriptor());
        Assert.Equal(false, pd.IsDataDescriptor());
    }

    [Fact]
    public void ReflectionDescriptorPropertyReadWrite()
    {
        var pdPropertyReadWrite = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass, 'PropertyReadWrite')");
        CheckPropertyDescriptor(pdPropertyReadWrite, false, true, true, false, true, true);

        var pd = _engine.Evaluate("testClass").AsObject().GetOwnProperty("PropertyReadWrite");
        if (checkType) Assert.IsType<ReflectionDescriptor>(pd);
        Assert.Equal(true, pd.IsAccessorDescriptor());
        Assert.Equal(false, pd.IsDataDescriptor());
    }

    [Fact]
    public void ReflectionDescriptorIndexerReadOnly()
    {
        var pdIndexerReadOnly = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass.IndexerReadOnly, '1')");
        CheckPropertyDescriptor(pdIndexerReadOnly, false, true, false, false, true, false);

        var pd1 = _engine.Evaluate("testClass.IndexerReadOnly");
        var pd = pd1.AsObject().GetOwnProperty("1");
        if (checkType) Assert.IsType<ReflectionDescriptor>(pd);
        Assert.Equal(true, pd.IsAccessorDescriptor());
        Assert.Equal(false, pd.IsDataDescriptor());
    }

    [Fact]
    public void ReflectionDescriptorIndexerWriteOnly()
    {
        var pdIndexerWriteOnly = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass.IndexerWriteOnly, '1')");
        CheckPropertyDescriptor(pdIndexerWriteOnly, false, true, true, false, false, true);

        var pd = _engine.Evaluate("testClass.IndexerWriteOnly").AsObject().GetOwnProperty("1");
        if (checkType) Assert.IsType<ReflectionDescriptor>(pd);
        Assert.Equal(true, pd.IsAccessorDescriptor());
        Assert.Equal(false, pd.IsDataDescriptor());
    }

    [Fact]
    public void ReflectionDescriptorIndexerReadWrite()
    {
        var pdIndexerReadWrite = _engine.Evaluate("Object.getOwnPropertyDescriptor(testClass.IndexerReadWrite, 1)");
        CheckPropertyDescriptor(pdIndexerReadWrite, false, true, true, false, true, true);

        var pd = _engine.Evaluate("testClass.IndexerReadWrite").AsObject().GetOwnProperty("1");
        if (checkType) Assert.IsType<ReflectionDescriptor>(pd);
        Assert.Equal(true, pd.IsAccessorDescriptor());
        Assert.Equal(false, pd.IsDataDescriptor());
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

        Assert.Equal(configurable, pd["configurable"].AsBoolean());
        Assert.Equal(enumerable, pd["enumerable"].AsBoolean());
        if (writable)
        {
            var writableActual = pd["writable"];
            if (!writableActual.IsUndefined())
            {
                Assert.True(writableActual.AsBoolean());
            }
        }

        Assert.Equal(hasValue, !pd["value"].IsUndefined());
        Assert.Equal(hasGet, !pd["get"].IsUndefined());
        Assert.Equal(hasSet, !pd["set"].IsUndefined());
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
        Assert.Equal(101, pd.AsObject().Get("value").AsInteger());
        CheckPropertyDescriptor(pd, true, false, false, true, false, false);
    }
}
