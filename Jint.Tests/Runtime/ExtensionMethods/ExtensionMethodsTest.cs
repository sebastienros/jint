using Jint.Native;
using Jint.Tests.Runtime.Domain;

namespace Jint.Tests.Runtime.ExtensionMethods;

public class ExtensionMethodsTest
{
    [Fact]
    public void ShouldInvokeObjectExtensionMethod()
    {
        var person = new Person();
        person.Name = "Mickey Mouse";
        person.Age = 35;

        var options = new Options();
        options.AddExtensionMethods(typeof(PersonExtensions));

        var engine = new Engine(options);
        engine.SetValue("person", person);
        var age = engine.Evaluate("person.MultiplyAge(2)").AsInteger();

        Assert.Equal(70, age);
    }

    [Fact]
    public void ShouldInvokeStringExtensionMethod()
    {
        var options = new Options();
        options.AddExtensionMethods(typeof(CustomStringExtensions));

        var engine = new Engine(options);
        var result = engine.Evaluate("\"Hello World!\".Backwards()").AsString();

        Assert.Equal("!dlroW olleH", result);
    }

    [Fact]
    public void ShouldInvokeNumberExtensionMethod()
    {
        var options = new Options();
        options.AddExtensionMethods(typeof(DoubleExtensions));

        var engine = new Engine(options);
        var result = engine.Evaluate("let numb = 27; numb.Add(13)").AsInteger();

        Assert.Equal(40, result);
    }

    [Fact]
    public void ShouldPrioritizingNonGenericMethod()
    {
        var options = new Options();
        options.AddExtensionMethods(typeof(CustomStringExtensions));

        var engine = new Engine(options);
        var result = engine.Evaluate("\"{'name':'Mickey'}\".DeserializeObject()").ToObject() as dynamic;

        Assert.Equal("Mickey", result.name);
    }

    [Fact]
    public void PrototypeFunctionsShouldNotBeOverridden()
    {
        var engine = new Engine(opts =>
        {
            opts.AddExtensionMethods(typeof(CustomStringExtensions));
        });

        //uses split function from StringPrototype
        var arr = engine.Evaluate("'yes,no'.split(',')").AsArray();
        Assert.Equal("yes", arr[0]);
        Assert.Equal("no", arr[1]);

        //uses split function from CustomStringExtensions
        var arr2 = engine.Evaluate("'yes,no'.split(2)").AsArray();
        Assert.Equal("ye", arr2[0]);
        Assert.Equal("s,no", arr2[1]);
    }

    [Fact]
    public void OverridePrototypeFunctions()
    {
        var engine = new Engine(opts =>
        {
            opts.AddExtensionMethods(typeof(OverrideStringPrototypeExtensions));
        });

        //uses the overridden split function from OverrideStringPrototypeExtensions
        var arr = engine.Evaluate("'yes,no'.split(',')").AsArray();
        Assert.Equal("YES", arr[0]);
        Assert.Equal("NO", arr[1]);
    }

    [Fact]
    public void HasOwnPropertyShouldWorkCorrectlyInPresenceOfExtensionMethods()
    {
        var person = new Person();

        var options = new Options();
        options.AddExtensionMethods(typeof(PersonExtensions));

        var engine = new Engine(options);
        engine.SetValue("person", person);

        var isBogusInPerson = engine.Evaluate("'bogus' in person").AsBoolean();
        Assert.False(isBogusInPerson);

        var propertyValue = engine.Evaluate("person.bogus");
        Assert.Equal(JsValue.Undefined, propertyValue);
    }

    private Engine GetLinqEngine()
    {
        return new Engine(opts =>
        {
            opts.AddExtensionMethods(typeof(Enumerable));
        });
    }

    [Fact]
    public void LinqExtensionMethodWithoutGenericParameter()
    {
        var engine = GetLinqEngine();
        var intList = new List<int>() { 0, 1, 2, 3 };

        engine.SetValue("intList", intList);
        var intSumRes = engine.Evaluate("intList.Sum()").AsNumber();
        Assert.Equal(6, intSumRes);
    }

    // TODO this fails due to double -> long assignment on FW
#if !NETFRAMEWORK
        [Fact]
        public void LinqExtensionMethodWithSingleGenericParameter()
        {
            var engine = GetLinqEngine();
            var stringList = new List<string>() { "working", "linq" };
            engine.SetValue("stringList", stringList);

            var stringSumRes = engine.Evaluate("stringList.Sum(x => x.length)").AsNumber();
            Assert.Equal(11, stringSumRes);
        }
#endif

    [Fact]
    public void LinqExtensionMethodWithMultipleGenericParameters()
    {
        var engine = GetLinqEngine();
        var stringList = new List<string>() { "working", "linq" };
        engine.SetValue("stringList", stringList);

        var stringRes = engine.Evaluate("stringList.Select((x) => x + 'a').ToArray().join()").AsString();
        Assert.Equal("workinga,linqa", stringRes);

        // The method ambiguity resolver is not so smart to choose the Select method with the correct number of parameters
        // Thus, the following script will not work as expected.
        // stringList.Select((x, i) => x + i).ToArray().join()
    }

