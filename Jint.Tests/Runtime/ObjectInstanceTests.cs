using Jint.Native;
using Jint.Native.Object;

namespace Jint.Tests.Runtime
{
    public class ObjectInstanceTests
    {
        [Fact]
        public void RemovingFirstPropertyFromObjectInstancePropertiesBucketAndEnumerating()
        {
            var engine = new Engine();
            var instance = new ObjectInstance(engine);
            instance.FastAddProperty("bare", JsValue.Null, true, true, true);
            instance.FastAddProperty("scope", JsValue.Null, true, true, true);
            instance.RemoveOwnProperty("bare");
            var propertyNames = instance.GetOwnProperties().Select(x => x.Key).ToList();
            Assert.Equal(new JsValue[] { "scope" }, propertyNames);
        }

        [Theory]
        [InlineData("Array")]
        [InlineData("Boolean")]
        [InlineData("Date")]
        [InlineData("Error")]
        [InlineData("Number")]
        [InlineData("Object")]
        [InlineData("String")]
        public void ExtendingObjects(string baseType)
        {
            var code = $@"
                class My{baseType} extends {baseType} {{
                    constructor(...args) {{
                        super(...args);
                    }} 
                }}
                const o = new My{baseType}();
                o.constructor === My{baseType}";

            var engine = new Engine();
            Assert.True(engine.Evaluate(code).AsBoolean());
        }

        [Fact]
        public void ShouldHavePrototypeInPlaceByDefault()
        {
            var engine = new Engine();
            var instance = new ObjectInstance(engine);
            Assert.NotNull(instance.GetPrototypeOf());
            Assert.Equal("[object Object]", instance.ToString());
        }
    }
}
