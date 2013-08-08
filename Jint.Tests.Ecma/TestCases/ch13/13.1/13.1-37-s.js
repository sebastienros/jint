/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-37-s.js
 * @description StrictMode - SyntaxError is thrown if 'eval' occurs as the Identifier of a FunctionExpression in strict eval code
 * @onlyStrict
 */


function testcase() {
        var _13_1_37_s = {};
        try {
            eval("'use strict'; _13_1_37_s.x = function eval() {};");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
