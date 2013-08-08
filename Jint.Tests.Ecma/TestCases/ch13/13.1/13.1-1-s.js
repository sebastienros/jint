/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 13.1; 
 * It is a SyntaxError if the Identifier "eval" or the Identifier "arguments" occurs within a FormalParameterList
 * of a strict mode FunctionDeclaration or FunctionExpression.
 *
 * @path ch13/13.1/13.1-1-s.js
 * @description Strict Mode - SyntaxError is thrown if the identifier 'eval' appears within a FormalParameterList of a strict mode FunctionDeclaration
 * @onlyStrict
 */


function testcase() {
        "use strict";

        try {
            eval("function _13_1_1_fun(eval) { }");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
