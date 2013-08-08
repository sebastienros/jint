/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-6.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is a boolean whose value is true
 */


function testcase() {
        var obj = { "true": 1 };

        var desc = Object.getOwnPropertyDescriptor(obj, true);

        return desc.value === 1;
    }
runTestCase(testcase);
