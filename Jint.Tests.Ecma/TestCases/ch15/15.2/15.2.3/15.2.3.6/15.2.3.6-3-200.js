/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-200.js
 * @description Object.defineProperty - 'writable' property in 'Attributes' is the Argument object (8.10.5 step 6.b)
 */


function testcase() {
        var obj = {};

        var argObj = (function () { return arguments; })(1, true, "a");

        Object.defineProperty(obj, "property", { writable: argObj });

        var beforeWrite = obj.hasOwnProperty("property");

        obj.property = "isWritable";

        var afterWrite = (obj.property === "isWritable");

        return beforeWrite === true && afterWrite === true;
    }
runTestCase(testcase);
