/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-219-1.js
 * @description Object.defineProperty - 'Attributes' is an Array object that uses Object's [[Get]] method to access the 'get' property of prototype object (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};
        try {
            Array.prototype.get = function () {
                return "arrayGetProperty";
            };
            var arrObj = [];

            Object.defineProperty(obj, "property", arrObj);

            return obj.property === "arrayGetProperty";
        } finally {
            delete Array.prototype.get;
        }
    }
runTestCase(testcase);
