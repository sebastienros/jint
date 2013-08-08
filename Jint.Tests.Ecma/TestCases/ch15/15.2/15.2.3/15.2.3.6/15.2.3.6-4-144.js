/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-144.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is the length property of 'O', test the [[Value]] field of 'desc' is a string containing a number with leading zeros (15.4.5.1 step 3.c)
 */


function testcase() {

        var arrObj = [];

        Object.defineProperty(arrObj, "length", {
            value: "0002.0"
        });
        return arrObj.length === 2;
    }
runTestCase(testcase);
