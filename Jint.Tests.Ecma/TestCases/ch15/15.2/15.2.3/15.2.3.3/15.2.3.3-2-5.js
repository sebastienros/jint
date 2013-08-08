/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-5.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is a boolean whose value is false
 */


function testcase() {
        var obj = { "false": 1 };

        var desc = Object.getOwnPropertyDescriptor(obj, false);

        return desc.value === 1;
    }
runTestCase(testcase);
