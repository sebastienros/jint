/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-17.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is a number that converts to a string (value is 1(following 21 zeros))
 */


function testcase() {
        var obj = { "1e+21": 1 };

        var desc = Object.getOwnPropertyDescriptor(obj, 1000000000000000000000);

        return desc.value === 1;
    }
runTestCase(testcase);
