/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10.1/12.10.1-11-s.js
 * @description Strict Mode - SyntaxError is thrown when using WithStatement in strict mode code
 * @onlyStrict
 */


function testcase() {
        "use strict";
        try {
            eval("with ({}) { throw new Error();}");

            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
