using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jint.Runtime.Debugger;
using Jint.Tests.Runtime.Domain;
using Jint.Tests.Runtime.ExtensionMethods;
using Xunit;
using Xunit.Abstractions;

namespace Jint.Tests.Runtime
{
    public class ExtensionMethodsTest : IDisposable
    {
        private readonly Engine _engine;

        public ExtensionMethodsTest(ITestOutputHelper output)
        {
            _engine = new Engine()
                    .SetValue("log", new Action<object>(o => output.WriteLine(o.ToString())))
                    .SetValue("assert", new Action<bool>(Assert.True))
                    .SetValue("equal", new Action<object, object>(Assert.Equal))
                ;
        }

        void IDisposable.Dispose()
        {
        }

        [Fact]
        public void ShouldInvokeObjectExtensionMethod()
        {
            var person = new Person();
            person.Name = "Mickey Mouse";
            person.Age = 35;

            var options = new Options();
            options.AddExtensionMethod(typeof(PersonExtensions));

            var engine = new Engine(options);
            engine.SetValue("person", person);
            var age = engine.Execute("person.MultiplyAge(2)").GetCompletionValue().AsInteger();

            Assert.Equal(70, age);
        }

        [Fact]
        public void ShouldInvokeStringExtensionMethod()
        {
            var options = new Options();
            options.AddExtensionMethod(typeof(CustomStringExtensions));

            var engine = new Engine(options);
            var result = engine.Execute("\"Hello World!\".Backwards()").GetCompletionValue().AsString();

            Assert.Equal("!dlroW olleH", result);
        }

        [Fact]
        public void ShouldInvokeNumberExtensionMethod()
        {
            var options = new Options();
            options.AddExtensionMethod(typeof(DoubleExtensions));

            var engine = new Engine(options);
            var result = engine.Execute("let numb = 27; numb.Add(13)").GetCompletionValue().AsInteger();

            Assert.Equal(40, result);
        }
    }
}
