/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-13-s.js
 * @description StrictMode - SyntaxError is thrown if 'arguments' occurs as the function name of a FunctionDeclaration in strict mode
 * @onlyStrict
 */


function testcase() {
        "use strict";

        try {
            eval("function arguments() { };")
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
