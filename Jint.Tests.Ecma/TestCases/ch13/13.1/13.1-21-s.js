/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 13.1; 
 * It is a SyntaxError if the Identifier "eval" or the Identifier "arguments" occurs within a FormalParameterList
 * of a strict mode FunctionDeclaration or FunctionExpression.
 *
 * @path ch13/13.1/13.1-21-s.js
 * @description StrictMode - SyntaxError is thrown if the identifier 'arguments' appears within a FormalParameterList of a strict mode FunctionExpression in strict eval code
 * @onlyStrict
 */


function testcase() {

        try {
            eval("'use strict'; var _13_1_21_fun = function (arguments) { }");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
