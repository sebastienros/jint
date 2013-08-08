/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-34.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is applied to string 'undefined' 
 */


function testcase() {
        var obj = { "undefined": 1 };

        var desc = Object.getOwnPropertyDescriptor(obj, "undefined");

        return desc.value === 1;
    }
runTestCase(testcase);
