using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_6_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.6.2")]
        public void NativeEcmascriptObjectsHaveAnInternalPropertyCalledPrototypeTheValueOfThisPropertyIsEitherNullOrAnObjectAndIsUsedForImplementingInheritance()
        {
			RunTest(@"TestCases/ch08/8.6/8.6.2/S8.6.2_A1.js", false);
        }

        [Fact]
        [Trait("Category", "8.6.2")]
        public void PropertiesOfThePrototypeObjectAreVisibleAsPropertiesOfTheChildObjectForThePurposesOfGetAccessButNotForPutAccess()
        {
			RunTest(@"TestCases/ch08/8.6/8.6.2/S8.6.2_A2.js", false);
        }

        [Fact]
        [Trait("Category", "8.6.2")]
        public void TheSpecificationDoesNotProvideAnyMeansForAProgramToAccessClassValueExceptThroughObjectPrototypeTostring()
        {
			RunTest(@"TestCases/ch08/8.6/8.6.2/S8.6.2_A3.js", false);
        }

        [Fact]
        [Trait("Category", "8.6.2")]
        public void HasinstanceReturnsABooleanValueIndicatingWhetherValueDelegatesBehaviourToThisObject()
        {
			RunTest(@"TestCases/ch08/8.6/8.6.2/S8.6.2_A4.js", false);
        }

        [Fact]
        [Trait("Category", "8.6.2")]
        public void CallExecutesCodeAssociatedWithTheObject()
        {
			RunTest(@"TestCases/ch08/8.6/8.6.2/S8.6.2_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "8.6.2")]
        public void CallExecutesCodeAssociatedWithTheObject2()
        {
			RunTest(@"TestCases/ch08/8.6/8.6.2/S8.6.2_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "8.6.2")]
        public void CallExecutesCodeAssociatedWithTheObject3()
        {
			RunTest(@"TestCases/ch08/8.6/8.6.2/S8.6.2_A5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "8.6.2")]
        public void CallExecutesCodeAssociatedWithTheObject4()
        {
			RunTest(@"TestCases/ch08/8.6/8.6.2/S8.6.2_A5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "8.6.2")]
        public void ConstructConstructsAnObjectInvokedViaTheNewOperatorObjectsThatImplementThisInternalMethodAreCalledConstructors()
        {
			RunTest(@"TestCases/ch08/8.6/8.6.2/S8.6.2_A6.js", false);
        }

        [Fact]
        [Trait("Category", "8.6.2")]
        public void ObjectsThatImplementInternalMethodConstructAreCalledConstructorsMathObjectIsNotConstructor()
        {
			RunTest(@"TestCases/ch08/8.6/8.6.2/S8.6.2_A7.js", true);
        }

        [Fact]
        [Trait("Category", "8.6.2")]
        public void ItShouldNotBePossibleToChangeThePrototypeOfANonExtensibleObject()
        {
			RunTest(@"TestCases/ch08/8.6/8.6.2/S8.6.2_A8.js", false);
        }


    }
}
