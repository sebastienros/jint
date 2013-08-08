/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-152.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is the length property of 'O',  test RangeError is thrown when the [[Value]] field of 'desc' is a positive non-integer values (15.4.5.1 step 3.c)
 */


function testcase() {

        var arrObj = [];

        try {
            Object.defineProperty(arrObj, "length", {
                value: 123.5
            });

            return false;
        } catch (e) {
            return e instanceof RangeError;
        }
    }
runTestCase(testcase);
