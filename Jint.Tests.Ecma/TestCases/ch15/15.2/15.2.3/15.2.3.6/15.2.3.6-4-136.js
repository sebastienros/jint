/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-136.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is the length property of 'O', test RangeError exception is thrown when the [[Value]] field of 'desc' is NaN (15.4.5.1 step 3.c)
 */


function testcase() {

        var arrObj = [];

        try {
            Object.defineProperty(arrObj, "length", {
                value: NaN
            });
            return false;
        } catch (e) {
            return e instanceof RangeError;
        }
    }
runTestCase(testcase);
