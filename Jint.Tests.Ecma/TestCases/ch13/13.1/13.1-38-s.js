/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-38-s.js
 * @description StrictMode - SyntaxError is thrown if 'eval' occurs as the Identifier of a FunctionExpression whose FunctionBody is contained in strict code
 * @onlyStrict
 */


function testcase() {
        var _13_1_38_s = {};
        try {
            eval("_13_1_38_s.x = function eval() {'use strict'; };");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
