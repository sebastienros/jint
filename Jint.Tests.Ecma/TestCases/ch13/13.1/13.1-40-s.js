/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-40-s.js
 * @description StrictMode - SyntaxError is thrown if 'arguments' occurs as the Identifier of a FunctionDeclaration whose FunctionBody is contained in strict code
 * @onlyStrict
 */


function testcase() {

        try {
            eval("function arguments() { 'use strict'; };")
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
