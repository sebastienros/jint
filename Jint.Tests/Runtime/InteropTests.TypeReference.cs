using Jint.Native;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Tests.Runtime.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Jint.Tests.Runtime;

public partial class InteropTests
{
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
    public void JavaScriptClassCanExtendClrType()
    {
        _engine.SetValue("TestClass", TypeReference.CreateTypeReference<TestClass>(_engine));

        _engine.Execute("class ExtendedType extends TestClass { constructor() { super(); this.a = 1; } get aProp() { return 'A'; } }");
        _engine.Execute("class MyExtendedType extends ExtendedType { constructor() { super(); this.b = 2; } get bProp() { return 'B'; } }");
        _engine.Evaluate("let obj = new MyExtendedType();");

        _engine.Evaluate("obj.setString('Hello World!');");

        Assert.Equal("Hello World!", _engine.Evaluate("obj.string"));
        Assert.Equal(1, _engine.Evaluate("obj.a"));
        Assert.Equal(2, _engine.Evaluate("obj.b"));

        Assert.Equal("A", _engine.Evaluate("obj.aProp"));
        Assert.Equal("B", _engine.Evaluate("obj.bProp"));

        // TODO we should have a special prototype based on wrapped type so we could differentiate between own and type properties
        // Assert.Equal("[\"a\"]", _engine.Evaluate("JSON.stringify(Object.getOwnPropertyNames(new ExtendedType()))"));
        // Assert.Equal("[\"a\",\"b\"]", _engine.Evaluate("JSON.stringify(Object.getOwnPropertyNames(new MyExtendedType()))"));
    }

    [Fact]
    public void ShouldAllowMethodsOnClrExtendedTypes()
    {
        _engine.SetValue("ClrBaseType", TypeReference.CreateTypeReference<TestClass>(_engine));
        _engine.Evaluate(@"
                class JsBaseType {}
                class ExtendsFromJs extends JsBaseType {
                    constructor() {
                        super();
                        this.a = 1;
                    }

                    getA() {
                        return this.a;
                    }
                }

                class ExtendsFromClr extends ClrBaseType {
                    constructor() {
                        super();
                        this.a = 1;
                    }

                    getA() {
                        return this.a;
                    }
                }
            ");

        var extendsFromJs = _engine.Construct("ExtendsFromJs");
        Assert.Equal(1, _engine.Evaluate("new ExtendsFromJs().getA();"));
        Assert.NotEqual(JsValue.Undefined, extendsFromJs.Get("getA"));

        var extendsFromClr = _engine.Construct("ExtendsFromClr");
        Assert.Equal(1, _engine.Evaluate("new ExtendsFromClr().getA();"));
        Assert.NotEqual(JsValue.Undefined, extendsFromClr.Get("getA"));
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
    public void IntegerEnumResolutionShouldWork()
    {
        var engine = new Engine(options => options.AllowClr(GetType().Assembly));
        engine.SetValue("a", new OverLoading());
        engine.SetValue("E", TypeReference.CreateTypeReference(engine, typeof(IntegerEnum)));
        Assert.Equal("integer-enum", engine.Evaluate("a.testFunc(E.a);").AsString());
    }

    [Fact]
    public void UnsignedIntegerEnumResolutionShouldWork()
    {
        var engine = new Engine(options => options.AllowClr(GetType().Assembly));
        engine.SetValue("E", TypeReference.CreateTypeReference(engine, typeof(UintEnum)));
        Assert.Equal(1, engine.Evaluate("E.b;").AsNumber());
    }


    [Fact]
    public void ExceptionFromConstructorShouldPropagate()
    {
        _engine.SetValue("Class", TypeReference.CreateTypeReference(_engine, typeof(MemberExceptionTest)));
        var ex = Assert.Throws<InvalidOperationException>(() => _engine.Evaluate("new Class(true);"));
        Assert.Equal("thrown as requested", ex.Message);
    }


    [Fact]
    public void ShouldScoreDoubleToDoubleParameterMatchHigherThanDoubleToFloat()
    {
        var engine = new Engine();
        var mathTypeReference = TypeReference.CreateTypeReference(engine, typeof(Math));

        engine.SetValue("Math2", mathTypeReference);
        var result = engine.Evaluate("Math2.Max(5.37, 5.56)").AsNumber();

        Assert.Equal(5.56d, result);
    }

    [Fact]
    public void TypeReferenceShouldGetIntermediaryPrototype()
    {
        var engine = new Engine();
        engine.SetValue("Person", TypeReference.CreateTypeReference<Person>(engine));

        var calls = new List<string>();
        engine.SetValue("log", new Action<string>(calls.Add));

        engine.Execute("Person.prototype.__defineGetter__('bar', function() { log('called'); return 5 });");
        engine.Execute("var instance = new Person();");
        engine.Execute("log(instance.bar)");

        engine.Execute("var z = {};");
        engine.Execute("z['bar'] = 20;");
        engine.Execute("log(z['bar']);");

        Assert.Equal("called#5#20", string.Join("#", calls));
    }

    [Fact]
    public void CanConfigureCustomInstanceCreator()
    {
        var collection = new ServiceCollection();
        collection.AddTransient<Injectable>();
        collection.AddTransient<Dependency>();
        var serviceProvider = collection.BuildServiceProvider();

        var engine = new Engine(options =>
        {
            options.Interop.CreateTypeReferenceObject = (e, type, arguments) =>
            {
                var instance = serviceProvider.GetRequiredService(type);
                return instance;
            };
        });
        engine.SetValue("Injectable", TypeReference.CreateTypeReference<Injectable>(engine));
        Assert.Equal("Hello world", engine.Evaluate("new Injectable(123, 'abc').getInjectedValue();"));
    }

    [Fact]
    public void ToStringTagShouldReflectType()
    {
        var reference = TypeReference.CreateTypeReference<Dependency>(_engine);

        _engine.SetValue("MyClass", reference);
        _engine.Execute("var c = new MyClass();");

        Assert.Equal("[object Dependency]", _engine.Evaluate("Object.prototype.toString.call(c);"));

        // engine uses registered type reference
        _engine.SetValue("c2", new Dependency());
        Assert.Equal("[object Dependency]", _engine.Evaluate("Object.prototype.toString.call(c2);"));
    }

    private class Injectable
    {
        private readonly Dependency _dependency;

        public Injectable(Dependency dependency)
        {
            _dependency = dependency;
        }

        public string GetInjectedValue() => _dependency.Value;
    }

    private class Dependency
    {
        public string Value => "Hello world";
    }
}
