using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_29 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.29")]
        public void TheDatePrototypePropertySetutcmillisecondsHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.29/S15.9.5.29_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.29")]
        public void TheDatePrototypePropertySetutcmillisecondsHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.29/S15.9.5.29_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.29")]
        public void TheDatePrototypePropertySetutcmillisecondsHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.29/S15.9.5.29_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.29")]
        public void TheLengthPropertyOfTheSetutcmillisecondsIs1()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.29/S15.9.5.29_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.29")]
        public void TheDatePrototypeSetutcmillisecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.29/S15.9.5.29_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.29")]
        public void TheDatePrototypeSetutcmillisecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.29/S15.9.5.29_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.29")]
        public void TheDatePrototypeSetutcmillisecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.29/S15.9.5.29_A3_T3.js", false);
        }


    }
}
