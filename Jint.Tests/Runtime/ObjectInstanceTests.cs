using System.Linq;
using Jint.Native;
using Jint.Native.Object;
using Xunit;

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
            Assert.Equal(new [] { "scope" }, propertyNames);
        }
    }
}