using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_8 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.8")]
        public void ValuesOfTheListTypeAreSimplyOrderedSequencesOfValues()
        {
			RunTest(@"TestCases/ch08/8.8/S8.8_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "8.8")]
        public void ValuesOfTheListTypeAreSimplyOrderedSequencesOfValues2()
        {
			RunTest(@"TestCases/ch08/8.8/S8.8_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "8.8")]
        public void ValuesOfTheListTypeAreSimplyOrderedSequencesOfValues3()
        {
			RunTest(@"TestCases/ch08/8.8/S8.8_A2_T3.js", false);
        }


    }
}
