using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_36 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.36")]
        public void TheDatePrototypePropertySetdateHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.36/S15.9.5.36_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.36")]
        public void TheDatePrototypePropertySetdateHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.36/S15.9.5.36_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.36")]
        public void TheDatePrototypePropertySetdateHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.36/S15.9.5.36_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.36")]
        public void TheLengthPropertyOfTheSetdateIs1()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.36/S15.9.5.36_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.36")]
        public void TheDatePrototypeSetdatePropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.36/S15.9.5.36_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.36")]
        public void TheDatePrototypeSetdatePropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.36/S15.9.5.36_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.36")]
        public void TheDatePrototypeSetdatePropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.36/S15.9.5.36_A3_T3.js", false);
        }


    }
}
