using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime.Domain
{
    [JintClass(DiscoveryModes.OptIn)]
    public class OptInTestClass
    {
        public int TestRemovedField;

        [JintField]
        public int TestField;

        public int TestRemovedProperty { get; set; }

        [JintProperty]
        public int TestProperty { get; set; }

        public void TestRemovedMethod()
        {
        }

        [JintMethod]
        public void TestMethod()
        {
        }
    }
}
