using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime.Domain
{
    [JintClass(DiscoveryModes.OptOut)]
    public class OptOutTestClass
    {
        [JintIgnore]
        public int TestRemovedField;

        public int TestField;

        [JintIgnore]
        public int TestRemovedProperty { get; set; }

        public int TestProperty { get; set; }

        [JintIgnore]
        public void TestRemovedMethod()
        {
        }

        public void TestMethod()
        {
        }
    }
}
