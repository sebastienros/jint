/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.2/15.10.2.5-3-1.js
 * @description Term - SyntaxError was thrown when max is finite and less than min (15.10.2.5 step 3)
 */


function testcase() {
        try {
            var regExp = new RegExp("0{2,1}");

            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
