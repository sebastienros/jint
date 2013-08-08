/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-261.js
 * @description Object.defineProperty - value of 'set' property in 'Attributes' is undefined (8.10.5 step 8.b)
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "property", {
            set: undefined
        });

        obj.property = "overrideData";
        var desc = Object.getOwnPropertyDescriptor(obj, "property");
        return obj.hasOwnProperty("property") && typeof obj.property === "undefined" &&
            typeof desc.set === "undefined";
    }
runTestCase(testcase);
