/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-125.js
 * @description Object.defineProperties - 'O' is an Array, 'name' is the length property of 'O', test the [[Value]] field of 'desc' is 0 (15.4.5.1 step 3.c)
 */


function testcase() {

        var arr = [0, 1];

        Object.defineProperties(arr, {
            length: { value: 0 }
        });
        return arr.length === 0;

    }
runTestCase(testcase);
