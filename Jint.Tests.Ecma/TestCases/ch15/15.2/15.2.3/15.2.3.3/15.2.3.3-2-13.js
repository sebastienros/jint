/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-13.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is a number that converts to a string (value is Infinity)
 */


function testcase() {
        var obj = { "Infinity": 1 };

        var desc = Object.getOwnPropertyDescriptor(obj, Infinity);

        return desc.value === 1;
    }
runTestCase(testcase);
