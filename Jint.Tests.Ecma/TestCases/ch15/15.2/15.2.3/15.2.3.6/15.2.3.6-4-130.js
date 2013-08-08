/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-130.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is the length property of 'O', test RangeError exception is not thrown when the [[Value]] field of 'desc' is +0 (15.4.5.1 step 3.c)
 */


function testcase() {

        var arrObj = [0, 1];

        Object.defineProperty(arrObj, "length", {
            value: +0
        });
        return arrObj.length === 0;

    }
runTestCase(testcase);
