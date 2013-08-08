/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-40.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is a Boolean Object that converts to a string
 */


function testcase() {
        var obj = { "true": 1 };

        var desc = Object.getOwnPropertyDescriptor(obj, new Boolean(true));

        return desc.value === 1;
    }
runTestCase(testcase);
