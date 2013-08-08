/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-188.js
 * @description Object.defineProperty - 'writable' property in 'Attributes' is an empty string  (8.10.5 step 6.b)
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "property", { writable: "" });

        var beforeWrite = obj.hasOwnProperty("property");

        obj.property = "isWritable";

        var afterWrite = (typeof (obj.property) === "undefined");

        return beforeWrite === true && afterWrite === true;
    }
runTestCase(testcase);