    [Fact]
    public void GenericTypeExtension()
    {
        var options = new Options();
        options.AddExtensionMethods(typeof(ObservableExtensions));

        var engine = new Engine(options);

        engine.SetValue("log", new System.Action<object>(System.Console.WriteLine));

        NameObservable observable = new NameObservable();

        engine.SetValue("observable", observable);
        engine.Evaluate(@"
                log('before');
                observable.Subscribe((name) =>{
                    log('observable: subscribe: name: ' + name);
                });

                observable.UpdateName('foobar');
                log('after');
            ");

        Assert.Equal("foobar", observable.Last);
    }

    [Fact]
    public void GenericExtensionMethodOnClosedGenericType()
    {
        var options = new Options();
        options.AddExtensionMethods(typeof(ObservableExtensions));

        var engine = new Engine(options);

        engine.SetValue("log", new System.Action<object>(System.Console.WriteLine));

        NameObservable observable = new NameObservable();
        engine.SetValue("observable", observable);
        var result = engine.Evaluate(@"
                log('before calling Select');
                var result = observable.Select('some text');
                log('result: ' + result);
                return result;
            ");

        //System.Console.WriteLine("GenericExtensionMethodOnGenericType: result: " + result + " result.ToString(): " + result.ToString());

        Assert.Equal("some text", result);
    }

    [Fact]
    public void GenericExtensionMethodOnClosedGenericType2()
    {
        var options = new Options();
        options.AddExtensionMethods(typeof(ObservableExtensions));

        var engine = new Engine(options);

        NameObservable observable = new NameObservable();
        observable.Where((text) =>
        {
            System.Console.WriteLine("GenericExtensionMethodOnClosedGenericType2: NameObservable: Where: text: " + text);
            return true;
        });
        engine.SetValue("observable", observable);
        var result = engine.Evaluate(@"
                var result = observable.Where(function(text){
                    return true;
                });

                observable.UpdateName('testing yo');
                observable.CommitName();
                return result;
            ");

        var nameObservableResult = result.ToObject() as NameObservable;
        Assert.NotNull(nameObservableResult);
        Assert.Equal("testing yo", nameObservableResult.Last);
    }

    [Fact]
    public void GenericExtensionMethodOnOpenGenericType()
    {
        var options = new Options();
        options.AddExtensionMethods(typeof(ObservableExtensions));

        var engine = new Engine(options);

        BaseObservable<string> observable = new BaseObservable<string>();
        observable.Where((text) =>
        {
            System.Console.WriteLine("GenericExtensionMethodOnOpenGenericType: BaseObservable: Where: text: " + text);
            return true;
        });
        engine.SetValue("observable", observable);
        var result = engine.Evaluate(@"
                var result = observable.Where(function(text){
                    return true;
                });

                observable.Update('testing yo');
                observable.BroadcastCompleted();

                return result;
            ");

        System.Console.WriteLine("GenericExtensionMethodOnOpenGenericType: result: " + result + " result.ToString(): " + result.ToString());
        var baseObservableResult = result.ToObject() as BaseObservable<string>;

        System.Console.WriteLine("GenericExtensionMethodOnOpenGenericType: baseObservableResult: " + baseObservableResult);
        Assert.NotNull(baseObservableResult);
        Assert.Equal("testing yo", baseObservableResult.Last);
    }

    [Fact]
    public void GenericExtensionMethodOnGenericTypeInstantiatedInJs()
    {
        var options = new Options();
        options.AddExtensionMethods(typeof(ObservableExtensions));

        var engine = new Engine(options);

        engine.SetValue("BaseObservable", typeof(BaseObservable<>));
        engine.SetValue("ObservableFactory", typeof(ObservableFactory));

        var result = engine.Evaluate(@"

                // you can't instantiate generic types in JS (without providing the types as arguments to the constructor) - i.e. not compatible with transpiled typescript
                //const observable = new BaseObservable();
                //const observable = BaseObservable.GetBoolBaseObservable();
                const observable = ObservableFactory.GetBoolBaseObservable();

                var result = observable.Where(function(someBool){
                    return true;
                });
                observable.Update(false);
                observable.BroadcastCompleted();

                return result;
            ");

        var baseObservableResult = result.ToObject() as BaseObservable<bool>;

        System.Console.WriteLine("GenericExtensionMethodOnOpenGenericType: baseObservableResult: " + baseObservableResult);
        Assert.NotNull(baseObservableResult);
        Assert.Equal(false, baseObservableResult.Last);
    }

    [Fact]
    public void CanProjectAndGroupWhenExpandoObjectWrappingDisabled()
    {
        var engine = new Engine(options =>
        {
            options.AllowClr().AddExtensionMethods(typeof(Enumerable));
            // prevent ExpandoObject wrapping
            options.Interop.CreateClrObject = null;
        });
        engine.Execute("var a = [ 2, 4 ];");

        var selected = engine.Evaluate("JSON.stringify(a.Select(m => ({a:m,b:m})).ToArray());").AsString();
        Assert.Equal("""[{"a":2,"b":2},{"a":4,"b":4}]""", selected);

        var grouped1 = engine.Evaluate("a.GroupBy(m => ({a:m,b:m})).ToArray()").AsArray();
        var grouped = engine.Evaluate("JSON.stringify(a.GroupBy(m => ({a:m,b:m})).Select(x => x.Key).ToArray());").AsString();
        Assert.Equal("""[{"a":2,"b":2},{"a":4,"b":4}]""", grouped);
    }
}