/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-32.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is applied to an empty string 
 */


function testcase() {
        var obj = { "": 1 };

        var desc = Object.getOwnPropertyDescriptor(obj, "");

        return desc.value === 1;
    }
runTestCase(testcase);
