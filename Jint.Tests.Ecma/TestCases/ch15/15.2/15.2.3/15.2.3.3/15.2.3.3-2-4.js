/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-4.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is null that converts to string 'null'
 */


function testcase() {
        var obj = { "null": 1 };

        var desc = Object.getOwnPropertyDescriptor(obj, null);

        return desc.value === 1;
    }
runTestCase(testcase);
