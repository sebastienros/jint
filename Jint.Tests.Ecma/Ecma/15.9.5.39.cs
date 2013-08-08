using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_39 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.39")]
        public void TheDatePrototypePropertySetutcmonthHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.39/S15.9.5.39_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.39")]
        public void TheDatePrototypePropertySetutcmonthHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.39/S15.9.5.39_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.39")]
        public void TheDatePrototypePropertySetutcmonthHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.39/S15.9.5.39_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.39")]
        public void TheLengthPropertyOfTheSetutcmonthIs2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.39/S15.9.5.39_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.39")]
        public void TheDatePrototypeSetutcmonthPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.39/S15.9.5.39_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.39")]
        public void TheDatePrototypeSetutcmonthPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.39/S15.9.5.39_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.39")]
        public void TheDatePrototypeSetutcmonthPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.39/S15.9.5.39_A3_T3.js", false);
        }


    }
}
