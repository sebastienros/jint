/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.2/15.10.2.15-3-2.js
 * @description Pattern - SyntaxError was thrown when 'B' does not contain exactly one character (15.10.2.5 step 3)
 */


function testcase() {
        try {
            var regExp = new RegExp("^[a-/w]$");

            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
