/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 13.1; 
 * It is a SyntaxError if any Identifier value occurs more than once within a FormalParameterList of a strict mode
 * FunctionDeclaration or FunctionExpression.
 *
 * @path ch13/13.1/13.1-6-s.js
 * @description Strict Mode - SyntaxError is thrown if a function is created in 'strict mode' using a FunctionDeclaration and the function has two identical parameters, which are separated by a unique parameter name
 * @onlyStrict
 */


function testcase() {
        "use strict";

        try {
            eval("function _13_1_6_fun(param1, param2, param1) { }");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
