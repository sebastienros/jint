using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_9_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "9.1")]
        public void ResultOfPrimitiveConversionFromObjectIsADefaultValueForTheObject()
        {
			RunTest(@"TestCases/ch09/9.1/S9.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.1")]
        public void ResultOfPrimitiveConversionFromObjectIsADefaultValueForTheObject2()
        {
			RunTest(@"TestCases/ch09/9.1/S9.1_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.1")]
        public void ResultOfPrimitiveConversionFromObjectIsADefaultValueForTheObject3()
        {
			RunTest(@"TestCases/ch09/9.1/S9.1_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "9.1")]
        public void ResultOfPrimitiveConversionFromObjectIsADefaultValueForTheObject4()
        {
			RunTest(@"TestCases/ch09/9.1/S9.1_A1_T4.js", false);
        }


    }
}
