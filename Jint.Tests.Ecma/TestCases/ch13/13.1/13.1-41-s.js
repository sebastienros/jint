/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-41-s.js
 * @description StrictMode - SyntaxError is thrown if 'arguments' occurs as the Identifier of a FunctionExpression in strict eval code
 * @onlyStrict
 */


function testcase() {
        var _13_1_41_s = {};
        try {
            eval("'use strict'; _13_1_41_s.x = function arguments() {};");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
