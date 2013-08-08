/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.2/15.10.2.2-1.js
 * @description Pattern - SyntaxError was thrown when compile a pattern
 */


function testcase() {
        try {
            var regExp = new RegExp("\\");

            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
