/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-35-s.js
 * @description StrictMode - SyntaxError is thrown if 'eval' occurs as the function name of a FunctionDeclaration in strict eval code
 * @onlyStrict
 */


function testcase() {

        try {
            eval("'use strict'; function eval() { };")
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
