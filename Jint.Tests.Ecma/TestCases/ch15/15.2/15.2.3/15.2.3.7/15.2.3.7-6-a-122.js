/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-122.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is the length property of 'O', test setting the [[Value]] field of 'desc' to null actuall is set to 0 (15.4.5.1 step 3.c)
 */


function testcase() {

        var arr = [0, 1];

        Object.defineProperties(arr, {
            length: { value: null }
        });
        return arr.length === 0;

    }
runTestCase(testcase);
