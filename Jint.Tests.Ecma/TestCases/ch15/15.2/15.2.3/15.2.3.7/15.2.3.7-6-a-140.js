/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-140.js
 * @description Object.defineProperties - 'O' is an Array, 'name' is the length property of 'O', test the [[Value]] field of 'desc' is a string containing an leading zero number (15.4.5.1 step 3.c)
 */


function testcase() {

        var arr = [];

        Object.defineProperties(arr, {
            length: {
                value: "0002.0"
            }
        });
        return arr.length === 2;
    }
runTestCase(testcase);
