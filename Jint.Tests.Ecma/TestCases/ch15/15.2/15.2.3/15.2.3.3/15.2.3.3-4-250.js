/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-250.js
 * @description Object.getOwnPropertyDescriptor - returned object contains the property 'get' if the value of property 'get' is not explicitly specified when defined by Object.defineProperty.
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, "property", {
            set: function () {},
            configurable: true
        });

        var desc = Object.getOwnPropertyDescriptor(obj, "property");

        return "get" in desc;
    }
runTestCase(testcase);
