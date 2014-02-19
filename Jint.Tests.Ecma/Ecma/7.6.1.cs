using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_7_6_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationVerifiedWithHasownpropertyNullTrueFalse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationVerifiedWithHasownpropertyInTryClass()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-1-10.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationVerifiedWithHasownpropertyEnumExtendsSuper()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-1-11.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationVerifiedWithHasownpropertyConstExportImport()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-1-12.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationVerifiedWithHasownpropertyImplementsLetPrivate()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-1-13.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationVerifiedWithHasownpropertyPublicYieldInterface()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-1-14.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationVerifiedWithHasownpropertyPackageProtectedStatic()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-1-15.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationVerifiedWithHasownpropertyUndeefinedNanInfinity()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-1-16.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationVerifiedWithHasownpropertyBreakCaseDo()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationVerifiedWithHasownpropertyInstanceofTypeofElse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationVerifiedWithHasownpropertyNewVarCatch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationVerifiedWithHasownpropertyFinallyReturnVoid()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationVerifiedWithHasownpropertyContinueForSwitch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-1-6.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationVerifiedWithHasownpropertyWhileDebuggerFunction()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-1-7.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationVerifiedWithHasownpropertyThisWithDefault()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-1-8.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationVerifiedWithHasownpropertyIfThrowDelete()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-1-9.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentVerifiedWithHasownpropertyNullTrueFalse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentVerifiedWithHasownpropertyInTryClass()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentVerifiedWithHasownpropertyEnumExtendsSuper()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentVerifiedWithHasownpropertyConstExportImport()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentVerifiedWithHasownpropertyImplementsLetPrivate()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentVerifiedWithHasownpropertyPublicYieldInterface()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentVerifiedWithHasownpropertyPackageProtectedStatic()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentVerifiedWithHasownpropertyUndefinedNanInfinity()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-2-16.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentVerifiedWithHasownpropertyBreakCaseDo()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentVerifiedWithHasownpropertyInstanceofTypeofElse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentVerifiedWithHasownpropertyNewVarCatch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentVerifiedWithHasownpropertyFinallyReturnVoid()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentVerifiedWithHasownpropertyContinueForSwitch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentVerifiedWithHasownpropertyWhileDebuggerFunction()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentVerifiedWithHasownpropertyThisWithDefault()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentVerifiedWithHasownpropertyIfThrowDelete()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-2-9.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentVerifiedWithHasownpropertyNullTrueFalse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentVerifiedWithHasownpropertyInTryClass()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-3-10.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentVerifiedWithHasownpropertyEnumExtendsSuper()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-3-11.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentVerifiedWithHasownpropertyConstExportImport()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-3-12.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentVerifiedWithHasownpropertyImplementsLetPrivate()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-3-13.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentVerifiedWithHasownpropertyPublicYieldInterface()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-3-14.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentVerifiedWithHasownpropertyPackageProtectedStatic()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-3-15.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentVerifiedWithHasownpropertyUndefinedNanInfinity()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-3-16.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentVerifiedWithHasownpropertyBreakCaseDo()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentVerifiedWithHasownpropertyInstanceofTypeofElse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentVerifiedWithHasownpropertyNewVarCatch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentVerifiedWithHasownpropertyFinallyReturnVoid()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentVerifiedWithHasownpropertyContinueForSwitch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentVerifiedWithHasownpropertyWhileDebuggerFunction()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentVerifiedWithHasownpropertyThisWithDefault()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentVerifiedWithHasownpropertyIfThrowDelete()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-3-9.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectVerifiedWithHasownpropertyNullTrueFalse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectVerifiedWithHasownpropertyInTryClass()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectVerifiedWithHasownpropertyEnumExtendsSuper()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectVerifiedWithHasownpropertyConstExportImport()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-4-12.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectVerifiedWithHasownpropertyImplementsLetPrivate()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-4-13.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectVerifiedWithHasownpropertyPublicYieldInterface()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-4-14.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectVerifiedWithHasownpropertyPackageProtectedStatic()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-4-15.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectVerifiedWithHasownpropertyUndefinedNanInfinity()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-4-16.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectVerifiedWithHasownpropertyBreakCaseDo()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectVerifiedWithHasownpropertyInstanceofTypeofElse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectVerifiedWithHasownpropertyNewVarCatch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectVerifiedWithHasownpropertyFinallyReturnVoid()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectVerifiedWithHasownpropertyContinueForSwitch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectVerifiedWithHasownpropertyWhileDebuggerFunction()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-4-7.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectVerifiedWithHasownpropertyThisWithDefault()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-4-8.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectVerifiedWithHasownpropertyIfThrowDelete()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-4-9.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationAccessedViaIndexingNullTrueFalse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationAccessedViaIndexingInTryClass()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-5-10.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationAccessedViaIndexingEnumExtendsSuper()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-5-11.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationAccessedViaIndexingConstExportImport()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-5-12.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationAccessedViaIndexingImplementsLetPrivate()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-5-13.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationAccessedViaIndexingPublicYieldInterface()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-5-14.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationAccessedViaIndexingPackageProtectedStatic()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-5-15.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationAccessedViaIndexingUndefinedNanInfinity()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-5-16.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationAccessedViaIndexingBreakCaseDo()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-5-2.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationAccessedViaIndexingInstanceofTypeofElse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-5-3.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationAccessedViaIndexingNewVarCatch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-5-4.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationAccessedViaIndexingFinallyReturnVoid()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-5-5.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationAccessedViaIndexingContinueForSwitch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-5-6.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationAccessedViaIndexingWhileDebuggerFunction()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-5-7.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationAccessedViaIndexingThisWithDefault()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-5-8.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesAtObjectInitializationAccessedViaIndexingIfThrowDelete()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-5-9.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentAccessedViaIndexingNullTrueFalse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-6-1.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentAccessedViaIndexingInTryClass()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-6-10.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentAccessedViaIndexingEnumExtendsSuper()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-6-11.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentAccessedViaIndexingConstExportImport()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-6-12.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentAccessedViaIndexingImplementsLetPrivate()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-6-13.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentAccessedViaIndexingPublicYieldInterface()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-6-14.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentAccessedViaIndexingPackageProtectedStatic()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-6-15.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentAccessedViaIndexingUndefinedNanInfinity()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-6-16.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentAccessedViaIndexingBreakCaseDo()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-6-2.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentAccessedViaIndexingInstanceofTypeofElse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-6-3.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentAccessedViaIndexingNewVarCatch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-6-4.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentAccessedViaIndexingFinallyReturnVoid()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-6-5.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentAccessedViaIndexingContinueForSwitch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-6-6.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentAccessedViaIndexingWhileDebuggerFunction()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-6-7.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentAccessedViaIndexingThisWithDefault()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-6-8.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByDotOperatorAssignmentAccessedViaIndexingIfThrowDelete()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-6-9.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentAccessedViaIndexingNullTrueFalse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-7-1.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentAccessedViaIndexingInTryClass()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-7-10.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentAccessedViaIndexingEnumExtendsSuper()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-7-11.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentAccessedViaIndexingConstExportImport()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-7-12.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentAccessedViaIndexingImplementsLetPrivate()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-7-13.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentAccessedViaIndexingPublicYieldInterface()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-7-14.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentAccessedViaIndexingPackageProtectedStatic()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-7-15.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentAccessedViaIndexingUndefinedNanInfinity()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-7-16.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentAccessedViaIndexingBreakCaseDo()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-7-2.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentAccessedViaIndexingInstanceofTypeofElse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-7-3.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentAccessedViaIndexingNewVarCatch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-7-4.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentAccessedViaIndexingFinallyReturnVoid()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-7-5.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentAccessedViaIndexingContinueForSwitch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-7-6.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentAccessedViaIndexingWhileDebuggerFunction()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-7-7.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentAccessedViaIndexingThisWithDefault()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-7-8.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesByIndexAssignmentAccessedViaIndexingIfThrowDelete()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-7-9.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectAccessedViaIndexingNullTrueFalse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-8-1.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectAccessedViaIndexingInTryClass()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-8-10.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectAccessedViaIndexingEnumExtendsSuper()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-8-11.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectAccessedViaIndexingConstExportImport()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-8-12.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectAccessedViaIndexingImplementsLetPrivate()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-8-13.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectAccessedViaIndexingPublicYieldInterface()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-8-14.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectAccessedViaIndexingPackageProtectedStatic()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-8-15.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectAccessedViaIndexingUndefinedNanInfinity()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-8-16.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectAccessedViaIndexingBreakCaseDo()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-8-2.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectAccessedViaIndexingInstanceofTypeofElse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-8-3.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectAccessedViaIndexingNewVarCatch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-8-4.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectAccessedViaIndexingFinallyReturnVoid()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-8-5.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectAccessedViaIndexingContinueForSwitch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-8-6.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectAccessedViaIndexingWhileDebuggerFunction()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-8-7.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectAccessedViaIndexingThisWithDefault()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-8-8.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void AllowReservedWordsAsPropertyNamesBySetFunctionWithinAnObjectAccessedViaIndexingIfThrowDelete()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1-8-9.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void TheNullTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/S7.6.1_A1.1.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void TheTrueTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/S7.6.1_A1.2.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void TheFalseTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/S7.6.1_A1.3.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void ListOfWordsThatAreNotReserved()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/S7.6.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8ImplementsImplements()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1-17-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8LU0065TLet()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1-18-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8PrivatU0065Private()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1-19-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8U0070U0075U0062U006CU0069U0063Public()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1-20-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8U0079IeldYield()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1-21-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8InteU0072FaceInterface()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1-22-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8PackagU0065Package()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1-23-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8U0070U0072U006FU0074U0065U0063U0074U0065U0064Protected()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1-24-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8U0073U0074U0061U0074U0069U0063Static()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1-25-s.js", false);
        }


    }
}
