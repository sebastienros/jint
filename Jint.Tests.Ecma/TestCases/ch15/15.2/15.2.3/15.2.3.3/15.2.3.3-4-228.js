/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-228.js
 * @description Object.getOwnPropertyDescriptor - ensure that 'writable' property of returned object is data property with correct 'writable' attribute
 */
function testcase() {
        var obj = { "property": "ownDataProperty" };

        var desc = Object.getOwnPropertyDescriptor(obj, "property");

        try {
            desc.writable = "overwriteDataProperty";
            return desc.writable === "overwriteDataProperty";
        } catch (e) {
            return false;
        }
    }
runTestCase(testcase);
