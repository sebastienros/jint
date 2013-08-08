/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-150.js
 * @description Object.defineProperties - 'O' is an Array, 'name' is the length property of 'O', test the [[Value]] field of 'desc' is boundary value 2^32 - 2 (15.4.5.1 step 3.c)
 */


function testcase() {

        var arr = [];

        Object.defineProperties(arr, {
            length: {
                value: 4294967294
            }
        });

        return arr.length === 4294967294;
    }
runTestCase(testcase);
