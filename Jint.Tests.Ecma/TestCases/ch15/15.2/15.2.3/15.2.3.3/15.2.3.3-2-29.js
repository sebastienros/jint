/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-29.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is a decimal that converts to a string (value is 123.456)
 */


function testcase() {
        var obj = { "123.456": 1 };

        var desc = Object.getOwnPropertyDescriptor(obj, 123.456);

        return desc.value === 1;
    }
runTestCase(testcase);
