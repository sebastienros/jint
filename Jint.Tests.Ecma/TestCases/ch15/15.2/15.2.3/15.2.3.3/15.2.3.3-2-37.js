/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-37.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is applied to string '1' 
 */


function testcase() {
        var obj = { "1": 1 };

        var desc = Object.getOwnPropertyDescriptor(obj, 1);

        return desc.value === 1;
    }
runTestCase(testcase);
