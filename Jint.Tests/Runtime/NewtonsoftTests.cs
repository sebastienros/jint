using System;
using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Tests.Runtime.Converters;
using Jint.Tests.Runtime.Domain;
using Shapes;
using Xunit;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Jint.Tests.Runtime
{
    public class NewtonsoftTests : IDisposable
    {
        private readonly Engine _engine;

        public NewtonsoftTests()
        {
            _engine = new Engine(cfg => cfg.AllowClr(typeof(Shape).Assembly))
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(Assert.True))
                ;
        }

        void IDisposable.Dispose()
        {
        }

        private void RunTest(string source)
        {
            _engine.Execute(source);
        }

        [Fact]
        public void PrimitiveTypesCanBeRead()
        {
            _engine.SetValue("o", new JsValue(new Jint.Native.Object.NTSObjectInstance(_engine, JObject.Parse(@"{""x"":10, ""y"":true, ""z"":""foo""}"))));

            RunTest(@"
                assert(o.x === 10);
                assert(o.y === true);
                assert(o.z === 'foo');
            ");
        }

        [Fact]
        public void PrimitiveTypesCanBeSet()
        {
            var obj = JObject.Parse(@"{""x"":10, ""y"":true, ""z"":""foo""}");
            _engine.SetValue("o", new JsValue(new Jint.Native.Object.NTSObjectInstance(_engine, obj)));

            RunTest(@"
                o.x=11;
                o.y=false;
                o.z='bar'
                assert(o.x === 11);
                assert(o.y === false);
                assert(o.z === 'bar');
            ");

            Assert.Equal(11, obj.Value<int>("x"));
            Assert.Equal(false, obj.Value<bool>("y"));
            Assert.Equal("bar", obj.Value<string>("z"));
        }

    }
}
