/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-247.js
 * @description Object.getOwnPropertyDescriptor - returned value is an instance of object
 */


function testcase() {
        var obj = { "property": 100 };

        var desc = Object.getOwnPropertyDescriptor(obj, "property");

        return desc instanceof Object;
    }
runTestCase(testcase);
