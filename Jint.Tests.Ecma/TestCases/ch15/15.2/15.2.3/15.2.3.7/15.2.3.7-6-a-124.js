/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-124.js
 * @description Object.defineProperties - 'O' is an Array, 'name' is the length property of 'O', test the [[Value]] field of 'desc' is a boolean with value true (15.4.5.1 step 3.c)
 */


function testcase() {

        var arr = [];

        Object.defineProperties(arr, {
            length: { value: true }
        });
        return arr.length === 1;

    }
runTestCase(testcase);
