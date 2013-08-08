/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-232.js
 * @description Object.getOwnPropertyDescriptor - ensure that 'enumerable' property of returned object is data property with correct 'writable' attribute
 */


function testcase() {
        var obj = { "property": "ownDataProperty" };

        var desc = Object.getOwnPropertyDescriptor(obj, "property");

        try {
            desc.enumerable = "overwriteDataProperty";
            return desc.enumerable === "overwriteDataProperty";
        } catch (e) {
            return false;
        }
    }
runTestCase(testcase);
