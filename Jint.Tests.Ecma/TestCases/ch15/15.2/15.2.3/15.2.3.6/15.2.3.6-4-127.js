/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-127.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is the length property of 'O', test the [[Value]] field of 'desc' is a boolean with value false (15.4.5.1 step 3.c)
 */


function testcase() {

        var arrObj = [0, 1];

        Object.defineProperty(arrObj, "length", {
            value: false
        });
        return arrObj.length === 0 && !arrObj.hasOwnProperty("0") && !arrObj.hasOwnProperty("1");

    }
runTestCase(testcase);
