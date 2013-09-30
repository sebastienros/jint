using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_7 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.7")]
        public void MultipleVariablesShouldReferringToASingleObject()
        {
			RunTest(@"TestCases/ch08/8.7/S8.7_A1.js", false);
        }

        [Fact]
        [Trait("Category", "8.7")]
        public void ReferenceToSelfModifyingObjectRemainTheIntegrity()
        {
			RunTest(@"TestCases/ch08/8.7/S8.7_A2.js", false);
        }

        [Fact]
        [Trait("Category", "8.7")]
        public void ChangingTheReferenceOfAnObjectWhileMaintainingIntegrity()
        {
			RunTest(@"TestCases/ch08/8.7/S8.7_A3.js", false);
        }

        [Fact]
        [Trait("Category", "8.7")]
        public void ObjectModificationResultingInANewObjectForNotASelfModifiedObjectLeadsToLossOfIntegrity()
        {
			RunTest(@"TestCases/ch08/8.7/S8.7_A4.js", false);
        }

        [Fact(Skip = "Doesn't work in Chrome either")]
        [Trait("Category", "8.7")]
        public void DeleteUnaryOperatorCanTDeleteObjectToBeReferenced()
        {
			RunTest(@"TestCases/ch08/8.7/S8.7_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "8.7")]
        public void DeleteUnaryOperatorCanTDeleteObjectToBeReferenced2()
        {
			RunTest(@"TestCases/ch08/8.7/S8.7_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "8.7")]
        public void PassingArgumentsByValueDiffersFromByReferenceAndDoNotChangeValuesToBePassed()
        {
			RunTest(@"TestCases/ch08/8.7/S8.7_A6.js", false);
        }

        [Fact]
        [Trait("Category", "8.7")]
        public void PassingArgumentsByReferenceDoChangeValuesOfReferenceToBePassed()
        {
			RunTest(@"TestCases/ch08/8.7/S8.7_A7.js", false);
        }


    }
}
