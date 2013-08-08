/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-42-s.js
 * @description StrictMode - SyntaxError is thrown if 'arguments' occurs as the Identifier of a FunctionExpression whose FunctionBody is contained in strict code
 * @onlyStrict
 */


function testcase() {
        var _13_1_42_s = {};
        try {
            eval("_13_1_42_s.x = function arguments() {'use strict';};");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
