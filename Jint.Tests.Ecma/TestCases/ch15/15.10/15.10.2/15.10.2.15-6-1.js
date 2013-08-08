/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.2/15.10.2.15-6-1.js
 * @description Pattern - SyntaxError was thrown when one character in CharSet 'A' greater than one character in CharSet 'B' (15.10.2.15 CharacterRange step 6)
 */


function testcase() {
        try {
            var regExp = new RegExp("^[z-a]$");

            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
