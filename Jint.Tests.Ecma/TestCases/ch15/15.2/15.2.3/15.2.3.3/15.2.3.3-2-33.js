/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-33.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is applied to string 'AB
 * \cd' 
 */


function testcase() {
        var obj = { "AB\n\\cd": 1 };

        var desc = Object.getOwnPropertyDescriptor(obj, "AB\n\\cd");

        return desc.value === 1;
    }
runTestCase(testcase);
