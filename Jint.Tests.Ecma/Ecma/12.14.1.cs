using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_14_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.14.1")]
        public void StrictModeSyntaxerrorIsThrownIfATrystatementWithACatchOccursWithinStrictCodeAndTheIdentifierOfTheCatchProductionIsEval()
        {
			RunTest(@"TestCases/ch12/12.14/12.14.1/12.14.1-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.14.1")]
        public void StrictModeSyntaxerrorIsThrownIfATrystatementWithACatchOccursWithinStrictCodeAndTheIdentifierOfTheCatchProductionIsEval2()
        {
			RunTest(@"TestCases/ch12/12.14/12.14.1/12.14.1-1gs.js", true);
        }

        [Fact]
        [Trait("Category", "12.14.1")]
        public void StrictModeSyntaxerrorIsThrownIfATrystatementWithACatchOccursWithinStrictCodeAndTheIdentifierOfTheCatchProductionIsArguments()
        {
			RunTest(@"TestCases/ch12/12.14/12.14.1/12.14.1-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.14.1")]
        public void StrictModeSyntaxerrorIsnTThrownIfATrystatementWithACatchOccursWithinStrictCodeAndTheIdentifierOfTheCatchProductionIsEvalButThrowsSyntaxerrorIfItIsEval()
        {
			RunTest(@"TestCases/ch12/12.14/12.14.1/12.14.1-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.14.1")]
        public void StrictModeSyntaxerrorIsnTThrownIfATrystatementWithACatchOccursWithinStrictCodeAndTheIdentifierOfTheCatchProductionIsEval()
        {
			RunTest(@"TestCases/ch12/12.14/12.14.1/12.14.1-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.14.1")]
        public void StrictModeSyntaxerrorIsnTThrownIfATrystatementWithACatchOccursWithinStrictCodeAndTheIdentifierOfTheCatchProductionIsArguments()
        {
			RunTest(@"TestCases/ch12/12.14/12.14.1/12.14.1-5-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.14.1")]
        public void StrictModeSyntaxerrorIsnTThrownIfATrystatementWithACatchOccursWithinStrictCodeAndTheIdentifierOfTheCatchProductionIsArguments2()
        {
			RunTest(@"TestCases/ch12/12.14/12.14.1/12.14.1-6-s.js", false);
        }


    }
}
