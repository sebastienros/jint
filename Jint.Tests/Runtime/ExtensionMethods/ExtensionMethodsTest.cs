using Jint.Native;
using Jint.Tests.Runtime.Domain;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using System.IO;
using System.Text;

namespace Jint.Tests.Runtime.ExtensionMethods
{
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

        [Fact]
        public void LinqExtensionMethodWithSingleGenericParameter()
        {
            var engine = GetLinqEngine();
            var stringList = new List<string>() { "working", "linq" };
            engine.SetValue("stringList", stringList);

            var stringSumRes = engine.Evaluate("stringList.Sum(x => x.length)").AsNumber();
            Assert.Equal(11, stringSumRes);
        }

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


        private class Converter : TextWriter
        {
            ITestOutputHelper _output;
            public Converter(ITestOutputHelper output)
            {
                _output = output;
            }
            public override Encoding Encoding
            {
                get { return Encoding.ASCII; }
            }
            public override void WriteLine(string message)
            {
                _output.WriteLine(message);
            }
            public override void WriteLine(string format, params object[] args)
            {
                _output.WriteLine(format, args);
            }

            public override void Write(char value)
            {
                throw new System.Exception("This text writer only supports WriteLine(string) and WriteLine(string, params object[]).");
            }
        }
    }

}
