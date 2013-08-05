namespace Jint.Parser
{
    // Error messages should be identical to V8.
    public class Messages
    {
        public static string UnexpectedToken = "Unexpected token {0}";
        public static string UnexpectedNumber = "Unexpected number";
        public static string UnexpectedString = "Unexpected string";
        public static string UnexpectedIdentifier = "Unexpected identifier";
        public static string UnexpectedReserved = "Unexpected reserved word";
        public static string UnexpectedEOS = "Unexpected end of input";
        public static string NewlineAfterThrow = "Illegal newline after throw";
        public static string InvalidRegExp = "Invalid regular expression";
        public static string UnterminatedRegExp = "Invalid regular expression= missing /";
        public static string InvalidLHSInAssignment = "Invalid left-hand side in assignment";
        public static string InvalidLHSInForIn = "Invalid left-hand side in for-in";
        public static string MultipleDefaultsInSwitch = "More than one default clause in switch statement";
        public static string NoCatchOrFinally = "Missing catch or finally after try";
        public static string UnknownLabel = "Undefined label \"{0}\"";
        public static string Redeclaration = "{0} \"{1}\" has already been declared";
        public static string IllegalContinue = "Illegal continue statement";
        public static string IllegalBreak = "Illegal break statement";
        public static string IllegalReturn = "Illegal return statement";
        public static string StrictModeWith = "Strict mode code may not include a with statement";
        public static string StrictCatchVariable = "Catch variable may not be eval or arguments in strict mode";
        public static string StrictVarName = "Variable name may not be eval or arguments in strict mode";
        public static string StrictParamName = "Parameter name eval or arguments is not allowed in strict mode";
        public static string StrictParamDupe = "Strict mode function may not have duplicate parameter names";
        public static string StrictFunctionName = "Function name may not be eval or arguments in strict mode";
        public static string StrictOctalLiteral = "Octal literals are not allowed in strict mode.";
        public static string StrictDelete = "Delete of an unqualified identifier in strict mode.";
        public static string StrictDuplicateProperty = "Duplicate data property in object literal not allowed in strict mode";
        public static string AccessorDataProperty = "Object literal may not have data and accessor property with the same name";
        public static string AccessorGetSet = "Object literal may not have multiple get/set accessors with the same name";
        public static string StrictLHSAssignment = "Assignment to eval or arguments is not allowed in strict mode";
        public static string StrictLHSPostfix = "Postfix increment/decrement may not have eval or arguments operand in strict mode";
        public static string StrictLHSPrefix = "Prefix increment/decrement may not have eval or arguments operand in strict mode";
        public static string StrictReservedWord = "Use of future reserved word in strict mode";
    };
}
