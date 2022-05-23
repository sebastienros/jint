using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.WeakMap;
using Jint.Native.WeakSet;
using Jint.Runtime;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class WeakSetMapTests
    {

        private static readonly Engine _engine = new Engine();

        [Fact]
        public void WeakMapShouldThrowWhenCalledWithoutNew()
        {
            var e = Assert.Throws<EvaluationException>(() => _engine.Execute("{ const m = new WeakMap(); WeakMap.call(m,[]); }"));
            Assert.Equal("Constructor WeakMap requires 'new'", e.Message);
        }

        [Fact]
        public void WeakSetShouldThrowWhenCalledWithoutNew()
        {
            var e = Assert.Throws<EvaluationException>(() => _engine.Execute("{ const s = new WeakSet(); WeakSet.call(s,[]); }"));
            Assert.Equal("Constructor WeakSet requires 'new'", e.Message);
        }

        public static IEnumerable<object[]> PrimitiveKeys = new TheoryData<JsValue>
        {
            JsValue.Null,
            JsValue.Undefined,
            0,
            100.04,
            double.NaN,
            "hello",
            true,
            new JsSymbol("hello")
        };

        [Theory]
        [MemberData(nameof(PrimitiveKeys))]
        public void WeakSetAddShouldThrowForPrimitiveKey(JsValue key) {
            var weakSet = new WeakSetInstance(_engine);

            var e = Assert.Throws<JavaScriptInternalException>(() => weakSet.WeakSetAdd(key));
            Assert.StartsWith("WeakSet value must be an object, got ", e.Message);

            Assert.False(weakSet.WeakSetHas(key));
        }

        [Theory]
        [MemberData(nameof(PrimitiveKeys))]
        public void WeakMapSetShouldThrowForPrimitiveKey(JsValue key) {
            var weakMap = new WeakMapInstance(_engine);

            var e = Assert.Throws<JavaScriptInternalException>(() => weakMap.WeakMapSet(key, new ObjectInstance(_engine)));
            Assert.StartsWith("WeakMap key must be an object, got ", e.Message);

            Assert.False(weakMap.WeakMapHas(key));
        }

    }

}