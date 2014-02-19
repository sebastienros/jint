using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_8 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.8")]
        public void TheDatePrototypePropertyValueofHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.8/S15.9.5.8_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.8")]
        public void TheDatePrototypePropertyValueofHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.8/S15.9.5.8_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.8")]
        public void TheDatePrototypePropertyValueofHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.8/S15.9.5.8_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.8")]
        public void TheLengthPropertyOfTheValueofIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.8/S15.9.5.8_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.8")]
        public void TheDatePrototypeValueofPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.8/S15.9.5.8_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.8")]
        public void TheDatePrototypeValueofPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.8/S15.9.5.8_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.8")]
        public void TheDatePrototypeValueofPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.8/S15.9.5.8_A3_T3.js", false);
        }


    }
}
