/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-165.js
 * @description Object.defineProperty - 'Attributes' is a Function object which implements its own [[Get]] method to access the 'writable' property  (8.10.5 step 6.a)
 */


function testcase() {
        var obj = {};

        var funObj = function (a, b) {
            return a + b;
        };

        funObj.writable = true;

        Object.defineProperty(obj, "property", funObj);

        var beforeWrite = obj.hasOwnProperty("property");

        obj.property = "isWritable";

        var afterWrite = (obj.property === "isWritable");

        return beforeWrite === true && afterWrite === true;
    }
runTestCase(testcase);
