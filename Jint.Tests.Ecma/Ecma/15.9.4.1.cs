using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_4_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.4.1")]
        public void TheDatePropertyPrototypeHasDontenumDontdeleteReadonlyAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.1/S15.9.4.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.1")]
        public void TheDatePropertyPrototypeHasDontenumDontdeleteReadonlyAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.1/S15.9.4.1_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.1")]
        public void TheDatePropertyPrototypeHasDontenumDontdeleteReadonlyAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.1/S15.9.4.1_A1_T3.js", false);
        }


    }
}
