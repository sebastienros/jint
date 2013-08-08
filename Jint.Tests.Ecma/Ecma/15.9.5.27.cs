using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_27 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.27")]
        public void TheDatePrototypePropertySettimeHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.27/S15.9.5.27_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.27")]
        public void TheDatePrototypePropertySettimeHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.27/S15.9.5.27_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.27")]
        public void TheDatePrototypePropertySettimeHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.27/S15.9.5.27_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.27")]
        public void TheLengthPropertyOfTheSettimeIs1()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.27/S15.9.5.27_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.27")]
        public void TheDatePrototypeSettimePropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.27/S15.9.5.27_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.27")]
        public void TheDatePrototypeSettimePropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.27/S15.9.5.27_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.27")]
        public void TheDatePrototypeSettimePropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.27/S15.9.5.27_A3_T3.js", false);
        }


    }
}
