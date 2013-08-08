/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-224.js
 * @description Object.getOwnPropertyDescriptor - ensure that 'value' property of returned object is data property with correct 'writable' attribute
 */


function testcase() {
        var obj = { "property": "ownDataProperty" };

        var desc = Object.getOwnPropertyDescriptor(obj, "property");

        try {
            desc.value = "overwriteDataProperty";
            return desc.value === "overwriteDataProperty";
        } catch (e) {
            return false;
        }
    }
runTestCase(testcase);
