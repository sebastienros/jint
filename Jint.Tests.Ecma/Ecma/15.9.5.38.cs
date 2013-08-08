using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_38 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.38")]
        public void TheDatePrototypePropertySetmonthHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.38/S15.9.5.38_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.38")]
        public void TheDatePrototypePropertySetmonthHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.38/S15.9.5.38_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.38")]
        public void TheDatePrototypePropertySetmonthHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.38/S15.9.5.38_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.38")]
        public void TheLengthPropertyOfTheSetmonthIs2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.38/S15.9.5.38_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.38")]
        public void TheDatePrototypeSetmonthPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.38/S15.9.5.38_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.38")]
        public void TheDatePrototypeSetmonthPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.38/S15.9.5.38_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.38")]
        public void TheDatePrototypeSetmonthPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.38/S15.9.5.38_A3_T3.js", false);
        }


    }
}
