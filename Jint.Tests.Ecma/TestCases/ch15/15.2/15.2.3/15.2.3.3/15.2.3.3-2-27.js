/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-27.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is a number that converts to a string (value is 1e-5)
 */


function testcase() {
        var obj = { "0.00001": 1 };

        var desc = Object.getOwnPropertyDescriptor(obj, 1e-5);

        return desc.value === 1;
    }
runTestCase(testcase);
