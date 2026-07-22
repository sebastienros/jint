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

        age.Should().Be(70);
    }

    [Fact]
    public void ShouldInvokeStringExtensionMethod()
    {
        var options = new Options();
        options.AddExtensionMethods(typeof(CustomStringExtensions));

        var engine = new Engine(options);
        var result = engine.Evaluate("\"Hello World!\".Backwards()").AsString();

        result.Should().Be("!dlroW olleH");
    }

    [Fact]
    public void ShouldInvokeNumberExtensionMethod()
    {
        var options = new Options();
        options.AddExtensionMethods(typeof(DoubleExtensions));

        var engine = new Engine(options);
        var result = engine.Evaluate("let numb = 27; numb.Add(13)").AsInteger();

        result.Should().Be(40);
    }

    [Fact]
    public void ShouldPrioritizingNonGenericMethod()
    {
        var options = new Options();
        options.AddExtensionMethods(typeof(CustomStringExtensions));

        var engine = new Engine(options);
        var result = engine.Evaluate("\"{'name':'Mickey'}\".DeserializeObject()").ToObject() as dynamic;

        ((string) result.name).Should().Be("Mickey");
    }

    [Fact]
    public void PrototypeFunctionsShouldNotBeOverridden()
    {
        var engine = new Engine(opts =>
        {
            opts.AddExtensionMethods(typeof(CustomStringExtensions));
            // this exercises extension-method dispatch producing a CLR array (string[]) that the test
            // reads back as a JS array; pin Copy so the result is a native JsArray (the LiveView default
            // would expose it as a live wrapper that AsArray() does not accept)
            opts.Interop.ArrayConversion = ArrayConversionMode.Copy;
        });

        //uses split function from StringPrototype
        var arr = engine.Evaluate("'yes,no'.split(',')").AsArray();
        arr[0].Should().Be("yes");
        arr[1].Should().Be("no");

        //uses split function from CustomStringExtensions
        var arr2 = engine.Evaluate("'yes,no'.split(2)").AsArray();
        arr2[0].Should().Be("ye");
        arr2[1].Should().Be("s,no");
    }

    [Fact]
    public void OverridePrototypeFunctions()
    {
        var engine = new Engine(opts =>
        {
            opts.AddExtensionMethods(typeof(OverrideStringPrototypeExtensions));
            // see PrototypeFunctionsShouldNotBeOverridden: the overriding split returns a CLR string[]
            // that is read back as a JS array, so pin Copy for a native JsArray result
            opts.Interop.ArrayConversion = ArrayConversionMode.Copy;
        });

        //uses the overridden split function from OverrideStringPrototypeExtensions
        var arr = engine.Evaluate("'yes,no'.split(',')").AsArray();
        arr[0].Should().Be("YES");
        arr[1].Should().Be("NO");
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
        isBogusInPerson.Should().BeFalse();

        var propertyValue = engine.Evaluate("person.bogus");
        propertyValue.Should().BeUndefined();
    }

    private Engine GetLinqEngine(Action<Options> additionalConfiguration = null)
    {
        return new Engine(opts =>
        {
            opts.AddExtensionMethods(typeof(Enumerable));
            additionalConfiguration?.Invoke(opts);
        });
    }

    [Fact]
    public void LinqExtensionMethodWithoutGenericParameter()
    {
        var engine = GetLinqEngine();
        var intList = new List<int>() { 0, 1, 2, 3 };

        engine.SetValue("intList", intList);
        var intSumRes = engine.Evaluate("intList.Sum()").AsNumber();
        intSumRes.Should().Be(6);
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
            stringSumRes.Should().Be(11);
        }
#endif

    [Fact]
    public void LinqExtensionMethodWithMultipleGenericParameters()
    {
        // Pin Copy: '.ToArray()' returns a CLR string[], and under the LiveView default the resulting
        // wrapper's target (string[] : IEnumerable<string>) makes the '.join()' call resolve to the CLR
        // 'Enumerable.Join' extension method attached to the type rather than falling back to native
        // 'Array.prototype.join'. Copy produces a native JsArray whose 'join' is the prototype method.
        // (Under LiveView the equivalent works with Options.Interop.PreferJsPrototypeMethods = true.)
        var engine = GetLinqEngine(opts => opts.Interop.ArrayConversion = ArrayConversionMode.Copy);
        var stringList = new List<string>() { "working", "linq" };
        engine.SetValue("stringList", stringList);

        var stringRes = engine.Evaluate("stringList.Select((x) => x + 'a').ToArray().join()").AsString();
        stringRes.Should().Be("workinga,linqa");

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

        observable.Last.Should().Be("foobar");
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

        result.Should().Be("some text");
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
        nameObservableResult.Should().NotBeNull();
        nameObservableResult.Last.Should().Be("testing yo");
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
        baseObservableResult.Should().NotBeNull();
        baseObservableResult.Last.Should().Be("testing yo");
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
        baseObservableResult.Should().NotBeNull();
        baseObservableResult.Last.Should().BeFalse();
    }

    [Fact]
    public void CanProjectAndGroupWhenExpandoObjectWrappingDisabled()
    {
        var engine = new Engine(options =>
        {
            options.AllowClr().AddExtensionMethods(typeof(Enumerable));
            // prevent ExpandoObject wrapping
            options.Interop.CreateClrObject = null;
            // '.ToArray()' results are read back as JS arrays via AsArray(); pin Copy so they are native
            // JsArrays rather than the LiveView default's live wrapper views
            options.Interop.ArrayConversion = ArrayConversionMode.Copy;
        });
        engine.Execute("var a = [ 2, 4 ];");

        var selected = engine.Evaluate("JSON.stringify(a.Select(m => ({a:m,b:m})).ToArray());").AsString();
        selected.Should().Be("""[{"a":2,"b":2},{"a":4,"b":4}]""");

        var grouped1 = engine.Evaluate("a.GroupBy(m => ({a:m,b:m})).ToArray()").AsArray();
        var grouped = engine.Evaluate("JSON.stringify(a.GroupBy(m => ({a:m,b:m})).Select(x => x.Key).ToArray());").AsString();
        grouped.Should().Be("""[{"a":2,"b":2},{"a":4,"b":4}]""");
    }
}