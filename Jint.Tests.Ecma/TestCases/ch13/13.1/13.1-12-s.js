/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-12-s.js
 * @description StrictMode - SyntaxError is thrown if 'eval' occurs as the Identifier of a FunctionExpression in strict mode
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var _13_1_12_s = {};

        try {
            eval("_13_1_12_s.x = function eval() {};");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
