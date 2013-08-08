/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-14-s.js
 * @description StrictMode - SyntaxError is thrown if 'arguments' occurs as the Identifier of a FunctionExpression in strict mode
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var _13_1_14_s = {};

        try {
            eval("_13_1_14_s.x = function arguments() {};");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
