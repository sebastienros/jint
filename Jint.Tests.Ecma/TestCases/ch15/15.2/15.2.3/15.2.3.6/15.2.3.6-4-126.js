/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-126.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is the length property of 'O', test the [[Value]] field of 'desc' is null (15.4.5.1 step 3.c)
 */


function testcase() {

        var arrObj = [0, 1];

        Object.defineProperty(arrObj, "length", {
            value: null
        });
        return arrObj.length === 0;

    }
runTestCase(testcase);
