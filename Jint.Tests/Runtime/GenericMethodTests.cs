namespace Jint.Tests.Runtime;

public class GenericMethodTests
{
    [Fact]
    public void TestGeneric()
    {
        var engine = new Engine();
        engine.SetValue("TestGenericBaseClass", typeof(TestGenericBaseClass<>));
        engine.SetValue("TestGenericClass", typeof(TestGenericClass));

        engine.Execute(@"
                var testGeneric = new TestGenericClass();
                testGeneric.Bar('testing testing 1 2 3');
                testGeneric.Foo('hello world');
                testGeneric.Add('blah');
            ");

        Assert.Equal(true, TestGenericClass.BarInvoked);
        Assert.Equal(true, TestGenericClass.FooInvoked);
    }

    [Fact]
    public void TestGeneric2()
    {
        var engine = new Engine();
        var testGenericObj = new TestGenericClass();
        engine.SetValue("testGenericObj", testGenericObj);

        engine.Execute(@"
                testGenericObj.Bar('testing testing 1 2 3');
                testGenericObj.Foo('hello world');
                testGenericObj.Add('blah');
            ");

        Assert.Equal(1, testGenericObj.Count);
    }

    [Fact]
    public void TestFancyGenericPass()
    {
        var engine = new Engine();
        var testGenericObj = new TestGenericClass();
        engine.SetValue("testGenericObj", testGenericObj);

        engine.Execute(@"
                testGenericObj.Fancy('test', 42, 'foo');
            ");

        Assert.Equal(true, testGenericObj.FancyInvoked);
    }

    [Fact]
    public void TestFancyGenericFail()
    {
        var engine = new Engine();
        var testGenericObj = new TestGenericClass();
        engine.SetValue("testGenericObj", testGenericObj);

        var argException = Assert.Throws<Jint.Runtime.JavaScriptException>(() =>
        {
            engine.Execute(@"
                    testGenericObj.Fancy('test', 'foo', 42);
                ");
        });

        Assert.Equal("No public methods with the specified arguments were found.", argException.Message);
    }

    // TPC: TODO: tldr; typescript transpiled to javascript does not include the types in the constructors - JINT should allow you to use generics without specifying type
    // The following doesn't work because JINT currently requires generic classes to be instantiated in a way that doesn't comply with typescript transpile of javascript
    // i.e. we shouldn't have to specify the type of the generic class when we instantiate it.  Since typescript takes the following:
    // const someGeneric = new Foo.Bar.MeGeneric<string>()
    // and it becomes the following javascript (thru transpile):
    // const someGeneric = new Foo.Bar.MeGeneric();
    // we _may_ be able to address this by simply instantiating generic types using System.Object for the generic arguments
    // This test currently generates the following error:
    // No public methods with the specified arguments were found.
    [Fact(Skip = "not supported yet")]
    public void TestGenericClassDeriveFromGenericInterface()
    {
        var engine = new Engine(cfg => cfg.AllowClr(typeof(OpenGenericTest<>).Assembly));

        engine.SetValue("ClosedGenericTest", typeof(ClosedGenericTest));
        engine.SetValue("OpenGenericTest", typeof(OpenGenericTest<>));
        engine.SetValue("log", new System.Action<object>(System.Console.WriteLine));
        engine.Execute(@"
            const closedGenericTest = new ClosedGenericTest();
            closedGenericTest.Foo(42);
            const temp = new OpenGenericTest(System.String);
        ");
    }

    [Fact]
    public void TestGenericMethodUsingCovarianceOrContraviance()
    {
        var engine = new Engine(cfg => cfg.AllowClr(typeof(PlayerChoiceManager).Assembly));

        engine.SetValue("PlayerChoiceManager", typeof(PlayerChoiceManager));
        engine.SetValue("TestSelectorWithoutProps", typeof(TestSelectorWithoutProps));

        engine.SetValue("TestGenericClass", typeof(TestGenericClass));

        // TPC: the following is the C# equivalent
        /*
        PlayerChoiceManager playerChoiceManager = new PlayerChoiceManager();
        var testSelectorWithoutProps = new TestSelectorWithoutProps();
        var result = playerChoiceManager.Store.Select(testSelectorWithoutProps);
        */
        engine.Execute(@"
            const playerChoiceManager = new PlayerChoiceManager();
            const testSelectorWithoutProps = new TestSelectorWithoutProps();
            const result = playerChoiceManager.Store.Select(testSelectorWithoutProps);
        ");

        Assert.Equal(true, ReduxStore<PlayerChoiceState>.SelectInvoked);
    }


    public interface IGenericTest<T>
    {
        void Foo<U>(U u);
    }

    public class OpenGenericTest<T> : IGenericTest<T>
    {
        public void Foo<U>(U u)
        {
            Console.WriteLine("OpenGenericTest: u: " + u);
        }
    }

    public class ClosedGenericTest : IGenericTest<string>
    {
        public void Foo<U>(U u)
        {
            Console.WriteLine("ClosedGenericTest: u: " + u);
        }
    }

    public class TestGenericBaseClass<T>
    {
        private readonly System.Collections.Generic.List<T> _list = new System.Collections.Generic.List<T>();

        public int Count
        {
            get { return _list.Count; }
        }

        public void Add(T t)
        {
            _list.Add(t);
        }
    }

    public class TestGenericClass : TestGenericBaseClass<string>
    {
        public static bool BarInvoked { get; private set; }

        public static bool FooInvoked { get; private set; }

        public bool FancyInvoked { get; private set; }

        public TestGenericClass()
        {
            BarInvoked = false;
            FooInvoked = false;
            FancyInvoked = false;
        }

        public void Bar(string text)
        {
            Console.WriteLine("TestGenericClass: Bar: text: " + text);
            BarInvoked = true;
        }

        public void Foo<T>(T t)
        {
            Console.WriteLine("TestGenericClass: Foo: t: " + t);
            FooInvoked = true;
        }

        public void Fancy<T, U>(T t1, U u, T t2)
        {
            Console.WriteLine("TestGenericClass: FancyInvoked: t1: " + t1 + "u: " + u + " t2: " + t2);
            FancyInvoked = true;
        }
    }

    public interface ISelector<in TInput, out TOutput>
    {
    }

    public interface ISelectorWithoutProps<in TInput, out TOutput> : ISelector<TInput, TOutput>
    {
        IObservable<TOutput> Apply(TInput input);
    }

    public sealed partial class ReduxStore<TState> where TState : class, new()
    {
        public static bool SelectInvoked
        {
            get;
            private set;
        }

        public ReduxStore()
        {
            SelectInvoked = false;
        }

        public IObservable<TResult> Select<TResult>(ISelectorWithoutProps<TState, TResult> selector, string optionsStr = null)
        {
            SelectInvoked = true;
            return selector.Apply(null);
        }
    }

    public class ManagerWithStore<Klass, TState> where Klass : ManagerWithStore<Klass, TState>, new() where TState : class, new()
    {
        public ReduxStore<TState> Store { get; private set; } = null;

        public ManagerWithStore()
        {
            Store = new ReduxStore<TState>();
        }
    }

    public class PlayerChoiceState
    {
    }

    public class TestSelectorWithoutProps : ISelectorWithoutProps<PlayerChoiceState, string>
    {
        public IObservable<string> Apply(PlayerChoiceState input)
        {
            return null;
        }
    }

    public class PlayerChoiceManager : ManagerWithStore<PlayerChoiceManager, PlayerChoiceState>
    {
    }
}

