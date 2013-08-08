using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_10_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "10.5")]
        public void StrictModeArgumentsObjectIsImmutable()
        {
			RunTest(@"TestCases/ch10/10.5/10.5-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.5")]
        public void StrictModeArgumentsCannotBeAssignedToInAStrictFunction()
        {
			RunTest(@"TestCases/ch10/10.5/10.5-1gs.js", true);
        }

        [Fact]
        [Trait("Category", "10.5")]
        public void StrictModeArgumentsObjectIsImmutableInEvalEdFunctions()
        {
			RunTest(@"TestCases/ch10/10.5/10.5-7-b-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.5")]
        public void StrictModeArgumentsObjectIndexAssignmentIsAllowed()
        {
			RunTest(@"TestCases/ch10/10.5/10.5-7-b-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.5")]
        public void StrictModeAddingPropertyToTheArgumentsObjectSuccessfulUnderStrictMode()
        {
			RunTest(@"TestCases/ch10/10.5/10.5-7-b-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.5")]
        public void StrictModeDeletingPropertyOfTheArgumentsObjectSuccessfulUnderStrictMode()
        {
			RunTest(@"TestCases/ch10/10.5/10.5-7-b-4-s.js", false);
        }


    }
}
