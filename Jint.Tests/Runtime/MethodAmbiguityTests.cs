using Jint.Native;
using System.Dynamic;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public class MethodAmbiguityTests : IDisposable
{
    private readonly Engine _engine;

    public MethodAmbiguityTests()
    {
        _engine = new Engine(cfg => cfg
                    .AllowOperatorOverloading())
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("throws", new Func<Action, Exception>(Assert.Throws<Exception>))
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("assertFalse", new Action<bool>(Assert.False))
                .SetValue("equal", new Action<object, object>(Assert.Equal))
                .SetValue("TestClass", typeof(TestClass))
                .SetValue("ChildTestClass", typeof(ChildTestClass))
            ;
    }

    void IDisposable.Dispose()
    {
    }

    private void RunTest(string source)
    {
        _engine.Execute(source);
    }

    public class TestClass
    {
        public string this[int index] => "int";
        public string this[string index] => "string";
        public int TestMethod(double a, string b, double c) => 0;
        public int TestMethod(double a, double b, double c) => 1;
        public int TestMethod(TestClass a, string b, double c) => 2;
        public int TestMethod(TestClass a, TestClass b, double c) => 3;
        public int TestMethod(TestClass a, TestClass b, TestClass c) => 4;
        public int TestMethod(TestClass a, double b, string c) => 5;
        public int TestMethod(ChildTestClass a, double b, string c) => 6;
        public int TestMethod(ChildTestClass a, string b, JsValue c) => 7;

        public static implicit operator TestClass(double i) => new TestClass();
        public static implicit operator double(TestClass tc) => 0;
        public static explicit operator string(TestClass tc) => "";
    }

    public class ChildTestClass : TestClass { }

    [Fact]
    public void BestMatchingMethodShouldBeCalled()
    {
        RunTest(@"
                var tc = new TestClass();
                var cc = new ChildTestClass();

                equal(0, tc.TestMethod(0, '', 0));
                equal(1, tc.TestMethod(0, 0, 0));
                equal(2, tc.TestMethod(tc, '', 0));
                equal(3, tc.TestMethod(tc, tc, 0));
                equal(4, tc.TestMethod(tc, tc, tc));
                equal(5, tc.TestMethod(tc, tc, ''));
                equal(5, tc.TestMethod(0, 0, ''));

                equal(6, tc.TestMethod(cc, 0, ''));
                equal(1, tc.TestMethod(cc, 0, 0));
                equal(6, tc.TestMethod(cc, cc, ''));
                equal(6, tc.TestMethod(cc, 0, tc));
                equal(7, tc.TestMethod(cc, '', {}));
            ");
    }

    [Fact]
    public void IndexerCachesMethodsCorrectly()
    {
        RunTest(@"
                var tc = new TestClass();
                equal('string:int', tc['Whistler'] + ':' + tc[10]);
                equal('int:string', tc[10] + ':' + tc['Whistler']);
            ");
    }

    [Fact]
    public void ShouldFavorOtherOverloadsOverObjectParameter()
    {
        var engine = new Engine(cfg => cfg.AllowOperatorOverloading());
        engine.SetValue("Class1", TypeReference.CreateTypeReference<Class1>(engine));
        engine.SetValue("Class2", TypeReference.CreateTypeReference<Class2>(engine));

        Assert.Equal("Class1.Double[]", engine.Evaluate("Class1.Print([ 1, 2 ]);"));
        Assert.Equal("Class1.ExpandoObject", engine.Evaluate("Class1.Print({ x: 1, y: 2 });"));
        Assert.Equal("Class1.Int32", engine.Evaluate("Class1.Print(5);"));
        Assert.Equal("Class2.Double[]", engine.Evaluate("Class2.Print([ 1, 2 ]); "));
        Assert.Equal("Class2.ExpandoObject", engine.Evaluate("Class2.Print({ x: 1, y: 2 });"));
        Assert.Equal("Class2.Int32", engine.Evaluate("Class2.Print(5);"));
        Assert.Equal("Class2.Object", engine.Evaluate("Class2.Print(() => '');"));
    }

    [Fact]
    public void ShouldMatchCorrectConstructors()
    {
        Engine engine = new Engine();
        engine.SetValue("MyClass", TypeReference.CreateTypeReference(engine, typeof(Class3)));

        engine.Execute(@"
                const a = new MyClass();
                a.Print(1); // Works
                a.Print([1, 2]); // Works

                const b = new MyClass(1); // Works
                const c = new MyClass([1, 2]); // Throws 'an object must implement IConvertible' exception
			");
    }

    private struct Class1
    {
        public static string Print(ExpandoObject eo) => nameof(Class1) + "." + nameof(ExpandoObject);
        public static string Print(double[] a) => nameof(Class1) + "." + nameof(Double) + "[]";
        public static string Print(int i) => nameof(Class1) + "." + nameof(Int32);
    }

    private struct Class2
    {
        public static string Print(ExpandoObject eo) => nameof(Class2) + "." + nameof(ExpandoObject);
        public static string Print(double[] a) => nameof(Class2) + "." + nameof(Double) + "[]";
        public static string Print(int i) => nameof(Class2) + "." + nameof(Int32);
        public static string Print(object o) => nameof(Class2) + "." + nameof(Object);
    }

    public class Class3
    {
        public Class3() { }

        public Class3(int a) => Console.WriteLine("Class1(int a): " + a);

        public Class3(object a) => Console.WriteLine("Class1(object a): " + a);

        public void Print(int a) => Console.WriteLine("Print(int a): " + a);

        public void Print(object a) => Console.WriteLine("Print(object a): " + a);
    }
}