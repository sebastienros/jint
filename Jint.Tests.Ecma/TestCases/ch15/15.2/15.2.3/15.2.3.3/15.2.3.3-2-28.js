/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-28.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is an integer that converts to a string (value is 123)
 */


function testcase() {
        var obj = { "123": 1 };

        var desc = Object.getOwnPropertyDescriptor(obj, 123);

        return desc.value === 1;
    }
runTestCase(testcase);
