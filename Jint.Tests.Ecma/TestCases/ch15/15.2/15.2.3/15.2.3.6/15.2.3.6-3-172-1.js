/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-172-1.js
 * @description Object.defineProperty - 'Attributes' is a RegExp object that uses Object's [[Get]] method to access the 'writable' property of prototype object (8.10.5 step 6.b)
 */


function testcase() {
        var obj = {};
        try {
            RegExp.prototype.writable = true;

            var regObj = new RegExp();

            Object.defineProperty(obj, "property", regObj);

            var beforeWrite = obj.hasOwnProperty("property");

            obj.property = "isWritable";

            var afterWrite = (obj.property === "isWritable");

            return beforeWrite === true && afterWrite === true;
        } finally {
            delete RegExp.prototype.writable;
        }
    }
runTestCase(testcase);
